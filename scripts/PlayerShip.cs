using Godot;

public partial class PlayerShip : RigidBody3D
{
    [Signal]
    public delegate void HitEventHandler();
    [Export] private float thrustPower = 5.0f;
    [Export] private float rotationSpeed = 3.0f;
    [Export] private float maxSpeed = 50.0f;
    [Export] private PackedScene bulletScene;

    [Export] private float mouseSensitivity = 0.002f;

    // Camera settings
    [Export] private float cameraDistance = 15.0f;
    [Export] private float cameraHeight = 5.0f;
    [Export] private float cameraSmoothness = 5.0f;

    [Export] private int maxLives = 3;
    [Export] private float immunityDuration = 3.0f;
    [Export] private float respawnDelay = 1.0f;

    private int playerId = 1;
    private Color playerColor = Colors.White;
    private MeshInstance3D meshInstance;
    private WorldGenerator world;
    private Camera3D playerCamera;
    private Node3D cameraRig;

    private Vector2 mouseRotation = Vector2.Zero;

    private ControllableBullet activeBullet = null;
    private bool canFire = true;
    private bool fireButtonPressed = false; // Track button state

    // Input state (for multiplayer sync)
    private Vector2 inputVector = Vector2.Zero;
    private bool isFiring = false;

    // Crosshair
    private Control crosshairUI;
    private TextureRect shipCrosshair;
    private TextureRect bulletCrosshair;

    // Stats
    private int currentLives;
    private bool isAlive = true;
    private bool isImmune = false;
    private float immunityTimer = 0.0f;

    // State tracking for movement/shooting
    private Vector3 spawnPosition;
    private bool hasMovedSinceSpawn = false;

    public override void _Ready()
    {

        // Debug print
        GD.Print($"PlayerShip {Name} Ready:");
        GD.Print($"  - Player ID: {playerId}");
        GD.Print($"  - Bullet Scene: {(bulletScene != null ? "SET" : "NULL")}");
        if (bulletScene != null)
        {
            GD.Print($"  - Bullet Scene Path: {bulletScene.ResourcePath}");
        }
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
        SetupCamera();

        // Create crosshair UI only for local player

        SetupCrosshair();


        // Physics setup
        GravityScale = 0.0f;
        LinearDamp = 0.5f;
        AngularDamp = 2.0f;

        // Set player color
        if (meshInstance.MaterialOverride is StandardMaterial3D material)
        {
            material.AlbedoColor = playerColor;
        }

        if (IsMultiplayerAuthority())
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    private void SetupCrosshair()
    {
        // Create UI layer for crosshair
        var canvasLayer = new CanvasLayer();
        canvasLayer.Name = "CrosshairLayer";
        AddChild(canvasLayer);

        // Create control for centering
        crosshairUI = new Control();
        crosshairUI.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        canvasLayer.AddChild(crosshairUI);

        // Create ship crosshair (cross shape)
        shipCrosshair = CreateCrossCrosshair();
        crosshairUI.AddChild(shipCrosshair);

        // Create bullet crosshair (circular)
        bulletCrosshair = CreateCircularCrosshair();
        bulletCrosshair.Visible = false;
        crosshairUI.AddChild(bulletCrosshair);
    }

    private TextureRect CreateCrossCrosshair()
    {
        // Create a cross-shaped crosshair using ImageTexture
        var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0)); // Transparent background

        var crossColor = new Color(0, 1, 0, 0.8f); // Green with some transparency

        // Draw horizontal line
        for (int x = 10; x < 22; x++)
        {
            if (x < 14 || x > 17) // Gap in center
            {
                image.SetPixel(x, 15, crossColor);
                image.SetPixel(x, 16, crossColor);
            }
        }

