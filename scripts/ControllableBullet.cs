// ControllableBullet.cs - New bullet class with player control
using Godot;

public partial class ControllableBullet : Area3D
{
    [Export] private float speed = 20.0f;
    [Export] private float lifetime = 5.0f; // Longer lifetime for controlled bullets
    [Export] private float rotationSpeed = 2.0f;
    [Export] private float cameraDistance = 8.0f;
    [Export] private float cameraHeight = 2.0f;

    [Export] private PackedScene explosionScene;

    private Vector3 velocity;
    private int ownerId = -1;
    private Color bulletColor = Colors.White;
    private float aliveTime = 0.0f;

    private Camera3D bulletCamera;
    private Node3D cameraRig;
    private PlayerShip ownerShip;
    private Vector2 mouseRotation = Vector2.Zero;
    private bool isControlled = false;

    public override void _Ready()
    {
        // Set up collision
        CollisionLayer = 1 << 1;  // Bullet layer
        CollisionMask = (1 << 2) | (1 << 3);  // Detect asteroids and players

        // Create visual
        CreateBulletMesh();

        // Create collision shape
        var collision = new CollisionShape3D();
        var shape = new SphereShape3D { Radius = 0.2f };
        collision.Shape = shape;
        AddChild(collision);

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
        trail.Lifetime = 0.5f;
        trail.EmissionShape = CpuParticles3D.EmissionShapeEnum.Point;
        trail.Direction = Vector3.Back;
        trail.InitialVelocityMin = 5.0f;
        trail.Scale = Vector3.One * 0.1f;
        AddChild(trail);
    }

    private void CreateBulletMesh()
    {
        var meshInstance = new MeshInstance3D();
        AddChild(meshInstance);

        // Create a more interesting bullet shape
        var capsuleMesh = new CapsuleMesh();
        capsuleMesh.Height = 0.8f;
        capsuleMesh.Radius = 0.15f;

        meshInstance.Mesh = capsuleMesh;
        meshInstance.Rotation = new Vector3(Mathf.Pi / 2, 0, 0); // Point forward

        meshInstance.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = bulletColor,
            EmissionEnabled = true,
            Emission = bulletColor,
            EmissionIntensity = 3.0f
        };
    }

    private void SetupCamera()
    {
        // Create camera rig for smooth following
        cameraRig = new Node3D();
        AddChild(cameraRig);

        // Create camera
        bulletCamera = new Camera3D();
        bulletCamera.Position = new Vector3(0, cameraHeight, -cameraDistance);

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
        velocity = startDirection.Normalized() * speed;
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
        if (IsMultiplayerAuthority() && isControlled)
        {
            // Apply mouse rotation
            Rotation = new Vector3(mouseRotation.Y, mouseRotation.X, 0);

            // Update velocity direction based on rotation
            velocity = -Transform.Basis.Z * speed;

            // Send state to other players
            Rpc(MethodName.UpdateBulletState, GlobalPosition, GlobalRotation, velocity);
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
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void UpdateBulletState(Vector3 position, Vector3 rotation, Vector3 vel)
    {
        GlobalPosition = GlobalPosition.Lerp(position, 0.2f);
        GlobalRotation = rotation;
        velocity = vel;
    }

    private void OnBodyEntered(Node3D body)
    {
        // Don't hit the owner
        if (body == ownerShip) return;

        if (body is Asteroid asteroid)
        {
            asteroid.QueueFree();
            Explode();
        }
        else if (body is PlayerShip ship)
        {
            if (ship.HasMethod("Die"))
            {
                ship.Call("Die", 25.0f);
            }
            Explode();
        }
    }

    private void Explode()
    {
        // Release control before destroying
        ReleaseControl();

        Explosion explosion = explosionScene.Instantiate<Explosion>();
        // SpawnExplosion(GlobalPosition);

        // Notify owner ship that bullet is destroyed
        if (ownerShip != null)
        {
            ownerShip.OnBulletDestroyed();
        }

        QueueFree();
    }

    public override void _ExitTree()
    {
        // Ensure control is released when bullet is freed
        ReleaseControl();
        base._ExitTree();
    }
}
