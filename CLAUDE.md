# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ArcaneEye is a 3D multiplayer space combat game built with Godot 4.4 and C#. Players control ships in a shared 3D space, engaging in combat with asteroids and each other using controllable bullets.

## Development Commands

### Build and Run
- **Build**: `dotnet build ArcaneEye.csproj` - Compiles the C# project
- **Run Game**: Launch through Godot Editor or use VS Code launch configurations
- **Run Editor**: Use "Launch Editor" configuration in `launch.json`

### Debugging
The project includes VS Code launch configurations:
- `Launch` - Run the game directly
- `Launch (Select Scene)` - Run with scene selection
- `Launch Editor` - Launch Godot Editor
- `Attach to Process` - Attach debugger to running process

**Requirements**: Set `GODOT4` environment variable pointing to Godot 4 executable

## Architecture

### Core Systems
- **NetworkManager** (`scripts/NetworkManager.cs`): Singleton handling multiplayer networking, player management, and ENet peer connections. Manages up to 32 players on localhost:42069.
- **PlayerShip** (`scripts/PlayerShip.cs`): Main player controller with physics-based movement, camera following, lives system, and immunity mechanics.
- **WorldGenerator** (`scripts/WorldGenerator.cs`): Procedural world creation with asteroids, world boundaries, and per-player grid visualization using shaders.
- **ControllableBullet** (`scripts/ControllableBullet.cs`): Player-controllable projectiles with physics and collision detection.

### Scene Structure
- **Main Scene**: `scenes/world.tscn` - Primary game world
- **Player**: `scenes/player_ship.tscn` - Player ship prefab
- **Projectiles**: `scenes/ControllableBullet.tscn` - Bullet prefab
- **Environment**: `scenes/asteroid.tscn` - Asteroid prefab
- **Effects**: `scenes/explosion.tscn`, `scenes/death.tscn` - Visual effects

### Input System
Defined in `project.godot`:
- W: Thrust
- A/D: Turn left/right  
- Space: Fire
- Q: Cancel bullet control

### Physics Layers
1. Ship
2. Bullet  
3. Asteroid

## Key Design Patterns

### Networking Architecture
Uses Godot's ENetMultiplayerPeer with centralized NetworkManager singleton. Players are tracked via Dictionary with PlayerInfo structs containing ID, name, and color.

### Entity Management
- Ships use RigidBody3D physics with custom thrust/rotation control
- Bullets are independently controlled entities with their own physics
- Asteroids use static collision bodies with procedural placement

### Visual Systems
- Shader-based grid visualization around players
- World boundary rendering with proximity-based visibility
- Death animations and explosion effects as separate scenes

## File Organization

- `/scripts/` - All C# game logic
- `/scenes/` - Godot scene files (.tscn)
- `/assets/` - Game assets and resources
- `/textures/` - Texture files
- `/shaders/` - Custom shader files
- `project.godot` - Godot project configuration
- `ArcaneEye.csproj` - C# project file targeting .NET 8.0