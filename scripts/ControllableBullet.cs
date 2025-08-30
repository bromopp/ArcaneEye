// ControllableBullet.cs - New bullet class with player control
using System;
using Godot;

public partial class ControllableBullet : Area3D
{
    [Export] private float speed = 20.0f;
    [Export] private float lifetime = 3.0f; // Longer lifetime for controlled bullets
    [Export] private float rampUpTime = 1.5f; // Time to reach max speed

    [Export] private float rotationSpeed = 2.0f;
    [Export] private float cameraDistance = 3.215f;
    [Export] private float cameraHeight = 1.0f;

    [Export] private PackedScene explosionScene;

    private Vector3 velocity;
    private float currentSpeed = 0.0f; // Current speed magnitude
    private Vector3 direction = Vector3.Forward; // Current direction

    private int ownerId = -1;
    private Color bulletColor = Colors.White;
    private float aliveTime = 0.0f;

    private Camera3D bulletCamera;
    private Node3D cameraRig;
    private PlayerShip ownerShip;
    private Vector2 mouseRotation = Vector2.Zero;
    private bool isControlled = false;
    private WorldGenerator world;
    public override void _Ready()
    {

        // Create visual
        world = GetNode<WorldGenerator>("/root/Main/WorldGenerator");

        // Set up camera
        if (IsMultiplayerAuthority())
        {
            SetupCamera();
        }
        // Connect signals
        BodyEntered += OnBodyEntered;

        // Enable monitoring
        Monitoring = true;
        Monitorable = true;

        // Add particle trail to bullet
        var trail = new CpuParticles3D();
        trail.Amount = 50;
        trail.Lifetime = 1.0f;
        trail.EmissionShape = CpuParticles3D.EmissionShapeEnum.Point;
        trail.Direction = Vector3.Back;
        trail.InitialVelocityMin = 2.0f;
        trail.Scale = Vector3.One * 0.1f;
        AddChild(trail);

        GD.Print($"[{Name}] Layers: {CollisionLayer}, Mask: {CollisionMask}");
    }


    private void SetupCamera()
    {
        // Create camera rig for smooth following
        cameraRig = new Node3D();
        AddChild(cameraRig);

        // Create camera
        bulletCamera = new Camera3D();
        bulletCamera.Position = new Vector3(0, cameraHeight, cameraDistance);

        bulletCamera.Fov = 90;
        bulletCamera.Current = false;

        cameraRig.AddChild(bulletCamera);

        //CallDeferred(nameof(SetupCameraLookAt));
    }
    private void SetupCameraLookAt()
    {
        if (bulletCamera != null && IsInsideTree())
        {
            bulletCamera.LookAt(bulletCamera.GlobalPosition + Vector3.Forward, Vector3.Up);
        }
    }
    public void Initialize(int playerId, Vector3 startDirection, Color color, PlayerShip ship, Vector3 shipRotation)
    {
        ownerId = playerId;
        direction = startDirection.Normalized();
        currentSpeed = 0.0f; // Start from 0 speed
        velocity = Vector3.Zero; // Will be calculated in _PhysicsProcess
        bulletColor = color;
        ownerShip = ship;

        // Set initial rotation to match ship's view direction including vertical look
        GlobalRotation = shipRotation;
        mouseRotation = new Vector2(shipRotation.Y, shipRotation.X);

        // Only the owner who has authority takes control
        if (IsMultiplayerAuthority() && ownerId == Multiplayer.GetUniqueId())
        {
            TakeControl();
        }
    }

