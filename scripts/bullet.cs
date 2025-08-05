using Godot;

public partial class Bullet : Area3D
{
    [Export] private float speed = 200.0f;
    [Export] private float lifetime = 2.0f;
    
    private Vector3 direction = Vector3.Forward;
    private int ownerId = -1;
    private Color bulletColor = Colors.White;
    private float aliveTime = 0.0f;
    
    public override void _Ready()
    {
        // Create bullet mesh
        var meshInstance = new MeshInstance3D();
        AddChild(meshInstance);
        
        var sphereMesh = new SphereMesh();
        sphereMesh.RadialSegments = 8;
        sphereMesh.Rings = 4;
        sphereMesh.Radius = 0.1f;
        sphereMesh.Height = 0.2f;
        
        meshInstance.Mesh = sphereMesh;
        meshInstance.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = bulletColor,
            EmissionEnabled = true,
            Emission = bulletColor,
            EmissionIntensity = 2.0f
        };
        
        // Add collision
        var collision = new CollisionShape3D();
        var shape = new SphereShape3D { Radius = 0.1f };
        collision.Shape = shape;
        AddChild(collision);
        
        // Connect area signals
        BodyEntered += OnBodyEntered;
    }
    
    public override void _PhysicsProcess(double delta)
    {
        Position += direction * speed * (float)delta;
        
        aliveTime += (float)delta;
        if (aliveTime >= lifetime)
        {
            QueueFree();
        }
    }
    
    public void Initialize(int playerId, Vector3 dir, Color color)
    {
        ownerId = playerId;
        direction = dir.Normalized();
        bulletColor = color;
    }
    
    private void OnBodyEntered(Node3D body)
    {
        // Check if hit an asteroid
        if (body is Asteroid)
        {
            // Destroy asteroid (implement asteroid destruction)
            body.QueueFree();
            QueueFree();
        }
        // Check if hit another player (not self)
        else if (body.HasMethod("SetPlayerId") && body.Get("player_id").AsInt32() != ownerId)
        {
            // Damage player (implement health system)
            QueueFree();
        }
    }
}