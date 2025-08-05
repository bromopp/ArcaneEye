using Godot;

public partial class PlayerShip : RigidBody3D
{
    [Export] private float thrustPower = 500.0f;
    [Export] private float rotationSpeed = 3.0f;
    [Export] private float maxSpeed = 100.0f;
    [Export] private PackedScene bulletScene;

    // Camera settings
    [Export] private float cameraDistance = 10.0f;
    [Export] private float cameraHeight = 5.0f;
    [Export] private float cameraSmoothness = 5.0f;

    private int playerId = 1;
    private Color playerColor = Colors.White;
    private MeshInstance3D meshInstance;
    private WorldGenerator world;
    private Camera3D playerCamera;
    private Node3D cameraRig;

    // Input state (for multiplayer sync)
    private Vector2 inputVector = Vector2.Zero;
    private bool isFiring = false;
    private float lastFireTime = 0.0f;
    private float fireRate = 0.25f; // Fire every 0.25 seconds

    public override void _Ready()
    {
        // Get world reference
        world = GetNode<WorldGenerator>("/root/Main/WorldGenerator");

        // Set up mesh
        meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
        if (meshInstance == null)
        {
            meshInstance = new MeshInstance3D();
            AddChild(meshInstance);
        }

        CreateShipMesh();

        // Physics setup
        GravityScale = 0.0f;
        LinearDamp = 0.5f;
        AngularDamp = 2.0f;

        // Set player color
        if (meshInstance.MaterialOverride is StandardMaterial3D material)
        {
            material.AlbedoColor = playerColor;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Only process input if we have authority
        if (IsMultiplayerAuthority())
        {
            HandleInput();

            // Send input state to other players
            Rpc(MethodName.UpdatePlayerState, GlobalPosition, GlobalRotation, LinearVelocity, inputVector, isFiring);
        }

        // Apply movement (for local player)
        if (IsMultiplayerAuthority())
        {
            ApplyMovement((float)delta);
        }

        // Handle firing
        if (isFiring && Time.GetUnixTimeFromSystem() - lastFireTime > fireRate)
        {
            Fire();
            lastFireTime = (float)Time.GetUnixTimeFromSystem();
        }

        // World wrapping
        if (world != null)
        {
            var pos = Position;
            world.WrapPosition(ref pos);
            Position = pos;
        }
    }

    private void HandleInput()
    {
        inputVector = Vector2.Zero;

        // Rotation
        if (Input.IsActionPressed("turn_left"))
            inputVector.X = -1;
        else if (Input.IsActionPressed("turn_right"))
            inputVector.X = 1;

        // Thrust
        if (Input.IsActionPressed("thrust"))
            inputVector.Y = 1;

        // Fire
        isFiring = Input.IsActionPressed("fire");
    }

    private void ApplyMovement(float delta)
    {
        // Rotation
        if (inputVector.X != 0)
        {
            var rotation = Transform.Basis.GetEuler();
            rotation.Y -= inputVector.X * rotationSpeed * delta;
            Transform = Transform.LookingAt(GlobalPosition + Transform.Basis.Z, Vector3.Up);
            RotateY(-inputVector.X * rotationSpeed * delta);
        }

        // Thrust
        if (inputVector.Y > 0)
        {
            var forward = -Transform.Basis.Z;
            ApplyCentralForce(forward * thrustPower * delta);

            // Clamp max speed
            if (LinearVelocity.Length() > maxSpeed)
            {
                LinearVelocity = LinearVelocity.Normalized() * maxSpeed;
            }
        }
    }

    private void Fire()
    {
        if (bulletScene == null) return;

        var bullet = bulletScene.Instantiate<Node3D>();
        GetTree().Root.AddChild(bullet);

        bullet.GlobalPosition = GlobalPosition + -Transform.Basis.Z * 2.0f;
        bullet.GlobalRotation = GlobalRotation;

        if (bullet.HasMethod("Initialize"))
        {
            bullet.Call("Initialize", playerId, -Transform.Basis.Z, playerColor);
        }

        // Sync bullet creation across network
        if (IsMultiplayerAuthority())
        {
            Rpc(MethodName.SpawnBullet, GlobalPosition, GlobalRotation, -Transform.Basis.Z);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void UpdatePlayerState(Vector3 position, Vector3 rotation, Vector3 velocity, Vector2 input, bool firing)
    {
        // Interpolate position and rotation for smooth movement
        GlobalPosition = GlobalPosition.Lerp(position, 0.1f);
        GlobalRotation = rotation;
        LinearVelocity = velocity;
        inputVector = input;
        isFiring = firing;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SpawnBullet(Vector3 position, Vector3 rotation, Vector3 direction)
    {
        if (bulletScene == null) return;

        var bullet = bulletScene.Instantiate<Node3D>();
        GetTree().Root.AddChild(bullet);

        bullet.GlobalPosition = position;
        bullet.GlobalRotation = rotation;

        if (bullet.HasMethod("Initialize"))
        {
            bullet.Call("Initialize", playerId, direction, playerColor);
        }
    }

    private void CreateShipMesh()
    {
        // Create a simple triangle ship mesh
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        // Triangle vertices for a simple ship shape
        var vertices = new Vector3[]
        {
            new Vector3(0, 0, -1.5f),  // Front
            new Vector3(-1, 0, 1),     // Left back
            new Vector3(1, 0, 1),      // Right back
            new Vector3(0, 0.5f, 0.5f) // Top
        };

        var uvs = new Vector2[]
        {
            new Vector2(0.5f, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(0.5f, 0.5f)
        };

        // Define triangles
        var indices = new int[]
        {
            0, 1, 2,  // Bottom
            0, 3, 1,  // Left side
            0, 2, 3,  // Right side
            1, 3, 2   // Back
        };

        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var arrayMesh = new ArrayMesh();
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        meshInstance.Mesh = arrayMesh;
        meshInstance.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = playerColor,
            Metallic = 0.3f,
            Roughness = 0.7f
        };

        // Add collision shape
        var collision = new CollisionShape3D();
        var shape = new ConvexPolygonShape3D();
        shape.Points = vertices;
        collision.Shape = shape;
        AddChild(collision);
    }

    public void SetPlayerId(int id)
    {
        playerId = id;
        SetMultiplayerAuthority(id);
    }

    public void SetPlayerColor(Color color)
    {
        playerColor = color;
        if (meshInstance?.MaterialOverride is StandardMaterial3D material)
        {
            material.AlbedoColor = color;
        }
    }

    private void SetupCamera()
    {
        // Create camera rig for smooth following
        cameraRig = new Node3D();
        AddChild(cameraRig);

        // Create camera
        playerCamera = new Camera3D();
        playerCamera.Position = new Vector3(0, cameraHeight, cameraDistance);
        playerCamera.LookAt(Vector3.Zero, Vector3.Up);
        playerCamera.Fov = 75;
        cameraRig.AddChild(playerCamera);

        // Only enable camera for the local player
        UpdateCameraState();
    }

    private void UpdateCameraState()
    {
        if (playerCamera != null)
        {
            // Enable camera only for the player who owns this ship
            playerCamera.Current = IsMultiplayerAuthority();

            // Also disable the main scene camera if this is our ship
            if (IsMultiplayerAuthority())
            {
                var mainCamera = GetNode<Camera3D>("/root/Main/Camera3D");
                if (mainCamera != null)
                    mainCamera.Current = false;
            }
        }
    }
    
        private void UpdateCamera(float delta)
    {
        if (cameraRig == null || playerCamera == null) return;
        
        // Smooth camera follow
        var targetRotation = GlobalRotation;
        cameraRig.GlobalRotation = cameraRig.GlobalRotation.Lerp(targetRotation, cameraSmoothness * delta);
        
    }
}