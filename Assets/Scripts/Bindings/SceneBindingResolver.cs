using UnityEngine;
using System.Linq;

public class SceneBindingResolver : MonoBehaviour
{
    public Transform ResolveTarget(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        string lowerName = name.ToLower();

        // ── Semantic bone mappings ────────────────────────────────────
        if (lowerName.EndsWith("_feet") || lowerName == "feet")
        {
            string rootName = name.Replace("_FEET", "").Replace("_feet", "");
            Transform feet = ResolveFeet(rootName);

            if (feet != null)
            {
                Debug.Log("🦶 Resolved FEET target → " + feet.name);
                return feet;
            }

            Debug.LogWarning($"⚠️ Could not resolve FEET target for: {name}");
        }

        if (lowerName.EndsWith("_head") || lowerName == "head")
        {
            string rootName = name.Replace("_HEAD", "").Replace("_head", "");
            Transform head = ResolveHead(rootName);

            if (head != null)
            {
                Debug.Log("🗣️ Resolved HEAD target → " + head.name);
                return head;
            }

            Debug.LogWarning($"⚠️ Could not resolve HEAD target for: {name}");
        }

        // ── Exact name match ──────────────────────────────────────────
        GameObject obj = GameObject.Find(name);

        if (obj != null)
        {
            Debug.Log($"✅ Found by EXACT name: {name}");
            return obj.transform;
        }

        // ── Tag match ─────────────────────────────────────────────────
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

        // ── Partial name fallback ─────────────────────────────────────
        GameObject partial = FindPartial(name);

        if (partial != null)
        {
            Debug.Log($"⚠️ Found by PARTIAL match: {partial.name}");
            return partial.transform;
        }

        // ── Environment fallback ──────────────────────────────────────
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

    public Transform ResolveSpeaker(string speaker) => ResolveTarget(speaker);
    public Transform ResolveFocus(string focus) => ResolveTarget(focus);

    private Transform ResolveFeet(string rootName)
    {
        if (string.IsNullOrEmpty(rootName))
            rootName = "UNCLE_BEN";

        Transform t = SafeFind(rootName + "/mixamorig:Hips/mixamorig:LeftUpLeg/mixamorig:LeftLeg/mixamorig:LeftFoot");

        if (t == null)
            t = SafeFind(rootName + "/mixamorig:Hips/mixamorig:RightUpLeg/mixamorig:RightLeg/mixamorig:RightFoot");

        if (t == null)
            t = FindBoneByPartialName(rootName, "LeftFoot");

        if (t == null)
            t = FindBoneByPartialName(rootName, "RightFoot");

        if (t == null)
            t = FindBoneByPartialName(rootName, "Foot");

        return t;
    }

    private Transform ResolveHead(string rootName)
    {
        if (string.IsNullOrEmpty(rootName))
            rootName = "UNCLE_BEN";

        Transform t = SafeFind(rootName + "/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck/mixamorig:Head");

        if (t == null)
            t = FindBoneByPartialName(rootName, "Head");

        return t;
    }

    private Transform FindBoneByPartialName(string rootName, string boneName)
    {
        GameObject root = GameObject.Find(rootName);

        if (root == null)
            return null;

        string lowerBoneName = boneName.ToLower();

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child.name.ToLower().Contains(lowerBoneName))
                return child;
        }

        return null;
    }

    private GameObject FindPartial(string keyword)
    {
        string lowerKeyword = keyword.ToLower();

        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        return allObjects.FirstOrDefault(obj => obj.name.ToLower().Contains(lowerKeyword));
    }

    private Transform SafeFind(string path)
    {
        GameObject obj = GameObject.Find(path);
        return obj != null ? obj.transform : null;
    }
}