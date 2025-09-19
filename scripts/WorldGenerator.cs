using Godot;
using System.Collections.Generic;

public partial class WorldGenerator : Node3D
{
	// Constants
	private const int PLAYER_GRID_LINES = 8;
	private const float PLAYER_GRID_PULSE_SPEED = 3.0f;
	private const float ASTEROID_BOUND_FACTOR = 0.8f;
	private const float ASTEROID_WORLD_MARGIN = 5.0f; // Distance from world bounds
	
	[Export] private float worldSize = 100.0f;
	[Export] private int asteroidCount = 4;
	[Export] private bool showWorldBounds = true;
	[Export] private bool showGrid = true;
	[Export] private PackedScene asteroidScene;
	[Export] private float proximityDistance = 10.0f;
	[Export] private float gridRadius = 10.0f;
	
	// Asteroid generation parameters
	[Export] private float asteroidMinSeparation = 8.0f; // Minimum distance between asteroids
	[Export] private float shipSizeReference = 1.5f; // Reference ship size for asteroid scaling
	
	private int worldSeed;
	private RandomNumberGenerator rng;
	
	// Per-player grid visualization using shader
	private MeshInstance3D boundaryGridMesh;
	private ShaderMaterial gridShaderMaterial;
	
	// World components
	private List<Asteroid> asteroids = new List<Asteroid>();
	
	// Multiplayer synchronization data
	private List<AsteroidSpawnData> asteroidSpawnData = new List<AsteroidSpawnData>();
	
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
		
		GD.Print($"Generating world with seed: {seed}");
		
		ClearWorld();
		GenerateAsteroids();
		
