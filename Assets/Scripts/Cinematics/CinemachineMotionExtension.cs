using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine Extension that drives orbit / dolly / pan / follow motion
/// entirely INSIDE Cinemachine's own update pipeline.
/// Speed values can be overridden per-shot via the beat script.
/// </summary>
[AddComponentMenu("Cinemachine/Extensions/Motion Extension")]
public class CinemachineMotionExtension : CinemachineExtension
{
    public enum MotionType { Static, Orbit, DollyIn, DollyOut, Follow, Pan }

    [Header("Motion Settings")]
    public MotionType motionType = MotionType.Static;
    public Transform target;
    public Vector3 initialOffset;

    // FIX: separate look-at transform. The follow/motion target (e.g. the
    // character whose shoulder we orbit) is often *not* the thing the
    // camera should aim at (e.g. the rock being revealed). When lookTarget
    // is null the extension falls back to `target` so behaviour is
    // unchanged for shots that don't need a split.
    [Tooltip("Optional look-at transform. Falls back to 'target' when null.")]
    public Transform lookTarget;

    // FIX: explicit orbit radius — set by CutsceneCompiler for aerial shots
    // so DoOrbit() doesn't have to derive it from XZ offset (which can be 0).
    [Header("Orbit Settings")]
    public float orbitRadius = 15f;   // world-unit radius of the circle
    public Vector3 worldAnchor;       // exact world position to orbit around
    public bool useWorldAnchor = false; // when true, orbit around worldAnchor
                                        // instead of target.position

    [Header("Speeds  (0 = use defaults)")]
    public float orbitSpeed = 20f;    // degrees / sec
    public float dollySpeed = 1.0f;   // lerp speed
    public float panSpeed = 15f;     // degrees / sec

    // FIX (Beat 4): Explicit pan radius — set by CutsceneCompiler so DoPan
    // doesn't derive radius from XZ offset magnitude (which is tiny for
    // behind-shoulder shots where offset.x=0, offset.z=-1).
    [Header("Pan Settings")]
    public float panRadius = 3f;  // world-unit radius of the pan arc

    // Runtime state
    private float _orbitAngle = 0f;
    private Vector3 _currentOffset;
    private bool _initialized = false;
    private bool _dollySeeded = false;  // FIX (Beat 5): seed _currentOffset from real cam pos on frame 1

    protected new void OnEnable()
    {
        base.OnEnable();
        _currentOffset = initialOffset;

        // FIX: When the orbit pivots around a worldAnchor (e.g. an aerial
        // orbit around a ROCK that the camera was placed around at an
        // arbitrary world position), seed _orbitAngle from the camera's
        // *actual* position relative to the anchor. The previous formula
        // used initialOffset.x/z which is (0, h, 0) for aerial shots, so
        // the camera always snapped due-north of the anchor on frame 1.
        if (useWorldAnchor)
        {
            Vector3 toCam = transform.position - worldAnchor;
            _orbitAngle = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
        }
        else
        {
            _orbitAngle = Mathf.Atan2(initialOffset.x, initialOffset.z) * Mathf.Rad2Deg;
        }

        _initialized = true;
        _dollySeeded = false;
    }

