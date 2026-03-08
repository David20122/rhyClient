using Godot;
using System;

public partial class FPSCounter : Label
{
    public uint Frames = 0;
    private uint FPS = 0;
    private double time = 0;

    public override void _Process(double delta)
    {
        
        Frames++;
        time += delta;

		if (time >= 1)
		{
            FPS = Frames;
			
            time--;
            Frames = 0;
        }
        
        Text = $"{FPS} FPS | {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000}ms process time | {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess)*1000}ms physics time";
    }
}