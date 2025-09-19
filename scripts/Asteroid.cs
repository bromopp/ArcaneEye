using Godot;
using System;
using System.Collections.Generic;

public partial class Asteroid : StaticBody3D
{
    [Export] private int seed = 0;
    [Export] private float baseRadius = 2.5f; // 2x to 4x ship size (ships are ~1.5 units)
    [Export] private float radiusVariation = 0.6f;
    [Export] private int complexity = 24; // Vertex resolution (increased for smoother surface)
    [Export] private float noiseScale = 0.35f;
    [Export] private float holeNoiseScale = 1.0f;
    [Export] private float minHoleThreshold = 0.55f;
    [Export] private float maxHoleThreshold = 0.75f;
    [Export] private int maxHoles = 2;

    private MeshInstance3D meshInstance;
    private CollisionShape3D collisionShape;
    private FastNoiseLite noise;
    private FastNoiseLite holeNoise;
    private WorldGenerator world;
    private RandomNumberGenerator rng;
    
    // Store tunnel data for consistent mesh generation
    private List<TunnelData> currentTunnelData = new List<TunnelData>();

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
        CollisionLayer = 3; // Asteroid layer (corrected)
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
        
        // Randomize size within 2x-4x ship size range, then reduce by 20%
        float sizeRange = 2.0f + rng.Randf() * 2.0f; // 2x to 4x
        float finalRadius = baseRadius * sizeMultiplier * sizeRange * 0.8f; // 20% smaller
        
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
        
        // Store vertices in instance variable for face generation
        this.vertices = vertices;
        
        // Generate sphere vertices with noise displacement and holes
        GenerateVertices(vertices, normals, uvs, radius);
        
        // Only generate faces if we have vertices
        if (vertices.Count == 0)
        {
            GD.PrintErr("No vertices generated for asteroid!");
            return CreateFallbackMesh(radius);
        }
        
        // Generate faces/triangles
        GenerateFaces(indices, complexity);
        
        // Validate arrays before creating mesh
        if (indices.Count == 0)
        {
            GD.PrintErr("No indices generated for asteroid!");
            return CreateFallbackMesh(radius);
        }
        
        // Ensure all arrays have consistent sizes
        while (normals.Count < vertices.Count)
        {
            normals.Add(Vector3.Up);
        }
        while (uvs.Count < vertices.Count)
        {
            uvs.Add(Vector2.Zero);
        }
        
        // Validate indices are within bounds
        for (int i = 0; i < indices.Count; i++)
        {
            if (indices[i] >= vertices.Count)
            {
                GD.PrintErr($"Index {indices[i]} out of bounds for vertex count {vertices.Count}!");
                return CreateFallbackMesh(radius);
            }
        }
        
        // Create the mesh
        var arrayMesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        
        try
        {
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            GD.Print($"Successfully created asteroid mesh with {vertices.Count} vertices and {indices.Count / 3} triangles");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Failed to create asteroid mesh: {ex.Message}");
            return CreateFallbackMesh(radius);
        }
        
