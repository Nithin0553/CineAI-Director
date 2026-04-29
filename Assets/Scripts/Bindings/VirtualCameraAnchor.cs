using UnityEngine;

public class VirtualCameraAnchor : MonoBehaviour
{
    public Transform targetRoot;
    public string anchorType = "BODY";

    public float headHeight = 1.65f;
    public float feetHeight = 0.35f;
    public float bodyHeight = 1.15f;

    public float headForwardOffset = 0.0f;
    public float feetForwardOffset = 0.0f;

    private void LateUpdate()
    {
        UpdateAnchorNow();
    }

    public void UpdateAnchorNow()
    {
        if (targetRoot == null)
            return;

        Vector3 offset = Vector3.zero;

        switch (anchorType)
        {
            case "HEAD":
                offset = Vector3.up * headHeight + targetRoot.forward * headForwardOffset;
                break;

            case "FEET":
                offset = Vector3.up * feetHeight + targetRoot.forward * feetForwardOffset;
                break;

            case "BODY":
            default:
                offset = Vector3.up * bodyHeight;
                break;
        }

        transform.position = targetRoot.position + offset;

        // Keep only clean Y rotation from the character root.
        // Do not use Mixamo bone rotation because it can twist cameras.
        transform.rotation = Quaternion.Euler(0f, targetRoot.eulerAngles.y, 0f);
    }
}