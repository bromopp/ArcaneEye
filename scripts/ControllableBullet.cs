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
    private bool wasConnected = true; // Track previous connection state
    
    // Helper method to safely check multiplayer authority
    private bool SafeIsMultiplayerAuthority()
    {
        try
        {
            return IsMultiplayerAuthority();
        }
        catch (System.Exception)
        {
            // Connection lost or multiplayer not available
            return false;
        }
    }
    
    // Check connection status and clean up if disconnected
    private bool CheckConnectionAndCleanup()
    {
        if (Multiplayer?.MultiplayerPeer == null)
        {
            // No multiplayer peer - clean up
            GD.Print($"[{Name}] No multiplayer peer found, cleaning up bullet");
            CleanupAndDestroy();
            return false;
        }
        
        var connectionStatus = MultiplayerPeer.ConnectionStatus.Disconnected;
        try
        {
            connectionStatus = Multiplayer.MultiplayerPeer.GetConnectionStatus();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[{Name}] Failed to get connection status: {ex.Message}");
            CleanupAndDestroy();
            return false;
        }
        
        bool isConnected = connectionStatus == MultiplayerPeer.ConnectionStatus.Connected;
        
        // If we were connected but now we're not, clean up
        if (wasConnected && !isConnected)
        {
            GD.Print($"[{Name}] Connection lost (was connected: {wasConnected}, now: {isConnected}), cleaning up bullet");
            CleanupAndDestroy();
            return false;
        }
        
        // Update connection state
        wasConnected = isConnected;
        return true;
    }
    
    // Clean up and destroy the bullet safely
    private void CleanupAndDestroy()
    {
        // Release control first
        if (isControlled)
        {
            ReleaseControl();
        }
        
        // Clean up without explosion (connection lost scenario)
        QueueFree();
    }
    
    // Handle peer disconnection
    private void OnPeerDisconnected(long id)
    {
        // If the owner disconnected, clean up the bullet
        if (id == ownerId)
        {
            GD.Print($"[{Name}] Owner {ownerId} disconnected, cleaning up bullet");
            CleanupAndDestroy();
        }
    }
    
    // Handle server disconnection
    private void OnServerDisconnected()
    {
        GD.Print($"[{Name}] Server disconnected, cleaning up bullet");
        CleanupAndDestroy();
    }
    
    public override void _Ready()
    {
        // Create visual - safely get WorldGenerator
        var worldNode = GetNodeOrNull<WorldGenerator>("/root/Main/WorldGenerator");
        if (IsInstanceValid(worldNode))
        {
            world = worldNode;
        }

        // Set up camera
        if (SafeIsMultiplayerAuthority() && Multiplayer?.MultiplayerPeer != null)
        {
            SetupCamera();
        }
        // Connect signals
        BodyEntered += OnBodyEntered;
        
        // Monitor multiplayer disconnections
        if (Multiplayer != null)
        {
            Multiplayer.PeerDisconnected += OnPeerDisconnected;
            Multiplayer.ServerDisconnected += OnServerDisconnected;
        }

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
        if (SafeIsMultiplayerAuthority() && Multiplayer?.MultiplayerPeer != null && ownerId == Multiplayer.GetUniqueId())
        {
            TakeControl();
        }
    }

    private void TakeControl()
    {
        if (!SafeIsMultiplayerAuthority()) return;

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
        if (IsInstanceValid(ownerShip) && ownerShip.IsMultiplayerAuthority())
        {
            ownerShip.DisableCamera();
            ownerShip.SetCrosshairVisible(false);
        }

        // Show bullet crosshair
        if (IsInstanceValid(ownerShip))
        {
            ownerShip.ShowBulletCrosshair(true);
        }

        // Capture mouse for rotation control
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public void ReleaseControl()
    {
        if (!SafeIsMultiplayerAuthority()) return;

        isControlled = false;

        // Disable bullet camera
        if (bulletCamera != null)
        {
            bulletCamera.Current = false;
        }

        // Re-enable ship camera only for the local player
        if (IsInstanceValid(ownerShip) && ownerShip.IsMultiplayerAuthority())
        {
            ownerShip.EnableCamera();
            ownerShip.SetCrosshairVisible(true); // Show ship crosshair
            ownerShip.ShowBulletCrosshair(false); // Hide bullet crosshair
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!isControlled || !SafeIsMultiplayerAuthority()) return;

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
        // Check for connection loss and clean up if disconnected
        if (!CheckConnectionAndCleanup())
        {
            return; // Bullet will be destroyed, exit early
        }
        
        if (Multiplayer?.MultiplayerPeer != null)
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

            if (SafeIsMultiplayerAuthority() && isControlled)
            {
                // Apply mouse rotation
                Rotation = new Vector3(mouseRotation.Y, mouseRotation.X, 0);

                // Update direction based on current rotation
                direction = -Transform.Basis.Z.Normalized();

                // Calculate velocity: direction * current speed
                velocity = direction * currentSpeed;

                // Send state to other players - check connection status
                if (Multiplayer?.MultiplayerPeer != null && 
                    Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
                {
                    try
                    {
                        Rpc(MethodName.UpdateBulletState, GlobalPosition, GlobalRotation, velocity);
                    }
                    catch (System.Exception ex)
                    {
                        GD.PrintErr($"Failed to send RPC: {ex.Message}");
                        // Continue execution - don't let RPC failures crash the bullet
                    }
                }
            }
            else if (!isControlled)
            {
                // For non-controlled bullets, just use the direction * current speed
                velocity = direction * currentSpeed;
            }

            // Move bullet
            Position += velocity * (float)delta;

            // Update camera to follow bullet smoothly
            if (cameraRig != null && isControlled && SafeIsMultiplayerAuthority() && Multiplayer?.MultiplayerPeer != null)
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
            if (IsInstanceValid(world))
            {
                var pos = Position;
                world.WrapPosition(ref pos);
                Position = pos;
            }
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

        // Don't hit the owner - check if owner still exists
        if (IsInstanceValid(ownerShip) && body == ownerShip) return;

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
        if (explosionScene != null && IsInsideTree())
        {
            try
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
            catch (System.Exception ex)
            {
                GD.PrintErr($"Failed to create explosion: {ex.Message}");
                // Continue with cleanup even if explosion fails
            }
        }

        // Notify owner ship that bullet is destroyed
        if (IsInstanceValid(ownerShip))
        {
            try
            {
                ownerShip.OnBulletDestroyed();
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"Failed to notify owner ship of bullet destruction: {ex.Message}");
                // Continue with cleanup even if notification fails
            }
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
        // Disconnect multiplayer signals to prevent memory leaks
        if (Multiplayer != null)
        {
            try
            {
                Multiplayer.PeerDisconnected -= OnPeerDisconnected;
                Multiplayer.ServerDisconnected -= OnServerDisconnected;
            }
            catch (System.Exception)
            {
                // Ignore errors during cleanup
            }
        }
        
        // Ensure control is released when bullet is freed
        ReleaseControl();
        base._ExitTree();
    }
}
