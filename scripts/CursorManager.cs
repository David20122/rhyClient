using System;
using Godot;

public partial class CursorManager : Node
{
    [Export] private PlayerInputController playerInputController;
    [Export] private ReplayManager replayManager;
    [Export] private Runner runner;
    [Export] private MeshInstance3D cursor;
    [Export] private Camera3D camera;
    public static Attempt Attempt;

    private SettingsProfile settings;
    private float sensitivity;

    public override void _Ready()
    {
        base._Ready();

        runner ??= GetNode<Runner>("root/SceneGame/Runner");
        cursor ??= GetNode<MeshInstance3D>("root/SceneGame/Cursor");
        camera ??= GetNode<Camera3D>("root/SceneGame/Camera3D");
        playerInputController ??= GetNode<PlayerInputController>("root/SceneGame/PlayerInputController");
        replayManager ??= GetNode<ReplayManager>("ReplayManager");

        playerInputController.OnMouseMove += (relative, absolute) =>
        {
            if (!runner.Playing || Attempt.IsReplay) return;

            if (!Attempt.Settings.AbsoluteInput)
            {
                UpdateCursor(relative);
            }
            else
            {
                // Take mouse position difference between center of the current window size
                // This is to make the mouse position the same as relative if it was locked, or confined
                Vector2 AbsolutePosition = absolute - (GetViewport().GetWindow().Size / 2);

                // Multiply by 0.582f to make it 1:1 to absolute scale on nightly
                UpdateCursor(AbsolutePosition * 0.582f);
            }

            Attempt.DistanceMM += relative.Length() / Attempt.Settings.Sensitivity / 57.5;
        };

        // wait for settings reference to be assigned
        CallDeferred(nameof(assignSettings));
    }

    public override void _Process(double delta)
    {
        updateCursorRotation(delta);
    }

    public void UpdateCursor(Vector2 inputDelta)
    {
        // might be better to change this later to be outside the hot loop?
        sensitivity = (float)(Attempt.IsReplay ? Attempt.Replays[0].Sensitivity : Attempt.Settings.Sensitivity);
        sensitivity *= Attempt.Settings.FoV.Value / 70f;

        if (Attempt.Settings.AbsoluteInput)
            updateAbsoluteInput();
        if (runner.SpinCamera)
            updateSpinState(inputDelta);
        else
            updateLockedState(inputDelta);
    }

    private void updateSpinState(Vector2 inputDelta)
    {
        Vector3 currentCursorPosition = Attempt.IsReplay
            ? new Vector3(inputDelta.X, inputDelta.Y, 0)
            : new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0);

        if (Attempt.IsReplay)
        {
            runner.Cursor.Position = currentCursorPosition;
            Attempt.CursorPosition = inputDelta;
            Attempt.RawCursorPosition = inputDelta;

            camera.Rotation = new Vector3(
                Mathf.Clamp(
                    inputDelta.Y / Mathf.Pi, Mathf.DegToRad(-90),
                    Mathf.DegToRad(90)),
                -inputDelta.X / Mathf.Pi,
                0
            );

            Vector3 origin = new Vector3(0, 0, 3.5f);
            // The pivot is to mimic ROBLOX' orbital camera
            Vector3 pivot = camera.Basis.Z / 4f;

            camera.Position = origin + currentCursorPosition * Attempt.Settings.CameraParallax.Value + pivot;
            runner.Cursor.Position = new Vector3(inputDelta.X, inputDelta.Y, 0);
        }
        else
        {
            camera.Rotation += new Vector3(-inputDelta.Y / 120 * sensitivity / (float)Math.PI, -inputDelta.X / 120 * sensitivity / (float)Math.PI, 0);
            camera.Rotation = new Vector3(Math.Clamp(runner.Camera.Rotation.X, Mathf.DegToRad(-90), Mathf.DegToRad(90)), runner.Camera.Rotation.Y, runner.Camera.Rotation.Z);

            Vector3 origin = new Vector3(0,0,3.5f);
            // The pivot is to mimic ROBLOX' orbital camera
            Vector3 pivot = runner.Camera.Basis.Z / 4f;

            camera.Position = origin + currentCursorPosition * Attempt.Settings.CameraParallax + pivot;

            Vector3 lookVector = runner.Camera.Basis.Z;
            Vector2 cameraVec2 = new Vector2(runner.Camera.Position.X, runner.Camera.Position.Y);
            Vector2 lookVec2 = new Vector2(lookVector.X, lookVector.Y);

            Attempt.RawCursorPosition = cameraVec2 - lookVec2 * Mathf.Abs(runner.Camera.Position.Z / lookVector.Z);
            Attempt.CursorPosition = Attempt.RawCursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
            cursor.Position = new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0);
        }
    }

    private void updateLockedState(Vector2 inputDelta)
    {
        Vector2 delta = new Vector2(1, -1) * (inputDelta * sensitivity / 120f);
        Vector3 currentCursorPosition = new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0);

        if (Attempt.Settings.CursorDrift)
        {
            Attempt.CursorPosition = Attempt.IsReplay
                ? replayManager.CursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS)
                : (Attempt.CursorPosition + delta).Clamp(-Constants.BOUNDS, Constants.BOUNDS);
        }
        else
        {
            Attempt.RawCursorPosition = Attempt.IsReplay
                ? replayManager.CursorPosition
                : Attempt.RawCursorPosition + delta;
            Attempt.CursorPosition = Attempt.RawCursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
        }

        cursor.Position = currentCursorPosition;
        camera.Position = new Vector3(0, 0, 3.75f) + currentCursorPosition * Attempt.Settings.CameraParallax.Value;
        camera.Rotation = Vector3.Zero;
    }

    /// Reset everything to zero so it doesn't spin endlessly, or have infinite sensitivity
    private void updateAbsoluteInput()
    {
        camera.Rotation = Vector3.Zero;
        Attempt.RawCursorPosition = Vector2.Zero;
        Attempt.CursorPosition = Vector2.Zero;
    }
    private void updateCursorRotation(double delta) => cursor.RotationDegrees += Vector3.Back * settings.CursorRotation * (float)delta;
    private void assignSettings() => settings = SettingsManager.Instance.Settings;
}
