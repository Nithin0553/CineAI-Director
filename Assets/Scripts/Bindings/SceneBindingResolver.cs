using UnityEngine;
using System.Linq;

public class SceneBindingResolver : MonoBehaviour
{
    [Header("Virtual Camera Anchor Settings")]
    [Tooltip("Approximate head height above character root.")]
    public float headAnchorHeight = 1.65f;

    [Tooltip("Approximate feet/step detail height above character root.")]
    public float feetAnchorHeight = 0.35f;

    [Tooltip("Approximate body/chest height above character root.")]
    public float bodyAnchorHeight = 1.15f;

    [Tooltip("Forward offset for face/head anchor. Keep 0 unless you need the anchor slightly forward.")]
    public float headForwardOffset = 0.0f;

    [Tooltip("Forward offset for feet anchor. Keep 0 unless you need the anchor slightly forward.")]
    public float feetForwardOffset = 0.0f;

    public Transform ResolveTarget(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        string lowerName = name.ToLower();

        if (lowerName.EndsWith("_feet") || lowerName == "feet")
        {
            string rootName = CleanSemanticSuffix(name, "_FEET", "_feet", "feet");
            Transform anchor = ResolveVirtualAnchor(rootName, "FEET");

            if (anchor != null)
            {
                Debug.Log("🦶 Resolved FEET camera anchor → " + anchor.name);
                return anchor;
            }

            Debug.LogWarning($"⚠️ Could not resolve FEET anchor for: {name}");
        }

        if (lowerName.EndsWith("_head") || lowerName == "head")
        {
            string rootName = CleanSemanticSuffix(name, "_HEAD", "_head", "head");
            Transform anchor = ResolveVirtualAnchor(rootName, "HEAD");

            if (anchor != null)
            {
                Debug.Log("🗣️ Resolved HEAD camera anchor → " + anchor.name);
                return anchor;
            }

            Debug.LogWarning($"⚠️ Could not resolve HEAD anchor for: {name}");
        }

        if (lowerName.EndsWith("_body") || lowerName == "body" || lowerName.EndsWith("_chest"))
        {
            string rootName = name
                .Replace("_BODY", "")
                .Replace("_body", "")
                .Replace("_CHEST", "")
                .Replace("_chest", "")
                .Replace("body", "")
                .Replace("chest", "")
                .Trim();

            Transform anchor = ResolveVirtualAnchor(rootName, "BODY");

            if (anchor != null)
            {
                Debug.Log("🎭 Resolved BODY camera anchor → " + anchor.name);
                return anchor;
            }

            Debug.LogWarning($"⚠️ Could not resolve BODY anchor for: {name}");
        }

        GameObject obj = GameObject.Find(name);

        if (obj != null)
        {
            Debug.Log($"✅ Found by EXACT name: {name}");
            return obj.transform;
        }

        try
        {
            GameObject tagged = GameObject.FindGameObjectWithTag(name);

            if (tagged != null)
            {
                Debug.Log($"✅ Found by TAG: {name}");
                return tagged.transform;
            }
        }
        catch
        {
            Debug.LogWarning($"⚠️ Tag not defined: {name}");
        }

        GameObject partial = FindPartial(name);

        if (partial != null)
        {
            Debug.Log($"⚠️ Found by PARTIAL match: {partial.name}");
            return partial.transform;
        }

        if (name.ToUpper() == "ENVIRONMENT")
        {
            GameObject env = GameObject.Find("ENVIRONMENT");

            if (env != null)
            {
                Debug.Log("🌍 Using ENVIRONMENT fallback");
                return env.transform;
            }
        }

        Debug.LogWarning($"❌ Could not resolve: '{name}' — check spelling and scene hierarchy.");
        return null;
    }

    public Transform ResolveSpeaker(string speaker)
    {
        return ResolveTarget(speaker);
    }

    public Transform ResolveFocus(string focus)
    {
        return ResolveTarget(focus);
    }

    private Transform ResolveVirtualAnchor(string rootName, string anchorType)
    {
        if (string.IsNullOrEmpty(rootName))
            rootName = "UNCLE_BEN";

        GameObject root = GameObject.Find(rootName);

        if (root == null)
        {
            GameObject partial = FindPartial(rootName);

            if (partial != null)
                root = partial;
        }

        if (root == null)
        {
            Debug.LogWarning($"❌ Could not find character root for anchor: {rootName}");
            return null;
        }

        string anchorName = root.name + "_" + anchorType + "_ANCHOR";

        GameObject existing = GameObject.Find(anchorName);

        if (existing != null)
        {
            VirtualCameraAnchor existingAnchor = existing.GetComponent<VirtualCameraAnchor>();

            if (existingAnchor == null)
            {
                Debug.LogWarning($"⚠️ Existing anchor has missing/invalid VirtualCameraAnchor script. Recreating: {anchorName}");
                DestroyImmediate(existing);
                existing = null;
            }
            else
            {
                ConfigureAnchor(existingAnchor, root.transform, anchorType);
                existingAnchor.UpdateAnchorNow();
                return existing.transform;
            }
        }

        GameObject anchorObj = new GameObject(anchorName);
        anchorObj.transform.SetParent(root.transform, false);

        VirtualCameraAnchor anchor = anchorObj.AddComponent<VirtualCameraAnchor>();
        ConfigureAnchor(anchor, root.transform, anchorType);
        anchor.UpdateAnchorNow();

        Debug.Log($"🎯 Created virtual camera anchor: {anchorName}");
        return anchorObj.transform;
    }

    private void ConfigureAnchor(VirtualCameraAnchor anchor, Transform root, string anchorType)
    {
        anchor.targetRoot = root;
        anchor.anchorType = anchorType;
        anchor.headHeight = headAnchorHeight;
        anchor.feetHeight = feetAnchorHeight;
        anchor.bodyHeight = bodyAnchorHeight;
        anchor.headForwardOffset = headForwardOffset;
        anchor.feetForwardOffset = feetForwardOffset;
    }

    private string CleanSemanticSuffix(string name, string upperSuffix, string lowerSuffix, string plainName)
    {
        string cleaned = name
            .Replace(upperSuffix, "")
            .Replace(lowerSuffix, "")
            .Replace(plainName, "")
            .Trim();

        if (string.IsNullOrEmpty(cleaned))
            cleaned = "UNCLE_BEN";

        return cleaned;
    }

    private GameObject FindPartial(string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
            return null;

        string lowerKeyword = keyword.ToLower();

        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        return allObjects.FirstOrDefault(obj => obj.name.ToLower().Contains(lowerKeyword));
    }
}