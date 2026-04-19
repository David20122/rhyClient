using Godot;
using System;
using System.ComponentModel;
using System.Formats.Tar;
using System.Linq;
using System.Security.Cryptography;

public partial class ReplayManager : Node
{
	public enum Mode
	{
		NONE,
		RECORD,
		PLAYBACK
	}

	[Export] public Runner Runner { get; set; }
	[Export] public Mode CurrentMode {get; set; }

	public Vector2 CursorPos;

	private FileAccess _file;
	private ulong statusOffset, frameCountOffset;

	public void NewReplay(Attempt attempt)
	{
		var settings = attempt.Settings;
		if (!settings.RecordReplays) return;
		
		_file = FileAccess.Open($"{Constants.USER_FOLDER}/replays/{attempt.ID}.phxr", FileAccess.ModeFlags.Write);
		_file.StoreString("phxr");	// sig
		_file.Store8(1);	// replay file version

		_file.StoreDouble(attempt.Speed);
		_file.StoreDouble(attempt.StartFrom);
		_file.StoreDouble(settings.ApproachRate);
		_file.StoreDouble(settings.ApproachDistance);
		_file.StoreDouble(settings.FadeIn);
		_file.Store8((byte)(settings.FadeOut ? 1 : 0));
		_file.Store8((byte)(settings.Pushback ? 1 : 0));
		_file.StoreDouble(settings.CameraParallax);
		_file.StoreDouble(settings.FoV.Value);
		_file.StoreDouble(settings.NoteSize);
		_file.StoreDouble(settings.Sensitivity);

		statusOffset = (uint)_file.GetPosition();
		_file.Store8(0);
		
		string mods = string.Join("_", Runner.Attempt.Mods.Where(mod => mod.Value).Select(mod => mod.Key));
		string mapName = attempt.Map.FilePath.GetBaseName();
		string player = "You";

		void StoreSizedString(string data)
		{
			_file.Store32((uint)data.Length);
			_file.StoreString(data);
		}

		StoreSizedString(mods);
		StoreSizedString(mapName);
		_file.Store64((ulong)attempt.Map.Notes.Length);
		StoreSizedString(player);

		frameCountOffset = (uint)_file.GetPosition();
		_file.Store64(0);	// reserve frame count
	}

	public void SaveReplay(Attempt attempt)
	{
		// var settings = attempt.Settings;

		_file.Seek(statusOffset);
		_file.Store8((byte)(attempt.Alive ? (attempt.Qualifies ? 0 : 1) : 2));
		_file.Seek(frameCountOffset);
		_file.Store64((ulong)attempt.ReplayFrames.Count);

		foreach (float[] frame in attempt.ReplayFrames)
		{
			_file.StoreFloat(frame[0]);
			_file.StoreFloat(frame[1]);
			_file.StoreFloat(frame[2]);
		}

		_file.Seek(_file.GetLength());
		_file.Store64(attempt.FirstNote);
		_file.Store64(attempt.Sum);
		for (ulong i = attempt.FirstNote; i < attempt.FirstNote + attempt.Sum; i++)
		{
			_file.Store8((byte)(attempt.HitsInfo[i] == -1 ? 255 : Math.Min(254, attempt.HitsInfo[i] * (254 / 55))));
		}
		
		_file.Store64((ulong)attempt.ReplaySkips.Count);
		foreach (float skip in attempt.ReplaySkips)
		{
			_file.StoreFloat(skip);
		}
		
		_file.Close();
		_file = Godot.FileAccess.Open($"{Constants.USER_FOLDER}/replays/{attempt.ID}.phxr", Godot.FileAccess.ModeFlags.ReadWrite);
		ulong length = _file.GetLength();
		byte[] hash = SHA256.HashData(_file.GetBuffer((long)length));
		_file.StoreBuffer(hash);
		_file.Close();
	}

	public override void _Ready()
	{
		base._Ready();

	}

	public override void _Process(double delta)
	{

		/*
		fog here!

		this is all barebones and copied and pasted just to observe functionality
		i will write functions for this maybe either in ReplayManager, or in GameScene.cs
		for now, i got simple cursor movement working so we can go off that as a base

		the replay system are meant to be done for multiple replays
		lowkey want to scrap that since it overcomplicates things + better off being an external tool
		sorry nyu :(

		anyways, had to speedrun some stupid functionality and hacks but replay cursor movement works, saving still broken
		*/
	
		if (Runner.Attempt.IsReplay)
		{
			for (int i = 0; i < Runner.Attempt.Replays.Length; i++)
			{
				var replay = Runner.Attempt.Replays[i];
				for (int j = Runner.Attempt.Replays[i].FrameIndex; j < Runner.Attempt.Replays[i].Frames.Length; j++)
				{
					if (Runner.Attempt.Progress < Runner.Attempt.Replays[i].Frames[j].Progress)
					{
						Runner.Attempt.Replays[i].FrameIndex = Math.Max(0, j - 1);
						break;
					}
				}
			
				// int next = Math.Min(Runner.Attempt.Replays[i].FrameIndex + 1, Runner.Attempt.Replays[i].Frames.Length - 2);

				// double inverse = Mathf.InverseLerp(Runner.Attempt.Replays[i].Frames[Runner.Attempt.Replays[i].FrameIndex].Progress, Runner.Attempt.Replays[i].Frames[next].Progress, Runner.Attempt.Progress);
				// Vector2 cursorPos = Runner.Attempt.Replays[i].Frames[Runner.Attempt.Replays[i].FrameIndex].CursorPosition.Lerp(Runner.Attempt.Replays[i].Frames[next].CursorPosition, (float)Math.Clamp(inverse, 0, 1));

				int next = Math.Min(replay.FrameIndex + 1, replay.Frames.Length - 2);

				double inverse = Mathf.InverseLerp(replay.Frames[replay.FrameIndex].Progress, replay.Frames[next].Progress, Runner.Attempt.Progress);
				Vector2 cursorPos = replay.Frames[replay.FrameIndex].CursorPosition.Lerp(replay.Frames[next].CursorPosition, (float)Math.Clamp(inverse, 0, 1));

				CursorPos = cursorPos;

			}
		}

	}
}
