using Godot;
using System;

public partial class Ingameoptions : ColorRect
{
	public static Ingameoptions Instance { get; set; }
	private HSlider volumeSlider;
	private Button disconnectButton;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (Instance == null)
        {
            Instance = this;
            GD.Print($"Ingameoptions singleton initialized from: {GetPath()}");
        }
        else
        {
            GD.Print($"Ingameoptions already exists at: {Instance.GetPath()}. Current instance at: {GetPath()} will be destroyed.");
            QueueFree();
            return;
        }
		
		volumeSlider = GetNodeOrNull<HSlider>("Volume/VolumeSlider");
		GD.Print($"The volume slider is: {volumeSlider}");
        disconnectButton = GetNodeOrNull<Button>("DisconnectButton");
		GD.Print($"The disconnect button is: {disconnectButton}");
		// Load current master bus volume and set slider value
		float currentVolume = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master"));
		GD.Print($"Audio Bus volume: {AudioServer.GetBusIndex("Master")}");
		// Convert from dB to linear (0-1 range)
		volumeSlider.Value = Mathf.DbToLinear(currentVolume);

		// Connect the slider's value changed signal
		volumeSlider.ValueChanged += OnVolumeChanged;
	}
	
	private void OnVolumeChanged(double value)
	{
		// Convert linear value (0-1) to dB and set master bus volume
		float volumeDb = Mathf.LinearToDb((float)value);
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), volumeDb);
	}
    public void ConnectDisconnectButton(System.Action onPressed)
    {
		GD.Print("Disconnect pressed");
        if (disconnectButton != null)
			disconnectButton.Pressed += onPressed;
    }
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
