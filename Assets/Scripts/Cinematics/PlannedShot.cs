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

    // Motion speed overrides (0 = use extension defaults)
    public float orbitSpeedOverride;
    public float dollySpeedOverride;
    public float panSpeedOverride;

    // When true, the compiler places the VCam at exactPosition instead
    // of followTarget.position + offset.
    public bool useExactPosition;
    public Vector3 exactPosition;
}