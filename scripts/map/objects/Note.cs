using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

public partial class Note : HitObject, IAnimatableObject<NoteAnimation>
{
    public int ObjectID { get; } = 0;     // map object type id
    public Tween CurrentTween { get; set; }
    public List<NoteAnimation> AnimationObjects { get; set; }
    public float Transparency { get; set; } = 1;
    
    public Note(int index, int millisecond, float x, float y) : base(index, millisecond, x, y)
    {

    }

    public int CompareTo(Note other)
    {
        return Millisecond.CompareTo(other.Millisecond);
    }
}
