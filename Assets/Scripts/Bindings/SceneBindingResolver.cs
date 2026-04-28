using UnityEngine;
using System.Linq;

public class SceneBindingResolver : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // MAIN ENTRY POINT
    // ─────────────────────────────────────────────────────────────────
    public Transform ResolveTarget(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        string lowerName = name.ToLower();

        // =====================================================
        // STEP 0: SEMANTIC MAPPINGS
        // =====================================================

        // FIX #4: Correct Mixamo rig foot bone paths.
        // The previous path "UNCLE_BEN/Armature/Hips/Leg/Foot" was generic
        // and always returned null. Mixamo rigs use the "mixamorig:" prefix.
        if (lowerName == "uncle_ben_feet" || lowerName == "feet")
        {
            // Try left foot first
            Transform t = SafeFind("UNCLE_BEN/mixamorig:Hips/mixamorig:LeftUpLeg/mixamorig:LeftLeg/mixamorig:LeftFoot");

            // Fallback to right foot
            if (t == null)
                t = SafeFind("UNCLE_BEN/mixamorig:Hips/mixamorig:RightUpLeg/mixamorig:RightLeg/mixamorig:RightFoot");

            // Last resort: search by partial bone name
            if (t == null)
                t = FindBoneByPartialName("UNCLE_BEN", "LeftFoot");
            if (t == null)
                t = FindBoneByPartialName("UNCLE_BEN", "Foot");

            if (t != null)
            {
                Debug.Log("🦶 Resolved FEET → " + t.name);
                return t;
            }

            Debug.LogWarning("⚠️ Foot bone not found. Check your Mixamo rig hierarchy in the Inspector.");
        }

        // Head bone mapping
        if (lowerName == "uncle_ben_head" || lowerName == "head")
        {
            Transform t = SafeFind("UNCLE_BEN/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck/mixamorig:Head");
            if (t == null)
                t = FindBoneByPartialName("UNCLE_BEN", "Head");
            if (t != null)
            {
                Debug.Log("🗣️ Resolved HEAD → " + t.name);
                return t;
            }
        }

        // =====================================================
        // 1. EXACT NAME MATCH
        // =====================================================
        GameObject obj = GameObject.Find(name);
        if (obj != null)
        {
            Debug.Log($"✅ Found by EXACT name: {name}");
            return obj.transform;
        }

        // =====================================================
        // 2. TAG MATCH (SAFE — NO CRASH)
        // =====================================================
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

        // =====================================================
        // 3. PARTIAL NAME MATCH (SMART FALLBACK)
        // =====================================================
        GameObject partial = FindPartial(name);
        if (partial != null)
        {
            Debug.Log($"⚠️ Found by PARTIAL match: {partial.name}");
            return partial.transform;
        }

        // =====================================================
        // 4. ENVIRONMENT FALLBACK
        // =====================================================
        if (name.ToUpper() == "ENVIRONMENT")
        {
            GameObject env = GameObject.Find("ENVIRONMENT");
            if (env != null)
            {
                Debug.Log("🌍 Using ENVIRONMENT fallback");
                return env.transform;
            }
        }

        // =====================================================
        // FINAL FAIL SAFE
        // =====================================================
        Debug.LogWarning($"❌ Could not resolve: '{name}' — check spelling and that the object exists in the scene.");
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    public Transform ResolveSpeaker(string speaker) => ResolveTarget(speaker);
    public Transform ResolveFocus(string focus) => ResolveTarget(focus);

    // ─────────────────────────────────────────────────────────────────
    // Searches all children of a named root object for a bone by partial name
    // ─────────────────────────────────────────────────────────────────
    private Transform FindBoneByPartialName(string rootName, string boneName)
    {
        GameObject root = GameObject.Find(rootName);
        if (root == null) return null;

        boneName = boneName.ToLower();

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child.name.ToLower().Contains(boneName))
                return child;
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // Partial name search across all scene objects
    // ─────────────────────────────────────────────────────────────────
    private GameObject FindPartial(string keyword)
    {
        keyword = keyword.ToLower();
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        return allObjects.FirstOrDefault(obj => obj.name.ToLower().Contains(keyword));
    }

    // ─────────────────────────────────────────────────────────────────
    // Safe find that returns null instead of throwing
    // ─────────────────────────────────────────────────────────────────
    private Transform SafeFind(string path)
    {
        GameObject obj = GameObject.Find(path);
        return obj != null ? obj.transform : null;
    }
}