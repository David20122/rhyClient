using Godot;
using System;

public partial class PanelLeft : UIComponent
{
	private SubViewport viewport;
	private ShaderMaterial multiplierProgressMaterial;
	private Color targetMultiplierColour = new Color(1,1,1,1);
	private float targetMultiplierProgress = 0;
	private Tween multiplierTween;

	private Label Score, Multiplier;

	public override void _ExitTree()
    {
        if (Runner.Attempt == null) return;
		Runner.Attempt.AttemptStatsUpdated -= OnStatsUpdated;
    }

	public override void Init()
	{
		viewport = GetNode<SubViewport>("PanelLeftViewport");
		viewport.GetNode<TextureRect>("Background").Texture = SkinManager.Instance.Skin.PanelLeftBackgroundImage;
		Score = viewport.GetNode<Label>("Score");
		Multiplier = viewport.GetNode<Label>("Multiplier");

		multiplierProgressMaterial = viewport.GetNode<Panel>("MultiplierProgress").Material as ShaderMaterial;
		multiplierProgressMaterial.SetShaderParameter("progress", targetMultiplierProgress);
		multiplierProgressMaterial.SetShaderParameter("colour", targetMultiplierColour);
		multiplierProgressMaterial.SetShaderParameter("sides", Math.Clamp(Runner.Attempt.ComboMultiplierIncrement, 3, 32));

		Runner.Attempt.AttemptStatsUpdated += OnStatsUpdated;
	}

    public void OnStatsUpdated(Attempt attempt)
	{
		Score.Text = Util.String.PadMagnitude(attempt.Score.ToString());
		Multiplier.Text = $"{attempt.ComboMultiplier}x";

		targetMultiplierProgress = (float)attempt.ComboMultiplierProgress / attempt.ComboMultiplierIncrement;

		if (attempt.ComboMultiplier == 8)
		{
			targetMultiplierColour = Color.Color8(255, 140, 0);
		} 
		else
		{
			targetMultiplierColour = Color.Color8(255, 255, 255);
		}

		multiplierTween = CreateTween().SetParallel(true);
		multiplierTween.TweenProperty(multiplierProgressMaterial, "shader_parameter/progress", targetMultiplierProgress, 0.2f);
		multiplierTween.TweenProperty(multiplierProgressMaterial, "shader_parameter/colour", targetMultiplierColour, 0.2f);
	}
}
