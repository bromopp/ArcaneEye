using Godot;
using Microsoft.VisualBasic;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
    private const string IP_ADDRESS = "localhost";
    private const int PORT = 42069;
    private const int MAX_PLAYERS = 32;
    
    private ENetMultiplayerPeer peer;
    private MultiplayerApi multiplayer;
    
    // Player management
    private Dictionary<int, PlayerInfo> players = new Dictionary<int, PlayerInfo>();
    private Dictionary<int, Node3D> playerShips = new Dictionary<int, Node3D>();
    
    [Export] private PackedScene playerShipScene;
    [Export] private Node3D playersContainer;
    [Export] private WorldGenerator worldGenerator;
    
    // UI References
    [Export] private Control mainMenu;
    [Export] private Control gameUI;
    [Export] private LineEdit playerNameInput;
    [Export] private LineEdit ipAddressInput;
    [Export] private Label connectionStatus;
    [Export] private ItemList playerList;
    
    [Signal]
    public delegate void PlayerConnectedEventHandler(int id, string name, Color color);
    
    [Signal]
    public delegate void PlayerDisconnectedEventHandler(int id);
    
    [Signal]
    public delegate void ServerDisconnectedEventHandler();
    
    public class PlayerInfo
    {
        public string Name { get; set; }
        public Color Color { get; set; }
        public bool IsReady { get; set; }
        
        public PlayerInfo(string name = "Player", Color? color = null)
        {
            Name = name;
            Color = color ?? new Color(GD.Randf(), GD.Randf(), GD.Randf());
            IsReady = false;
        }
    }

    public override void _Ready()
    {
        peer = new ENetMultiplayerPeer();

        // Connect multiplayer signals
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;

        // Set default IP in UI if available
        if (ipAddressInput != null)
            ipAddressInput.Text = IP_ADDRESS;
            
        // Connect UI buttons if they exist
        var hostButton = GetNode<Button>("../UI/MainMenu/CenterContainer/VBoxContainer/HostSection/HostButton");
        if (hostButton != null)
            hostButton.Pressed += StartServer;
            
        var joinButton = GetNode<Button>("../UI/MainMenu/CenterContainer/VBoxContainer/JoinSection/JoinButton");
        if (joinButton != null)
            joinButton.Pressed += StartClient;
            
        var disconnectButton = GetNode<Button>("../UI/GameUI/Panel/VBoxContainer/DisconnectButton");
        if (disconnectButton != null)
            disconnectButton.Pressed += Disconnect;
    }
    
    // Server functions
    public void StartServer()
    {
        var error = peer.CreateServer(PORT, MAX_PLAYERS);
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to create server: {error}");
            UpdateConnectionStatus($"Failed to create server: {error}");
            return;
        }
        
        Multiplayer.MultiplayerPeer = peer;
        GD.Print($"Server started on port {PORT}");
        UpdateConnectionStatus($"Server running on port {PORT}");
        
        // Add server player
        var serverInfo = new PlayerInfo(playerNameInput?.Text ?? "Host");
        AddPlayer(1, serverInfo); // Server is always ID 1
        
        // Start the game
        StartGame();
    }
    
    // Client functions
    public void StartClient()
    {
        var address = ipAddressInput?.Text ?? IP_ADDRESS;
        var error = peer.CreateClient(address, PORT);
        
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to connect to server: {error}");
            UpdateConnectionStatus($"Failed to connect: {error}");
            return;
        }
        
        Multiplayer.MultiplayerPeer = peer;
        GD.Print($"Connecting to {address}:{PORT}");
        UpdateConnectionStatus($"Connecting to {address}:{PORT}...");
    }
    
    // Connection event handlers
    private void OnPeerConnected(long id)
    {
        GD.Print($"Peer connected: {id}");
        
        // If we're the server, send current game state to new player
        if (Multiplayer.IsServer())
        {
            // Send all existing players to the new player
            foreach (var kvp in players)
            {
                RpcId(id, MethodName.ReceivePlayerInfo, kvp.Key, kvp.Value.Name, kvp.Value.Color);
            }
            
            // Send world configuration
            RpcId(id, MethodName.ReceiveWorldConfig, 
                worldGenerator.WorldSize, 
                (int)worldGenerator.CurrentWorldType);
        }
    }
    
    private void OnPeerDisconnected(long id)
    {
        GD.Print($"Peer disconnected: {id}");
        RemovePlayer((int)id);
    }
    
    private void OnConnectedToServer()
    {
        GD.Print("Connected to server!");
        UpdateConnectionStatus("Connected to server!");
        
        // Send our player info to server
        var playerName = playerNameInput?.Text ?? $"Player{GD.Randi() % 1000}";
        var playerColor = new Color(GD.Randf(), GD.Randf(), GD.Randf());
        
        RpcId(1, MethodName.RegisterPlayer, playerName, playerColor);
    }
    
    private void OnConnectionFailed()
    {
        GD.PrintErr("Connection failed!");
        UpdateConnectionStatus("Connection failed!");
    }
    
    private void OnServerDisconnected()
    {
        GD.Print("Server disconnected!");
        UpdateConnectionStatus("Server disconnected!");
        EmitSignal(SignalName.ServerDisconnected);
        
        // Clean up
        CleanupGame();
    }
    
    // RPC Methods
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RegisterPlayer(string name, Color color)
    {
        var senderId = Multiplayer.GetRemoteSenderId();
        GD.Print($"Registering player {senderId}: {name}");
        
        var playerInfo = new PlayerInfo(name, color);
        AddPlayer(senderId, playerInfo);
        
        // Broadcast new player to all clients
        Rpc(MethodName.ReceivePlayerInfo, senderId, name, color);
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceivePlayerInfo(int id, string name, Color color)
    {
        GD.Print($"Received player info for {id}: {name}");
        var playerInfo = new PlayerInfo(name, color);
        AddPlayer(id, playerInfo);
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveWorldConfig(float worldSize, int worldType)
    {
        GD.Print($"[{Multiplayer.GetUniqueId()}] Received config from {Multiplayer.GetRemoteSenderId()}");
        GD.Print($"World size: {worldSize}, Type: {worldType}");
        
        if (worldGenerator == null)
        {
            GD.PrintErr("WorldGenerator is null! Cannot apply config.");
            return;
        }
        
        // Apply world configuration
        worldGenerator.Set("worldSize", worldSize);
        worldGenerator.Set("worldType", ((WorldGenerator.WorldType)worldType).ToString());
        worldGenerator.GenerateWorld();
        
        // Start the game
        StartGame();
    }
    
    // Player management
    private void AddPlayer(int id, PlayerInfo info)
    {
        players[id] = info;
        EmitSignal(SignalName.PlayerConnected, id, info.Name, info.Color);
        
        // Spawn player ship
        if (playerShipScene != null && playersContainer != null)
        {
            var ship = playerShipScene.Instantiate<Node3D>();
            ship.Name = $"Player_{id}";
            ship.Set("player_id", id);
            ship.Set("player_color", info.Color);
            
            // Random spawn position within world bounds
            var spawnRadius = worldGenerator.WorldSize * 0.3f;
            ship.Position = new Vector3(
                GD.Randf() * spawnRadius - spawnRadius / 2,
                GD.Randf() * spawnRadius - spawnRadius / 2,
                GD.Randf() * spawnRadius - spawnRadius / 2
            );
            
            playersContainer.AddChild(ship);
            playerShips[id] = ship;
            
            // Set authority for the player's ship
            ship.SetMultiplayerAuthority(id);
        }
        
        UpdatePlayerList();
    }
    
    private void RemovePlayer(int id)
    {
        if (players.ContainsKey(id))
        {
            players.Remove(id);
            EmitSignal(SignalName.PlayerDisconnected, id);
            
            // Remove player ship
            if (playerShips.ContainsKey(id))
            {
                playerShips[id].QueueFree();
                playerShips.Remove(id);
            }
            
            UpdatePlayerList();
        }
    }
    
    // UI Methods
    private void StartGame()
    {
        mainMenu?.Hide();
        gameUI?.Show();
    }
    
    private void CleanupGame()
    {
        // Remove all players
        foreach (var id in players.Keys)
        {
            if (playerShips.ContainsKey(id))
            {
                playerShips[id].QueueFree();
            }
        }
        
        players.Clear();
        playerShips.Clear();
        
        // Reset UI
        mainMenu?.Show();
        gameUI?.Hide();
        UpdatePlayerList();
    }
    
    private void UpdateConnectionStatus(string status)
    {
        if (connectionStatus != null)
            connectionStatus.Text = status;
    }
    
    private void UpdatePlayerList()
    {
        if (playerList == null) return;
        
        playerList.Clear();
        foreach (var kvp in players)
        {
            var text = $"{kvp.Value.Name} (ID: {kvp.Key})";
            if (kvp.Key == 1) text += " [HOST]";
            if (kvp.Key == Multiplayer.GetUniqueId()) text += " [YOU]";
            
            playerList.AddItem(text);
            var idx = playerList.ItemCount - 1;
            playerList.SetItemCustomBgColor(idx, kvp.Value.Color * 0.3f);
        }
    }
    
    // Public utility methods
    public bool IsServer()
    {
        return Multiplayer.IsServer();
    }
    
    public int GetLocalPlayerId()
    {
        return Multiplayer.GetUniqueId();
    }
    
    public Dictionary<int, PlayerInfo> GetPlayers()
    {
        return players;
    }
    
    public void Disconnect()
    {
        peer.Close();
        CleanupGame();
    }
}