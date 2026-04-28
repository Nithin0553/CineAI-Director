using UnityEngine;

[System.Serializable]
public class PlannedShot
{
    public string shotType;
    public string movementType;

    public Transform followTarget;
    public Transform lookTarget;

    public Vector3 offset;
    public float fov;
    public float duration;

    public float orbitSpeedOverride;
    public float dollySpeedOverride;
    public float panSpeedOverride;

    public bool useExactPosition;
    public Vector3 exactPosition;

    // AI-generated offsets should usually be applied in world space.
    // This prevents head/foot bone rotations from twisting the camera into grass or sky.
    public bool useWorldOffset = true;
}