		// If we're the server, broadcast asteroid data to clients
		if (Multiplayer.IsServer())
		{
			BroadcastAsteroidData();
		}
	}
	
	private int GenerateRandomSeed()
	{
		return (int)(Time.GetUnixTimeFromSystem() % int.MaxValue);
	}
	
	private void ClearWorld()
	{
		// Clear existing asteroids
		foreach (var asteroid in asteroids)
		{
			asteroid?.QueueFree();
		}
		asteroids.Clear();
		asteroidSpawnData.Clear();
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
		
		// Set shader parameters
		gridShaderMaterial.SetShaderParameter("world_size", worldSize);
		gridShaderMaterial.SetShaderParameter("proximity_distance", proximityDistance);
		gridShaderMaterial.SetShaderParameter("distortion_strength", 1.0f);
		gridShaderMaterial.SetShaderParameter("wormhole_speed", 0.5f);
		gridShaderMaterial.SetShaderParameter("spiral_intensity", 3.0f);
		
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
		
		// Calculate asteroid spawn bounds with margin from world edges
		float halfWorldSize = worldSize / 2.0f;
		float asteroidBound = (halfWorldSize - ASTEROID_WORLD_MARGIN) * ASTEROID_BOUND_FACTOR;
		
		GD.Print($"Asteroid spawn area: {asteroidBound * 2}x{asteroidBound * 2}x{asteroidBound * 2} (margin: {ASTEROID_WORLD_MARGIN})");
		
		var positions = GenerateAsteroidPositions(asteroidBound);
		
		for (int i = 0; i < positions.Count; i++)
		{
			// Create deterministic seed for each asteroid
			int asteroidSeed = worldSeed + i * 1000;
			
			var asteroid = asteroidScene.Instantiate<Asteroid>();
			AddChild(asteroid);
			
			// Set position
			asteroid.Position = positions[i];
			
			// Calculate size multiplier (2x to 4x ship size, then 20% smaller)
			var tempRng = new RandomNumberGenerator();
			tempRng.Seed = (ulong)asteroidSeed;
			float sizeMultiplier = 2.0f + tempRng.Randf() * 2.0f; // 2x to 4x
			
			// Store spawn data for multiplayer sync
			var spawnData = new AsteroidSpawnData
			{
				Position = positions[i],
				Seed = asteroidSeed,
				SizeMultiplier = sizeMultiplier,
				Index = i
			};
			asteroidSpawnData.Add(spawnData);
			
			// Initialize asteroid with deterministic parameters
			asteroid.Initialize(this, asteroidSeed, sizeMultiplier);
			asteroids.Add(asteroid);
		}
		
		GD.Print($"Generated {asteroids.Count} procedural asteroids within bounds");
	}
	
	private List<Vector3> GenerateAsteroidPositions(float bound)
	{
		var positions = new List<Vector3>();
		int attempts = 0;
		int maxAttempts = asteroidCount * 10; // Prevent infinite loops
		
		while (positions.Count < asteroidCount && attempts < maxAttempts)
		{
			attempts++;
			
			// Generate random position
			var candidatePos = new Vector3(
				rng.Randf() * bound * 2 - bound,
				rng.Randf() * bound * 2 - bound,
				rng.Randf() * bound * 2 - bound
			);
			
			// Check minimum separation from existing asteroids
			bool validPosition = true;
			foreach (var existingPos in positions)
			{
				if (candidatePos.DistanceTo(existingPos) < asteroidMinSeparation)
				{
					validPosition = false;
					break;
				}
			}
			
			// Also ensure not too close to world center (spawn area)
			if (candidatePos.Length() < asteroidMinSeparation * 2)
			{
				validPosition = false;
			}
			
			if (validPosition)
			{
				positions.Add(candidatePos);
			}
		}
		
		if (positions.Count < asteroidCount)
		{
			GD.PrintErr($"Could only place {positions.Count}/{asteroidCount} asteroids with current separation constraints");
		}
		
		return positions;
	}
	
	// Multiplayer synchronization methods
	private void BroadcastAsteroidData()
	{
		if (!Multiplayer.IsServer()) return;
		
		// Send asteroid spawn data to all clients
		foreach (var spawnData in asteroidSpawnData)
		{
			Rpc(MethodName.ReceiveAsteroidSpawnData, 
				spawnData.Index, 
				spawnData.Position, 
				spawnData.Seed, 
				spawnData.SizeMultiplier);
		}
		
		// Signal that asteroid data transmission is complete
		Rpc(MethodName.OnAsteroidDataComplete);
	}
	
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceiveAsteroidSpawnData(int index, Vector3 position, int seed, float sizeMultiplier)
	{
		// Store received data for later instantiation
		var spawnData = new AsteroidSpawnData
		{
			Index = index,
			Position = position,
			Seed = seed,
			SizeMultiplier = sizeMultiplier
		};
		
		// Ensure list is large enough
		while (asteroidSpawnData.Count <= index)
		{
			asteroidSpawnData.Add(null);
		}
		
		asteroidSpawnData[index] = spawnData;
		GD.Print($"Received asteroid data for index {index}");
	}
	
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void OnAsteroidDataComplete()
	{
		GD.Print("Asteroid data transmission complete, generating client asteroids");
		GenerateClientAsteroids();
	}
	
	private void GenerateClientAsteroids()
	{
		if (asteroidScene == null)
		{
			GD.PrintErr("Asteroid scene not set on client!");
			return;
		}
		
		// Clear any existing asteroids
		ClearWorld();
		
		// Generate asteroids from received data
		foreach (var spawnData in asteroidSpawnData)
		{
			if (spawnData == null) continue;
			
			var asteroid = asteroidScene.Instantiate<Asteroid>();
			AddChild(asteroid);
			
			asteroid.Position = spawnData.Position;
			asteroid.Initialize(this, spawnData.Seed, spawnData.SizeMultiplier);
			asteroids.Add(asteroid);
		}
		
		GD.Print($"Client generated {asteroids.Count} synchronized asteroids");
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

		float distanceToNearestFace = Mathf.Min(
			Mathf.Min(half - Mathf.Abs(position.X), half - Mathf.Abs(position.Y)),
			half - Mathf.Abs(position.Z)
		);

		return distanceToNearestFace <= threshold;
	}
	
	// Method to validate asteroid synchronization (debugging)
	public void ValidateAsteroidSync()
	{
		GD.Print("=== Asteroid Synchronization Validation ===");
		GD.Print($"World Seed: {worldSeed}");
		GD.Print($"Asteroid Count: {asteroids.Count}");
		
		for (int i = 0; i < asteroids.Count && i < asteroidSpawnData.Count; i++)
		{
			if (asteroids[i] != null && asteroidSpawnData[i] != null)
			{
				var asteroid = asteroids[i];
				var data = asteroidSpawnData[i];
				
				GD.Print($"Asteroid {i}: Pos={asteroid.GlobalPosition}, Seed={data.Seed}, Size={data.SizeMultiplier}");
			}
		}
	}
}

// Data structure for asteroid spawn information
public partial class AsteroidSpawnData : GodotObject
{
	public int Index { get; set; }
	public Vector3 Position { get; set; }
	public int Seed { get; set; }
	public float SizeMultiplier { get; set; }
}