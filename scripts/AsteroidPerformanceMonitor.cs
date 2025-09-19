using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class AsteroidPerformanceMonitor : Node
{
    [Export] private float performanceCheckInterval = 5.0f;
    [Export] private float targetFPS = 60.0f;
    [Export] private int maxComplexityPerAsteroid = 400; // Adjusted for concave meshes
    
    private Timer performanceTimer;
    private List<Asteroid> monitoredAsteroids = new List<Asteroid>();
    private WorldGenerator worldGenerator;
    
    public override void _Ready()
    {
        // Set up performance monitoring timer
        performanceTimer = new Timer();
        performanceTimer.Timeout += CheckPerformance;
        performanceTimer.WaitTime = performanceCheckInterval;
        performanceTimer.Autostart = true;
        AddChild(performanceTimer);
        
        // Find world generator
        worldGenerator = GetNodeOrNull<WorldGenerator>("/root/Main/WorldGenerator");
    }
    
    public void RegisterAsteroid(Asteroid asteroid)
    {
        if (!monitoredAsteroids.Contains(asteroid))
        {
            monitoredAsteroids.Add(asteroid);
        }
    }
    
    public void UnregisterAsteroid(Asteroid asteroid)
    {
        monitoredAsteroids.Remove(asteroid);
    }
    
    private void CheckPerformance()
    {
        // Clean up null references
        monitoredAsteroids.RemoveAll(a => a == null || !IsInstanceValid(a));
        
        float currentFPS = (float)Engine.GetFramesPerSecond();
        int activeAsteroids = monitoredAsteroids.Count;
        
        GD.Print($"=== Asteroid Performance Report ===");
        GD.Print($"Current FPS: {currentFPS:F1}");
        GD.Print($"Active Asteroids: {activeAsteroids}");
        
        if (currentFPS < targetFPS * 0.8f) // Performance degradation detected
        {
            GD.PrintErr($"Performance warning: FPS dropped to {currentFPS:F1} (target: {targetFPS})");
            OptimizeAsteroids();
        }
        
        // Calculate total complexity
        float totalComplexity = monitoredAsteroids.Sum(a => a.GetComplexityScore());
        float averageComplexity = activeAsteroids > 0 ? totalComplexity / activeAsteroids : 0;
        
        GD.Print($"Total Complexity: {totalComplexity:F1}");
        GD.Print($"Average Complexity per Asteroid: {averageComplexity:F1}");
        
        // Report high-complexity asteroids
        var highComplexityAsteroids = monitoredAsteroids
            .Where(a => a.GetComplexityScore() > maxComplexityPerAsteroid)
            .ToList();
            
        if (highComplexityAsteroids.Count > 0)
        {
            GD.Print($"High complexity asteroids detected: {highComplexityAsteroids.Count}");
        }
    }
    
    private void OptimizeAsteroids()
    {
        GD.Print("Attempting asteroid optimization...");
        
        // Find asteroids that are far from all players
        var networkManager = NetworkManager.Instance;
        if (networkManager == null) return;
        
        var playerShips = networkManager.GetPlayerShips();
        var playerPositions = playerShips.Values.Select(ship => ship.GlobalPosition).ToList();
        
        if (playerPositions.Count == 0) return;
        
        int optimizedCount = 0;
        
        foreach (var asteroid in monitoredAsteroids.ToList())
        {
            if (asteroid == null || !IsInstanceValid(asteroid)) continue;
            
            // Check distance to nearest player
            float nearestPlayerDistance = playerPositions
                .Min(playerPos => asteroid.GlobalPosition.DistanceTo(playerPos));
            
            // If asteroid is very far from all players, consider reducing its complexity
            if (nearestPlayerDistance > worldGenerator.WorldSize * 0.3f)
            {
                // This asteroid is far from players - could implement LOD here
                // For now, just log the opportunity
                GD.Print($"Asteroid at {asteroid.GlobalPosition} is far from players (distance: {nearestPlayerDistance:F1})");
                optimizedCount++;
            }
        }
        
        if (optimizedCount > 0)
        {
            GD.Print($"Identified {optimizedCount} asteroids for potential optimization");
        }
    }
    
    // Method to suggest optimal asteroid settings based on player count
    public AsteroidSettings GetOptimalSettings(int playerCount)
    {
        var settings = new AsteroidSettings();
        
        // Adjust complexity based on player count
        if (playerCount <= 4)
        {
            settings.Complexity = 24;
            settings.MaxAsteroids = 20;
            settings.MaxHoles = 3;
        }
        else if (playerCount <= 16)
        {
            settings.Complexity = 20;
            settings.MaxAsteroids = 15;
            settings.MaxHoles = 2;
        }
        else // 16+ players
        {
            settings.Complexity = 16;
            settings.MaxAsteroids = 12;
            settings.MaxHoles = 1;
        }
        
        GD.Print($"Recommended settings for {playerCount} players:");
        GD.Print($"  Complexity: {settings.Complexity}");
        GD.Print($"  Max Asteroids: {settings.MaxAsteroids}");
        GD.Print($"  Max Holes: {settings.MaxHoles}");
        
        return settings;
    }
    
    // Method to validate trimesh collision performance
    public void ValidateTrimeshPerformance()
    {
        var warnings = new List<string>();
        
        foreach (var asteroid in monitoredAsteroids)
        {
            if (asteroid?.GetNode<CollisionShape3D>("CollisionShape3D")?.Shape is ConcavePolygonShape3D trimesh)
            {
                // Check for potential performance issues with trimesh collision
                if (asteroid.GetComplexityScore() > maxComplexityPerAsteroid * 1.5f)
                {
                    warnings.Add($"Asteroid at {asteroid.GlobalPosition} has very high trimesh complexity");
                }
            }
        }
        
        if (warnings.Count > 0)
        {
            GD.PrintErr("Trimesh collision performance warnings:");
            foreach (var warning in warnings)
            {
                GD.PrintErr($"  - {warning}");
            }
            
            GD.Print("Consider reducing asteroid complexity or using convex approximations for distant asteroids");
        }
    }
}

// Settings data structure for asteroid optimization
public partial class AsteroidSettings : GodotObject
{
    public int Complexity { get; set; } = 20;
    public int MaxAsteroids { get; set; } = 15;
    public int MaxHoles { get; set; } = 2;
    public float NoiseScale { get; set; } = 0.35f;
    public float RadiusVariation { get; set; } = 0.6f;
}