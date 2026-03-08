using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public partial class GameScene : BaseScene
{
	[Export] public Runner Runner;
	[Export] public HudManager HudManager;
	[Export] public Panel Menu;
	public static Attempt Attempt;

	public bool MenuShown = false;

	public static GameScene Instance;

    public override void _EnterTree()
    {
        Instance = this;
    }

	public override void _Ready()
	{
		base._Ready();

		HudManager.Init();

		Input.MouseMode = Attempt.Settings.AbsoluteInput.Value || Attempt.IsReplay ? Input.MouseModeEnum.ConfinedHidden : Input.MouseModeEnum.Captured;
		Input.UseAccumulatedInput = false;

		Attempt.HitStateChanged += HitStateChanged;

		Panel menuButtonsHolder = Menu.GetNode<Panel>("Holder");

		Menu.GetNode<Button>("Button").Pressed += HideMenu;
		menuButtonsHolder.GetNode<Button>("Resume").Pressed += HideMenu;
		//menuButtonsHolder.GetNode<Button>("Restart").Pressed += Restart;
		menuButtonsHolder.GetNode<Button>("Settings").Pressed += () => {
			SettingsManager.ShowMenu();
		};
		menuButtonsHolder.GetNode<Button>("Quit").Pressed += () => {
			if (Attempt.Alive)
			{
				SoundManager.FailSound.Play();
			}

			Attempt.Alive = false;
			Attempt.Qualifies = false;

			if (Attempt.DeathTime == -1)
			{
				Attempt.DeathTime = Math.Max(0, Attempt.Progress);
			}

			Runner.Stop();
		};
		
		Runner.Attempt = Attempt;
		Runner.Set(Attempt.Map);
    	Runner.Play();
	}

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    public override void Load()
    {
        base.Load();

		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		MenuCursor.Instance.UpdateVisible(false, false);
        SceneManager.Space.UpdateState(true);
		SceneManager.Space.UpdateMap(Attempt.Map);
    }

	public static void Play(Map map, double speed, double startFrom, Dictionary<string, bool> mods, string[] players = null, Replay[] replays = null)
	{
		map = MapParser.Decode(map.FilePath);
		Attempt = new Attempt(map, speed, startFrom, mods ?? [], null, null);
		SceneManager.Load("res://scenes/game.tscn");
	}

	public void HitStateChanged(HitObject hitObject, HitState hitState)
	{
		float lateness = Attempt.IsReplay ? Attempt.HitsInfo[hitObject.Index] : (float)(((int)Attempt.Progress - Attempt.Map.Notes[hitObject.Index].Millisecond) / Attempt.Speed);
		float factor = 1 - Math.Max(0, lateness - 25) / 150f;
		uint hitScore = (uint)(100 * Attempt.ComboMultiplier * Attempt.ModsMultiplier * factor * ((Attempt.Speed - 1) / 2.5 + 1));
		
		switch (hitState)
		{
			case HitState.HIT:
				Attempt.Hits++;
				Attempt.Sum++;
				Attempt.Accuracy = Math.Floor((float)Attempt.Hits / Attempt.Sum * 10000) / 100;
				Attempt.Combo++;
				Attempt.ComboMultiplierProgress++;
				Attempt.LastHitColour = SkinManager.Instance.Skin.NoteColors[hitObject.Index % SkinManager.Instance.Skin.NoteColors.Length];
				Attempt.Score += hitScore;
				Attempt.HealthStep = Math.Max(Attempt.HealthStep / 1.45, 15);
				Attempt.Health = Math.Min(100, Attempt.Health + Attempt.HealthStep / 1.75);
				Stats.NotesHit++;
				if (Attempt.Combo > Stats.HighestCombo) Stats.HighestCombo = Attempt.Combo;
				Attempt.HitsInfo[hitObject.Index] = lateness;

				if (Attempt.ComboMultiplierProgress == Attempt.ComboMultiplierIncrement)
				{
					if (Attempt.ComboMultiplier < 8)
					{
						Attempt.ComboMultiplierProgress = Attempt.ComboMultiplier == 7 ? Attempt.ComboMultiplierIncrement : 0;
						Attempt.ComboMultiplier++;
					}
				}
				break;
			case HitState.MISS:
				Attempt.Misses++;
				Attempt.Sum++;
				Attempt.Accuracy = Mathf.Floor((float)Attempt.Hits / Attempt.Sum * 10000) / 100;
				Attempt.Combo = 0;
				Attempt.ComboMultiplierProgress = 0;
				Attempt.ComboMultiplier = Math.Max(1, Attempt.ComboMultiplier - 1);
				Attempt.Health = Math.Max(0, Attempt.Health - Attempt.HealthStep);
				Attempt.HealthStep = Math.Min(Attempt.HealthStep * 1.2, 100);
				Stats.NotesMissed++;
				Attempt.HitsInfo[hitObject.Index] = -1;
				break;
			default:
				break;
		}

		HudManager.UpdateHud(Attempt);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion eventMouseMotion)
		{
			if (!Runner.Playing || Attempt.IsReplay) return;

			if (!Attempt.Settings.AbsoluteInput)
			{
				UpdateCursor(eventMouseMotion.Relative);
			}
			else
			{
				// Take mouse position difference between center of the current window size
				// This is to make the mouse position the same as relative if it was locked, or confined
				Vector2 AbsolutePosition = eventMouseMotion.Position - (GetViewport().GetWindow().Size / 2);

				// Multiply by 0.582f to make it 1:1 to absolute scale on nightly
				UpdateCursor(AbsolutePosition * 0.582f);
			}

			Attempt.DistanceMM += eventMouseMotion.Relative.Length() / Attempt.Settings.Sensitivity / 57.5;
		}
		else if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			switch (eventKey.PhysicalKeycode)
			{
				case Key.Escape:
					Attempt.Qualifies = false;

					if (SettingsManager.Shown)
					{
						SettingsManager.HideMenu();
					}
					else
					{
						ShowMenu(!MenuShown);
					}

					break;
				case Key.Quoteleft:
					//Restart();
					break;
				case Key.F1:
					if (Attempt.IsReplay)
					{
						//ShowReplayViewer(!ReplayViewerShown);
					}
					break;
				case Key.Space:
					if (Attempt.IsReplay)
					{
						Runner.Playing = !Runner.Playing;
						SoundManager.Song.PitchScale = Runner.Playing ? (float)Attempt.Speed : 0.00000000000001f;	// ooohh my goood
						//replayViewerPause.TextureNormal = GD.Load<Texture2D>(Playing ? "res://textures/ui/pause.png" : "res://textures/ui/play.png");
					}
					else
					{
						if (Lobby.Players.Count > 1)
						{
							break;
						}

						//Skip();
					}
					break;
				case Key.F:
					Attempt.Settings.FadeOut.Value = !Attempt.Settings.FadeOut;
					break;
				case Key.P:
					Attempt.Settings.Pushback.Value = !Attempt.Settings.Pushback;
					break;
			}
		}
		else if (@event is InputEventMouseButton eventMouseButton)
		{
			switch (eventMouseButton.ButtonIndex)
			{
				case MouseButton.Left:
					//leftMouseButtonDown = eventMouseButton.Pressed;
					break;
			}
		}
	}

    public void UpdateCursor(Vector2 mouseDelta)
	{
		float sensitivity = (float)(Attempt.IsReplay ? Attempt.Replays[0].Sensitivity : Attempt.Settings.Sensitivity);
		sensitivity *= (float)Attempt.Settings.FoV.Value / 70f;

		if (Attempt.Settings.AbsoluteInput)
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
				Attempt.CursorPosition = (Attempt.CursorPosition + new Vector2(1, -1) * mouseDelta / 120 * sensitivity).Clamp(-Constants.BOUNDS, Constants.BOUNDS);
			}
			else
			{
				Attempt.RawCursorPosition += new Vector2(1, -1) * (mouseDelta * sensitivity / 120f);
				Attempt.CursorPosition = Attempt.RawCursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
			}

			Runner.Cursor.Position = new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0);
			Runner.Camera.Position = new Vector3(0, 0, 3.75f) + new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0) * (float)(Attempt.IsReplay ? Attempt.Replays[0].Parallax : Attempt.Settings.CameraParallax);
			Runner.Camera.Rotation = Vector3.Zero;

			//videoQuad.Position = new Vector3(Camera.Position.X, Camera.Position.Y, -100);
		}
		else
		{
			Runner.Camera.Rotation += new Vector3(-mouseDelta.Y / 120 * sensitivity / (float)Math.PI, -mouseDelta.X / 120 * sensitivity / (float)Math.PI, 0);

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

	public void ShowMenu(bool show = true)
	{
		MenuShown = show;
		Runner.Playing = !MenuShown;
		SoundManager.Song.PitchScale = Runner.Playing ? (float)Attempt.Speed : 0.00000000000001f;	// not again

		MenuCursor.Instance.UpdateVisible(MenuShown && SettingsManager.Instance.Settings.UseCursorInMenus.Value);

		if (MenuShown)
		{
			Menu.Visible = true;
			Input.WarpMouse(GetViewport().GetWindow().Size / 2);
		}
		else
		{
			Input.MouseMode = Attempt.Settings.AbsoluteInput || Attempt.IsReplay ? Input.MouseModeEnum.ConfinedHidden : Input.MouseModeEnum.Captured;
		}

		Tween tween = Menu.CreateTween();
		tween.TweenProperty(Menu, "modulate", Color.Color8(255, 255, 255, (byte)(MenuShown ? 255 : 0)), 0.25).SetTrans(Tween.TransitionType.Quad);
		tween.TweenCallback(Callable.From(() => {
			Menu.Visible = MenuShown;
		}));
		tween.Play();
	}

	public void HideMenu()
	{
		ShowMenu(false);
	}
}
