using Godot;
using System;

public partial class HealthBar : UIComponent
{
	private SubViewport viewport;
	private TextureRect healthBarTexture;
	private TextureRect healthBarBGTexture;
	private Tween tween;

    public override void _ExitTree()
    {
        if (Runner.Attempt == null) return;
		Runner.Attempt.AttemptStatsUpdated -= OnStatsUpdated;
		tween?.Kill();
    }

	public override void Init()
	{
		viewport = GetNode<SubViewport>("HealthViewport");
		healthBarTexture = viewport.GetNode<TextureRect>("Main");
		healthBarBGTexture = viewport.GetNode<TextureRect>("Background");
		healthBarTexture.Texture = SkinManager.Instance.Skin.HealthImage;
		healthBarBGTexture.Texture = SkinManager.Instance.Skin.HealthBackgroundImage;

		Runner.Attempt.AttemptStatsUpdated += OnStatsUpdated;
	}

	public void OnStatsUpdated(Attempt attempt)
	{
		float targetWidth = 32 + (float)attempt.Health * 10.24f;
		Vector2 targetSize = new Vector2(targetWidth, 80);

		tween?.Kill();
		tween = CreateTween();
		tween.TweenProperty(healthBarTexture, "size", targetSize, 0.2f);

		if (!attempt.IsReplay && attempt.Health <= 0)
		{
			healthBarTexture.Modulate = Color.Color8(255, 255, 255, 128);
			healthBarBGTexture.Modulate = healthBarTexture.Modulate;
		}
	}
}
