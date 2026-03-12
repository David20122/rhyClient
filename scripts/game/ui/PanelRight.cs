using Godot;
using System;

public partial class PanelRight : UIComponent
{
	private SubViewport viewport;
	private Label Accuracy, Hits, Misses, SimpleMisses, Sum;
	private Tween hitTween;
    private Tween missTween;

	public override void _ExitTree()
    {
        if (Runner.Attempt == null) return;
		Runner.Attempt.AttemptStatsUpdated -= OnStatsUpdated;
		Runner.Attempt.HitStateChanged -= OnHitStateChanged;
    }

	public override void Init()
	{
		viewport = GetNode<SubViewport>("PanelRightViewport");
		viewport.GetNode<TextureRect>("Background").Texture = SkinManager.Instance.Skin.PanelRightBackgroundImage;
		viewport.GetNode<TextureRect>("HitsIcon").Texture = SkinManager.Instance.Skin.HitsImage;
		viewport.GetNode<TextureRect>("MissesIcon").Texture = SkinManager.Instance.Skin.MissesImage;

		Accuracy = viewport.GetNode<Label>("Accuracy");
		Hits = viewport.GetNode<Label>("Hits");
		Misses = viewport.GetNode<Label>("Misses");
		SimpleMisses = viewport.GetNode<Label>("SimpleMisses");
		Sum = viewport.GetNode<Label>("Sum");

		Hits.LabelSettings.FontColor = Color.Color8(255, 255, 255, 140);
		Misses.LabelSettings.FontColor = Color.Color8(255, 255, 255, 140);

		Runner.Attempt.AttemptStatsUpdated += OnStatsUpdated;
		Runner.Attempt.HitStateChanged += OnHitStateChanged;
	}

	public void OnHitStateChanged(HitObject obj, HitState state)
	{
		switch (state)
		{
			case HitState.MISS:
				Misses.LabelSettings.FontColor = Color.Color8(255, 255, 255, 255);
				missTween?.Kill();
				missTween = Misses.CreateTween();
				missTween.TweenProperty(Misses.LabelSettings, "font_color", Color.Color8(255, 255, 255, 160), 1);
				missTween.Play();
				break;
			case HitState.HIT:
				Hits.LabelSettings.FontColor = Color.Color8(255, 255, 255, 255);
				hitTween?.Kill();
				hitTween = Hits.CreateTween();
				hitTween.TweenProperty(Hits.LabelSettings, "font_color", Color.Color8(255, 255, 255, 160), 1);
				hitTween.Play();
				break;
		}
	}

    public void OnStatsUpdated(Attempt attempt)
	{
		Accuracy.Text = $"{(attempt.Hits + attempt.Misses == 0 ? "100.00" : $"{attempt.Accuracy:F2}")}%";
		Hits.Text = $"{attempt.Hits}";
		Misses.Text = $"{attempt.Misses}";
		SimpleMisses.Text = $"{attempt.Misses}";
		Sum.Text = Util.String.PadMagnitude(attempt.Sum.ToString());
	}
}
