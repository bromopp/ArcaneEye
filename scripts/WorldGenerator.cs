using Godot;
using System.Collections.Generic;

public partial class WorldGenerator : Node3D
{
	// Constants
	private const int PLAYER_GRID_LINES = 8;
	private const float PLAYER_GRID_PULSE_SPEED = 3.0f;
	private const float ASTEROID_BOUND_FACTOR = 0.8f;
	
	[Export] private float worldSize = 100.0f;
	[Export] private int asteroidCount = 15;
	[Export] private bool showWorldBounds = true;
	[Export] private bool showGrid = true;
	[Export] private PackedScene asteroidScene;
	[Export] private float proximityDistance = 10.0f; // Distance to show bounds
	[Export] private float gridRadius = 10.0f; // Radius for per-player grid visualization
	
	private int worldSeed;
	private RandomNumberGenerator rng;
	
	// Per-player grid visualization using shader
	private MeshInstance3D boundaryGridMesh;
	private ShaderMaterial gridShaderMaterial;
	
	// World components
	private MeshInstance3D worldBoundsMesh;
	private List<Asteroid> asteroids = new List<Asteroid>();
	
	public float WorldSize => worldSize;
	public int WorldSeed => worldSeed;
	
	public override void _Ready()
	{
		// Start monitoring player proximity for bounds visibility
		SetProcess(true);
		
		// Initialize shader-based grid system
		InitializeShaderGrid();
	}
	
	public void GenerateWorld()
	{
		GenerateWorld(GenerateRandomSeed());
	}
	
	public void GenerateWorld(int seed)
	{
		worldSeed = seed;
		rng = new RandomNumberGenerator();
		rng.Seed = (ulong)seed;
		
		ClearWorld();
		GenerateAsteroids();
	}
	
	private int GenerateRandomSeed()
	{
		return (int)(Time.GetUnixTimeFromSystem() % int.MaxValue);
	}
	
	private void ClearWorld()
	{
		// Clear asteroids
		foreach (var asteroid in asteroids)
		{
			asteroid?.QueueFree();
		}
		asteroids.Clear();
	}
	
	private void InitializeShaderGrid()
	{
		// Create a large cube mesh that covers the world boundaries
		boundaryGridMesh = new MeshInstance3D();
		AddChild(boundaryGridMesh);
		
		// Create box mesh covering the world boundaries
		var boxMesh = new BoxMesh();
		boxMesh.Size = new Vector3(worldSize, worldSize, worldSize);
		boundaryGridMesh.Mesh = boxMesh;
		
		// Load the boundary grid shader
		var shader = GD.Load<Shader>("res://shaders/boundary_grid.gdshader");
		gridShaderMaterial = new ShaderMaterial();
		gridShaderMaterial.Shader = shader;
		
		// Set shader parameters for wormhole effect
		gridShaderMaterial.SetShaderParameter("world_size", worldSize);
		gridShaderMaterial.SetShaderParameter("proximity_distance", proximityDistance);
		gridShaderMaterial.SetShaderParameter("distortion_strength", 1.0f);
		gridShaderMaterial.SetShaderParameter("wormhole_speed", 0.5f);
		gridShaderMaterial.SetShaderParameter("spiral_intensity", 3.0f);
		
		// Depth testing and transparency are handled in the shader
		
		boundaryGridMesh.MaterialOverride = gridShaderMaterial;
		boundaryGridMesh.Visible = showGrid;
	}
		
	private void GenerateAsteroids()
	{
		if (asteroidScene == null)
		{
			GD.PrintErr("Asteroid scene not set!");
			return;
		}
		
		float bound = worldSize / 2.0f * ASTEROID_BOUND_FACTOR; // Keep asteroids away from edges initially
		
		for (int i = 0; i < asteroidCount; i++)
		{
			var asteroid = asteroidScene.Instantiate<Asteroid>();
			AddChild(asteroid);
			
			// Random position
			asteroid.Position = new Vector3(
				rng.Randf() * bound * 2 - bound,
				rng.Randf() * bound * 2 - bound,
				rng.Randf() * bound * 2 - bound
			);
			
			// Random size
			float size = rng.Randf() * 2.0f + 1.0f;
			asteroid.Scale = Vector3.One * size;
			
			// Initialize with world reference
			asteroid.Initialize(this);
			asteroids.Add(asteroid);
		}
	}
	
	public void WrapPosition(ref Vector3 position)
	{
		float half = worldSize / 2.0f;
		
		if (position.X > half) position.X = -half;
		else if (position.X < -half) position.X = half;
		
		if (position.Y > half) position.Y = -half;
		else if (position.Y < -half) position.Y = half;
		
		if (position.Z > half) position.Z = -half;
		else if (position.Z < -half) position.Z = half;
	}

	public override void _Process(double delta)
	{
		// Update shader-based grid visualization
		UpdateShaderGrid();
	}

	private void UpdateShaderGrid()
	{
		if (!showGrid || gridShaderMaterial == null) return;

		var networkManager = NetworkManager.Instance;
		if (networkManager == null) return;

		var playerShips = networkManager.GetPlayerShips();
		if (playerShips == null) return;

		// Only show grid for the local player
		var localPlayerId = networkManager.GetLocalPlayerId();
		if (localPlayerId == -1 || !playerShips.ContainsKey(localPlayerId))
		{
			boundaryGridMesh.Visible = false;
			return;
		}

		var localShip = playerShips[localPlayerId];
		if (localShip == null)
		{
			boundaryGridMesh.Visible = false;
			return;
		}

		// Update player position in shader
		var playerPos = localShip.GlobalPosition;
		gridShaderMaterial.SetShaderParameter("player_position", playerPos);
		
		// Show grid only when player is near boundaries
		bool isNearBound = IsPlayerNearBounds(playerPos);
		boundaryGridMesh.Visible = isNearBound;
	}

	private bool IsPlayerNearBounds(Vector3 position)
	{
		float half = worldSize / 2.0f;
		float threshold = proximityDistance;

		// Check distance to any face of the cube
		float distanceToNearestFace = Mathf.Min(
			Mathf.Min(half - Mathf.Abs(position.X), half - Mathf.Abs(position.Y)),
			half - Mathf.Abs(position.Z)
		);

		return distanceToNearestFace <= threshold;
	}

}
