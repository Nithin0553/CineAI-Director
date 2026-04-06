using UnityEngine;

public class ShotPlanner : MonoBehaviour
{
    private string lastShotType = "";

    public PlannedShot PlanShot(Beat beat, Transform speaker, Transform focus)
    {
        PlannedShot shot = new PlannedShot();

        // 🎯 BASIC INFO
        shot.shotType = beat.shot_type;
        shot.duration = beat.duration;

        // 🎯 TARGET SELECTION (SMART)
        shot.followTarget = speaker != null ? speaker : focus;
        shot.lookTarget = focus != null ? focus : speaker;

        // 🧠 NORMALIZE MOVEMENT TYPE
        shot.movementType = NormalizeMovement(beat.camera_movement);

        // 🎥 BASE SHOT SETUP
        switch (beat.shot_type)
        {
            case "wide_shot":
                shot.offset = new Vector3(0, 6, -12);
                shot.fov = 60f;
                break;

            case "medium_shot":
                shot.offset = new Vector3(0, 3, -6);
                shot.fov = 50f;
                break;

            case "close_up":
                shot.offset = new Vector3(0, 1.5f, -2);
                shot.fov = 40f;
                break;

            default:
                shot.offset = new Vector3(0, 4, -8);
                shot.fov = 55f;
                break;
        }

        // 🎬 CAMERA ANGLE
        ApplyCameraAngle(beat.camera_angle, ref shot);

        // =====================================================
        // 🧠 AI CINEMATOGRAPHY LAYER
        // =====================================================

        // Rule of thirds framing
        shot.offset = CinematicFraming.ApplyRuleOfThirds(
            shot.offset,
            shot.lookTarget
        );

        // Headroom control
        shot.offset = CinematicFraming.ApplyHeadroom(
            shot.offset,
            shot.shotType
        );

        // Intent-based look bias
        shot.offset = CinematicFraming.ApplyLookBias(
            shot.offset,
            beat.intent
        );

        // Emotion-based FOV
        shot.fov = CinematicFraming.AdjustFOV(
            shot.fov,
            beat.emotion
        );

        // =====================================================
        // 🚫 ANTI-REPETITION SYSTEM
        // =====================================================
        if (lastShotType == beat.shot_type)
        {
            shot.offset.x += Random.Range(-2f, 2f);
            shot.offset.z += Random.Range(-2f, 2f);
        }

        lastShotType = beat.shot_type;

        // =====================================================
        // 🎥 NATURAL VARIATION
        // =====================================================
        ApplyNaturalVariation(ref shot);

        Debug.Log($"🎥 Planned Shot → {shot.shotType} | {shot.movementType}");

        return shot;
    }

    // 🧠 NORMALIZE MOVEMENT TYPES
    private string NormalizeMovement(string movement)
    {
        if (string.IsNullOrEmpty(movement))
            return "static";

        movement = movement.ToLower();

        if (movement.Contains("orbit"))
            return "orbit";

        if (movement.Contains("dolly") && movement.Contains("in"))
            return "dolly_in";

        if (movement.Contains("dolly") && movement.Contains("out"))
            return "dolly_out";

        if (movement.Contains("follow"))
            return "follow";

        if (movement.Contains("pan"))
            return "orbit";

        return "static";
    }

    // 🎬 CAMERA ANGLE LOGIC
    private void ApplyCameraAngle(string angle, ref PlannedShot shot)
    {
        if (string.IsNullOrEmpty(angle))
            return;

        angle = angle.ToLower();

        switch (angle)
        {
            case "high_angle":
                shot.offset.y += 3f;
                break;

            case "low_angle":
                shot.offset.y -= 2f;
                break;

            case "eye_level":
                break;
        }
    }

    // 🎥 NATURAL RANDOM VARIATION
    private void ApplyNaturalVariation(ref PlannedShot shot)
    {
        float randomX = Random.Range(-0.5f, 0.5f);
        float randomY = Random.Range(-0.3f, 0.3f);
        float randomZ = Random.Range(-0.5f, 0.5f);

        shot.offset += new Vector3(randomX, randomY, randomZ);

        shot.fov += Random.Range(-2f, 2f);
    }
}