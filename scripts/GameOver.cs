using Godot;
using System;

public partial class GameOver : Control
{
	[Export] private Button backToMenuButton;
	
	public override void _Ready()
	{
		// Connect back to menu button if it exists
		if (backToMenuButton != null)
		{
			backToMenuButton.Pressed += BackToMenu;
		}
		else
		{
			// Try to find the button by name if not exported
			var button = FindChild("BackToMenuButton") as Button;
			if (button != null)
			{
				button.Pressed += BackToMenu;
			}
		}
	}
	
	private void BackToMenu()
	{
		// Use UIManager for proper UI state management
		UIManager.Instance?.ShowMainMenu();
		
		// Also clean up the game state via NetworkManager
		NetworkManager.Instance?.Disconnect();
	}
}