    // Returns the transform the camera should aim at: explicit lookTarget
    // when set, otherwise the motion target.
    private Transform GetLookAt()
    {
        return lookTarget != null ? lookTarget : target;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;
        if (!_initialized) return;
        if (deltaTime <= 0) return;

        // Allow orbit/static to work even without a target if worldAnchor
        // or lookTarget is set.
        if (target == null && !useWorldAnchor && lookTarget == null) return;

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

    void DoOrbit(ref CameraState state, float deltaTime)
    {
        float speed = orbitSpeed > 0 ? orbitSpeed : 20f;
        _orbitAngle += speed * deltaTime;

        // FIX: Use explicit orbitRadius instead of deriving from XZ offset.
        // Previously radius = XZ magnitude of initialOffset, which was 0
        // for aerial shots where offset is purely (0, height, 0).
        float radius = orbitRadius > 0 ? orbitRadius : 15f;
        float rad = _orbitAngle * Mathf.Deg2Rad;

        // FIX: Use worldAnchor when set so aerial shots orbit the exact
        // world position of the rock regardless of target transform offset.
        Vector3 center = useWorldAnchor ? worldAnchor :
                         (target != null ? target.position : worldAnchor);

        Vector3 orbitPos = center + new Vector3(
            Mathf.Sin(rad) * radius,
            initialOffset.y,
            Mathf.Cos(rad) * radius);

        state.RawPosition = orbitPos;

        // FIX: aim at the explicit look target when one is provided so
        // an OTS / reveal orbit can pivot around the character but look
        // at the focus (e.g. the rock).
        Transform lookAt = GetLookAt();
        Vector3 aimPoint = lookAt != null ? lookAt.position : center;
        state.RawOrientation = Quaternion.LookRotation(aimPoint - orbitPos, Vector3.up);
    }

    void DoDolly(ref CameraState state, float deltaTime, bool shrink)
    {
        if (target == null) return;
        float speed = dollySpeed > 0 ? dollySpeed : 1.0f;

        // FIX (Beat 5): On the very first frame seed _currentOffset from the
        // camera's ACTUAL world position relative to the target, not from
        // initialOffset. The compiler places the camera at
        // target.position + facingRot * shot.offset (world-space), but
        // initialOffset is also that world-space value — however state.RawPosition
        // on frame 1 already holds the correct spawn position, so reading it
        // here gives a perfect no-jump start, and we dolly smoothly from there.
        if (!_dollySeeded)
        {
            _currentOffset = state.RawPosition - target.position;
            _dollySeeded = true;
        }

        Vector3 targetOffset = shrink
            ? initialOffset * 0.5f
            : initialOffset * 2.2f;

        _currentOffset = Vector3.Lerp(_currentOffset, targetOffset, deltaTime * speed);

        Vector3 newPos = target.position + _currentOffset;
        state.RawPosition = newPos;

        Transform lookAt = GetLookAt();
        Vector3 aimPoint = lookAt != null ? lookAt.position : target.position;
        state.RawOrientation = Quaternion.LookRotation(aimPoint - newPos, Vector3.up);
    }

    void DoFollow(ref CameraState state, float deltaTime)
    {
        if (target == null) return;

        // FIX (Beat 2): Snap the camera rigidly to the follow offset every frame.
        // The previous Lerp caused visible lag on tight follow shots (close-up feet).
        // Smoothing for a follow shot should come from the character's movement
        // curve, not from camera lag. This makes the feet shot frame-accurate.
        Vector3 desired = target.position + initialOffset;
        state.RawPosition = desired;

        Transform lookAt = GetLookAt();
        Vector3 aimPoint = lookAt != null ? lookAt.position : target.position;
        state.RawOrientation = Quaternion.LookRotation(aimPoint - desired, Vector3.up);
    }

    void DoPan(ref CameraState state, float deltaTime)
    {
        if (target == null) return;
        float speed = panSpeed > 0 ? panSpeed : 15f;
        _orbitAngle += speed * deltaTime;

        // FIX (Beat 4): Use the explicit panRadius set by the compiler instead
        // of deriving it from the XZ magnitude of initialOffset. For OTS shots
        // the offset is (0, 1.75, -1) giving a XZ radius of only ~1 unit —
        // the camera barely moves. The compiler sets panRadius to the actual
        // horizontal distance from camera spawn to character, which produces a
        // natural shoulder-level arc that sweeps across the scene.
        float radius = panRadius > 0.01f ? panRadius : new Vector3(initialOffset.x, 0, initialOffset.z).magnitude;
        if (radius < 0.01f) radius = 1f;
        float rad = _orbitAngle * Mathf.Deg2Rad;

        Vector3 panPos = target.position + new Vector3(
            Mathf.Sin(rad) * radius,
            initialOffset.y,
            Mathf.Cos(rad) * radius);

        state.RawPosition = panPos;

        // FIX: Pan/OTS reveal — aim at lookTarget when set so a pan around
        // a character (target = UNCLE_BEN) can reveal a different focus
        // (lookTarget = ROCK). Previously the camera always looked at the
        // pan pivot, which kept Ben centered and never revealed the rock.
        Transform lookAt = GetLookAt();
        Vector3 aimPoint = lookAt != null ? lookAt.position : target.position;
        state.RawOrientation = Quaternion.LookRotation(aimPoint - panPos, Vector3.up);
    }

    void DoStatic(ref CameraState state)
    {
        Transform lookAt = GetLookAt();
        if (lookAt == null) return;
        state.RawOrientation = Quaternion.LookRotation(
            lookAt.position - state.RawPosition, Vector3.up);
    }
}