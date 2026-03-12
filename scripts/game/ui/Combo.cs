using Godot;
using System;

public partial class Combo : UIComponent
{
    private Label3D label;

	public override void OnExitTree()
    {
        if (Runner.Attempt == null) return;
		Runner.Attempt.AttemptStatsUpdated -= OnStatsUpdated;
    }

	public override void Init()
	{
		label = GetNode<Label3D>("Label");
		Runner.Attempt.AttemptStatsUpdated += OnStatsUpdated;
	}

    public void OnStatsUpdated(Attempt attempt)
	{
		label.Text = attempt.Combo.ToString();
	}
}
