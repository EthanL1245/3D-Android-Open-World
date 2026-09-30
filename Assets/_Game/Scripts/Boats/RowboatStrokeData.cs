using System;
using UnityEngine;

// Baked directly from the supplied rigid-mesh Blender actions, not procedural rowing.
public sealed class RowboatStrokeData : ScriptableObject
{
    public float SamplesPerSecond=96f;
    public float Duration=1.5f;
    public Track[] Tracks;
    [Serializable] public sealed class Track
    {
        public Vector3[] Positions;
        public Quaternion[] Rotations;
    }
}
