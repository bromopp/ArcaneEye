using Godot;
using Microsoft.VisualBasic;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public partial class NetworkManager : Node
{
    private const string IP_ADDRESS = "localhost";
    private const int PORT = 42069;
    private const int MAX_PLAYERS = 32;

    private ENetMultiplayerPeer peer;
    private MultiplayerApi multiplayer;

    public static NetworkManager Instance { get; private set; }

    // Player management
    private Dictionary<int, PlayerInfo> players = new Dictionary<int, PlayerInfo>();
    private Dictionary<int, Node3D> playerShips = new Dictionary<int, Node3D>();

    [Export] private PackedScene playerShipScene;
    [Export] private Node3D playersContainer;
    [Export] private WorldGenerator worldGenerator;

    // UI Manager reference
    private UIManager uiManager;

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
        public int Lives { get; set; }

        public PlayerInfo(string name = "Player", Color? color = null, int lives = 3)
        {
            Name = name;
            Color = color ?? new Color(GD.Randf(), GD.Randf(), GD.Randf());
            IsReady = false;
            Lives = lives;
        }
    }

    public override void _Ready()
    {
        // Simple singleton pattern - first one wins
        if (Instance == null)
        {
            Instance = this;
            GD.Print($"NetworkManager singleton initialized from: {GetPath()}");
        }
        else
        {
            GD.Print($"NetworkManager already exists at: {Instance.GetPath()}. Current instance at: {GetPath()} will be destroyed.");
            QueueFree();
            return;
        }
        
        peer = new ENetMultiplayerPeer();

        // Get UI Manager reference with retry
        InitializeUIManager();

        // Connect multiplayer signals
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;

        // Set up UI
        SetupUI();
    }

    private void InitializeUIManager()
    {
        uiManager = UIManager.Instance;
        
        if (uiManager == null)
        {
            GD.PrintErr("UIManager not found! Attempting to find UIManager in scene tree...");
            
            // Try to find UIManager in the scene tree as a fallback
            var uiManagerNode = GetTree().GetFirstNodeInGroup("ui_manager");
            if (uiManagerNode is UIManager foundUIManager)
            {
                uiManager = foundUIManager;
                UIManager.Instance = foundUIManager; // Update the singleton reference
                GD.Print("Found UIManager in scene tree and updated singleton reference");
            }
            else
            {
                // Last resort: try to find by name
                uiManagerNode = GetNode<UIManager>("/root/Main/UIManager");
                if (uiManagerNode != null)
                {
                    uiManager = (UIManager)uiManagerNode;
                    UIManager.Instance = (UIManager)uiManagerNode;
                    GD.Print("Found UIManager by path and updated singleton reference");
                }
                else
                {
                    GD.PrintErr("UIManager could not be found! UI functionality will be limited.");
                }
            }
        }
    }

    private void SetupUI()
    {
        if (uiManager == null) return;

        // Set default IP address
        uiManager.SetDefaultIPAddress(IP_ADDRESS);

        // Connect UI buttons
        uiManager.ConnectHostButton(StartServer);
        uiManager.ConnectJoinButton(StartClient);
        uiManager.ConnectDisconnectButton(Disconnect);
    }

    // Server functions
    public void StartServer()
    {
        // Ensure we have a clean peer before starting server
        if (peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            GD.Print("Cleaning up existing peer before starting server");
            peer.Close();
            peer = new ENetMultiplayerPeer();
        }
        
        var error = peer.CreateServer(PORT, MAX_PLAYERS);
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to create server: {error}");
            uiManager.UpdateConnectionStatus($"Failed to create server: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        GD.Print($"Server started on port {PORT}");
        uiManager?.UpdateConnectionStatus($"Server running on port {PORT}");

        // Add server player
        var serverInfo = new PlayerInfo(uiManager?.GetPlayerName() ?? "Host");
        AddPlayer(1, serverInfo); // Server is always ID 1

        // Generate world with seed
        worldGenerator.GenerateWorld();

        // Start the game
        StartGame();
    }

    // Client functions
    public void StartClient()
    {
        // Ensure we have a clean peer before connecting as client
        if (peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            GD.Print("Cleaning up existing peer before connecting as client");
            peer.Close();
            peer = new ENetMultiplayerPeer();
        }
        
        var address = uiManager?.GetIPAddress() ?? IP_ADDRESS;
        var error = peer.CreateClient(address, PORT);

        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to connect to server: {error}");
            uiManager?.UpdateConnectionStatus($"Failed to connect: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        GD.Print($"Connecting to {address}:{PORT}");
        uiManager?.UpdateConnectionStatus($"Connecting to {address}:{PORT}...");
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

            // Send world configuration including seed
            RpcId(id, MethodName.ReceiveWorldConfig,
                worldGenerator.WorldSize,
                worldGenerator.WorldSeed);
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
        uiManager?.UpdateConnectionStatus("Connected to server!");

        // Send our player info to server
        var playerName = uiManager?.GetPlayerName() ?? $"Player{GD.Randi() % 1000}";
        var playerColor = new Color(GD.Randf(), GD.Randf(), GD.Randf());

        RpcId(1, MethodName.RegisterPlayer, playerName, playerColor);
    }

    private void OnConnectionFailed()
    {
        GD.PrintErr("Connection failed!");
        uiManager?.UpdateConnectionStatus("Connection failed!");
        
        // Reset multiplayer peer safely to prevent "AlreadyInUse" error
        if (Multiplayer != null)
        {
            Multiplayer.MultiplayerPeer = null;
        }
        peer = new ENetMultiplayerPeer();
    }

    private void OnServerDisconnected()
    {
        GD.Print("Server disconnected!");
        uiManager?.UpdateConnectionStatus("Server disconnected!");
        EmitSignal(SignalName.ServerDisconnected);
        
        // Reset multiplayer peer safely
        if (Multiplayer != null)
        {
            Multiplayer.MultiplayerPeer = null;
        }
        peer = new ENetMultiplayerPeer();
        
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
    private void ReceiveWorldConfig(float worldSize, int worldSeed)
    {
        GD.Print($"[{Multiplayer.GetUniqueId()}] Received config from {Multiplayer.GetRemoteSenderId()}");
        GD.Print($"World size: {worldSize}, Seed: {worldSeed}");

        if (worldGenerator == null)
        {
            GD.PrintErr("WorldGenerator is null! Cannot apply config.");
            return;
        }

        // Apply world configuration (only cube world supported now)
        worldGenerator.Set("worldSize", worldSize);

        // Generate world with the received seed
        worldGenerator.GenerateWorld(worldSeed);

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
            var ship = playerShipScene.Instantiate<PlayerShip>();
            ship.Name = $"Player_{id}";
            ship.SetPlayerId(id);
            ship.SetPlayerColor(info.Color);

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
        uiManager?.ShowGameUI();
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
        uiManager?.ShowMainMenu();
        UpdatePlayerList();
    }

    private void UpdatePlayerList()
    {
        var localId = GetLocalPlayerId();
        uiManager?.UpdatePlayerList(players, localId);
    }

    // Public utility methods
    public bool IsServer()
    {
        return Multiplayer?.MultiplayerPeer != null && Multiplayer.IsServer();
    }

    public int GetLocalPlayerId()
    {
        if (Multiplayer?.MultiplayerPeer == null) return -1;
        return Multiplayer.GetUniqueId();
    }

    public Dictionary<int, PlayerInfo> GetPlayers()
    {
        return players;
    }

    public void Disconnect()
    {
        // Check if multiplayer is active before attempting operations
        if (Multiplayer != null && Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            peer?.Close();
        }
        
        // Reset multiplayer peer to null to prevent further operations
        if (Multiplayer != null)
        {
            Multiplayer.MultiplayerPeer = null;
        }
        
        // Create new peer instance for future connections
        peer = new ENetMultiplayerPeer();
        
        CleanupGame();
    }

    // Life system methods
    public Dictionary<int, Node3D> GetPlayerShips()
    {
        return playerShips;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void UpdatePlayerLives(int playerId, int newLives)
    {
        if (players.ContainsKey(playerId))
        {
            players[playerId].Lives = newLives;
            GD.Print($"Player {playerId} lives updated to {newLives}");

            // Broadcast life update to all clients if we're the server
            if (Multiplayer.IsServer())
            {
                Rpc(MethodName.ReceivePlayerLivesUpdate, playerId, newLives);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceivePlayerLivesUpdate(int playerId, int newLives)
    {
        if (players.ContainsKey(playerId))
        {
            players[playerId].Lives = newLives;

            // Update the actual player ship instance too
            if (playerShips.ContainsKey(playerId) && playerShips[playerId] is PlayerShip ship)
            {
                ship.SetCurrentLives(newLives);
            }

            GD.Print($"Received life update: Player {playerId} now has {newLives} lives");
        }
    }

    // Method to respawn a player at a specific position
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RespawnPlayer(int playerId, Vector3 position)
    {
        if (playerShips.ContainsKey(playerId))
        {
            var ship = playerShips[playerId];
            ship.GlobalPosition = position;
            ship.Set("LinearVelocity", Vector3.Zero);
            ship.Set("AngularVelocity", Vector3.Zero);

            GD.Print($"Player {playerId} respawned at {position}");
        }
    }

    // Method to get a player's current lives
    public int GetPlayerLives(int playerId)
    {
        if (players.ContainsKey(playerId))
        {
            return players[playerId].Lives;
        }
        return 0;
    }

    // Method to check if all players (except one) are eliminated
    public void CheckForGameEnd()
    {
        int alivePlayers = 0;
        int lastAlivePlayer = -1;

        foreach (var kvp in players)
        {
            if (kvp.Value.Lives > 0)
            {
                alivePlayers++;
                lastAlivePlayer = kvp.Key;
            }
        }

        if (alivePlayers <= 1 && alivePlayers > 0)
        {
            // Broadcast winner to all clients
            var winnerName = players.ContainsKey(lastAlivePlayer) ? players[lastAlivePlayer].Name : "Unknown";
            Rpc(MethodName.EndGameWithWinner, winnerName);
            GD.Print($"Game Over! {winnerName} wins!");

        }
        else if (alivePlayers == 0)
        {
            // All players are dead - it's a tie
            Rpc(MethodName.EndGameWithWinner, "It's a Tie!");
            GD.Print("Game Over! It's a tie!");

        }

    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void EndGameWithWinner(string winnerText)
    {
        GD.Print($"EndGame called - {winnerText}");

        // Set the winner text on all clients
        uiManager?.SetWinnerText($"{winnerText} Wins!");

        // Show game over screen
        uiManager?.ShowGameOver();
    }
    
    public override void _ExitTree()
    {
        // Clean up singleton reference when destroyed
        if (Instance == this)
        {
            Instance = null;
        }
    }
}