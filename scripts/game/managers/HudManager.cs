using Godot;
using System;
using System.Collections.Generic;

public partial class HudManager : Node
{
	[Export] public Runner Runner;
	[Export] public Node3D HUDContainer;

	public struct hudElements
	{
		public Panel MultiplierProgress;
		public Label Score, MultiplierLabel, Hits, Misses, SimpleMisses, Sum, Accuracy;
		public Label3D Progress, Title, Combo, Speed, Skip;
		public TextureRect HealthBarTexture, ProgressBarTexture;
	}

	public hudElements Hud = new hudElements();

	public void Init()
	{
		Runner ??= GetParent<Runner>();

		var healthViewport = GetNode<MeshInstance3D>("%Health").GetNode<SubViewport>("HealthViewport");
		var progressBarViewport = GetNode<MeshInstance3D>("%ProgressBar").GetNode<SubViewport>("ProgressBarViewport");
		var panelLeftViewport = GetNode<MeshInstance3D>("%PanelLeft").GetNode<SubViewport>("PanelLeftViewport");
		var panelRightViewport = GetNode<MeshInstance3D>("%PanelRight").GetNode<SubViewport>("PanelRightViewport");

		Hud.Score = panelLeftViewport.GetNode<Label>("Score");
		Hud.MultiplierLabel = panelLeftViewport.GetNode<Label>("Multiplier");
		Hud.MultiplierProgress = panelLeftViewport.GetNode<Panel>("MultiplierProgress");
		Hud.Accuracy = panelRightViewport.GetNode<Label>("Accuracy");
		Hud.Hits = panelRightViewport.GetNode<Label>("Hits");
		Hud.Misses = panelRightViewport.GetNode<Label>("Misses");
		Hud.SimpleMisses = panelRightViewport.GetNode<Label>("SimpleMisses");
		Hud.Sum = panelRightViewport.GetNode<Label>("Sum");
		Hud.Progress = GetNode<Label3D>("%Progress");
		Hud.Title = GetNode<Label3D>("%Title");
		Hud.Combo = GetNode<Label3D>("%Combo");
		Hud.Speed = GetNode<Label3D>("%Speed");
		Hud.Skip = GetNode<Label3D>("%Skip");

		Hud.HealthBarTexture = healthViewport.GetNode<TextureRect>("Main");
		Hud.ProgressBarTexture = progressBarViewport.GetNode<TextureRect>("Main");

		/* --- */

		Hud.Title.Text = GameScene.Attempt.Map.PrettyTitle;
	}

	public override void _Process(double delta)
	{
		Hud.Progress.Text = $"{Util.String.FormatTime(Math.Max(0, Runner.Attempt.Progress) / 1000)} / {Util.String.FormatTime(Runner.Attempt.MapLength / 1000)}";
		Hud.ProgressBarTexture.Size = new Vector2(32 + (float)(Runner.Attempt.Progress / Runner.Attempt.MapLength) * 1024, 80);
	}

	public void UpdateHud(Attempt attempt)
	{
		if (!Runner.Playing) return;	

		Hud.Score.Text = Util.String.PadMagnitude(attempt.Score.ToString());
		Hud.MultiplierLabel.Text = $"{attempt.ComboMultiplier}x";
		Hud.Hits.Text = $"{attempt.Hits}";
		Hud.Misses.Text = $"{attempt.Misses}";
		Hud.SimpleMisses.Text = $"{attempt.Misses}";
		Hud.Sum.Text = Util.String.PadMagnitude(attempt.Sum.ToString());
		Hud.Accuracy.Text = $"{(attempt.Hits + attempt.Misses == 0 ? "100.00" : $"{attempt.Accuracy:F2}")}%";
		Hud.Combo.Text = attempt.Combo.ToString();
	}
}
