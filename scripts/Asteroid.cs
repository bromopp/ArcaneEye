using Godot;
using System;
using System.Collections.Generic;

public partial class Asteroid : StaticBody3D
{
    [Export] private int seed = 0;
    [Export] private float baseRadius = 2.0f; // 2x to 4x ship size (ships are ~1.5 units)
    [Export] private float radiusVariation = 1.0f;
    [Export] private int complexity = 32; // Vertex resolution
    [Export] private float noiseScale = 0.3f;
    [Export] private float holeNoiseScale = 0.8f;
    [Export] private float minHoleThreshold = 0.6f;
    [Export] private float maxHoleThreshold = 0.9f;
    [Export] private int maxHoles = 3;

    private MeshInstance3D meshInstance;
    private CollisionShape3D collisionShape;
    private FastNoiseLite noise;
    private FastNoiseLite holeNoise;
    private WorldGenerator world;
    private RandomNumberGenerator rng;

    public override void _Ready()
    {
        // Set up the asteroid mesh
        meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
        if (meshInstance == null)
        {
            meshInstance = new MeshInstance3D();
            AddChild(meshInstance);
        }

        // Set up collision
        collisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (collisionShape == null)
        {
            collisionShape = new CollisionShape3D();
            AddChild(collisionShape);
        }

        // Set collision layers for multiplayer
        CollisionLayer = 3; // Asteroid layer
        CollisionMask = 3;  // Collide with ships (1) and bullets (2)
    }

    public void Initialize(WorldGenerator worldGen, int asteroidSeed, float sizeMultiplier = 1.0f)
    {
        world = worldGen;
        seed = asteroidSeed;
        
        // Initialize RNG and noise with deterministic seed
        rng = new RandomNumberGenerator();
        rng.Seed = (ulong)seed;
        
        noise = new FastNoiseLite();
        noise.Seed = seed;
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        noise.Frequency = noiseScale;
        
        holeNoise = new FastNoiseLite();
        holeNoise.Seed = seed + 1000; // Offset for different pattern
        holeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
        holeNoise.Frequency = holeNoiseScale;
        
        // Randomize size within 2x-4x ship size range
        float finalRadius = baseRadius * sizeMultiplier * (2.0f + rng.Randf() * 2.0f);
        
        // Generate the procedural asteroid
        CreateProceduralAsteroid(finalRadius);
    }

    private void CreateProceduralAsteroid(float radius)
    {
        var mesh = GenerateAsteroidMesh(radius);
        meshInstance.Mesh = mesh;
        
        // Create material with random rocky appearance
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(
                0.4f + rng.Randf() * 0.3f,  // Brown-gray base
                0.3f + rng.Randf() * 0.2f,
                0.2f + rng.Randf() * 0.1f
            ),
            Roughness = 0.95f,
            Metallic = 0.0f,
            NormalScale = 1.2f
        };
        
        // Add some color variation for visual interest
        if (rng.Randf() < 0.3f) // 30% chance for iron-rich asteroids
        {
            material.AlbedoColor = material.AlbedoColor.Lerp(new Color(0.6f, 0.3f, 0.1f), 0.4f);
            material.Metallic = 0.2f;
        }
        
        meshInstance.MaterialOverride = material;
        
