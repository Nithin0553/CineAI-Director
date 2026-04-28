using UnityEngine;

public class ShotPlanner : MonoBehaviour
{
    [Tooltip("Apply Rule-of-Thirds horizontal shift to inferred shots")]
    public bool applyRuleOfThirds = true;

    [Tooltip("Apply headroom adjustment to inferred shots")]
    public bool applyHeadroom = true;

    [Tooltip("Apply emotion-based FOV adjustment to inferred shots")]
    public bool applyEmotionFOV = true;

    public PlannedShot PlanShot(Beat beat, Transform speaker, Transform focus)
    {
        PlannedShot shot = new PlannedShot();
        shot.shotType = beat.shot_type;
        shot.duration = beat.duration;

        Transform explicitFollow = null;
        Transform explicitLookAt = null;

        if (!string.IsNullOrEmpty(beat.camera_follow_target))
            explicitFollow = FindTarget(beat.camera_follow_target);

        if (!string.IsNullOrEmpty(beat.camera_look_at_target))
            explicitLookAt = FindTarget(beat.camera_look_at_target);

        if (explicitFollow != null || explicitLookAt != null)
        {
            shot.followTarget = explicitFollow;
            shot.lookTarget = explicitLookAt != null ? explicitLookAt : explicitFollow;
        }
        else if (!string.IsNullOrEmpty(beat.speaker) && beat.speaker.ToUpper() == "ENVIRONMENT")
        {
            shot.followTarget = focus;
            shot.lookTarget = focus;
        }
        else
        {
            shot.followTarget = speaker != null ? speaker : focus;
            shot.lookTarget = focus != null ? focus : speaker;
        }

        shot.movementType = NormalizeMovement(beat.camera_movement);

        shot.orbitSpeedOverride = beat.orbit_speed_override;
        shot.dollySpeedOverride = beat.dolly_speed_override;
        shot.panSpeedOverride = beat.pan_speed_override;

        if (beat.use_exact_camera_position)
        {
            shot.useExactPosition = true;
            shot.exactPosition = new Vector3(
                beat.camera_position_x,
                beat.camera_position_y,
                beat.camera_position_z
            );

            if (shot.followTarget != null)
                shot.offset = shot.exactPosition - shot.followTarget.position;
            else
                shot.offset = shot.exactPosition;

            shot.fov = beat.fov_override > 0 ? beat.fov_override : 50f;

            Debug.Log($"🎥 Beat {beat.beat_id} → AI EXACT WORLD POSITION {shot.exactPosition}");
            return shot;
        }

        if (beat.use_exact_camera_offset)
        {
            shot.offset = new Vector3(
                beat.camera_offset_x,
                beat.camera_offset_y,
                beat.camera_offset_z
            );

            shot.fov = beat.fov_override > 0 ? beat.fov_override : 50f;

            if (shot.followTarget == null && speaker != null)
                shot.followTarget = speaker;

            if (shot.lookTarget == null)
                shot.lookTarget = focus != null ? focus : speaker;

            Debug.Log($"🎥 Beat {beat.beat_id} → AI TARGET-RELATIVE OFFSET {shot.offset}");
            return shot;
        }

        float offsetX = 0f;
        float offsetY = 0f;
        float offsetZ = 0f;
        float fov = 50f;

        switch (beat.shot_type)
        {
            case "wide_shot":
            case "establishing_shot":
                offsetX = 0f;
                offsetZ = -18f;
                offsetY = 10f;
                fov = 60f;
                break;

            case "medium_shot":
                offsetX = 0f;
                offsetZ = -5f;
                offsetY = 1.7f;
                fov = 50f;
                break;

            case "close_up":
            case "extreme_close_up":
                bool isFeetShot = beat.focus_target != null &&
                                  (beat.focus_target.ToLower().Contains("feet") ||
                                   beat.focus_target.ToLower().Contains("foot"));

                offsetX = 0f;
                offsetZ = isFeetShot ? -1.2f : -2f;
                offsetY = isFeetShot ? 0.4f : 1.6f;
                fov = beat.shot_type == "extreme_close_up" ? 30f : 40f;
                break;

            case "over_the_shoulder":
                offsetX = 0.6f;
                offsetZ = -1.2f;
                offsetY = 1.8f;
                fov = 50f;
                shot.lookTarget = focus != null ? focus : speaker;
                break;

            case "insert_shot":
                offsetX = 0f;
                offsetZ = -2f;
                offsetY = 1f;
                fov = 35f;
                break;

            case "reaction_shot":
                offsetX = 0f;
                offsetZ = -2.5f;
                offsetY = 1.6f;
                fov = 38f;
                break;

            default:
                offsetX = 0f;
                offsetZ = -10f;
                offsetY = 5f;
                fov = 55f;
                break;
        }

        if (applyEmotionFOV)
            fov = CinematicFraming.AdjustFOV(fov, beat.emotion);

        shot.fov = beat.fov_override > 0 ? beat.fov_override : fov;

        float angleYAdjust = 0f;

        if (!string.IsNullOrEmpty(beat.camera_angle))
        {
            string angle = beat.camera_angle.ToLower();

            if (angle == "high_angle") angleYAdjust = 2.5f;
            if (angle == "low_angle") angleYAdjust = -0.3f;
        }

        shot.offset = new Vector3(offsetX, offsetY + angleYAdjust, offsetZ);

        if (applyHeadroom)
            shot.offset = CinematicFraming.ApplyHeadroom(shot.offset, beat.shot_type);

        if (applyRuleOfThirds)
            shot.offset = CinematicFraming.ApplyRuleOfThirds(shot.offset, shot.followTarget);

        shot.offset = CinematicFraming.ApplyLookBias(shot.offset, beat.intent);

        bool isFeetFocus = beat.focus_target != null &&
                           (beat.focus_target.ToLower().Contains("feet") ||
                            beat.focus_target.ToLower().Contains("foot"));

        if (isFeetFocus && speaker != null)
        {
            shot.followTarget = speaker;
            shot.lookTarget = focus;
        }

        Debug.Log($"🎥 Shot {beat.beat_id} → {shot.shotType} | {shot.movementType} | offset {shot.offset} | fov {shot.fov}");
        return shot;
    }

    private Transform FindTarget(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        GameObject obj = GameObject.Find(targetName);

        if (obj != null)
            return obj.transform;

        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject candidate in allObjects)
        {
            if (candidate.name.ToLower().Contains(targetName.ToLower()))
                return candidate.transform;
        }

        return null;
    }

    private string NormalizeMovement(string movement)
    {
        if (string.IsNullOrEmpty(movement))
            return "static";

        movement = movement.ToLower();

        if (movement.Contains("orbit")) return "orbit";
        if (movement.Contains("pan")) return "pan";
        if (movement.Contains("tilt")) return "pan";
        if (movement.Contains("truck_left")) return "pan";
        if (movement.Contains("truck_right")) return "pan";
        if (movement.Contains("dolly") && movement.Contains("in")) return "dolly_in";
        if (movement.Contains("dolly") && movement.Contains("out")) return "dolly_out";
        if (movement.Contains("follow")) return "follow";
        if (movement.Contains("slow_zoom") || movement.Contains("zoom")) return "dolly_in";

        return "static";
    }
}