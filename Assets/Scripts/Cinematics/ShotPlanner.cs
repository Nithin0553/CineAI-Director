using UnityEngine;

public class ShotPlanner : MonoBehaviour
{

    public PlannedShot PlanShot(Beat beat, Transform speaker, Transform focus)
    {
        PlannedShot shot = new PlannedShot();

        shot.shotType = beat.shot_type;
        shot.duration = beat.duration;

        // 🔥 FIXED TARGET LOGIC
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

        // 🔥 MOVEMENT NORMALIZATION
        shot.movementType = NormalizeMovement(beat.camera_movement);

        // 🎥 BASE SHOT
        switch (beat.shot_type)
        {
            case "wide_shot":
                shot.offset = new Vector3(0, 10, -18); // 🔥 FIXED HEIGHT
                shot.fov = 60f;
                break;

            case "medium_shot":
                shot.offset = new Vector3(0, 4, -8);
                shot.fov = 50f;
                break;

            case "close_up":
                shot.offset = new Vector3(0, 2, -3);
                shot.fov = 40f;
                break;

            default:
                shot.offset = new Vector3(0, 5, -10);
                shot.fov = 55f;
                break;
        }

        ApplyCameraAngle(beat.camera_angle, ref shot);

        Debug.Log($"🎥 Shot → {shot.shotType} | {shot.movementType}");

        return shot;
    }

    private string NormalizeMovement(string movement)
    {
        if (string.IsNullOrEmpty(movement))
            return "static";

        movement = movement.ToLower();

        if (movement.Contains("orbit"))
            return "orbit";

        if (movement.Contains("pan"))
            return "pan";

        if (movement.Contains("dolly") && movement.Contains("in"))
            return "dolly_in";

        if (movement.Contains("dolly") && movement.Contains("out"))
            return "dolly_out";

        if (movement.Contains("follow"))
            return "follow";

        return "static";
    }

    private void ApplyCameraAngle(string angle, ref PlannedShot shot)
    {
        if (string.IsNullOrEmpty(angle)) return;

        angle = angle.ToLower();

        if (angle == "high_angle")
            shot.offset.y += 3f;

        if (angle == "low_angle")
            shot.offset.y -= 2f;
    }
}