        return arrayMesh;
    }

    private ArrayMesh CreateFallbackMesh(float radius)
    {
        GD.Print("Creating fallback sphere mesh for asteroid");
        
        // Create a simple sphere as fallback
        var sphereMesh = new SphereMesh();
        sphereMesh.RadialSegments = 16;
        sphereMesh.Rings = 8;
        sphereMesh.Radius = radius;
        sphereMesh.Height = radius * 2;
        
        // Convert to ArrayMesh
        var arrayMesh = new ArrayMesh();
        var arrays = sphereMesh.SurfaceGetArrays(0);
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        
        return arrayMesh;
    }

    private void GenerateVertices(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, float radius)
    {
        int rings = complexity / 2;
        int sectors = complexity;
        
        // Calculate tunnel data for this asteroid and store it
        currentTunnelData = CalculateTunnelData(radius);
        
        // Create ALL sphere vertices first (don't skip any for tunnels)
        var allSurfaceVertices = new List<Vector3>();
        var allSurfaceNormals = new List<Vector3>();
        var allSurfaceUVs = new List<Vector2>();
        
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
                Vector3 worldPos = basePos * radius;
                
                // Check distance to tunnels for smooth transitions
                float minDistanceToTunnel = float.MaxValue;
                foreach (var tunnel in currentTunnelData)
                {
                    float distanceToTunnel = DistancePointToLine(worldPos, tunnel.Start, tunnel.End);
                    minDistanceToTunnel = Mathf.Min(minDistanceToTunnel, distanceToTunnel);
                }
                
                // Apply noise for irregular surface
                float noiseValue = noise.GetNoise3D(basePos.X, basePos.Y, basePos.Z);
                float surfaceDisplacement = 1.0f + noiseValue * radiusVariation;
                
                // Apply secondary noise for surface detail
                float detailNoise = noise.GetNoise3D(basePos.X * 3, basePos.Y * 3, basePos.Z * 3) * 0.1f;
                surfaceDisplacement += detailNoise;
                
                // Create smooth tunnel entrances
                foreach (var tunnel in currentTunnelData)
                {
                    float distanceToTunnel = DistancePointToLine(worldPos, tunnel.Start, tunnel.End);
                    float tunnelInfluence = tunnel.Radius * 1.3f; // Area of influence
                    
                    if (distanceToTunnel < tunnelInfluence)
                    {
                        // Create smooth transition into tunnel
                        float factor = 1.0f - (distanceToTunnel / tunnelInfluence);
                        factor = Mathf.SmoothStep(0.0f, 1.0f, factor); // Smooth curve
                        
                        // Push vertex inward gradually
                        float pushAmount = factor * 0.7f; // How much to push inward
                        surfaceDisplacement *= (1.0f - pushAmount);
                    }
                }
                
                Vector3 finalPos = basePos * radius * surfaceDisplacement;
                
                allSurfaceVertices.Add(finalPos);
                allSurfaceNormals.Add(basePos.Normalized());
                
                // UV mapping for textures
                float u = (float)sector / sectors;
                float v = (float)ring / rings;
                allSurfaceUVs.Add(new Vector2(u, v));
            }
        }
        
        // Add all surface vertices to main lists
        vertices.AddRange(allSurfaceVertices);
        normals.AddRange(allSurfaceNormals);
        uvs.AddRange(allSurfaceUVs);
        
        var surfaceVertexCount = vertices.Count;
        
        // Now add tunnel wall vertices
        GenerateTunnelWalls(vertices, normals, uvs, currentTunnelData);
        
        GD.Print($"Generated {vertices.Count} vertices for asteroid ({surfaceVertexCount} surface + {vertices.Count - surfaceVertexCount} tunnel)");
        
        // Ensure we have minimum vertices
        if (vertices.Count < 3)
        {
            CreateFallbackVertices(vertices, normals, uvs, radius);
        }
    }

    private List<TunnelData> CalculateTunnelData(float radius)
    {
        var tunnels = new List<TunnelData>();
        int numTunnels = Mathf.Min((int)(rng.Randf() * maxHoles) + 1, 2); // Max 2 tunnels for stability
        
        for (int tunnelIndex = 0; tunnelIndex < numTunnels; tunnelIndex++)
        {
            // Create deterministic tunnel direction
            var tunnelRng = new RandomNumberGenerator();
            tunnelRng.Seed = (ulong)(seed + tunnelIndex * 500);
            
            // Random tunnel direction
            Vector3 tunnelDirection = new Vector3(
                tunnelRng.Randf() * 2 - 1,
                tunnelRng.Randf() * 2 - 1,
                tunnelRng.Randf() * 2 - 1
            ).Normalized();
            
            // Tunnel parameters
            float tunnelRadius = radius * (0.2f + tunnelRng.Randf() * 0.2f); // 20-40% of asteroid radius
            float tunnelLength = radius * 2.2f; // Goes through the asteroid
            
            var tunnel = new TunnelData
            {
                Direction = tunnelDirection,
                Radius = tunnelRadius,
                Length = tunnelLength,
                Start = -tunnelDirection * (tunnelLength * 0.5f),
                End = tunnelDirection * (tunnelLength * 0.5f),
                Index = tunnelIndex
            };
            
            tunnels.Add(tunnel);
        }
        
        return tunnels;
    }

    private void GenerateTunnelWalls(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<TunnelData> tunnelData)
    {
        foreach (var tunnel in tunnelData)
        {
            CreateTunnelWallGeometry(vertices, normals, uvs, tunnel);
        }
    }

    private void CreateTunnelWallGeometry(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, TunnelData tunnel)
    {
        int segments = 8;  // Circular segments around tunnel
        int rings = 8;     // Segments along tunnel length (increased for smoother tunnels)
        
        // Create perpendicular vectors for tunnel cross-section
        Vector3 perpendicular1 = Vector3.Up.Cross(tunnel.Direction).Normalized();
        if (perpendicular1.Length() < 0.1f)
            perpendicular1 = Vector3.Right.Cross(tunnel.Direction).Normalized();
        Vector3 perpendicular2 = tunnel.Direction.Cross(perpendicular1).Normalized();
        
        // Store starting vertex index for this tunnel (after surface vertices)
        tunnel.StartVertexIndex = vertices.Count;
        
        // Generate wall vertices with smooth connection to surface
        for (int ring = 0; ring <= rings; ring++)
        {
            float t = (float)ring / rings;
            Vector3 ringCenter = tunnel.Start.Lerp(tunnel.End, t);
            
            // Gradually transition radius from surface to tunnel interior
            float radiusVariation = 1.0f;
            if (ring == 0 || ring == rings) // Entrance/exit
            {
                radiusVariation = 1.2f; // Slightly larger at entrances for smooth transition
            }
            else
            {
                radiusVariation = 1.0f + Mathf.Sin(t * Mathf.Pi) * 0.1f; // Subtle variation
            }
            
            float currentRadius = tunnel.Radius * radiusVariation;
            
            for (int segment = 0; segment <= segments; segment++)
            {
                float angle = 2.0f * Mathf.Pi * segment / segments;
                
                // Position on tunnel wall
                Vector3 localPos = perpendicular1 * Mathf.Cos(angle) + perpendicular2 * Mathf.Sin(angle);
                
                // Very subtle noise for tunnel walls (less than before)
                float wallNoise = noise.GetNoise3D(
                    ringCenter.X * 0.05f + localPos.X,
                    ringCenter.Y * 0.05f + localPos.Y,
                    ringCenter.Z * 0.05f + localPos.Z
                ) * 0.02f; // Even more subtle noise
                
                Vector3 wallVertex = ringCenter + localPos * (currentRadius + wallNoise);
                
                vertices.Add(wallVertex);
                normals.Add(-localPos.Normalized()); // Inward-facing normals
                
                // UV coordinates for tunnel walls
                float u = (float)segment / segments;
                float v = t;
                uvs.Add(new Vector2(u, v));
            }
        }
        
        tunnel.VertexCount = vertices.Count - tunnel.StartVertexIndex;
        GD.Print($"Generated tunnel {tunnel.Index} with {tunnel.VertexCount} wall vertices starting at index {tunnel.StartVertexIndex}");
    }

    private float DistancePointToLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDirection = (lineEnd - lineStart);
        float lineLength = lineDirection.Length();
        
        if (lineLength < 0.001f) return point.DistanceTo(lineStart);
        
        lineDirection = lineDirection / lineLength;
        Vector3 pointToStart = point - lineStart;
        float projection = pointToStart.Dot(lineDirection);
        
        // Clamp projection to line segment
        projection = Mathf.Clamp(projection, 0, lineLength);
        
        Vector3 closestPoint = lineStart + lineDirection * projection;
        return point.DistanceTo(closestPoint);
    }

    private void CreateFallbackVertices(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, float radius)
    {
        vertices.Clear();
        normals.Clear();
        uvs.Clear();
        
        // Add minimum triangle
        vertices.Add(new Vector3(0, radius, 0));
        vertices.Add(new Vector3(-radius * 0.5f, -radius * 0.5f, 0));
        vertices.Add(new Vector3(radius * 0.5f, -radius * 0.5f, 0));
        
        normals.Add(Vector3.Up);
        normals.Add(Vector3.Up);
        normals.Add(Vector3.Up);
        
        uvs.Add(new Vector2(0.5f, 1));
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
    }

    private List<TunnelData> PreCalculateTunnels(float radius)
    {
        var tunnels = new List<TunnelData>();
        int numTunnels = (int)(rng.Randf() * maxHoles) + 1;
        
        for (int tunnelIndex = 0; tunnelIndex < numTunnels; tunnelIndex++)
        {
            var tunnelRng = new RandomNumberGenerator();
            tunnelRng.Seed = (ulong)(seed + tunnelIndex * 500);
            
            Vector3 tunnelDirection = new Vector3(
                tunnelRng.Randf() * 2 - 1,
                tunnelRng.Randf() * 2 - 1,
                tunnelRng.Randf() * 2 - 1
            ).Normalized();
            
            float tunnelRadius = radius * (0.15f + tunnelRng.Randf() * 0.25f);
            float tunnelLength = radius * 2.5f;
            
            tunnels.Add(new TunnelData
            {
                Direction = tunnelDirection,
                Radius = tunnelRadius,
                Length = tunnelLength,
                Start = -tunnelDirection * (tunnelLength * 0.5f),
                End = tunnelDirection * (tunnelLength * 0.5f)
            });
        }
        
        return tunnels;
    }

    private void GenerateIntegratedTunnelVertices(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, float radius, List<TunnelData> tunnelData)
    {
        foreach (var tunnel in tunnelData)
        {
            CreateIntegratedTunnelGeometry(vertices, normals, uvs, tunnel);
        }
    }

    private void CreateIntegratedTunnelGeometry(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, TunnelData tunnel)
    {
        int tunnelSegments = 8; // Reduced for better performance
        int tunnelRings = 6;    // Reduced for better performance
        
        // Create perpendicular vectors for tunnel cross-section
        Vector3 perpendicular1 = Vector3.Up.Cross(tunnel.Direction).Normalized();
        if (perpendicular1.Length() < 0.1f)
            perpendicular1 = Vector3.Right.Cross(tunnel.Direction).Normalized();
        Vector3 perpendicular2 = tunnel.Direction.Cross(perpendicular1).Normalized();
        
        // Generate tunnel wall vertices with better surface integration
        for (int ring = 0; ring <= tunnelRings; ring++)
        {
            float t = (float)ring / tunnelRings;
            Vector3 ringCenter = tunnel.Start.Lerp(tunnel.End, t);
            
            // Vary tunnel radius along length for more organic shape
            float radiusVariation = 1.0f + Mathf.Sin(t * Mathf.Pi) * 0.2f; // Reduced variation
            float currentRadius = tunnel.Radius * radiusVariation;
            
            for (int segment = 0; segment <= tunnelSegments; segment++)
            {
                float angle = 2.0f * Mathf.Pi * segment / tunnelSegments;
                
                Vector3 circlePos = perpendicular1 * Mathf.Cos(angle) + perpendicular2 * Mathf.Sin(angle);
                
                // Reduced noise for smoother tunnel walls
                float wallNoise = noise.GetNoise3D(
                    ringCenter.X + circlePos.X * 2,
                    ringCenter.Y + circlePos.Y * 2,
                    ringCenter.Z + circlePos.Z * 2
                ) * 0.1f; // Much smaller noise
                
                Vector3 wallVertex = ringCenter + circlePos * (currentRadius + wallNoise);
                
                vertices.Add(wallVertex);
                normals.Add(-circlePos); // Normal points inward for tunnel walls
                
                float u = (float)segment / tunnelSegments;
                float v = t;
                uvs.Add(new Vector2(u, v));
            }
        }
    }

    private void GenerateTunnelVertices(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, float radius)
    {
        // Generate 1-3 tunnels through the asteroid
        int numTunnels = (int)(rng.Randf() * maxHoles) + 1;
        
        for (int tunnelIndex = 0; tunnelIndex < numTunnels; tunnelIndex++)
        {
            // Create deterministic tunnel direction
            var tunnelRng = new RandomNumberGenerator();
            tunnelRng.Seed = (ulong)(seed + tunnelIndex * 500);
            
            // Random tunnel direction
            Vector3 tunnelDirection = new Vector3(
                tunnelRng.Randf() * 2 - 1,
                tunnelRng.Randf() * 2 - 1,
                tunnelRng.Randf() * 2 - 1
            ).Normalized();
            
            // Tunnel parameters
            float tunnelRadius = radius * (0.15f + tunnelRng.Randf() * 0.25f); // 15-40% of asteroid radius
            float tunnelLength = radius * 2.5f; // Goes through the entire asteroid
            
            // Create tunnel geometry
            CreateTunnelGeometry(vertices, normals, uvs, tunnelDirection, tunnelRadius, tunnelLength, tunnelIndex);
        }
    }

    private void CreateTunnelGeometry(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, 
                                    Vector3 direction, float tunnelRadius, float tunnelLength, int tunnelIndex)
    {
        int tunnelSegments = 12; // Circular segments
        int tunnelRings = 8;     // Length segments
        
        Vector3 tunnelStart = -direction * (tunnelLength * 0.5f);
        Vector3 tunnelEnd = direction * (tunnelLength * 0.5f);
        
        // Create perpendicular vectors for tunnel cross-section
        Vector3 perpendicular1 = Vector3.Up.Cross(direction).Normalized();
        if (perpendicular1.Length() < 0.1f) // Handle case where direction is parallel to up
            perpendicular1 = Vector3.Right.Cross(direction).Normalized();
        Vector3 perpendicular2 = direction.Cross(perpendicular1).Normalized();
        
        // Generate tunnel wall vertices
        for (int ring = 0; ring <= tunnelRings; ring++)
        {
            float t = (float)ring / tunnelRings;
            Vector3 ringCenter = tunnelStart.Lerp(tunnelEnd, t);
            
            // Vary tunnel radius along length for more organic shape
            float radiusVariation = 1.0f + Mathf.Sin(t * Mathf.Pi) * 0.3f;
            float currentRadius = tunnelRadius * radiusVariation;
            
            // Add some noise to tunnel walls
            float noiseScale = 0.5f;
            
            for (int segment = 0; segment <= tunnelSegments; segment++)
            {
                float angle = 2.0f * Mathf.Pi * segment / tunnelSegments;
                
                // Calculate position on tunnel wall
                Vector3 circlePos = perpendicular1 * Mathf.Cos(angle) + perpendicular2 * Mathf.Sin(angle);
                
                // Apply noise to tunnel walls
                float wallNoise = noise.GetNoise3D(
                    ringCenter.X + circlePos.X,
                    ringCenter.Y + circlePos.Y,
                    ringCenter.Z + circlePos.Z
                ) * noiseScale;
                
                Vector3 wallVertex = ringCenter + circlePos * (currentRadius + wallNoise);
                
                vertices.Add(wallVertex);
                normals.Add(-circlePos); // Normal points inward for tunnel walls
                
                // UV coordinates for tunnel walls
                float u = (float)segment / tunnelSegments;
                float v = t;
                uvs.Add(new Vector2(u, v));
            }
        }
    }

    private void GenerateFaces(List<int> indices, int complexity)
    {
        int rings = complexity / 2;
        int sectors = complexity;
        
        // Generate faces for the main asteroid surface (simplified)
        GenerateSimplifiedSurfaceFaces(indices, rings, sectors);
        
        // Generate faces for tunnels
        GenerateSimplifiedTunnelFaces(indices);
    }

    private void GenerateSimplifiedSurfaceFaces(List<int> indices, int rings, int sectors)
    {
        // Generate faces for a basic sphere with holes
        for (int ring = 0; ring < rings; ring++)
        {
            for (int sector = 0; sector < sectors; sector++)
            {
                int current = ring * (sectors + 1) + sector;
                int next = ring * (sectors + 1) + ((sector + 1) % (sectors + 1));
                int below = (ring + 1) * (sectors + 1) + sector;
                int belowNext = (ring + 1) * (sectors + 1) + ((sector + 1) % (sectors + 1));
                
                // Ensure indices are within bounds
                if (current < vertices.Count && next < vertices.Count && 
                    below < vertices.Count && belowNext < vertices.Count)
                {
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
        
        GD.Print($"Generated {indices.Count / 3} triangles from sphere geometry");
    }

    private void GenerateSimplifiedTunnelFaces(List<int> indices)
    {
        // This method is now replaced by GenerateTunnelWallFaces
        // Keeping for compatibility but not used
    }

    private int EstimateSurfaceVertexCount()
    {
        // This is an approximation - in a more complex implementation,
        // you'd track this during vertex generation
        int rings = complexity / 2;
        int sectors = complexity;
        return (rings + 1) * (sectors + 1) / 2; // Rough estimate accounting for skipped vertices
    }

    // Add storage for vertices during generation
    private List<Vector3> vertices = new List<Vector3>();

    private bool IsFaceInsideTunnel(int v1, int v2, int v3, int v4)
    {
        // This is a simplified check - in practice, you'd want more sophisticated
        // tunnel intersection testing. For now, we use noise to determine if 
        // a face should be removed to create holes
        
        // Get the approximate center of the face quad
        int rings = complexity / 2;
        int sectors = complexity;
        
        // Convert vertex indices back to ring/sector coordinates
        int ring1 = v1 / (sectors + 1);
        int sector1 = v1 % (sectors + 1);
        
        // Use noise to determine if this area should have holes
        float phi = Mathf.Pi * ring1 / rings;
        float theta = 2.0f * Mathf.Pi * sector1 / sectors;
        
        float x = Mathf.Sin(phi) * Mathf.Cos(theta);
        float y = Mathf.Cos(phi);
        float z = Mathf.Sin(phi) * Mathf.Sin(theta);
        
        Vector3 testPos = new Vector3(x, y, z);
        
        // Use cellular noise to determine if this face is inside a hole
        float holeValue = holeNoise.GetNoise3D(testPos.X * 2, testPos.Y * 2, testPos.Z * 2);
        return holeValue > minHoleThreshold && holeValue < maxHoleThreshold && rng.Randf() < 0.8f;
    }

    private void RecalculateNormals(List<Vector3> vertices, List<Vector3> normals, int complexity)
    {
        // Reset all normals
        for (int i = 0; i < normals.Count; i++)
        {
            normals[i] = Vector3.Zero;
        }
        
        int rings = complexity / 2;
        int sectors = complexity;
        int surfaceVertexCount = (rings + 1) * (sectors + 1);
        
        // Calculate normals for surface vertices
        RecalculateSurfaceNormals(vertices, normals, rings, sectors);
        
        // Calculate normals for tunnel vertices  
        RecalculateTunnelNormals(vertices, normals, surfaceVertexCount);
        
        // Normalize all normals
        for (int i = 0; i < normals.Count; i++)
        {
            if (normals[i].Length() > 0.01f)
            {
                normals[i] = normals[i].Normalized();
            }
        }
    }

    private void RecalculateSurfaceNormals(List<Vector3> vertices, List<Vector3> normals, int rings, int sectors)
    {
        // Calculate normals from adjacent faces for surface vertices
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
                    // Skip faces that are inside tunnels
                    if (IsFaceInsideTunnel(current, next, below, belowNext))
                        continue;
                    
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
    }

    private void RecalculateTunnelNormals(List<Vector3> vertices, List<Vector3> normals, int surfaceVertexCount)
    {
        int tunnelSegments = 12;
        int tunnelRings = 8;
        int numTunnels = (int)(rng.Randf() * maxHoles) + 1;
        
        for (int tunnelIndex = 0; tunnelIndex < numTunnels; tunnelIndex++)
        {
            int tunnelStartVertex = surfaceVertexCount + tunnelIndex * (tunnelRings + 1) * (tunnelSegments + 1);
            
            // Calculate normals for tunnel wall vertices
            for (int ring = 0; ring <= tunnelRings; ring++)
            {
                for (int segment = 0; segment <= tunnelSegments; segment++)
                {
                    int vertexIndex = tunnelStartVertex + ring * (tunnelSegments + 1) + segment;
                    
                    if (vertexIndex < normals.Count)
                    {
                        // For tunnel walls, normals should point inward (already set in generation)
                        // Just ensure they're properly oriented
                        if (normals[vertexIndex].Length() < 0.01f)
                        {
                            // Fallback: calculate normal from vertex position relative to tunnel center
                            Vector3 vertexPos = vertices[vertexIndex];
                            // This is a simplified approach - you might want more sophisticated normal calculation
                            normals[vertexIndex] = -vertexPos.Normalized();
                        }
                    }
                }
            }
        }
    }

    private void CreateCollisionFromMesh(ArrayMesh mesh)
    {
        // Use trimesh collision for accurate concave collision detection
        // This allows for proper collision with holes and tunnels
        var shape = mesh.CreateTrimeshShape();
        collisionShape.Shape = shape;
        
        GD.Print($"Created concave trimesh collision for asteroid with {mesh.GetSurfaceCount()} surfaces");
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

    // Enhanced debugging method
    public void DebugAsteroidGeometry()
    {
        if (meshInstance?.Mesh is ArrayMesh arrayMesh)
        {
            GD.Print($"=== Asteroid {GetInstanceId()} Debug Info ===");
            GD.Print($"Seed: {seed}");
            GD.Print($"Surface count: {arrayMesh.GetSurfaceCount()}");
            
            if (arrayMesh.SurfaceGetArrayLen(0) > 0)
            {
                var vertexArray = arrayMesh.SurfaceGetArrays(0);
                GD.Print($"Vertex count: {vertexArray.Count}");
            }
            
            if (collisionShape?.Shape != null)
            {
                GD.Print($"Collision shape type: {collisionShape.Shape.GetType().Name}");
            }
        }
    }

    // Performance monitoring
    public float GetComplexityScore()
    {
        float score = complexity * complexity; // Base complexity
        
        if (meshInstance?.Mesh is ArrayMesh arrayMesh)
        {
            if (arrayMesh.SurfaceGetArrayLen(0) > 0)
            {
                var vertexArray = arrayMesh.SurfaceGetArrays(0);
                score += vertexArray.Count * 0.1f; // Vertex complexity
            }
        }
        
        return score;
    }
}

// Data structure for tunnel information
public partial class TunnelData : GodotObject
{
    public Vector3 Direction { get; set; }
    public float Radius { get; set; }
    public float Length { get; set; }
    public Vector3 Start { get; set; }
    public Vector3 End { get; set; }
    public int Index { get; set; }
    public int StartVertexIndex { get; set; }
    public int VertexCount { get; set; }
}

// Data structure for multiplayer synchronization
public partial class AsteroidData : GodotObject
{
    public int Seed { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; }
    public float BaseRadius { get; set; }
}