using Godot;
using System;
using System.Collections.Generic;

public partial class GameScene : BaseScene
{
	[Export] public Runner Runner;
	[Export] public Panel Menu;
	[Export] public ReplayManager ReplayManager;
	public PlayerInputController PlayerInputController { get; private set; }
    public CursorManager CursorManager { get; private set; }
	public static Attempt Attempt;

	public bool MenuShown = false;

	public static GameScene Instance;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		Instance.QueueFree();
	}

	public override void _Ready()
	{
		base._Ready();
        CursorManager ??= GetNode<CursorManager>("CursorManager");
        if (CursorManager == null)
            GD.PrintErr("No CursorManager found!");

		PlayerInputController ??= GetNode<PlayerInputController>("PlayerInputController");
        if (PlayerInputController == null)
            GD.PrintErr("No PlayerInputController found!");

		PlayerInputController.OnLeftMouseButton += isPressed =>
		{
			if (!isPressed) return;

			ReplayManager.LMB = isPressed;
		};

		PlayerInputController.OnTogglePaused += () =>
		{
			Attempt.Qualifies = false;

			if (SettingsManager.Shown)
			{
				SettingsManager.HideMenu();
			}
			else
			{
				ShowMenu(!MenuShown);
			}
		};

		PlayerInputController.OnToggleReplayViewerVisibility += () =>
		{
			if (Attempt.IsReplay)
			{
				ReplayManager.ShowReplayViewer(Attempt);
			}
		};

		PlayerInputController.OnPauseOrSkip += () =>
		{
			if (Attempt.IsReplay)
			{
				Runner.Playing = !Runner.Playing;
				SoundManager.Song.PitchScale = (float)Attempt.Speed;
				SoundManager.Song.StreamPaused = !Runner.Playing;

				string texturePath =
					Runner.Playing ? "res://textures/ui/pause.png" : "res://textures/ui/play.png";
				ReplayManager.SeekerPause.TextureNormal = GD.Load<Texture2D>(texturePath);
			}
			else
			{
				if (Lobby.Players.Count > 1) return;

				Runner.Skip();
			}
		};

		PlayerInputController.OnToggleFade += () =>
		Attempt.Settings.FadeOut.Value = !Attempt.Settings.FadeOut;
		PlayerInputController.OnTogglePushback += () =>
		Attempt.Settings.Pushback.Value = !Attempt.Settings.Pushback;
		PlayerInputController.OnRestartPressed += Restart;

		Control focused = SceneManager.Root.GetViewport().GuiGetFocusOwner();
		focused?.ReleaseFocus();
		Input.MouseMode = Attempt.Settings.AbsoluteInput.Value || Attempt.IsReplay ? Input.MouseModeEnum.ConfinedHidden : Input.MouseModeEnum.Captured;
		Input.UseAccumulatedInput = false;

		Panel menuButtonsHolder = Menu.GetNode<Panel>("Holder");

		Menu.GetNode<Button>("Button").Pressed += HideMenu;
		menuButtonsHolder.GetNode<Button>("Resume").Pressed += HideMenu;
		menuButtonsHolder.GetNode<Button>("Restart").Pressed += Restart;
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
		ReplayManager.InitReplayLength();

		if (Runner.Attempt.IsReplay)
		{
			GD.Print("Replay Mode: playback");
			ReplayManager.CurrentMode = ReplayManager.Mode.PLAYBACK;
		}
		else if (Runner.Attempt.Settings.RecordReplays)
		{
			ReplayManager.NewReplay(Runner.Attempt);
			GD.Print("Replay Mode: record");
			ReplayManager.CurrentMode = ReplayManager.Mode.RECORD;
		}
		else
		{
			GD.Print("Replay Mode: none");
			ReplayManager.CurrentMode = ReplayManager.Mode.NONE;
		}

		Runner.Play();
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
		Attempt = new Attempt(map, speed, startFrom, mods ?? [], players, replays);

        // temp fix as rewriting the cursor stuff requires attempt context (remove later) -thom
        CursorManager.Attempt = Attempt;

		SceneManager.Load("res://scenes/game.tscn");
	}

	public void Restart()
	{
		Attempt.Alive = false;
		Attempt.Qualifies = false;
		Runner.Stop(false);

		Attempt oldAttempt = Attempt;
		Map map = MapParser.Decode(oldAttempt.Map.FilePath);
		Attempt = new Attempt(map, oldAttempt.Speed, oldAttempt.StartFrom, oldAttempt.Mods, oldAttempt.Players, oldAttempt.Replays);

        // temp fix as rewriting the cursor stuff requires attempt context (remove later) -thom
        CursorManager.Attempt = Attempt;

		SceneManager.ReloadCurrentScene();
	}

	public override void _Process(double delta)
	{
		if (ReplayManager.CurrentMode == ReplayManager.Mode.PLAYBACK)
		{
			ReplayManager.UpdateReplayCursor(Attempt); }
	}

	public void ShowMenu(bool show = true)
	{
		MenuShown = show;
		Runner.Playing = !MenuShown;

		// rest in peace 0.000000000000000001f pitch scale -fog
		SoundManager.Song.PitchScale = (float)Attempt.Speed;
		SoundManager.Song.StreamPaused = !Runner.Playing;

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
