using UnityEngine;

public static class CinematicFraming
{
    // 🎯 Rule of Thirds offset adjustment
    //
    // FIX: was Random.Range(-1.5f, 1.5f), which produced different framing
    // on every regeneration AND could push close-up subjects entirely out
    // of frame (a ±1.5 unit lateral shift on a camera 2 units from the face
    // is a huge angular swing). Now: a small deterministic lateral bias
    // scaled by the camera's distance from the subject, so the rule-of-thirds
    // offset is roughly the same fraction of the screen at every focal length.
    public static Vector3 ApplyRuleOfThirds(Vector3 offset, Transform target)
    {
        if (target == null) return offset;

        float distance = new Vector3(offset.x, 0f, offset.z).magnitude;
        float shift = Mathf.Clamp(distance * 0.08f, 0.05f, 0.8f);

        // Shift subject toward one third (positive bias, in target-local +X).
        offset.x += shift;

        return offset;
    }

    // 🎯 Headroom control (important for close-ups)
    public static Vector3 ApplyHeadroom(Vector3 offset, string shotType)
    {
        switch (shotType)
        {
            case "close_up":
                offset.y += 0.3f;
                break;

            case "medium_shot":
                offset.y += 0.5f;
                break;

            case "wide_shot":
                offset.y += 1.0f;
                break;
        }

        return offset;
    }

    // 🎯 Look direction bias (character looking somewhere)
    //
    // FIX: "argument" used to call Random.Range(-1f, 1f) which produced
    // jittery, irreproducible framing. Replaced with a small deterministic
    // bias so the same beat always renders the same way.
    public static Vector3 ApplyLookBias(Vector3 offset, string intent)
    {
        if (string.IsNullOrEmpty(intent)) return offset;

        string i = intent.ToLower();

        if (i.Contains("question"))
            offset.x += 0.3f;

        if (i.Contains("argument"))
            offset.x += 0.4f;

        return offset;
    }

    // 🎯 Emotion-based camera adjustment
    public static float AdjustFOV(float baseFov, string emotion)
    {
        if (string.IsNullOrEmpty(emotion)) return baseFov;

        switch (emotion.ToLower())
        {
            case "anger":
                return baseFov - 5f; // tighter

            case "fear":
                return baseFov + 5f; // wider

            case "sad":
                return baseFov - 2f;

            case "happy":
                return baseFov + 2f;

            default:
                return baseFov;
        }
    }
}