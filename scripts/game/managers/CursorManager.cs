using System;
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
    [Export] private Camera3D camera;

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

        if (runner.Attempt.Settings.AbsoluteInput || runner.Attempt.IsReplay)
            repositionAbsolute();

        if (runner.SpinCamera)
            updateSpinState(inputDelta);
        else
            updateLockedState(inputDelta);
    }

    private void updateSpinState(Vector2 inputDelta)
    {
        Attempt attempt = runner.Attempt;

        if (!attempt.IsReplay)
        {
            camera.Rotation += new Vector3(-inputDelta.Y / 120 * sensitivity / (float)Math.PI, -inputDelta.X / 120 * sensitivity / (float)Math.PI, 0);
        }
        else
        {
            camera.Rotation += new Vector3(inputDelta.Y / (float)Math.PI, -inputDelta.X / (float)Math.PI, 0);
        }
        camera.Rotation = new Vector3((float)Math.Clamp(camera.Rotation.X, Mathf.DegToRad(-90), Mathf.DegToRad(90)), camera.Rotation.Y, camera.Rotation.Z);

        Vector3 Origin = new Vector3(0, 0, 3.5f);
        Vector3 CursorLock = new Vector3(attempt.CursorPosition.X, attempt.CursorPosition.Y, 0);
        // The pivot is to mimic ROBLOX's orbital camera
        Vector3 Pivot = camera.Basis.Z / 4f;

        // Proper Parallax Support
        camera.Position = Origin + CursorLock * (float)attempt.Settings.CameraParallax + Pivot;

        Vector3 LookVector = camera.Basis.Z;
        Vector2 CameraVector2 = new Vector2(camera.Position.X, camera.Position.Y);
        Vector2 LookVector2 = new Vector2(LookVector.X, LookVector.Y);

        // Project Cursor from Camera's "ray cast"
        attempt.RawCursorPosition = CameraVector2 - LookVector2 * Mathf.Abs(camera.Position.Z / LookVector.Z);
        attempt.CursorPosition = attempt.RawCursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS);

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

        // Update visual cursor's position
        cursorMesh.Position = new Vector3(attempt.CursorPosition.X, attempt.CursorPosition.Y, 0);

        Vector3 Origin = new Vector3(0, 0, 3.75f);
        float Parallax = (float)(attempt.IsReplay ? attempt.Replays[0].Parallax : attempt.Settings.CameraParallax);

        camera.Position = Origin + new Vector3(attempt.CursorPosition.X, attempt.CursorPosition.Y, 0) * Parallax;
        camera.Rotation = Vector3.Zero;
    }

    // Reset everything to zero so it doesn't have infinite sensitivity
    private void repositionAbsolute()
    {
        camera.Rotation = Vector3.Zero;
        runner.Attempt.RawCursorPosition = Vector2.Zero;
        runner.Attempt.CursorPosition = Vector2.Zero;
    }
    private void updateCursorRotation(double delta) => cursorMesh.RotationDegrees += Vector3.Back * SettingsManager.Instance.Settings.CursorRotation * (float)delta;
}
