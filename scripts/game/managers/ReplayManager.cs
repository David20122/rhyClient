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
	
		if (Runner.Attempt.IsReplay && Runner.Playing)
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

	public void UpdateReplayCursor(Attempt Attempt)
	{
		if (Attempt.IsReplay)
		{
			// Reset everything to zero so it doesn't spin endlessly, or have infinite sensitivity
			Runner.Camera.Rotation = Vector3.Zero;
			Attempt.RawCursorPosition = Vector2.Zero;
			Attempt.CursorPosition = Vector2.Zero;
		}

		if (!Runner.SpinCamera)
		{
			if (Attempt.Settings.CursorDrift)
			{
				Attempt.CursorPosition = CursorPos.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
			}
			else
			{
				Attempt.RawCursorPosition = CursorPos;
				Attempt.CursorPosition = Attempt.RawCursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
			}

			Runner.Cursor.Position = new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0);
			Runner.Camera.Position = new Vector3(0, 0, 3.75f) + new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0) * (float)Attempt.Replays[0].Parallax;
			Runner.Camera.Rotation = Vector3.Zero;

			//videoQuad.Position = new Vector3(Camera.Position.X, Camera.Position.Y, -100);
		}
		else
		{
			Runner.Camera.Rotation += new Vector3(CursorPos.Y / (float)Math.PI, -CursorPos.X / (float)Math.PI, 0);

			Runner.Camera.Rotation = new Vector3((float)Math.Clamp(Runner.Camera.Rotation.X, Mathf.DegToRad(-90), Mathf.DegToRad(90)), Runner.Camera.Rotation.Y, Runner.Camera.Rotation.Z);

			Vector3 Origin = new Vector3(0,0,3.5f);
			Vector3 CursorLock = new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0);
			// The pivot is to mimic ROBLOX's orbital camera
			Vector3 Pivot = Runner.Camera.Basis.Z / 4f;

			Runner.Camera.Position = Origin + CursorLock * Attempt.Settings.CameraParallax + Pivot;

			Vector3 LookVector = Runner.Camera.Basis.Z;
			Vector2 CameraVec2 = new Vector2(Runner.Camera.Position.X, Runner.Camera.Position.Y);
			Vector2 LookVec2 = new Vector2(LookVector.X, LookVector.Y);

			Attempt.RawCursorPosition = CameraVec2 - LookVec2 * Mathf.Abs(Runner.Camera.Position.Z / LookVector.Z);

			Attempt.CursorPosition = Attempt.RawCursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
			Runner.Cursor.Position = new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0);

			//videoQuad.Position = Camera.Position - Camera.Basis.Z * 103.75f;
			//videoQuad.Rotation = Camera.Rotation;
		}
	}
}
