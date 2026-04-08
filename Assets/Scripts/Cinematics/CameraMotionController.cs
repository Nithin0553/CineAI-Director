using UnityEngine;

public class CameraMotionController : MonoBehaviour
{
    private Transform target;
    private string movementType;

    private Transform orbitPivot;
    private Vector3 offset;

    private float orbitSpeed = 20f;
    private float dollySpeed = 2f;

    // INIT
    public void Initialize(Transform followTarget, string movement, Vector3 initialOffset)
    {
        target = followTarget;
        movementType = movement;
        offset = initialOffset;

        if (movementType == "orbit" && target != null)
        {
            GameObject pivot = new GameObject("OrbitPivot");
            pivot.transform.position = target.position;

            orbitPivot = pivot.transform;

            transform.SetParent(orbitPivot);

            // KEEP ORIGINAL OFFSET
            transform.localPosition = offset;
        }
    }

    void Update()
    {
        if (target == null) return;

        switch (movementType)
        {
            case "orbit":
                Orbit();
                break;

            case "dolly_in":
                DollyIn();
                break;

            case "dolly_out":
                DollyOut();
                break;

            case "follow":
                Follow();
                break;
        }
    }

    // 🎯 FIXED ORBIT (AUTO, NOT INPUT)
    void Orbit()
    {
        if (orbitPivot == null) return;

        orbitPivot.position = target.position;

        orbitPivot.Rotate(Vector3.up * orbitSpeed * Time.deltaTime);

        transform.LookAt(target);
    }

    // 🎯 DOLLY IN
    void DollyIn()
    {
        Vector3 targetPos = target.position + offset * 0.3f;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * dollySpeed
        );

        transform.LookAt(target);
    }

    // 🎯 DOLLY OUT
    void DollyOut()
    {
        Vector3 targetPos = target.position + offset * 2f;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * dollySpeed
        );

        transform.LookAt(target);
    }

    // 🎯 FOLLOW (SMOOTH)
    void Follow()
    {
        Vector3 targetPos = target.position + offset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * 3f
        );

        transform.LookAt(target);
    }
}