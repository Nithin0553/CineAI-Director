using UnityEngine;
using System.Linq;

public class SceneBindingResolver : MonoBehaviour
{
    // 🔹 MAIN ENTRY POINT
    public Transform ResolveTarget(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        string lowerName = name.ToLower();

        // =====================================================
        // ✅ STEP 0: SEMANTIC INTELLIGENCE (AI-LIKE MAPPING)
        // =====================================================

        // 👉 Example: FEET → character foot bone
        if (lowerName == "feet")
        {
            Transform t = SafeFind("UNCLE_BEN/Armature/Hips/Leg/Foot");
            if (t != null)
            {
                Debug.Log("🦶 Resolved FEET → Foot bone");
                return t;
            }
        }

        // 👉 Add more mappings later like:
        // head, hand, eyes, etc.

        // =====================================================
        // 1️⃣ EXACT NAME MATCH
        // =====================================================
        GameObject obj = GameObject.Find(name);
        if (obj != null)
        {
            Debug.Log($"✅ Found by EXACT name: {name}");
            return obj.transform;
        }

        // =====================================================
        // 2️⃣ TAG MATCH (SAFE — NO CRASH)
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
        // 3️⃣ PARTIAL MATCH (SMART FALLBACK)
        // =====================================================
        GameObject partial = FindPartial(name);
        if (partial != null)
        {
            Debug.Log($"⚠️ Found by PARTIAL match: {partial.name}");
            return partial.transform;
        }

        // =====================================================
        // 4️⃣ ENVIRONMENT FALLBACK
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
        // ❌ FINAL FAIL SAFE
        // =====================================================
        Debug.LogWarning($"❌ Could not resolve: {name}");
        return null;
    }

    // 🔹 Resolve speaker (character)
    public Transform ResolveSpeaker(string speaker)
    {
        return ResolveTarget(speaker);
    }

    // 🔹 Resolve focus target (camera target)
    public Transform ResolveFocus(string focus)
    {
        return ResolveTarget(focus);
    }

    // 🔹 SMART PARTIAL SEARCH
    private GameObject FindPartial(string keyword)
    {
        keyword = keyword.ToLower();

        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

        return allObjects.FirstOrDefault(obj =>
            obj.name.ToLower().Contains(keyword)
        );
    }

    // 🔹 SAFE FIND (NO CRASH)
    private Transform SafeFind(string path)
    {
        GameObject obj = GameObject.Find(path);
        return obj != null ? obj.transform : null;
    }
}