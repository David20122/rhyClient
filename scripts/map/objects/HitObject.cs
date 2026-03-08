using Godot;
using System;
using System.Net;

public enum HitState
{
    UNKNOWN,    
    HOLD,
    HIT,
    MISS
}

public partial class HitObject : GodotObject, IHitObject, IComparable<ITimelineObject>
{
    public int Id => (int)ObjectType.Unknown;

    // IHitObject impl
    public float X { get; set; }
    public float Y { get; set; }
    public HitState HitState { get; set; }

    public bool Hittable { get; set; } = false;

    public int Index;
    public int Millisecond { get; set; }

    public HitObject(int index, int millisecond, float x, float y)
    {
        Index = index;
        Millisecond = millisecond;
        X = x;
        Y = y;
    }

    public int CompareTo(ITimelineObject other)
    {
        return Millisecond.CompareTo(other.Millisecond);
    }

    public void Hit(Attempt attempt)
    {
        if (HitState != HitState.UNKNOWN) return;
        HitState = HitState.HIT;
        attempt.EmitSignal(Attempt.SignalName.HitStateChanged, this, (int)HitState);
        SoundManager.HitSound.Play();
    }

    public void Miss(Attempt attempt)
    {
        if (HitState != HitState.UNKNOWN) return;
        HitState = HitState.MISS;
        attempt.EmitSignal(Attempt.SignalName.HitStateChanged, this, (int)HitState);
        SoundManager.MissSound.Play();
    }

    public void CheckHit(Attempt attempt, HitObject hitObject)
    {
        if (hitObject.HitState == HitState.HIT) return;
        /*
        float noteSize = (float)(attempt.IsReplay ? attempt.Replays[0].NoteSize : attempt.Settings.NoteSize.Value);
        Vector2 notePos = new Vector2(hitObject.X, hitObject.Y);
        Rect2 noteHitbox = new Rect2(notePos - new Vector2(noteSize / 2, noteSize / 2), new Vector2(noteSize, noteSize));

        if (noteHitbox.HasPoint(attempt.CursorPosition))
        {
            hitObject.Hit(attempt);
        }*/
        if (attempt.CursorPosition.X + Constants.HIT_BOX_SIZE >= hitObject.X - 0.5f &&
            attempt.CursorPosition.X - Constants.HIT_BOX_SIZE <= hitObject.X + 0.5f &&
            attempt.CursorPosition.Y + Constants.HIT_BOX_SIZE >= hitObject.Y - 0.5f &&
            attempt.CursorPosition.Y - Constants.HIT_BOX_SIZE <= hitObject.Y + 0.5f)
		{
			hitObject.Hit(attempt);
		}
    }
}