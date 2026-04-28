using UnityEngine;
using System;

/// <summary>
/// Stores one movement instruction for a character during cutscene playback.
/// Created at compile time by CutsceneCompiler, executed at runtime by CutsceneCharacterMover.
/// </summary>
[Serializable]
public class CharacterMoveData
{
    public string characterName;   // must match GameObject.name in scene
    public float startTime;       // seconds into the timeline
    public float duration;        // how long the move takes
    public Vector3 startPosition;
    public Vector3 endPosition;
    public float facingY;         // euler Y to face during move (-1 = auto face direction)
    public bool shouldMove;      // false = just snap + face, no translation
}