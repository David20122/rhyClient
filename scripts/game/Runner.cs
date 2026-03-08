using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

public partial class Runner : Node3D
{
	public Attempt Attempt;
	public Map Map;
	private SettingsProfile settings;
	public double Speed = 1;
	public bool Paused = false;
	public bool Playing = false;
	public bool StopQueued = false;

	public int ToProcess = 0;
	public List<Note> ProcessNotes = [];
	private double lastFrame = Time.GetTicksUsec();

	public bool SpinCamera = false;

	[ExportCategory("Settings")]
	[Export] public bool HideHUD = false;

	[ExportCategory("Nodes")]
	[Export] public Camera3D Camera;
	[Export] public MeshInstance3D Grid;
	[Export] public MeshInstance3D Cursor;
	[Export] public MultiMeshInstance3D Notes;

	public override void _Ready()
	{
		base._Ready();

		// if null, assign them to nodes under Runner
		Camera ??= GetNode<Camera3D>("Camera3D");
		Grid ??= GetNode<MeshInstance3D>("Grid");
		Cursor ??= GetNode<MeshInstance3D>("Cursor");
		Notes ??= GetNode<MultiMeshInstance3D>("Notes");
	}

	public override void _Process(double delta)
	{
		ulong now = Time.GetTicksUsec();
		delta = (now - lastFrame) / 1000000;    // more reliable
		lastFrame = now;

		if (!Playing) return;
		Attempt.Progress += delta * 1000 * Attempt.Speed;
		Attempt.CanSkip = false;

		if (Attempt.Map.AudioBuffer != null)
		{
			if (Attempt.Progress >= Attempt.MapLength - Constants.HIT_WINDOW)
			{
				if (SoundManager.Song.Playing)
				{
					SoundManager.Song.Stop();
				}
			}
			else if (!SoundManager.Song.Playing && Attempt.Progress >= 0)
			{
				SoundManager.Song.Play();
				SoundManager.Song.Seek((float)Attempt.Progress / 1000);
			}
		}

		int nextNoteMillisecond = Attempt.PassedNotes >= Attempt.Map.Notes.Length ? (int)Attempt.MapLength + Constants.BREAK_TIME : Attempt.Map.Notes[Attempt.PassedNotes].Millisecond;

		if (nextNoteMillisecond - Attempt.Progress >= Constants.BREAK_TIME * Attempt.Speed)
		{
			int lastNoteMillisecond = Attempt.PassedNotes > 0 ? Attempt.Map.Notes[Attempt.PassedNotes - 1].Millisecond : 0;
			int skipWindow = nextNoteMillisecond - Constants.BREAK_TIME - lastNoteMillisecond;

			if (skipWindow >= 1000 * Attempt.Speed) // only allow skipping if i'm gonna allow it for at least 1 second
			{
				Attempt.CanSkip = true;
			}
		}

		ToProcess = 0;
		ProcessNotes.Clear();

		// note process check
		double at = Attempt.IsReplay ? Attempt.Replays[0].ApproachTime : settings.ApproachTime;

		for (uint i = Attempt.PassedNotes; i < Attempt.Map.Notes.Length; i++)
		{
			Note note = Attempt.Map.Notes[i];

			if (note.Millisecond < Attempt.StartFrom)
			{
				continue;
			}

			if (note.Millisecond + Constants.HIT_WINDOW * Attempt.Speed < Attempt.Progress) // past hit window
			{
				if (i + 1 > Attempt.PassedNotes)
				{
					if (Attempt.IsReplay && Attempt.Replays.Length <= 1 && Attempt.Replays[0].Notes[note.Index] == -1 || !Attempt.IsReplay && note.HitState != HitState.HIT)
					{
						note.Miss(Attempt);
					}

					Attempt.PassedNotes = i + 1;
				}

				if (!Attempt.IsReplay)
				{
					continue;
				}
			}
			else if (note.Millisecond > Attempt.Progress + at * 1000 * Attempt.Speed)   // past approach distance
			{
				break;
			}
			else if (note.HitState == HitState.HIT) // no point
			{
				continue;
			}

			if (settings.AlwaysPlayHitSound && !Attempt.Map.Notes[i].Hittable && note.Millisecond < Attempt.Progress)
			{
				Attempt.Map.Notes[i].Hittable = true;

				SoundManager.HitSound.Play();
			}

			ToProcess++;
			ProcessNotes.Add(note);
		}

		// hitreg check
		for (int i = 0; i < ToProcess; i++)
		{
			Note note = ProcessNotes[i];
			if (note.HitState == HitState.HIT) continue;

			if (!Attempt.IsReplay)
			{
				if (note.Millisecond - Attempt.Progress > 0)
				{
					continue;
				}
				note.CheckHit(Attempt, note);
			}
			else if (Attempt.Replays.Length > 1 && note.Millisecond - Attempt.Progress <= 0 || Attempt.Replays[0].Notes[note.Index] != -1 && note.Millisecond - Attempt.Progress + Attempt.Replays[0].Notes[note.Index] * Attempt.Speed <= 0)
			{
				//Attempt.Hit(note.Index);
			}
		}

		if (Attempt.Progress >= Attempt.MapLength)
		{
			Stop();
			return;
		}

		// if (Attempt.Skippable)
		// {
		// 	targetSkipLabelAlpha = 100f / 255f;
		// 	progressLabel.Modulate = Color.Color8(255, 255, 255, (byte)(96 + (int)(140 * (Math.Sin(Math.PI * now / 750000) / 2 + 0.5))));
		// }
		// else
		// {
		// 	targetSkipLabelAlpha = 0;
		// 	progressLabel.Modulate = Color.Color8(255, 255, 255, 190);
		// }

		// progressLabel.Text = $"{Util.String.FormatTime(Math.Max(0, Attempt.Progress) / 1000)} / {Util.String.FormatTime(MapLength / 1000)}";
		// healthTexture.Size = healthTexture.Size.Lerp(new Vector2(32 + (float)Attempt.Health * 10.24f, 80), Math.Min(1, (float)delta * 64));
		// progressBarTexture.Size = new Vector2(32 + (float)(Attempt.Progress / MapLength) * 1024, 80);
		// skipLabel.Modulate = Color.Color8(255, 255, 255, (byte)(skipLabelAlpha * 255));

		//Cursor.RotationDegrees += Vector3.Back * settings.CursorRotation * (float)delta;
	}

