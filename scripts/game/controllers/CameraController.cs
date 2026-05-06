using System;
using Godot;

public partial class CameraController : Node
{
    [Export] private CursorManager cursorManager;
    [Export] private Runner runner;
    [Export] private Camera3D camera;

    private float sensitivity;

    public override void _Ready()
    {
        cursorManager ??= GetNode<CursorManager>("/root/CursorManager");
        runner ??= GetNode<Runner>("Runner");
        cursorManager.OnCursorUpdated += updateCameraState;
    }

    private void updateCameraState(Vector2 position)
    {
        Attempt attempt = runner.Attempt;

        sensitivity = (float)(attempt.IsReplay ? attempt.Replays[0].Sensitivity : attempt.Settings.Sensitivity);
        sensitivity *= attempt.Settings.FoV.Value / 70f;

        if (attempt.Settings.AbsoluteInput)
            updateAbsoluteInput();

        if (runner.SpinCamera)
            updateSpinState(position);
        else
            updateLockedState();
    }

    private void updateSpinState(Vector2 inputDelta)
    {
        Vector3 origin = new Vector3(0,0,3.5f);
        Vector3 currentCursorPosition = new Vector3(inputDelta.X, inputDelta.Y, 0);
        // The pivot is to mimic ROBLOX' orbital camera
        Vector3 pivot = runner.Camera.Basis.Z / 4f;

        if (runner.Attempt.IsReplay)
        {
            camera.Rotation = new Vector3(Mathf.Clamp(
                    inputDelta.Y / Mathf.Pi,
                    Mathf.DegToRad(-90),
                    Mathf.DegToRad(90)
                ), -inputDelta.X / Mathf.Pi,
                0
            );
        }
        else
        {
            camera.Rotation += new Vector3(
                -inputDelta.Y / 120 * sensitivity / (float)Math.PI,
                -inputDelta.X / 120 * sensitivity / (float)Math.PI,
                0
            );
            camera.Rotation = new Vector3(
                Math.Clamp(runner.Camera.Rotation.X, Mathf.DegToRad(-90), Mathf.DegToRad(90)),
                runner.Camera.Rotation.Y,
                runner.Camera.Rotation.Z
            );
            camera.Position = origin + currentCursorPosition * runner.Attempt.Settings.CameraParallax + pivot;
        }

    }

    private void updateLockedState()
    {
        Attempt attempt = runner.Attempt;

        if (runner.Attempt.Settings.CursorDrift) return;

        camera.Position = new Vector3(0, 0, 3.75f) + new Vector3(attempt.CursorPosition.X, attempt.CursorPosition.Y, 0) * (float)attempt.Settings.CameraParallax;
        camera.Rotation = Vector3.Zero;
    }

    // Reset everything to zero so it doesn't spin endlessly
    private void updateAbsoluteInput() => camera.Rotation = Vector3.Zero;
    private void assignAttempt() => runner.Attempt = runner.Attempt;
}
