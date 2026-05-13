using System;
using System.Linq;
using Godot;

public partial class LegacyRenderer : MultiMeshInstance3D
{
    [Export] public Runner Runner;
    private SettingsProfile settings;

    public override void _Ready()
    {
        Runner ??= GetParent<Runner>();
        settings = SettingsManager.Instance.Settings;
    }

    public override void _Process(double delta)
    {
//         if (!LegacyRunner.Playing || LegacyRunner.CurrentAttempt.Stopped)
        if (!Runner.Playing)
        {
            return;
        }

        Multimesh.InstanceCount = Runner.ToProcess;

        float ar = (float)(Runner.Attempt.IsReplay ? Runner.Attempt.Replays[0].ApproachRate : settings.ApproachRate.Value);
        float ad = (float)(Runner.Attempt.IsReplay ? Runner.Attempt.Replays[0].ApproachDistance : settings.ApproachDistance.Value);
        float at = ad / ar;
        float fadeIn = (float)(Runner.Attempt.IsReplay ? Runner.Attempt.Replays[0].FadeIn : settings.FadeIn.Value);
        bool fadeOut = Runner.Attempt.IsReplay ? Runner.Attempt.Replays[0].FadeOut : settings.FadeOut.Value;
        bool pushback = Runner.Attempt.IsReplay ? Runner.Attempt.Replays[0].Pushback : settings.Pushback.Value;
        float hitWindowDepth = pushback ? (float)Constants.HIT_WINDOW * ar / 1000 : 0;
        float noteSize = (float)(Runner.Attempt.IsReplay ? Runner.Attempt.Replays[0].NoteSize : settings.NoteSize.Value) / 4;
        
        // will fix this after everything works and is merged
        
//         if (LegacyRunner.ToProcess == 0)
//         {
//             return;
//         }

//         float ar = (float)(LegacyRunner.CurrentAttempt.IsReplay ? LegacyRunner.CurrentAttempt.Replays[0].ApproachRate : settings.ApproachRate.Value);
//         float ad = (float)(LegacyRunner.CurrentAttempt.IsReplay ? LegacyRunner.CurrentAttempt.Replays[0].ApproachDistance : settings.ApproachDistance.Value);
//         float at = ad / ar;
//         float fadeIn = (float)(LegacyRunner.CurrentAttempt.IsReplay ? LegacyRunner.CurrentAttempt.Replays[0].FadeIn : settings.FadeIn.Value) / 100;
//         float fadeOut = (float)(LegacyRunner.CurrentAttempt.IsReplay ? (LegacyRunner.CurrentAttempt.Replays[0].FadeOut ? 100 : 0) : settings.FadeOut.Value) / 100;
//         bool pushback = LegacyRunner.CurrentAttempt.IsReplay ? LegacyRunner.CurrentAttempt.Replays[0].Pushback : settings.Pushback.Value;
//         float hitWindowDepth = pushback ? (float)Constants.HIT_WINDOW * ar / 1000 : 0;
//         float noteOpacity = (float)settings.NoteOpacity;
//         float noteOpacityExponent = (float)settings.NoteOpacityExponent;

//         if (noteOpacity > 1)
//         {
//             // Backward compatibility: older profiles may have stored opacity in a 0-100 range.
//             noteOpacity /= 100f;
//         }

//         noteOpacity = Math.Clamp(noteOpacity, 0, 1);

//         float noteSize = (float)(LegacyRunner.CurrentAttempt.IsReplay ? LegacyRunner.CurrentAttempt.Replays[0].NoteSize : settings.NoteSize.Value) / 2;
        Transform3D transform = new(Vector3.Right * noteSize, Vector3.Up * noteSize, Vector3.Back * noteSize, Vector3.Zero);

        for (int i = 0; i < Runner.ToProcess; i++)
        {
            Note note = Runner.ProcessNotes[i];
            float depth = (note.Millisecond - (float)Runner.Attempt.Progress) / (1000 * at) * ad / (float)Runner.Attempt.Speed;
            float alpha = Math.Clamp((1 - (float)depth / ad) / (fadeIn / 100), 0, 1);
            
            // will also fix this later
            
//             Note note = LegacyRunner.ProcessNotes[i];
//             float depth = (note.Millisecond - (float)LegacyRunner.CurrentAttempt.Progress) / (1000 * at) * ad / (float)LegacyRunner.CurrentAttempt.Speed;
//             float progress = 1 - Math.Max(0, (depth + hitWindowDepth) / (ad + hitWindowDepth));
//             float alpha = 1;

//             if (fadeIn > 0)
//             {
//                 alpha = Math.Min(1, progress / fadeIn);
//             }

            if (Runner.Attempt.Mods["Ghost"])
            {
                alpha -= (ad - depth) / (ad / 2);
            }
            else if (fadeOut > 0)
            {
                // alpha -= (ad - depth) / (ad + (float)Constants.HIT_WINDOW * ar / 1000);

                // runner rewrite fork
                // alpha *= Math.Min(1, (depth + hitWindowDepth) / (ad + hitWindowDepth));
                
                // indev
                alpha -= 1 - Math.Min(1, (1 - progress) / fadeOut);
            }

            if (!pushback && note.Millisecond - Runner.Attempt.Progress <= 0)
            {
                alpha = 0;
            }

            int j = Runner.ToProcess - i - 1;
            Color color = SkinManager.Instance.Skin.NoteColors[note.Index % SkinManager.Instance.Skin.NoteColors.Length];

            transform.Origin = new Vector3(note.X, note.Y, -depth);
            color.A = Math.Clamp((float)Math.Pow(alpha * noteOpacity, noteOpacityExponent), 0, 1);
            Multimesh.SetInstanceTransform(j, transform);
            Multimesh.SetInstanceColor(j, color);
        }
    }
}