	public void Set(Map map) => Map = map;

	public void Play()
	{
		if (Attempt == null)
		{
			return;
		}

		Control focused = SceneManager.Root.GetViewport().GuiGetFocusOwner();
		focused?.ReleaseFocus();

		Playing = true;
		Attempt.TimeStarted = Time.GetTicksUsec();
		SpinCamera = Attempt.Mods.Any(mod => mod.Name == "Spin");

		settings = SettingsManager.Instance.Settings;
		Camera.Fov = settings.FoV.Value;
		(Cursor.Mesh as QuadMesh).Size = new Vector2((float)(Constants.CURSOR_SIZE * settings.CursorScale.Value), (float)(Constants.CURSOR_SIZE * settings.CursorScale.Value));

		(Cursor.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = SkinManager.Instance.Skin.CursorImage;
		(Grid.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = SkinManager.Instance.Skin.GridImage;
		Notes.Multimesh.Mesh = SkinManager.Instance.Skin.NoteMesh;

		if (Attempt.Map.AudioBuffer != null)
		{
			SoundManager.Song.Stream = Util.Audio.LoadStream(Attempt.Map.AudioBuffer);
			SoundManager.Song.PitchScale = (float)Attempt.Speed;
			Attempt.MapLength = (float)SoundManager.Song.Stream.GetLength() * 1000;
		}
		else
		{
			Attempt.MapLength = Attempt.Map.Length + 1000;
		}

		Attempt.MapLength += Constants.HIT_WINDOW;
		SoundManager.UpdateVolume();
	}

	public void Restart()
	{
		Attempt.Alive = false;
		Attempt.Qualifies = false;
		Stop(false);

		SceneManager.ReloadCurrentScene();
		GameScene.Play(MapParser.Decode(Attempt.Map.FilePath), Attempt.Speed, Attempt.StartFrom, null, Attempt.Players, Attempt.Replays);
	}

	public void Pause()
	{

	}

	public void QueueStop()
	{
		if (!Playing)
		{
			return;
		}

		Playing = false;
		StopQueued = true;
	}


	public void Stop(bool results = true)
	{
		if (Attempt.Stopped)
		{
			return;
		}

		//Attempt.Stop();

		if (!Attempt.IsReplay)
		{
			Stats.GamePlaytime += (Time.GetTicksUsec() - Attempt.TimeStarted) / 1000000;
			Stats.TotalDistance += (ulong)Attempt.DistanceMM;

			if (Attempt.StartFrom == 0)
			{
				// if (!File.Exists($"{Constants.USER_FOLDER}/pbs/{Attempt.Map.Name}"))
				// {
				// 	List<byte> bytes = [0, 0, 0, 0];
				// 	bytes.AddRange(SHA256.HashData([0, 0, 0, 0]));
				// 	File.WriteAllBytes($"{Constants.USER_FOLDER}/pbs/{Attempt.Map.Name}", [.. bytes]);
				// }

				// Leaderboard leaderboard = new(Attempt.Map.Name, $"{Constants.USER_FOLDER}/pbs/{Attempt.Map.Name}");

				// leaderboard.Add(new(Attempt.ID, "You", Attempt.Qualifies, Attempt.Score, Attempt.Accuracy, Time.GetUnixTimeFromSystem(), Attempt.Progress, Attempt.Map.Length, Attempt.Speed, Attempt.Mods));
				// leaderboard.Save();

				if (Attempt.Qualifies)
				{
					Stats.Passes++;
					Stats.TotalScore += Attempt.Score;

					if (Attempt.Accuracy == 100)
					{
						Stats.FullCombos++;
					}

					if (Attempt.Score > Stats.HighestScore)
					{
						Stats.HighestScore = Attempt.Score;
					}

					Stats.PassAccuracies.Add(Attempt.Accuracy);
				}
			}
		}

		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Adaptive);

		if (results)
		{
			SceneManager.Load("res://scenes/results.tscn");
		}
	}
}