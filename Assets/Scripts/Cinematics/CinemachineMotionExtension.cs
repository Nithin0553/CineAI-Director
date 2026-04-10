using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine Extension that drives orbit / dolly / pan / follow motion
/// entirely INSIDE Cinemachine's own update pipeline.
///
/// WHY THIS REPLACES CameraMotionController:
///   The old MonoBehaviour set transform.position in Update(), but Cinemachine
///   also sets the VCam's position every frame in its own LateUpdate pass —
///   and Cinemachine always wins, so all movement was silently discarded.
///
///   CinemachineExtensions run via PostPipelineStageCallback(), which is
///   called by Cinemachine itself after it finishes its own positioning.
///   We modify the final StateOutput here, so nothing overwrites our changes.
/// </summary>
[AddComponentMenu("Cinemachine/Extensions/Motion Extension")]
public class CinemachineMotionExtension : CinemachineExtension
{
    public enum MotionType { Static, Orbit, DollyIn, DollyOut, Follow, Pan }

    [Header("Motion Settings")]
    public MotionType motionType = MotionType.Static;
    public Transform target;
    public Vector3 initialOffset;

    [Header("Speeds")]
    public float orbitSpeed = 20f;   // degrees / sec
    public float dollySpeed = 1.0f;  // units / sec (lerp speed)
    public float panSpeed = 15f;   // degrees / sec

    // Runtime state
    private float _orbitAngle = 0f;
    private Vector3 _currentOffset;
    private bool _initialized = false;

    // Called once by Cinemachine when the extension is first used
    protected new void OnEnable()
    {
        base.OnEnable();
        _currentOffset = initialOffset;
        _orbitAngle = Mathf.Atan2(initialOffset.x, initialOffset.z) * Mathf.Rad2Deg;
        _initialized = true;
    }

    /// <summary>
    /// Cinemachine calls this after every pipeline stage.
    /// We hook into the Finalize stage so we get the very last word on position.
    /// </summary>
    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        // Only act at the Finalize stage (after Cinemachine finished all its own work)
        if (stage != CinemachineCore.Stage.Finalize) return;
        if (target == null || !_initialized) return;
        if (deltaTime <= 0) return;   // editor scrubbing — don't move

        switch (motionType)
        {
            case MotionType.Orbit: DoOrbit(ref state, deltaTime); break;
            case MotionType.DollyIn: DoDolly(ref state, deltaTime, shrink: true); break;
            case MotionType.DollyOut: DoDolly(ref state, deltaTime, shrink: false); break;
            case MotionType.Follow: DoFollow(ref state, deltaTime); break;
            case MotionType.Pan: DoPan(ref state, deltaTime); break;
            case MotionType.Static: DoStatic(ref state); break;
        }
    }

    // ── ORBIT ────────────────────────────────────────────────────────
    // Rotates around the target on the XZ plane, maintaining Y height.
    void DoOrbit(ref CameraState state, float deltaTime)
    {
        _orbitAngle += orbitSpeed * deltaTime;

        float radius = new Vector3(initialOffset.x, 0, initialOffset.z).magnitude;
        float rad = _orbitAngle * Mathf.Deg2Rad;

        Vector3 orbitPos = target.position + new Vector3(
            Mathf.Sin(rad) * radius,
            initialOffset.y,
            Mathf.Cos(rad) * radius
        );

        state.RawPosition = orbitPos;
        state.RawOrientation = Quaternion.LookRotation(target.position - orbitPos, Vector3.up);
    }

    // ── DOLLY IN / OUT ───────────────────────────────────────────────
    // Smoothly moves camera closer (dolly in) or further (dolly out).
    void DoDolly(ref CameraState state, float deltaTime, bool shrink)
    {
        Vector3 targetOffset = shrink
            ? initialOffset * 0.5f   // dolly in → move to 50% of original offset
            : initialOffset * 2.2f;  // dolly out → pull back to 220%

        _currentOffset = Vector3.Lerp(_currentOffset, targetOffset, deltaTime * dollySpeed);

        Vector3 newPos = target.position + _currentOffset;
        state.RawPosition = newPos;
        state.RawOrientation = Quaternion.LookRotation(target.position - newPos, Vector3.up);
    }

    // ── FOLLOW ───────────────────────────────────────────────────────
    // Smoothly tracks the target maintaining the initial offset.
    void DoFollow(ref CameraState state, float deltaTime)
    {
        Vector3 desired = target.position + initialOffset;
        state.RawPosition = Vector3.Lerp(state.RawPosition, desired, deltaTime * 3f);
        state.RawOrientation = Quaternion.LookRotation(target.position - state.RawPosition, Vector3.up);
    }

    // ── PAN ──────────────────────────────────────────────────────────
    // Rotates camera around target's Y axis (yaw), staying at current distance.
    void DoPan(ref CameraState state, float deltaTime)
    {
        _orbitAngle += panSpeed * deltaTime;

        float radius = new Vector3(initialOffset.x, 0, initialOffset.z).magnitude;
        float rad = _orbitAngle * Mathf.Deg2Rad;

        Vector3 panPos = target.position + new Vector3(
            Mathf.Sin(rad) * radius,
            initialOffset.y,
            Mathf.Cos(rad) * radius
        );

        state.RawPosition = panPos;
        state.RawOrientation = Quaternion.LookRotation(target.position - panPos, Vector3.up);
    }

    // ── STATIC ───────────────────────────────────────────────────────
    // Holds position, just keeps looking at target.
    void DoStatic(ref CameraState state)
    {
        state.RawOrientation = Quaternion.LookRotation(
            target.position - state.RawPosition, Vector3.up);
    }
}