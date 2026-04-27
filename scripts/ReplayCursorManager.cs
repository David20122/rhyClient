// using System;
// using Godot;
//
// public partial class ReplayCursorManager : Node
// {
//     [Export] private ReplayManager replayManager;
//     [Export] private Runner runner;
//     [Export] private MeshInstance3D cursor;
//     [Export] private Camera3D camera;
//     public static Attempt Attempt;
//     private SettingsProfile settings;
//
//     public override void _Ready()
//     {
//         base._Ready();
//
//         runner ??= GetNode<Runner>("root/SceneGame/Runner");
//         cursor ??= GetNode<MeshInstance3D>("root/SceneGame/Cursor");
//         camera ??= GetNode<Camera3D>("root/SceneGame/Camera3D");
//
//         replayManager.OnReplayNewFrame += updateCursor;
//
//         // wait for settings reference to be assigned
//         CallDeferred(nameof(assignSettings));
//     }
//
//     public override void _Process(double delta)
//     {
//         updateCursorRotation(delta);
//     }
//
//     private void updateCursor()
// 	{
//         updateAbsoluteInput();
//
//         if (!runner.SpinCamera)
//             updateLockedState();
//         else
//             updateSpinState();
// 	}
//
//     private void updateSpinState()
//     {
//
//     }
//
//     private void updateLockedState()
//     {
//         if (Attempt.Settings.CursorDrift)
//         {
//             Attempt.CursorPosition = replayManager.CursorPos.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
//         }
//         else
//         {
//             Attempt.RawCursorPosition = replayManager.CursorPos;
//             Attempt.CursorPosition = Attempt.RawCursorPosition.Clamp(-Constants.BOUNDS, Constants.BOUNDS);
//         }
//
//         runner.Cursor.Position = new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0);
//         runner.Camera.Position = new Vector3(0, 0, 3.75f) + new Vector3(Attempt.CursorPosition.X, Attempt.CursorPosition.Y, 0) * (float)Attempt.Replays[0].Parallax;
//         runner.Camera.Rotation = Vector3.Zero;
//     }
//
//     /// Reset everything to zero so it doesn't spin endlessly, or have infinite sensitivity
//     private void updateAbsoluteInput()
//     {
//         camera.Rotation = Vector3.Zero;
//         Attempt.RawCursorPosition = Vector2.Zero;
//         Attempt.CursorPosition = Vector2.Zero;
//     }
//     private void updateCursorRotation(double delta) => cursor.RotationDegrees += Vector3.Back * settings.CursorRotation * (float)delta;
//     private void assignSettings() => settings = SettingsManager.Instance.Settings;
// }
