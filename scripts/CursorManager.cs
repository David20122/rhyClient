using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Class containing all gameplay related logic regarding the cursor.
/// </summary>
public partial class CursorManager : Node
{
    // fml
    public static Attempt Attempt;

    [Export] private PlayerInputController playerInputController;
    [Export] private ReplayManager replayManager;
    [Export] private Runner runner;
    [Export] private MeshInstance3D cursor;
    [Export] private MultiMeshInstance3D cursorTrail;
    [Export] private Camera3D camera;

    private SettingsProfile settings;
    private float sensitivity;

    // Trails
    // I did my refresh rate but this can change at will
    private const float trail_spawn_rate = 240;
    private const float trail_min_detail = 0;
    private const float trail_max_detail = 100f;
    private double trailDeltaAccumulator;
    private List<CursorTrailData> activeTrailsData = [];

    public override void _Ready()
    {
        base._Ready();

        runner ??= GetNode<Runner>("Runner");
        cursor ??= GetNode<MeshInstance3D>("Cursor");
        camera ??= GetNode<Camera3D>("Camera3D");
        playerInputController ??= GetNode<PlayerInputController>("/PlayerInputController");
        replayManager ??= GetNode<ReplayManager>("ReplayManager");

        // wait for settings reference to be assigned
        CallDeferred(nameof(assignSettings));
    }

    public override void _Process(double delta)
    {
        if (!runner.Playing) return;

        rotateCursor(delta);
        if (Attempt.Settings.CursorTrail)
            updateCursorTrail(delta);
    }

    public override void _ExitTree()
    {
        //cleanup so meshes/data doesn't stay after scene reload
        if (activeTrailsData.Count > 0)
            activeTrailsData.Clear();
        cursorTrail.Multimesh.InstanceCount = 0;
    }

    public void UpdateCursor(Vector2 inputDelta)
    {
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
        Vector3 origin = new Vector3(0,0,3.5f);
        // The pivot is to mimic ROBLOX' orbital camera
        Vector3 pivot = runner.Camera.Basis.Z / 4f;

        if (Attempt.IsReplay)
        {
            runner.Cursor.Position = currentCursorPosition;
            Attempt.CursorPosition = inputDelta;
            Attempt.RawCursorPosition = inputDelta;

            camera.Rotation = new Vector3(
                Mathf.Clamp(
                    inputDelta.Y / Mathf.Pi,
                    Mathf.DegToRad(-90),
                    Mathf.DegToRad(90)),
                -inputDelta.X / Mathf.Pi,
                0
            );

            camera.Position = origin + currentCursorPosition * Attempt.Settings.CameraParallax.Value + pivot;
            runner.Cursor.Position = new Vector3(inputDelta.X, inputDelta.Y, 0);
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
            camera.Position = origin + currentCursorPosition * Attempt.Settings.CameraParallax + pivot;

            Vector3 cameraForward3D = runner.Camera.Basis.Z;
            Vector2 cameraPosition2D = new Vector2(runner.Camera.Position.X, runner.Camera.Position.Y);
            Vector2 cameraForward2D = new Vector2(cameraForward3D.X, cameraForward3D.Y);

            Attempt.RawCursorPosition = cameraPosition2D - cameraForward2D * Mathf.Abs(runner.Camera.Position.Z / cameraForward3D.Z);
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

    private void updateCursorTrail(double delta)
    {
        ulong now = Time.GetTicksUsec();

        processTrailSpawning(delta, now);
        cullExpiredTrails(now);
        updateTrailRendering(now);
    }

    private void processTrailSpawning(double delta, ulong now)
    {
        float trailDetail = Mathf.Clamp(settings.TrailDetail.Value, trail_min_detail, trail_max_detail);
        float wantedEmission = trailDetail / trail_max_detail;
        float rate = wantedEmission * trail_spawn_rate;

        if (rate <= 0f)
            return;

        double interval = 1.0 / rate;
        trailDeltaAccumulator += delta;
        int steps = (int)(trailDeltaAccumulator / interval);

        if (steps <= 0)
            return;

        trailDeltaAccumulator -= steps * interval;

        activeTrailsData.Add(new CursorTrailData(
            time: now,
            position: Attempt.CursorPosition,
            rotation: cursor.Rotation.Z
        ));
    }

    private void cullExpiredTrails(ulong now)
    {
        ulong maxLifeTime = (ulong)(settings.TrailTime.Value * 1_000_000);
        for (int i = activeTrailsData.Count - 1; i >= 0; i--)
        {
            CursorTrailData trail = activeTrailsData[i];
            ulong age = now - trail.Time;

            if (age >= maxLifeTime)
            {
                activeTrailsData.RemoveAt(i);
            }
        }
    }

    private void updateTrailRendering(ulong now)
    {
        float size = ((Vector2)cursor.Mesh.Get("size")).X;
        cursorTrail.Multimesh.InstanceCount = activeTrailsData.Count;

        for (int j = 0; j < activeTrailsData.Count; j++)
        {
            CursorTrailData trail = activeTrailsData[j];

            // if same position as mouse skip rendering trail
            if (trail.Position.DistanceTo(Attempt.CursorPosition) == 0)
                continue;

            Transform3D transform = Transform3D.Identity
                .Scaled(new Vector3(size, size, size))
                .Rotated(Vector3.Back, trail.Rotation);
            transform.Origin = new Vector3(trail.Position.X, trail.Position.Y, 0);

            // calculate trail's transparency
            //1. find how long trail exists
            //2. find amount of steps till it fades
            //3. lerp from 1 (fully opaque) to 0 (fully transparent) with interpolated steps
            float elapsed = (now - trail.Time) / 1_000_000f;
            float step = Math.Clamp(elapsed / settings.TrailTime.Value, 0f, 1f);
            float alpha = Mathf.Lerp(1, 0, step);
            cursorTrail.Multimesh.SetInstanceTransform(j, transform);
            cursorTrail.Multimesh.SetInstanceColor(j, new Color(1, 1, 1, alpha));
        }
    }

    // Reset everything to zero so it doesn't spin endlessly, or have infinite sensitivity
    private void updateAbsoluteInput()
    {
        camera.Rotation = Vector3.Zero;
        Attempt.RawCursorPosition = Vector2.Zero;
        Attempt.CursorPosition = Vector2.Zero;
    }
    private void updateCursorRotation(double delta) => cursor.RotationDegrees += Vector3.Back * settings.CursorRotation * (float)delta;
    private void assignSettings() => settings = SettingsManager.Instance.Settings;
    private void rotateCursor(double delta) => cursor.RotationDegrees += Vector3.Back * settings.CursorRotation * (float)delta;
}
