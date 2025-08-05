using Godot;
using System;

public partial class Asteroid : RigidBody3D
{
	[Export] private float minSpeed = 0.01f;
	[Export] private float maxSpeed = 0.1f;
	[Export] private float minRotationSpeed = 0.5f;
	[Export] private float maxRotationSpeed = 2.0f;
	
	private WorldGenerator world;
	private Vector3 velocity;
	private Vector3 rotationSpeed;
	private MeshInstance3D meshInstance;
	
	public override void _Ready()
	{
		// Set up the asteroid mesh
		meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
		if (meshInstance == null)
		{
			meshInstance = new MeshInstance3D();
			AddChild(meshInstance);
		}
		
		// Create a rock-like mesh
		CreateAsteroidMesh();
		
		// Disable gravity and set up physics
		GravityScale = 0.0f;
		LinearDamp = 0.0f;
		AngularDamp = 0.0f;
		
		// Random initial rotation
		Rotation = new Vector3(
			GD.Randf() * Mathf.Pi * 2,
			GD.Randf() * Mathf.Pi * 2,
			GD.Randf() * Mathf.Pi * 2
		);
	}
	
	public void Initialize(WorldGenerator worldGen)
	{
		world = worldGen;
		
		// Random velocity
		velocity = new Vector3(
			0,0,0
		).Normalized() * (float)GD.RandRange(minSpeed, maxSpeed);
		
		// Random rotation speed
		rotationSpeed = new Vector3(
			GD.Randf() * 2 - 1,
			GD.Randf() * 2 - 1,
			GD.Randf() * 2 - 1
		) * (float)GD.RandRange(minRotationSpeed, maxRotationSpeed);
		
		// Apply initial velocity
		LinearVelocity = velocity;
		AngularVelocity = rotationSpeed;
	}
	
	private void CreateAsteroidMesh()
	{
		// Create an irregular asteroid shape using a modified sphere
		var sphereMesh = new SphereMesh
		{
			RadialSegments = 8,
			Rings = 6,
			Radius = 1.0f,
			Height = 2.0f
		};
		
		// Create material with random brownish color
		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(
				0.5f + GD.Randf() * 0.3f,
				0.3f + GD.Randf() * 0.2f,
				0.2f + GD.Randf() * 0.1f
			),
			Roughness = 0.9f,
			Metallic = 0.1f
		};
		
		meshInstance.Mesh = sphereMesh;
		meshInstance.MaterialOverride = material;
		
		// Add collision shape
		var collision = new CollisionShape3D();
		var shape = new SphereShape3D { Radius = 1.0f };
		collision.Shape = shape;
		AddChild(collision);
	}
	
	public override void _PhysicsProcess(double delta)
	{
		// Check for world wrapping
		if (world != null)
		{
			var pos = Position;
			world.WrapPosition(ref pos);
			Position = pos;
			
			// Maintain velocity through wrapping
			LinearVelocity = velocity;
			AngularVelocity = rotationSpeed;
		}
	}
}
