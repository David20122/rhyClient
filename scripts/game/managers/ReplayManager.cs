using Godot;
using System;
using System.ComponentModel;
using System.Formats.Tar;
using System.Linq;
using System.Security.Cryptography;

public partial class ReplayManager : Node
{
	[Export] public Runner Runner { get; set; }
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
		var settings = attempt.Settings;

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

	public override void _Process(double delta)
	{
		
	}
}
