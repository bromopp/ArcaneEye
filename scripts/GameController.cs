using Godot;
using System;

public partial class GameController : Node
{
	[Export] private NodePath worldGeneratorPath;
	
	// UI Controls
	[Export] private NodePath worldSizeSliderPath;
	[Export] private NodePath worldSizeLabelPath;
	[Export] private NodePath asteroidCountSliderPath;
	[Export] private NodePath asteroidCountLabelPath;
	[Export] private NodePath worldTypeOptionPath;
	[Export] private NodePath showBoundsCheckPath;
	[Export] private NodePath showGridCheckPath;
	[Export] private NodePath generateButtonPath;
	
	private WorldGenerator worldGenerator;
	private bool isPaused = false;
	
	public override void _Ready()
	{
		// Get references
		worldGenerator = GetNode<WorldGenerator>(worldGeneratorPath);
		
		// Set up UI connections
		SetupUI();
	}
	
	private void SetupUI()
	{
		var worldSizeSlider = GetNode<HSlider>(worldSizeSliderPath);
		var worldSizeLabel = GetNode<Label>(worldSizeLabelPath);
		var asteroidCountSlider = GetNode<HSlider>(asteroidCountSliderPath);
		var asteroidCountLabel = GetNode<Label>(asteroidCountLabelPath);
		var worldTypeOption = GetNode<OptionButton>(worldTypeOptionPath);
		var showBoundsCheck = GetNode<CheckBox>(showBoundsCheckPath);
		var showGridCheck = GetNode<CheckBox>(showGridCheckPath);
		var generateButton = GetNode<Button>(generateButtonPath);
		
		// Connect signals
		worldSizeSlider.ValueChanged += (double value) => {
			worldSizeLabel.Text = value.ToString("0");
		};
		
		asteroidCountSlider.ValueChanged += (double value) => {
			asteroidCountLabel.Text = value.ToString("0");
		};
		
		showBoundsCheck.Toggled += (bool pressed) => {
			worldGenerator.SetShowBounds(pressed);
		};
		
		showGridCheck.Toggled += (bool pressed) => {
			worldGenerator.SetShowGrid(pressed);
		};
		
		generateButton.Pressed += OnGeneratePressed;
		
		// Add world type options
		worldTypeOption.AddItem("Cube");
		worldTypeOption.AddItem("Sphere");
		worldTypeOption.Selected = 0;
	}
	
	private void OnGeneratePressed()
	{
		var worldSizeSlider = GetNode<HSlider>(worldSizeSliderPath);
		var asteroidCountSlider = GetNode<HSlider>(asteroidCountSliderPath);
		var worldTypeOption = GetNode<OptionButton>(worldTypeOptionPath);
		
		// Update world generator settings
		worldGenerator.Set("worldSize", (float)worldSizeSlider.Value);
		worldGenerator.Set("asteroidCount", (int)asteroidCountSlider.Value);
		worldGenerator.Set("worldType", worldTypeOption.Selected);
		
		// Regenerate world
		worldGenerator.GenerateWorld();
	}
	
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
}