        // Create collision shape from mesh
        CreateCollisionFromMesh(mesh);
    }

    private ArrayMesh GenerateAsteroidMesh(float radius)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        var uvs = new List<Vector2>();
        
        // Generate sphere vertices with noise displacement and holes
        GenerateVertices(vertices, normals, uvs, radius);
        
        // Generate faces/triangles
        GenerateFaces(indices, complexity);
        
        // Create the mesh
        var arrayMesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        
        return arrayMesh;
    }

    private void GenerateVertices(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, float radius)
    {
        int rings = complexity / 2;
        int sectors = complexity;
        
        for (int ring = 0; ring <= rings; ring++)
        {
            float phi = Mathf.Pi * ring / rings; // 0 to PI
            
            for (int sector = 0; sector <= sectors; sector++)
            {
                float theta = 2.0f * Mathf.Pi * sector / sectors; // 0 to 2PI
                
                // Basic sphere coordinates
                float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = Mathf.Cos(phi);
                float z = Mathf.Sin(phi) * Mathf.Sin(theta);
                
                Vector3 basePos = new Vector3(x, y, z);
                
                // Apply noise for irregular surface
                float noiseValue = noise.GetNoise3D(basePos.X, basePos.Y, basePos.Z);
                float surfaceDisplacement = 1.0f + noiseValue * radiusVariation;
                
                // Check for holes using cellular noise
                float holeValue = holeNoise.GetNoise3D(basePos.X * 2, basePos.Y * 2, basePos.Z * 2);
                bool isHole = holeValue > minHoleThreshold && holeValue < maxHoleThreshold;
                
                // Create holes by significantly reducing radius
                if (isHole && rng.Randf() < 0.7f) // 70% chance to actually create hole
                {
                    surfaceDisplacement *= 0.3f; // Create deep indentations
                }
                
                // Apply secondary noise for surface detail
                float detailNoise = noise.GetNoise3D(basePos.X * 3, basePos.Y * 3, basePos.Z * 3) * 0.1f;
                surfaceDisplacement += detailNoise;
                
                Vector3 finalPos = basePos * radius * surfaceDisplacement;
                
                vertices.Add(finalPos);
                normals.Add(basePos); // Will recalculate proper normals later
                
                // UV mapping for textures
                float u = (float)sector / sectors;
                float v = (float)ring / rings;
                uvs.Add(new Vector2(u, v));
            }
        }
        
        // Recalculate normals based on actual surface
        RecalculateNormals(vertices, normals, complexity);
    }

    private void RecalculateNormals(List<Vector3> vertices, List<Vector3> normals, int complexity)
    {
        // Reset normals
        for (int i = 0; i < normals.Count; i++)
        {
            normals[i] = Vector3.Zero;
        }
        
        int rings = complexity / 2;
        int sectors = complexity;
        
        // Calculate normals from adjacent faces
        for (int ring = 0; ring < rings; ring++)
        {
            for (int sector = 0; sector < sectors; sector++)
            {
                int current = ring * (sectors + 1) + sector;
                int next = ring * (sectors + 1) + ((sector + 1) % (sectors + 1));
                int below = (ring + 1) * (sectors + 1) + sector;
                int belowNext = (ring + 1) * (sectors + 1) + ((sector + 1) % (sectors + 1));
                
                if (ring < rings && sector < sectors)
                {
                    // Calculate face normal for two triangles
                    Vector3 v1 = vertices[next] - vertices[current];
                    Vector3 v2 = vertices[below] - vertices[current];
                    Vector3 normal1 = v1.Cross(v2).Normalized();
                    
                    Vector3 v3 = vertices[belowNext] - vertices[next];
                    Vector3 v4 = vertices[below] - vertices[next];
                    Vector3 normal2 = v3.Cross(v4).Normalized();
                    
                    // Accumulate normals
                    normals[current] += normal1;
                    normals[next] += normal1 + normal2;
                    normals[below] += normal1 + normal2;
                    normals[belowNext] += normal2;
                }
            }
        }
        
        // Normalize all normals
        for (int i = 0; i < normals.Count; i++)
        {
            normals[i] = normals[i].Normalized();
        }
    }

    private void GenerateFaces(List<int> indices, int complexity)
    {
        int rings = complexity / 2;
        int sectors = complexity;
        
        for (int ring = 0; ring < rings; ring++)
        {
            for (int sector = 0; sector < sectors; sector++)
            {
                int current = ring * (sectors + 1) + sector;
                int next = ring * (sectors + 1) + ((sector + 1) % (sectors + 1));
                int below = (ring + 1) * (sectors + 1) + sector;
                int belowNext = (ring + 1) * (sectors + 1) + ((sector + 1) % (sectors + 1));
                
                if (ring == 0) // Top cap
                {
                    indices.Add(current);
                    indices.Add(below);
                    indices.Add(belowNext);
                }
                else if (ring == rings - 1) // Bottom cap
                {
                    indices.Add(current);
                    indices.Add(next);
                    indices.Add(below);
                }
                else // Body quads (two triangles each)
                {
                    // First triangle
                    indices.Add(current);
                    indices.Add(below);
                    indices.Add(next);
                    
                    // Second triangle
                    indices.Add(next);
                    indices.Add(below);
                    indices.Add(belowNext);
                }
            }
        }
    }

    private void CreateCollisionFromMesh(ArrayMesh mesh)
    {
        // Create convex collision shape from the mesh
        var shape = mesh.CreateConvexShape();
        collisionShape.Shape = shape;
        
        // For complex asteroids with holes, you might want multiple collision shapes
        // This is a simplified approach - for more accuracy, consider using
        // mesh.CreateTrimeshShape() or multiple ConvexPolygonShape3D objects
    }

    public override void _PhysicsProcess(double delta)
    {
        // Asteroids are now static, but we still need world wrapping for consistency
        if (world != null)
        {
            var pos = Position;
            world.WrapPosition(ref pos);
            Position = pos;
        }
    }

    // Method to get deterministic properties for multiplayer sync verification
    public AsteroidData GetAsteroidData()
    {
        return new AsteroidData
        {
            Seed = seed,
            Position = GlobalPosition,
            Scale = Scale,
            BaseRadius = baseRadius
        };
    }
}

// Data structure for multiplayer synchronization
public partial class AsteroidData : GodotObject
{
    public int Seed { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; }
    public float BaseRadius { get; set; }
}