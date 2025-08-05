using Godot;
using System.Collections.Generic;

public partial class WorldGenerator : Node3D
{
	[Export] private float worldSize = 100.0f;
	[Export] private int asteroidCount = 15;
	[Export] private bool showWorldBounds = true;
	[Export] private bool showGrid = true;
	[Export] private PackedScene asteroidScene;
	
	public enum WorldType
	{
		Cube,
		Sphere
	}
	
	[Export] private WorldType worldType = WorldType.Cube;
	
	private MeshInstance3D worldBoundsMesh;
	private MeshInstance3D gridMesh;
	private List<Asteroid> asteroids = new List<Asteroid>();
	
	public float WorldSize => worldSize;
	public WorldType CurrentWorldType => worldType;
	
	public override void _Ready()
	{
		GenerateWorld();
	}
	
	public void GenerateWorld()
	{
		ClearWorld();
		CreateWorldBounds();
		CreateGrid();
		GenerateAsteroids();
	}
	
	private void ClearWorld()
	{
		// Clear asteroids
		foreach (var asteroid in asteroids)
		{
			asteroid?.QueueFree();
		}
		asteroids.Clear();
		
		// Clear world bounds
		worldBoundsMesh?.QueueFree();
		gridMesh?.QueueFree();
	}
	
	private void CreateWorldBounds()
	{
		worldBoundsMesh = new MeshInstance3D();
		AddChild(worldBoundsMesh);
		
		var material = new StandardMaterial3D
		{
			VertexColorUseAsAlbedo = true,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0, 1, 0, 0.5f)
		};
		
		if (worldType == WorldType.Cube)
		{
			var box = new BoxMesh
			{
				Size = Vector3.One * worldSize
			};
			worldBoundsMesh.Mesh = box;
		}
		else // Sphere
		{
			var sphere = new SphereMesh
			{
				RadialSegments = 32,
				Rings = 16,
				Radius = worldSize / 2.0f,
				Height = worldSize
			};
			worldBoundsMesh.Mesh = sphere;
		}
		
		worldBoundsMesh.MaterialOverride = material;
		worldBoundsMesh.Visible = showWorldBounds;
	}
	
	private void CreateGrid()
	{
		gridMesh = new MeshInstance3D();
		AddChild(gridMesh);
		
		// Create a simple grid using ArrayMesh
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		
		var vertices = new List<Vector3>();
		var colors = new List<Color>();
		
		int gridLines = 20;
		float spacing = worldSize / gridLines;
		float halfSize = worldSize / 2.0f;
		
		// Create grid lines along X
		for (int i = 0; i <= gridLines; i++)
		{
			float pos = -halfSize + i * spacing;
			vertices.Add(new Vector3(pos, 0, -halfSize));
			vertices.Add(new Vector3(pos, 0, halfSize));
			colors.Add(new Color(0, 0.4f, 0, 0.5f));
			colors.Add(new Color(0, 0.4f, 0, 0.5f));
		}
		
		// Create grid lines along Z
		for (int i = 0; i <= gridLines; i++)
		{
			float pos = -halfSize + i * spacing;
			vertices.Add(new Vector3(-halfSize, 0, pos));
			vertices.Add(new Vector3(halfSize, 0, pos));
			colors.Add(new Color(0, 0.4f, 0, 0.5f));
			colors.Add(new Color(0, 0.4f, 0, 0.5f));
		}
		
		arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
		arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
		
		var arrayMesh = new ArrayMesh();
		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		
		gridMesh.Mesh = arrayMesh;
		gridMesh.MaterialOverride = new StandardMaterial3D
		{
			VertexColorUseAsAlbedo = true,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		gridMesh.Visible = showGrid;
	}
	
	private void GenerateAsteroids()
	{
		if (asteroidScene == null)
		{
			GD.PrintErr("Asteroid scene not set!");
			return;
		}
		
		float bound = worldSize / 2.0f * 0.8f; // Keep asteroids away from edges initially
		
		for (int i = 0; i < asteroidCount; i++)
		{
			var asteroid = asteroidScene.Instantiate<Asteroid>();
			AddChild(asteroid);
			
			// Random position
			asteroid.Position = new Vector3(
				GD.Randf() * bound * 2 - bound,
				GD.Randf() * bound * 2 - bound,
				GD.Randf() * bound * 2 - bound
			);
			
			// Random size
			float size = GD.Randf() * 2.0f + 1.0f;
			asteroid.Scale = Vector3.One * size;
			
			// Initialize with world reference
			asteroid.Initialize(this);
			asteroids.Add(asteroid);
		}
	}
	
	public void SetShowBounds(bool show)
	{
		showWorldBounds = show;
		if (worldBoundsMesh != null)
			worldBoundsMesh.Visible = show;
	}
	
	public void SetShowGrid(bool show)
	{
		showGrid = show;
		if (gridMesh != null)
			gridMesh.Visible = show;
	}
	
	public void WrapPosition(ref Vector3 position)
	{
		if (worldType == WorldType.Cube)
		{
			WrapCube(ref position);
		}
		else
		{
			WrapSphere(ref position);
		}
	}
	
	private void WrapCube(ref Vector3 position)
	{
		float half = worldSize / 2.0f;
		
		if (position.X > half) position.X = -half;
		else if (position.X < -half) position.X = half;
		
		if (position.Y > half) position.Y = -half;
		else if (position.Y < -half) position.Y = half;
		
		if (position.Z > half) position.Z = -half;
		else if (position.Z < -half) position.Z = half;
	}
	
	private void WrapSphere(ref Vector3 position)
	{
		float radius = worldSize / 2.0f;
		float distance = position.Length();
		
		if (distance > radius)
		{
			// Wrap to opposite side
			position = position.Normalized() * (-radius * 0.95f);
		}
	}
}