    private void TakeControl()
    {
        if (!IsMultiplayerAuthority()) return;

        isControlled = true;

        // Switch camera only for the local player
        if (bulletCamera != null)
        {
            GD.Print($"Enabling bullet camera for player {ownerId}");

            bulletCamera.Current = true;
        }
        else
        {
            GD.PrintErr($"No bullet camera for player {ownerId}!");
        }

        // Disable ship camera only for the local player
        if (ownerShip != null && ownerShip.IsMultiplayerAuthority())
        {
            ownerShip.DisableCamera();
            ownerShip.SetCrosshairVisible(false);
        }

        // Show bullet crosshair
        if (ownerShip != null)
        {
            ownerShip.ShowBulletCrosshair(true);
        }

        // Capture mouse for rotation control
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public void ReleaseControl()
    {
        if (!IsMultiplayerAuthority()) return;

        isControlled = false;

        // Disable bullet camera
        if (bulletCamera != null)
        {
            bulletCamera.Current = false;
        }

        // Re-enable ship camera only for the local player
        if (ownerShip != null && ownerShip.IsMultiplayerAuthority())
        {
            ownerShip.EnableCamera();
            ownerShip.SetCrosshairVisible(true); // Show ship crosshair
            ownerShip.ShowBulletCrosshair(false); // Hide bullet crosshair
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!isControlled || !IsMultiplayerAuthority()) return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            mouseRotation.X -= mouseMotion.Relative.X * 0.002f;
            mouseRotation.Y -= mouseMotion.Relative.Y * 0.002f;
            mouseRotation.Y = Mathf.Clamp(mouseRotation.Y, -1.0f, 1.0f);
        }
        if (Input.IsActionPressed("cancel_bullet"))
        {
            Explode(); // Destroy bullet and return control
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Linear speed ramp-up over rampUpTime seconds
        if (aliveTime < rampUpTime)
        {
            // Linear interpolation: speed increases from 0 to max over rampUpTime
            currentSpeed = (aliveTime / rampUpTime) * speed;
        }
        else
        {
            currentSpeed = speed; // Reached max speed
        }

        if (IsMultiplayerAuthority() && isControlled)
        {
            // Apply mouse rotation
            Rotation = new Vector3(mouseRotation.Y, mouseRotation.X, 0);

            // Update direction based on current rotation
            direction = -Transform.Basis.Z.Normalized();
            
            // Calculate velocity: direction * current speed
            velocity = direction * currentSpeed;
            
            // Send state to other players
            Rpc(MethodName.UpdateBulletState, GlobalPosition, GlobalRotation, velocity);
        }
        else if (!isControlled)
        {
            // For non-controlled bullets, just use the direction * current speed
            velocity = direction * currentSpeed;
        }

        // Move bullet
        Position += velocity * (float)delta;

        // Update camera to follow bullet smoothly
        if (cameraRig != null && isControlled && IsMultiplayerAuthority())
        {
            cameraRig.GlobalPosition = GlobalPosition;
            cameraRig.GlobalRotation = GlobalRotation;
        }

        // Check lifetime
        aliveTime += (float)delta;
        if (aliveTime >= lifetime)
        {
            Explode();
        }

        // World wrapping
        if (world != null)
        {
            var pos = Position;
            world.WrapPosition(ref pos);
            Position = pos;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void UpdateBulletState(Vector3 position, Vector3 rotation, Vector3 vel)
    {
        GlobalPosition = GlobalPosition.Lerp(position, 0.2f);
        GlobalRotation = rotation;
        velocity = vel;
        
        // Update direction for non-authoritative clients
        if (vel.Length() > 0.01f)
        {
            direction = vel.Normalized();
        }
    }

    private void OnBodyEntered(Node body)
    {
        GD.Print($"{body} entered bullet");

        // Don't hit the owner
        if (body == ownerShip) return;

        if (body is Asteroid asteroid)
        {
            asteroid.QueueFree();
            Explode();
        }
        else if (body is PlayerShip ship)
        {
            ship.Die();
            Explode();
        }
    }

    private void Explode()
    {
        // Release control before destroying
        ReleaseControl();

        // Create explosion at bullet position
        if (explosionScene != null)
        {
            var explosion = explosionScene.Instantiate<Explosion>();
            var bulletPosition = GlobalPosition; // Store position before QueueFree

            // Add to scene tree first
            GetTree().Root.AddChild(explosion);

            // Set position after it's in the tree
            explosion.GlobalPosition = bulletPosition;

            // Trigger explosion effect
            _ = explosion.Explode();
        }

        // Notify owner ship that bullet is destroyed
        if (ownerShip != null)
        {
            ownerShip.OnBulletDestroyed();
        }

        QueueFree();
    }

    // Public method to get the bullet's current velocity
    public Vector3 GetVelocity()
    {
        return velocity;
    }

    // Public method to get the owner ID
    public int GetOwnerId()
    {
        return ownerId;
    }

    public override void _ExitTree()
    {
        // Ensure control is released when bullet is freed
        ReleaseControl();
        base._ExitTree();
    }
}
