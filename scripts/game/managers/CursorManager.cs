using Godot;

/// <summary>
/// Class containing all gameplay related logic regarding the cursor.
/// </summary>
public partial class CursorManager : Node
{
    [Export] private Runner runner;
    [Export] private PlayerInputController playerInputController;
    [Export] private ReplayManager replayManager;
    [Export] private MeshInstance3D cursorMesh;

    private float sensitivity;

    [Signal]
    public delegate void OnCursorUpdatedEventHandler(
        Vector2 position
    );

    public override void _Ready()
    {
        base._Ready();

        cursorMesh ??= GetNode<MeshInstance3D>("Cursor");
        playerInputController ??= GetNode<PlayerInputController>("/PlayerInputController");
        replayManager ??= GetNode<ReplayManager>("ReplayManager");
    }

    public override void _Process(double delta)
    {
        if (!runner.Playing) return;

        updateCursorRotation(delta);
    }

    public void UpdateCursor(Vector2 inputDelta)
    {
        EmitSignalOnCursorUpdated(inputDelta);

        sensitivity = (float)(runner.Attempt.IsReplay ? runner.Attempt.Replays[0].Sensitivity : runner.Attempt.Settings.Sensitivity);
        sensitivity *= runner.Attempt.Settings.FoV.Value / 70f;

        if (runner.Attempt.Settings.AbsoluteInput)
            updateAbsoluteInput();

        if (runner.SpinCamera)
            updateSpinState(inputDelta);
        else
            updateLockedState(inputDelta);
    }

    private void updateSpinState(Vector2 inputDelta)
    {
        Attempt attempt = runner.Attempt;

        if (attempt.IsReplay)
        {
            attempt.CursorPosition = inputDelta;
        }
        else
        {
            Vector3 cameraForward3D = runner.Camera.Basis.Z;
            Vector2 cameraPosition2D = new Vector2(runner.Camera.Position.X, runner.Camera.Position.Y);
            Vector2 cameraForward2D = new Vector2(cameraForward3D.X, cameraForward3D.Y);

            attempt.RawCursorPosition = cameraPosition2D - cameraForward2D * Mathf.Abs(runner.Camera.Position.Z / cameraForward3D.Z);
            attempt.CursorPosition = attempt.RawCursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
        }

        cursorMesh.Position = new Vector3(attempt.CursorPosition.X, attempt.CursorPosition.Y, 0);
    }

    private void updateLockedState(Vector2 inputDelta)
    {
        Attempt attempt = runner.Attempt;
        Vector2 delta = new Vector2(1, -1) * (inputDelta * sensitivity / 120f);

        if (runner.Attempt.Settings.CursorDrift)
        {
            attempt.CursorPosition = attempt.IsReplay
                ? replayManager.CursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS)
                : (attempt.CursorPosition + delta).Clamp(-Constants.BOUNDS, Constants.BOUNDS);
        }
        else
        {
            attempt.RawCursorPosition = attempt.IsReplay
                ? replayManager.CursorPosition
                : attempt.RawCursorPosition + delta;
            attempt.CursorPosition = attempt.RawCursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
        }

        cursorMesh.Position = new Vector3(attempt.CursorPosition.X, attempt.CursorPosition.Y, 0);
    }

    // Reset everything to zero so it doesn't have infinite sensitivity
    private void updateAbsoluteInput()
    {
        runner.Attempt.RawCursorPosition = Vector2.Zero;
        runner.Attempt.CursorPosition = Vector2.Zero;
    }
    private void updateCursorRotation(double delta) => cursorMesh.RotationDegrees += Vector3.Back * SettingsManager.Instance.Settings.CursorRotation * (float)delta;
}
