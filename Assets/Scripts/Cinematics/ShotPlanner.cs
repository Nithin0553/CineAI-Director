using UnityEngine;

public class ShotPlanner : MonoBehaviour
{
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

        // ── Movement normalization ────────────────────────────────────
        shot.movementType = NormalizeMovement(beat.camera_movement);

        // ── Base offsets (LOCAL to character forward direction) ───────
        // FIX: offsets are now applied in world-space relative to the
        //      character's facing so the camera always frames correctly.
        switch (beat.shot_type)
        {
            case "wide_shot":
                shot.offset = new Vector3(0, 10, -18);
                shot.fov = 60f;
                break;

            case "medium_shot":
                shot.offset = new Vector3(0, 2, -5);
                shot.fov = 50f;
                break;

            case "close_up":
                shot.offset = new Vector3(0, 1.6f, -2);
                shot.fov = 40f;
                break;

            // FIX: over_the_shoulder was completely missing — now handled
            case "over_the_shoulder":
                shot.offset = new Vector3(0.6f, 1.8f, -1.2f);   // offset from speaker's right shoulder
                shot.fov = 50f;
                // Look at the focus (the thing being revealed), not the speaker
                shot.lookTarget = focus != null ? focus : speaker;
                break;

            default:
                shot.offset = new Vector3(0, 5, -10);
                shot.fov = 55f;
                break;
        }

        ApplyCameraAngle(beat.camera_angle, ref shot);

        // ── FIX: rotate offset to match speaker's facing direction ────
        // Without this, all offsets are in world-space and the camera
        // always ends up on the same world-side regardless of character rotation.
        if (shot.followTarget != null && beat.shot_type != "wide_shot")
        {
            shot.offset = shot.followTarget.rotation * shot.offset;
        }

        Debug.Log($"🎥 Shot → {shot.shotType} | {shot.movementType} | offset {shot.offset}");
        return shot;
    }

    // ── Movement normalisation (FIX: added slow_zoom, pan) ──────────
    private string NormalizeMovement(string movement)
    {
        if (string.IsNullOrEmpty(movement))
            return "static";

        movement = movement.ToLower();

        if (movement.Contains("orbit")) return "orbit";
        if (movement.Contains("pan")) return "pan";
        if (movement.Contains("dolly") && movement.Contains("in")) return "dolly_in";
        if (movement.Contains("dolly") && movement.Contains("out")) return "dolly_out";
        if (movement.Contains("follow")) return "follow";
        if (movement.Contains("slow_zoom") || movement.Contains("zoom")) return "dolly_in"; // FIX: map slow_zoom
        return "static";
    }

    private void ApplyCameraAngle(string angle, ref PlannedShot shot)
    {
        if (string.IsNullOrEmpty(angle)) return;
        angle = angle.ToLower();

        if (angle == "high_angle") shot.offset.y += 3f;
        if (angle == "low_angle") shot.offset.y -= 1f;  // FIX: was -2f which put camera underground on close-ups
    }
}