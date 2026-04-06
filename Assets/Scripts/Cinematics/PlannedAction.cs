using UnityEngine;

[System.Serializable]
public class PlannedAction
{
    public string animationState;
    public Transform moveTarget;
    public Transform lookTarget;

    public bool useRootMotion;
    public float duration;
}