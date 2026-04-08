using UnityEngine;

public class CameraMotionController : MonoBehaviour
{
    private Transform target;
    private string movementType;
    private Transform orbitPivot;

    private float orbitSpeed = 20f;
    private float dollySpeed = 2f;

    private Vector3 offset;

    public void Initialize(Transform followTarget, string movement, Vector3 camOffset)
    {
        target = followTarget;
        movementType = movement;
        offset = camOffset;

        if (target == null) return;

        if (movementType == "orbit")
        {
            GameObject pivot = new GameObject("OrbitPivot");

            Vector3 basePos = target.position;
            basePos.y += 2f; // 🔥 FIX HEIGHT

            pivot.transform.position = basePos;
            orbitPivot = pivot.transform;

            transform.SetParent(orbitPivot);
            transform.localPosition = offset;
            transform.localRotation = Quaternion.identity;
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

            case "pan":
                Pan();
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

    void Orbit()
    {
        if (orbitPivot == null) return;

        orbitPivot.Rotate(Vector3.up, orbitSpeed * Time.deltaTime);
        transform.LookAt(target);
    }

    void Pan()
    {
        transform.RotateAround(target.position, Vector3.up, orbitSpeed * 0.5f * Time.deltaTime);
        transform.LookAt(target);
    }

    void DollyIn()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            dollySpeed * Time.deltaTime
        );
        transform.LookAt(target);
    }

    void DollyOut()
    {
        Vector3 dir = (transform.position - target.position).normalized;
        transform.position += dir * dollySpeed * Time.deltaTime;
        transform.LookAt(target);
    }

    void Follow()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            target.position + offset,
            Time.deltaTime * 2f
        );
        transform.LookAt(target);
    }
}