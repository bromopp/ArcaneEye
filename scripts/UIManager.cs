using Godot;

public partial class UIManager : Node
{
    public static UIManager Instance { get; set; }

    // UI References
    [Export] private Control mainMenu;
    [Export] private Control gameUI;
    [Export] private Control gameOver;
    [Export] private RichTextLabel podiumText;
    [Export] private LineEdit playerNameInput;
    [Export] private LineEdit ipAddressInput;
    [Export] private Label connectionStatus;
    [Export] private ItemList playerList;
    
    // Bullet velocity tracking
    [Export] private Label bulletVelocityLabel;

    // UI Button references
    private Button hostButton;
    private Button joinButton;
    private Button disconnectButton;

    public override void _Ready()
    {
        // Add to group for easy finding
        AddToGroup("ui_manager");
        
        // Simple singleton pattern - first one wins
        if (Instance == null)
        {
            Instance = this;
            GD.Print($"UIManager singleton initialized from: {GetPath()}");
        }
        else
        {
            GD.Print($"UIManager already exists at: {Instance.GetPath()}. Current instance at: {GetPath()} will be destroyed.");
            QueueFree();
            return;
        }

        // Connect UI buttons
        ConnectUIButtons();
        
        // Initialize bullet velocity label
        InitializeBulletVelocityLabel();
        
        // Show main menu by default
        ShowMainMenu();
    }

    public override void _ExitTree()
    {
        // Clean up singleton reference
        if (Instance == this)
        {
            Instance = null;
        }
    }
	private bool isPaused = false;

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (keyEvent.Keycode == Key.Escape)
			{
				// Toggle mouse for menu access
				if (Input.MouseMode == Input.MouseModeEnum.Captured)
					Input.MouseMode = Input.MouseModeEnum.Visible;
				else
					Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			else if (keyEvent.Keycode == Key.P)
			{
				isPaused = !isPaused;
				GetTree().Paused = isPaused;
			}
		}
	}
    private void ConnectUIButtons()
    {
        // Try to find and connect buttons
        hostButton = GetNodeOrNull<Button>("../UI/MainMenu/CenterContainer/VBoxContainer/HostSection/HostButton");
        joinButton = GetNodeOrNull<Button>("../UI/MainMenu/CenterContainer/VBoxContainer/JoinSection/JoinButton");
        disconnectButton = GetNodeOrNull<Button>("../UI/GameUI/Panel/VBoxContainer/DisconnectButton");

        // Note: Button connections will be handled by NetworkManager
        // This allows for better separation while maintaining functionality
    }

    // UI State Management
    public void ShowMainMenu()
    {
        GD.Print($"main menu is: {mainMenu}");
        mainMenu?.Show();
        gameUI?.Hide();
        gameOver?.Hide();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void ShowGameUI()
    {
        mainMenu?.Hide();
        gameUI?.Show();
        gameOver?.Hide();
    }

    public void ShowGameOver()
    {
        mainMenu?.Hide();
        gameUI?.Hide();
        gameOver?.Show();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    // Connection Status
    public void UpdateConnectionStatus(string status)
    {
        if (connectionStatus != null)
            connectionStatus.Text = status;
    }

    // Player List Management
    public void UpdatePlayerList(System.Collections.Generic.Dictionary<int, NetworkManager.PlayerInfo> players, int localPlayerId)
    {
        if (playerList == null) return;

        playerList.Clear();
        foreach (var kvp in players)
        {
            var text = $"{kvp.Value.Name} (ID: {kvp.Key})";
            if (kvp.Key == 1) text += " [HOST]";
            if (kvp.Key == localPlayerId) text += " [YOU]";

            playerList.AddItem(text);
            var idx = playerList.ItemCount - 1;
            playerList.SetItemCustomBgColor(idx, kvp.Value.Color * 0.3f);
        }
    }

    public void ClearPlayerList()
    {
        playerList?.Clear();
    }

    // Game Over Screen
    public void SetWinnerText(string winnerText)
    {
        if (podiumText != null)
        {
            podiumText.Text = winnerText;
        }
    }

    // Input Field Access
    public string GetPlayerName()
    {
        return playerNameInput?.Text ?? "Player";
    }

    public string GetIPAddress()
    {
        return ipAddressInput?.Text ?? "localhost";
    }

    public void SetDefaultIPAddress(string ipAddress)
    {
        if (ipAddressInput != null)
            ipAddressInput.Text = ipAddress;
    }

    // Button Connection Methods (called by NetworkManager)
    public void ConnectHostButton(System.Action onPressed)
    {
        if (hostButton != null)
            hostButton.Pressed += onPressed;
    }

    public void ConnectJoinButton(System.Action onPressed)
    {
        if (joinButton != null)
            joinButton.Pressed += onPressed;
    }

    public void ConnectDisconnectButton(System.Action onPressed)
    {
        if (disconnectButton != null)
            disconnectButton.Pressed += onPressed;
    }

    // Utility Methods
    public bool IsMainMenuVisible()
    {
        return mainMenu?.Visible ?? false;
    }

    public bool IsGameUIVisible()
    {
        return gameUI?.Visible ?? false;
    }

    public bool IsGameOverVisible()
    {
        return gameOver?.Visible ?? false;
    }

    // Bullet Velocity Tracking
    private void InitializeBulletVelocityLabel()
    {
        // Try to find the bullet velocity label if not exported
        if (bulletVelocityLabel == null)
        {
            bulletVelocityLabel = GetNodeOrNull<Label>("../GameUI/ScorePanel/VBoxContainer/BulletVelocityLabel");
        }
        
        // Hide by default
        if (bulletVelocityLabel != null)
        {
            bulletVelocityLabel.Visible = false;
        }
    }

    public void ShowBulletVelocity(Vector3 velocity)
    {
        if (bulletVelocityLabel != null)
        {
            bulletVelocityLabel.Visible = true;
            bulletVelocityLabel.Text = $"Bullet Speed: {velocity.Length():F1} m/s";
        }
    }

    public void HideBulletVelocity()
    {
        if (bulletVelocityLabel != null)
        {
            bulletVelocityLabel.Visible = false;
        }
    }

    public void UpdateBulletVelocity(Vector3 velocity)
    {
        if (bulletVelocityLabel != null && bulletVelocityLabel.Visible)
        {
            bulletVelocityLabel.Text = $"Bullet Speed: {velocity.Length():F1} m/s";
        }
    }
}