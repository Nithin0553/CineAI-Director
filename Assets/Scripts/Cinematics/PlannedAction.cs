using UnityEngine;

[System.Serializable]
public class PlannedAction
{
    public string animationState;
    public Transform moveTarget;
    public Transform lookTarget;

    public bool useRootMotion;
    public float duration;

    // Explicit world-space positions (set by Beat overrides)
    public bool useExactStartPosition;
    public Vector3 exactStartPosition;

    public bool useExactEndPosition;
    public Vector3 exactEndPosition;

    public bool useExactFacing;
    public float exactFacingY;   // euler Y degrees
}