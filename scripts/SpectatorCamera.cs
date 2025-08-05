// SpectatorCamera.cs - For dead players or observers
using Godot;

public partial class SpectatorCamera : Node3D
{
    [Export] private float moveSpeed = 20.0f;
    [Export] private float lookSensitivity = 0.002f;
    
    private Camera3D camera;
    private Vector2 mouseRotation = Vector2.Zero;
    private bool isActive = false;
    
    public override void _Ready()
    {
        camera = new Camera3D();
        AddChild(camera);
        camera.Fov = 75;
    }
    
    public void Activate()
    {
        isActive = true;
        camera.Current = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
    
    public void Deactivate()
    {
        isActive = false;
        camera.Current = false;
    }
    
    public override void _Input(InputEvent @event)
    {
        if (!isActive) return;
        
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            mouseRotation.X -= mouseMotion.Relative.X * lookSensitivity;
            mouseRotation.Y -= mouseMotion.Relative.Y * lookSensitivity;
            mouseRotation.Y = Mathf.Clamp(mouseRotation.Y, -1.5f, 1.5f);
        }
    }
    
    public override void _Process(double delta)
    {
        if (!isActive) return;
        
        // Apply rotation
        Rotation = new Vector3(mouseRotation.Y, mouseRotation.X, 0);
        
        // Movement
        Vector3 velocity = Vector3.Zero;
        
        if (Input.IsActionPressed("move_forward"))
            velocity -= Transform.Basis.Z;
        if (Input.IsActionPressed("move_back"))
            velocity += Transform.Basis.Z;
        if (Input.IsActionPressed("move_left"))
            velocity -= Transform.Basis.X;
        if (Input.IsActionPressed("move_right"))
            velocity += Transform.Basis.X;
        if (Input.IsActionPressed("move_up"))
            velocity += Vector3.Up;
        if (Input.IsActionPressed("move_down"))
            velocity -= Vector3.Up;
        
        if (velocity.Length() > 0)
        {
            Position += velocity.Normalized() * moveSpeed * (float)delta;
        }
    }
    
    public void FollowPlayer(Node3D target)
    {
        // Smoothly follow a target player
        if (target != null)
        {
            var targetPos = target.GlobalPosition + Vector3.Up * 5 + Vector3.Back * 10;
            GlobalPosition = GlobalPosition.Lerp(targetPos, 2.0f * (float)GetProcessDeltaTime());
            LookAt(target.GlobalPosition, Vector3.Up);
        }
    }
}