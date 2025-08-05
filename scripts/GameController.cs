using Godot;
using System;

public partial class GameController : Node
{
	[Export] private NodePath worldGeneratorPath;
	[Export] private NodePath cameraPath;
	
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
	private Camera3D camera;
	private bool isPaused = false;
	
	// Camera control
	private float cameraSensitivity = 0.002f;
	private float cameraSpeed = 50.0f;
	private Vector2 cameraRotation = Vector2.Zero;
	
	public override void _Ready()
	{
		
		// Get references
		worldGenerator = GetNode<WorldGenerator>(worldGeneratorPath);
		camera = GetNode<Camera3D>(cameraPath);
		
		// Set up UI connections
		SetupUI();
		
		// Capture mouse
		Input.MouseMode = Input.MouseModeEnum.Captured;

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
		
		// Reset camera position
		camera.Position = new Vector3(0, 0, (float)worldSizeSlider.Value * 0.8f);
		camera.Rotation = Vector3.Zero;
		cameraRotation = Vector2.Zero;
	}
	
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			cameraRotation.X -= mouseMotion.Relative.X * cameraSensitivity;
			cameraRotation.Y -= mouseMotion.Relative.Y * cameraSensitivity;
			cameraRotation.Y = Mathf.Clamp(cameraRotation.Y, -Mathf.Pi / 2.0f, Mathf.Pi / 2.0f);
		}
		else if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{

		if (keyEvent.Keycode == Key.Escape)
			{
				if (Input.MouseMode == Input.MouseModeEnum.Captured)
					Input.MouseMode = Input.MouseModeEnum.Visible;
				else
					Input.MouseMode = Input.MouseModeEnum.Captured;
			}else if (keyEvent.Keycode == Key.P)
			{
				isPaused = !isPaused;
				GetTree().Paused = isPaused;
			}
		}
	}
	
	public override void _Process(double delta)
	{
		// Update camera rotation
		// camera.Rotation = new Vector3(cameraRotation.Y, cameraRotation.X, 0);
		
		// // Camera movement
		// Vector3 velocity = Vector3.Zero;
		
		// if (Input.IsActionPressed("move_forward"))
		// 	velocity -= camera.Transform.Basis.Z;
		// if (Input.IsActionPressed("move_back"))
		// 	velocity += camera.Transform.Basis.Z;
		// if (Input.IsActionPressed("move_left"))
		// 	velocity -= camera.Transform.Basis.X;
		// if (Input.IsActionPressed("move_right"))
		// 	velocity += camera.Transform.Basis.X;
		// if (Input.IsActionPressed("move_up"))
		// 	velocity += Vector3.Up;
		// if (Input.IsActionPressed("move_down"))
		// 	velocity -= Vector3.Up;
		
		// if (velocity.Length() > 0)
		// {
		// 	velocity = velocity.Normalized() * cameraSpeed * (float)delta;
		// 	camera.Position += velocity;
		// }
	}
}
