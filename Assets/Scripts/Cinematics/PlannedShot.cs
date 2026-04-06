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
}