        // Draw vertical line
        for (int y = 10; y < 22; y++)
        {
            if (y < 14 || y > 17) // Gap in center
            {
                image.SetPixel(15, y, crossColor);
                image.SetPixel(16, y, crossColor);
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        var textureRect = new TextureRect();
        textureRect.Texture = texture;
        textureRect.Position = new Vector2(-16, -16);

        return textureRect;
    }

    private TextureRect CreateCircularCrosshair()
    {
        // Create a circular crosshair
        var image = Image.CreateEmpty(48, 48, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0)); // Transparent background

        var circleColor = new Color(1, 0.5f, 0, 0.9f); // Orange
        var centerColor = new Color(1, 1, 0, 1); // Yellow center dot

        // Draw circle
        float centerX = 24;
        float centerY = 24;
        float radius = 20;

        for (int x = 0; x < 48; x++)
        {
            for (int y = 0; y < 48; y++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // Draw circle outline
                if (distance >= radius - 1.5f && distance <= radius + 1.5f)
                {
                    image.SetPixel(x, y, circleColor);
                }

                // Draw center dot
                if (distance <= 2)
                {
                    image.SetPixel(x, y, centerColor);
                }
            }
        }

        // Add cross lines inside circle
        for (int i = -10; i <= 10; i++)
        {
            if (Mathf.Abs(i) > 3) // Gap in center
            {
                image.SetPixel((int)centerX + i, (int)centerY, circleColor);
                image.SetPixel((int)centerX, (int)centerY + i, circleColor);
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        var textureRect = new TextureRect();
        textureRect.Texture = texture;
        textureRect.Position = new Vector2(-24, -24);

        return textureRect;
    }

    public void SetCrosshairVisible(bool visible)
    {
        if (shipCrosshair != null)
            shipCrosshair.Visible = visible;
    }

    public void ShowBulletCrosshair(bool show)
    {
        if (bulletCrosshair != null)
            bulletCrosshair.Visible = show;
    }

    private void SetupCamera()
    {
        // Get existing camera rig from scene
        cameraRig = GetNodeOrNull<Node3D>("CameraRig");
        if (cameraRig == null)
        {
            cameraRig = new Node3D();
            AddChild(cameraRig);
        }

        playerCamera = cameraRig.GetNodeOrNull<Camera3D>("Camera3D");
        if (playerCamera == null)
        {
            playerCamera = new Camera3D();
            playerCamera.Position = new Vector3(0, cameraHeight, -cameraDistance);
            playerCamera.LookAtFromPosition(playerCamera.Position, Vector3.Zero, Vector3.Up);
            playerCamera.Fov = 75;
            cameraRig.AddChild(playerCamera);
        }

        UpdateCameraState();
    }

    private void UpdateCameraState()
    {
        if (playerCamera != null)
        {
            playerCamera.Current = IsMultiplayerAuthority();
        }
    }

    public void DisableCamera()
    {
        if (playerCamera != null && IsMultiplayerAuthority())
            playerCamera.Current = false;
    }

    public void EnableCamera()
    {
        if (playerCamera != null && IsMultiplayerAuthority())
            playerCamera.Current = true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsMultiplayerAuthority()) return;

        // Don't process mouse input if controlling bullet
        if (activeBullet != null) return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            mouseRotation.X -= mouseMotion.Relative.X * mouseSensitivity;
            mouseRotation.Y -= mouseMotion.Relative.Y * mouseSensitivity;
            mouseRotation.Y = Mathf.Clamp(mouseRotation.Y, -1.5f, 1.5f); // Limit vertical look
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Only process input if we have authority
        if (IsMultiplayerAuthority())
        {
            HandleInput();

            // Apply mouse rotation to ship
            if (activeBullet == null) // Only rotate ship when not controlling bullet
            {
                GlobalRotation = new Vector3(0, mouseRotation.X, 0);
            }

            UpdateCamera((float)delta);

            // Send input state to other players
            Rpc(MethodName.UpdatePlayerState, GlobalPosition, GlobalRotation, LinearVelocity, inputVector, isFiring);
        }

        // Apply movement
        if (IsMultiplayerAuthority())
        {
            ApplyMovement((float)delta);

            bool currentFirePressed = Input.IsActionPressed("fire");

            // Fire only on button press, not hold
            if (currentFirePressed && !fireButtonPressed && canFire && activeBullet == null)
            {
                Fire();
            }

            fireButtonPressed = currentFirePressed;
        }

        // World wrapping
        if (world != null)
        {
            var pos = Position;
            world.WrapPosition(ref pos);
            Position = pos;
        }
    }

    private void UpdateCamera(float delta)
    {
        if (cameraRig == null || playerCamera == null || activeBullet != null) return;

        // Camera follows ship rotation with some vertical tilt
        var targetRotation = new Vector3(mouseRotation.Y, GlobalRotation.Y, 0);
        cameraRig.GlobalRotation = cameraRig.GlobalRotation.Lerp(targetRotation, cameraSmoothness * delta);
    }

    private void HandleInput()
    {
        inputVector = Vector2.Zero;

        // Don't allow ship control while controlling bullet
        if (activeBullet != null)
        {
            isFiring = false;
            return;
        }

        // Rotation with keys (backup for mouse)
        if (Input.IsActionPressed("turn_left"))
            mouseRotation.X += rotationSpeed * (float)GetPhysicsProcessDeltaTime();
        else if (Input.IsActionPressed("turn_right"))
            mouseRotation.X -= rotationSpeed * (float)GetPhysicsProcessDeltaTime();

        // Thrust
        if (Input.IsActionPressed("thrust"))
            inputVector.Y = 1;

        // Note: Fire is handled separately with button state tracking
    }

    private void ApplyMovement(float delta)
    {
        // Don't move ship while controlling bullet
        if (activeBullet != null) return;

        // Thrust
        if (inputVector.Y > 0)
        {
            var forward = -cameraRig.GlobalTransform.Basis.Z;
            ApplyCentralForce(forward * thrustPower);

            // Clamp max speed
            if (LinearVelocity.Length() > maxSpeed)
            {
                LinearVelocity = LinearVelocity.Normalized() * maxSpeed;
            }
        }
    }

    private void Fire()
    {
        if (bulletScene == null || !canFire || activeBullet != null) return;
        GD.Print($"Player {playerId} (ID: {Multiplayer.GetUniqueId()}) firing bullet");

        canFire = false;

        var bullet = bulletScene.Instantiate<ControllableBullet>();
        bullet.SetMultiplayerAuthority(Multiplayer.GetUniqueId()); // Use actual network ID

        GetTree().Root.AddChild(bullet);

        var firingDirection = -cameraRig.GlobalTransform.Basis.Z;
        bullet.GlobalPosition = GlobalPosition + firingDirection * 2.0f;
        bullet.GlobalRotation = cameraRig.GlobalRotation;

        // Initialize bullet with reference to this ship
        bullet.Initialize(playerId, firingDirection, playerColor, this, cameraRig.GlobalRotation);


        // Track active bullet
        activeBullet = bullet;

        // Sync bullet creation across network
        Rpc(MethodName.SpawnBullet, GlobalPosition, cameraRig.GlobalRotation, firingDirection);

    }

    public void OnBulletDestroyed()
    {
        activeBullet = null;
        canFire = true;

        // Re-enable camera
        if (IsMultiplayerAuthority())
        {
            EnableCamera();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void UpdatePlayerState(Vector3 position, Vector3 rotation, Vector3 velocity, Vector2 input, bool firing)
    {
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

        var bullet = bulletScene.Instantiate<ControllableBullet>();
        GetTree().Root.AddChild(bullet);

        bullet.GlobalPosition = position + direction * 2.0f;

        if (bullet.HasMethod("Initialize"))
        {
            bullet.Call("Initialize", playerId, direction, playerColor, this, rotation);
        }
    }

    private void CreateShipMesh()
    {
        // Use the existing mesh from the scene if available
        if (meshInstance.Mesh != null)
        {
            // Apply color to existing mesh
            meshInstance.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = playerColor,
                Metallic = 0.3f,
                Roughness = 0.7f
            };
            return;
        }

        // Create a simple triangle ship mesh if no mesh exists
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

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
    }

    public void SetPlayerId(int id)
    {
        playerId = id;
        SetMultiplayerAuthority(id);
        if (IsNodeReady())
            UpdateCameraState();
    }

    public void SetPlayerColor(Color color)
    {
        playerColor = color;
        if (meshInstance?.MaterialOverride is StandardMaterial3D material)
        {
            material.AlbedoColor = color;
        }
    }

    public int GetPlayerId()
    {
        return playerId;
    }

    public void Die()
    {
        EmitSignal(SignalName.Hit);
    }
    private void OnBulletDetectorBodyEntered(Node3D body)
    {
        Die();
    }
}