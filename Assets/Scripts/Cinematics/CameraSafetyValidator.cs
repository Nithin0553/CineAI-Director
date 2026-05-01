using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Universal camera safety validator.
///
/// This is not story-specific and not tied to Uncle Ben, feet, or the rock.
/// It validates any generated Cinemachine camera using scene geometry.
///
/// It fixes common procedural camera problems:
/// - Camera below terrain/ground
/// - Camera too close to target
/// - Camera looking too far upward into the sky
/// - Camera placed under the target
/// </summary>
public class CameraSafetyValidator : MonoBehaviour
{
    [Header("General Safety")]
    public bool enableSafety = true;
    public bool enableDebugLogs = true;

    [Header("Height Safety")]
    public float minimumWorldY = 0.2f;
    public float minimumHeightAboveTarget = -0.25f;
    public float minimumTerrainClearance = 0.35f;

    [Header("Distance Safety")]
    public float minimumDistanceFromTarget = 1.4f;
    public float maximumDistanceFromTarget = 25.0f;

    [Header("Pitch Safety")]
    public float maximumUpwardPitch = 25.0f;
    public float maximumDownwardPitch = 65.0f;

    [Header("Raycast")]
    public LayerMask terrainMask = ~0;
    public float terrainRaycastHeight = 100.0f;
    public float terrainRaycastDistance = 300.0f;

    public void ValidateCamera(CinemachineCamera cam)
    {
        if (!enableSafety || cam == null)
            return;

        Transform target = ResolveTarget(cam);

        if (target == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"⚠ CameraSafetyValidator: No target found for {cam.name}");

            return;
        }

        Vector3 originalPosition = cam.transform.position;
        Quaternion originalRotation = cam.transform.rotation;

        Vector3 safePosition = cam.transform.position;
        safePosition = EnforceDistanceSafety(safePosition, target.position);
        safePosition = EnforceHeightSafety(safePosition, target.position);
        safePosition = EnforceTerrainSafety(safePosition);

        cam.transform.position = safePosition;

        Quaternion safeRotation = ComputeSafeLookRotation(
            cameraPosition: safePosition,
            targetPosition: target.position,
            fallbackRotation: originalRotation
        );

        cam.transform.rotation = safeRotation;

        if (enableDebugLogs)
        {
            float movedDistance = Vector3.Distance(originalPosition, safePosition);

            if (movedDistance > 0.01f || Quaternion.Angle(originalRotation, safeRotation) > 0.5f)
            {
                Debug.Log(
                    $"🛡 Camera safety adjusted {cam.name}: " +
                    $"pos {originalPosition} → {safePosition}"
                );
            }
        }
    }

    public void ValidateAllGeneratedCameras()
    {
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);

        foreach (CinemachineCamera cam in cameras)
        {
            if (cam == null)
                continue;

            if (!cam.name.StartsWith("VCam_"))
                continue;

            ValidateCamera(cam);
        }
    }

    private Transform ResolveTarget(CinemachineCamera cam)
    {
        if (cam.Target.TrackingTarget != null)
            return cam.Target.TrackingTarget;

        if (cam.Target.LookAtTarget != null)
            return cam.Target.LookAtTarget;

        return null;
    }

    private Vector3 EnforceDistanceSafety(Vector3 cameraPosition, Vector3 targetPosition)
    {
        Vector3 fromTarget = cameraPosition - targetPosition;
        float distance = fromTarget.magnitude;

        if (distance < 0.001f)
            fromTarget = new Vector3(0.0f, 1.0f, -1.5f).normalized;

        if (distance < minimumDistanceFromTarget)
        {
            fromTarget = fromTarget.normalized * minimumDistanceFromTarget;
            cameraPosition = targetPosition + fromTarget;
        }

        if (distance > maximumDistanceFromTarget)
        {
            fromTarget = fromTarget.normalized * maximumDistanceFromTarget;
            cameraPosition = targetPosition + fromTarget;
        }

        return cameraPosition;
    }

    private Vector3 EnforceHeightSafety(Vector3 cameraPosition, Vector3 targetPosition)
    {
        float minYFromWorld = minimumWorldY;
        float minYFromTarget = targetPosition.y + minimumHeightAboveTarget;
        float safeY = Mathf.Max(cameraPosition.y, minYFromWorld, minYFromTarget);

        cameraPosition.y = safeY;
        return cameraPosition;
    }

    private Vector3 EnforceTerrainSafety(Vector3 cameraPosition)
    {
        Vector3 rayStart = new Vector3(
            cameraPosition.x,
            cameraPosition.y + terrainRaycastHeight,
            cameraPosition.z
        );

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, terrainRaycastDistance, terrainMask))
        {
            float terrainSafeY = hit.point.y + minimumTerrainClearance;

            if (cameraPosition.y < terrainSafeY)
                cameraPosition.y = terrainSafeY;
        }

        return cameraPosition;
    }

    private Quaternion ComputeSafeLookRotation(
        Vector3 cameraPosition,
        Vector3 targetPosition,
        Quaternion fallbackRotation)
    {
        Vector3 direction = targetPosition - cameraPosition;

        if (direction.sqrMagnitude < 0.0001f)
            return fallbackRotation;

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Vector3 euler = lookRotation.eulerAngles;

        float pitch = NormalizeAngle(euler.x);
        pitch = Mathf.Clamp(pitch, -maximumDownwardPitch, maximumUpwardPitch);

        return Quaternion.Euler(pitch, euler.y, 0.0f);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180.0f)
            angle -= 360.0f;

        while (angle < -180.0f)
            angle += 360.0f;

        return angle;
    }
}