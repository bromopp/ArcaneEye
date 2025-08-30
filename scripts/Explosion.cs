using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;



public partial class Explosion : Node3D
{
	[Export] private GpuParticles3D Debris;
	[Export] private GpuParticles3D Smoke;
	[Export] private GpuParticles3D Fire;
	[Export] private AudioStreamPlayer3D ExplosionSound;

	public async Task Explode()
	{
		Debris.Emitting = true;
		Smoke.Emitting = true;
		Fire.Emitting = true;
		ExplosionSound.Play();
		await ToSignal(GetTree().CreateTimer(2.0),SceneTreeTimer.SignalName.Timeout);
		QueueFree();
	}
}
