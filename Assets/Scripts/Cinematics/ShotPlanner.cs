using UnityEngine;

public class ShotPlanner : MonoBehaviour
{
    [Tooltip("Apply Rule-of-Thirds horizontal shift to inferred shots")]
    public bool applyRuleOfThirds = true;

    [Tooltip("Apply headroom adjustment to inferred shots")]
    public bool applyHeadroom = true;

    [Tooltip("Apply emotion-based FOV adjustment to inferred shots")]
    public bool applyEmotionFOV = true;

    // ─────────────────────────────────────────────────────────────────
    public PlannedShot PlanShot(Beat beat, Transform speaker, Transform focus)
    {
        PlannedShot shot = new PlannedShot();
        shot.shotType = beat.shot_type;
        shot.duration = beat.duration;

        // ── Target logic ──────────────────────────────────────────────
        if (beat.speaker.ToUpper() == "ENVIRONMENT")
        {
            shot.followTarget = focus;
            shot.lookTarget = focus;
        }
        else
        {
            shot.followTarget = speaker != null ? speaker : focus;
            shot.lookTarget = focus != null ? focus : speaker;
        }

        // ── Movement ─────────────────────────────────────────────────
        shot.movementType = NormalizeMovement(beat.camera_movement);

        // ── Speed overrides ──────────────────────────────────────────
        shot.orbitSpeedOverride = beat.orbit_speed_override;
        shot.dollySpeedOverride = beat.dolly_speed_override;
        shot.panSpeedOverride = beat.pan_speed_override;

        // ════════════════════════════════════════════════════════════
        // TIER 1 — EXACT WORLD POSITION
        // ════════════════════════════════════════════════════════════
        if (beat.use_exact_camera_position)
        {
            shot.useExactPosition = true;
            shot.exactPosition = new Vector3(
                beat.camera_position_x,
                beat.camera_position_y,
                beat.camera_position_z);

            if (shot.followTarget != null)
                shot.offset = shot.exactPosition - shot.followTarget.position;
            else
                shot.offset = shot.exactPosition;

            shot.fov = beat.fov_override > 0 ? beat.fov_override : 50f;

            Debug.Log($"🎥 Beat {beat.beat_id} → TIER1 EXACT POSITION {shot.exactPosition}");
            return shot;
        }

        // ════════════════════════════════════════════════════════════
        // TIER 2 — EXACT OFFSET
        // ════════════════════════════════════════════════════════════
        if (beat.use_exact_camera_offset)
        {
            shot.offset = new Vector3(
                beat.camera_offset_x,
                beat.camera_offset_y,
                beat.camera_offset_z);

            shot.fov = beat.fov_override > 0 ? beat.fov_override : 50f;

            Debug.Log($"🎥 Beat {beat.beat_id} → TIER2 EXACT OFFSET {shot.offset}");
            return shot;
        }

        // ════════════════════════════════════════════════════════════
        // TIER 3 — INFERRED DEFAULTS
        // ════════════════════════════════════════════════════════════
        float offsetX = 0f, offsetZ = 0f, offsetY = 0f, fov = 50f;

        switch (beat.shot_type)
        {
            case "wide_shot":
                offsetX = 0f; offsetZ = -18f; offsetY = 10f; fov = 60f;
                break;

            case "medium_shot":
                offsetX = 0f; offsetZ = -5f; offsetY = 1.7f; fov = 50f;
                break;

            case "close_up":
                bool isFeetShot = beat.focus_target != null &&
                                  (beat.focus_target.ToLower().Contains("feet") ||
                                   beat.focus_target.ToLower().Contains("foot"));
                offsetX = 0f;
                offsetZ = isFeetShot ? -1.2f : -2f;
                offsetY = isFeetShot ? 0.4f : 1.6f;
                fov = 40f;
                break;

            case "over_the_shoulder":
                offsetX = 0.6f; offsetZ = -1.2f; offsetY = 1.8f; fov = 50f;
                shot.lookTarget = focus != null ? focus : speaker;
                break;

            default:
                offsetX = 0f; offsetZ = -10f; offsetY = 5f; fov = 55f;
                break;
        }

        // ── Emotion-based FOV adjustment (CinematicFraming) ───────────
        if (applyEmotionFOV)
            fov = CinematicFraming.AdjustFOV(fov, beat.emotion);

        // ── FOV override wins over everything else ────────────────────
        shot.fov = beat.fov_override > 0 ? beat.fov_override : fov;

        // ── Camera angle modifier ─────────────────────────────────────
        float angleYAdjust = 0f;
        if (!string.IsNullOrEmpty(beat.camera_angle))
        {
            string angle = beat.camera_angle.ToLower();
            if (angle == "high_angle") angleYAdjust = 2.5f;
            if (angle == "low_angle") angleYAdjust = -0.3f;
        }

        // ── Store offset in TARGET-LOCAL space ────────────────────────
        // FIX: Previously the XZ offset was pre-rotated by the character's
        // yaw here, and then CutsceneCompiler.CreateShotCamera rotated
        // followTarget.rotation * shot.offset *again* when placing the
        // virtual camera. The double rotation flipped inferred shots to
        // the wrong side whenever the character did not face world +Z
        // (e.g. with char_facing_y = 180 the camera ended up in front of
        // the character on a "behind the shoulder" medium shot).
        //
        // Keep the offset in target-local space so all framing math
        // (rule-of-thirds, look-bias, headroom) operates relative to the
        // character, and let the compiler apply followTarget.rotation
        // exactly once when placing the camera and binding Cinemachine.
        shot.offset = new Vector3(offsetX, offsetY + angleYAdjust, offsetZ);

        // ── CinematicFraming modifiers (inferred shots only) ──────────
        if (applyHeadroom)
            shot.offset = CinematicFraming.ApplyHeadroom(shot.offset, beat.shot_type);

        if (applyRuleOfThirds)
            shot.offset = CinematicFraming.ApplyRuleOfThirds(shot.offset, shot.followTarget);

        shot.offset = CinematicFraming.ApplyLookBias(shot.offset, beat.intent);

        // ── Feet-shot root follow fix ─────────────────────────────────
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

    // ─────────────────────────────────────────────────────────────────
    private string NormalizeMovement(string movement)
    {
        if (string.IsNullOrEmpty(movement)) return "static";
        movement = movement.ToLower();
        if (movement.Contains("orbit")) return "orbit";
        if (movement.Contains("pan")) return "pan";
        if (movement.Contains("dolly") && movement.Contains("in")) return "dolly_in";
        if (movement.Contains("dolly") && movement.Contains("out")) return "dolly_out";
        if (movement.Contains("follow")) return "follow";
        if (movement.Contains("slow_zoom") || movement.Contains("zoom")) return "dolly_in";
        return "static";
    }
}