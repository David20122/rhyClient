using Godot;
using System;
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
	[Export] public Mode CurrentMode { get; set; }
	[Export] public Panel ReplayViewer { get; set; }
    [Export] public CursorManager CursorManager { get; private set; }

	public bool ViewerVisible;

	// only public variable because of GameScene
	public static TextureButton SeekerPause;
	private static Label seekerTime;
	private static HSlider seekerTimeline;
	private static bool seekerHovered;
	public static bool LMB; // fml
	public float ReplayLength;
	public string ReplayPath;
	public Vector2 CursorPosition { get; private set; }

	private FileAccess _file;
	private ulong statusOffset, frameCountOffset;

	public void NewReplay(Attempt attempt)
	{
		var settings = attempt.Settings;
		if (!settings.RecordReplays) return;
		ReplayPath = $"{Constants.USER_FOLDER}/replays/{attempt.ID}.phxr";
		_file = FileAccess.Open(ReplayPath, FileAccess.ModeFlags.Write);

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
		string mapName = attempt.Map.FilePath.GetFile().GetBaseName();
		string player = "You";

		void storeSizedString(string data)
		{
			_file.Store32((uint)data.Length);
			_file.StoreString(data);
		}

		storeSizedString(mods);
		storeSizedString(mapName);
		_file.Store64((ulong)attempt.Map.Notes.Length);
		storeSizedString(player);

		frameCountOffset = (uint)_file.GetPosition();
		_file.Store64(0);	// reserve frame count
	}

	public void SaveReplay(Attempt attempt)
	{
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
		// GD.Print(string.Join(", ", attempt.HitsInfo));
		// GD.Print($"Sum: {attempt.FirstNote}+{attempt.Sum}={attempt.FirstNote + attempt.Sum}");
		// GD.Print($"HitsInfoCount: {attempt.HitsInfo.Count()}");

		if (attempt.FirstNote + attempt.Sum != (uint)attempt.HitsInfo.Length)
		{

			_file.Close();

			if (FileAccess.FileExists(ReplayPath))
			{

                string mismatch = $"Sum: {attempt.FirstNote}+{attempt.Sum}={attempt.FirstNote + attempt.Sum}";
				string hitsInfoDebug = string.Join(", ", attempt.HitsInfo);
				string passedNotesDebug = $"Passed Notes: {attempt.PassedNotes}";
				DirAccess.RemoveAbsolute(ReplayPath);
				GD.PushWarning($"Corrupted De-synced replay deleted!\nPath: {ReplayPath}\n{mismatch}\nHits Info: {hitsInfoDebug}\n{passedNotesDebug}");
			}

			return;
		}
		
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

		// open replay to store hash
		_file = FileAccess.Open($"{Constants.USER_FOLDER}/replays/{attempt.ID}.phxr", FileAccess.ModeFlags.ReadWrite);
		ulong length = _file.GetLength();
		byte[] hash = SHA256.HashData(_file.GetBuffer((long)length));
		_file.StoreBuffer(hash);

		_file.Close();

		attempt.ReplayPath = ReplayPath;
	}

	public void InitReplayLength()
	{
		if (Runner?.Attempt == null || !Runner.Attempt.IsReplay) return;
		ReplayLength = Runner.Attempt.Replays[0].Length;
	}

	public override void _Ready()
	{
		base._Ready();

		// this entire code lowkey sucks, so i am just copy and pasting it because i am lazy -fog
		SeekerPause = ReplayViewer.GetNode<TextureButton>("Pause");
		seekerTime = ReplayViewer.GetNode<Label>("Time");
		seekerTimeline = ReplayViewer.GetNode<HSlider>("Seek");
        CursorManager ??= GetNode<CursorManager>("CursorManager");

		SeekerPause.Pressed += () =>
		{
			Runner.Playing = !Runner.Playing;
			SoundManager.Song.PitchScale = (float)Runner.Attempt.Speed;
			SoundManager.Song.StreamPaused = !Runner.Playing;

			string texturePath = Runner.Playing ? "res://textures/ui/pause.png" : "res://textures/ui/play.png";
			SeekerPause.TextureNormal = GD.Load<Texture2D>(texturePath);
		};

		seekerTimeline.ValueChanged += (double value) =>
		{
			string current = $"{Util.String.FormatTime(value * ReplayLength / 1000)}";
			string end = $"{Util.String.FormatTime(ReplayLength / 1000)}";
			seekerTime.Text = $"{current} / {end}";
		};

		seekerTimeline.DragEnded += (bool _) =>
		{
			resetToSeekedPosition((float)seekerTimeline.Value);
		};

		seekerTimeline.FocusEntered += () => {
			seekerHovered = true;
		};
		seekerTimeline.FocusExited += () => {
			seekerHovered = false;
		};

	}

	public override void _Process(double delta)
    {
        if (!Runner.Attempt.IsReplay || !Runner.Playing) return;

        if (!seekerHovered || !LMB)
        {
            seekerTimeline.Value = Runner.Attempt.Progress / Runner.Attempt.Replays[0].Length;
        }

        for (int i = 0; i < Runner.Attempt.Replays.Length; i++)
        {
            var replay = Runner.Attempt.Replays[i];

            // advance frame forward deterministically making sure frames only advance when allowed
            while (replay.FrameIndex < replay.Frames.Length - 1 &&
                   Runner.Attempt.Progress >= replay.Frames[replay.FrameIndex + 1].Progress)
            {
                replay.FrameIndex++;
            }

            int next = Math.Min(replay.FrameIndex + 1, replay.Frames.Length - 1);

            var currentFrame = replay.Frames[replay.FrameIndex];
            var nextFrame = replay.Frames[next];

            double inverse = Mathf.InverseLerp(
                currentFrame.Progress,
                nextFrame.Progress,
                Runner.Attempt.Progress
            );

            Vector2 cursorPos = currentFrame.CursorPosition.Lerp(
                nextFrame.CursorPosition,
                (float)Math.Clamp(inverse, 0, 1)
            );

            CursorPosition = cursorPos;
        }

        CursorManager.UpdateCursor(CursorPosition);
    }

    public void ShowReplayViewer(Attempt attempt)
        {
            ViewerVisible = !ViewerVisible;
            bool visible = ViewerVisible && attempt.IsReplay;

            ReplayViewer.Visible = visible;

            Input.MouseMode = visible
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Hidden;
        }

        private void resetToSeekedPosition(float seekedTime)
        {
            Attempt att = Runner.Attempt;

            att.Hits = 0;
            att.Misses = 0;
            att.Sum = 0;
            att.Accuracy = 100;
            att.Score = 0;
            att.PassedNotes = 0;
            att.Combo = 0;
            att.ComboMultiplier = 1;
            att.ComboMultiplierProgress = 0;
            att.Health = 100;
            att.HealthStep = 15;

            for (int i = 0; i < att.Map.Notes.Length; i++)
            {
                att.Map.Notes[i].Hittable = false;
            }

            att.Progress = seekedTime * ReplayLength;

            for (int i = 0; i < att.Replays[0].Frames.Length; i++)
            {
                if (att.Progress < att.Replays[0].Frames[i].Progress)
                {
                    att.Replays[0].FrameIndex = Math.Max(0, i - 1);
                    break;
                }
            }

            if (!SoundManager.Song.Playing)
            {
                SoundManager.Song.Play();
            }

            SoundManager.Song.Seek((float)att.Progress / 1000);
        }
}
