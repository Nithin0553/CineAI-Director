using UnityEngine;

public class CameraMotionController : MonoBehaviour
{
    private Transform target;
    private string movementType;

    private Transform orbitPivot;

    private float orbitSpeed = 25f;
    private float dollySpeed = 2f;

    public void Initialize(Transform followTarget, string movement, Vector3 offset)
    {
        target = followTarget;
        movementType = movement;

        // 🔥 Create pivot for orbit
        if (movementType == "orbit" && target != null)
        {
            GameObject pivot = new GameObject("OrbitPivot");
            pivot.transform.position = target.position;

            orbitPivot = pivot.transform;

            transform.SetParent(orbitPivot);
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

    void Orbit()
    {
        if (orbitPivot == null) return;

        orbitPivot.Rotate(Vector3.up * orbitSpeed * Time.deltaTime);

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
            target.position,
            Time.deltaTime * 2f
        );

        transform.LookAt(target);
    }
}