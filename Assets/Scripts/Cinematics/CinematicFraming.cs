using UnityEngine;

public static class CinematicFraming
{
    // 🎯 Rule of Thirds offset adjustment
    public static Vector3 ApplyRuleOfThirds(Vector3 offset, Transform target)
    {
        if (target == null) return offset;

        float horizontalShift = Random.Range(-1.5f, 1.5f);

        // Shift subject left/right (rule of thirds)
        offset.x += horizontalShift;

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
    public static Vector3 ApplyLookBias(Vector3 offset, string intent)
    {
        if (string.IsNullOrEmpty(intent)) return offset;

        if (intent.ToLower().Contains("question"))
            offset.x += 0.5f;

        if (intent.ToLower().Contains("argument"))
            offset.x += Random.Range(-1f, 1f);

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