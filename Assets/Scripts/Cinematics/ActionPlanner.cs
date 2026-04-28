using UnityEngine;

public class ActionPlanner : MonoBehaviour
{
    public PlannedAction PlanAction(Beat beat, Transform speaker, Transform focus)
    {
        PlannedAction action = new PlannedAction();
        action.duration = beat.duration;
        action.lookTarget = focus;

        // ── Exact character positions ─────────────────────────────────
        if (beat.use_char_start_position)
        {
            action.useExactStartPosition = true;
            action.exactStartPosition = new Vector3(
                beat.char_start_x, beat.char_start_y, beat.char_start_z);
        }

        if (beat.use_char_end_position)
        {
            action.useExactEndPosition = true;
            action.exactEndPosition = new Vector3(
                beat.char_end_x, beat.char_end_y, beat.char_end_z);
        }

        if (beat.use_char_facing)
        {
            action.useExactFacing = true;
            action.exactFacingY = beat.char_facing_y;
        }

        // ── Animation / movement intent ───────────────────────────────
        // Priority order:
        //   1. Walking/Running (character is physically moving)
        //   2. Sitting
        //   3. Idle (stop/stand/look)
        //   4. Talking (ONLY if no movement and dialogue present)
        //      → falls back to Idle if no Talking clip exists
        // Walking beats may also have dialogue (voiceover) — in that
        // case Walking takes priority and the dialogue is audio-only.

        string act = string.IsNullOrEmpty(beat.action) ? "" : beat.action.ToLower();

        if (act.Contains("walk") || act.Contains("approach") || act.Contains("move")
            || beat.use_char_end_position)
        {
            action.animationState = "Walking";
            action.useRootMotion = true;
            action.moveTarget = action.useExactEndPosition ? null : focus;
        }
        else if (act.Contains("run"))
        {
            action.animationState = "Running";
            action.useRootMotion = true;
            action.moveTarget = action.useExactEndPosition ? null : focus;
        }
        else if (act.Contains("sit"))
        {
            action.animationState = "Sitting";
            action.useRootMotion = false;
        }
        else if (act.Contains("stop") || act.Contains("stand") || act.Contains("idle")
                 || act.Contains("look") || act.Contains("watch") || act.Contains("stare"))
        {
            action.animationState = "Idle";
            action.useRootMotion = false;

            // Auto-face focus target for look/watch beats
            if ((act.Contains("look") || act.Contains("watch") || act.Contains("stare"))
                && speaker != null && focus != null)
            {
                Vector3 dir = (focus.position - speaker.position);
                dir.y = 0;
                if (dir != Vector3.zero)
                    speaker.rotation = Quaternion.LookRotation(dir);
            }
        }
        else if (!string.IsNullOrEmpty(beat.dialogue))
        {
            // FIX: Dialogue-only beats use Idle animation.
            // Previously this returned "Talking" which caused a missing
            // animation warning because there is no Talking.fbx in Resources.
            // Use Idle for close-up/dialogue shots — it looks correct and
            // avoids the missing clip warning.
            action.animationState = "Idle";
            action.useRootMotion = false;
        }
        else
        {
            action.animationState = "Idle";
            action.useRootMotion = false;
        }

        Debug.Log($"🎭 Beat {beat.beat_id} → Action: '{beat.action}' → Anim: {action.animationState}");
        return action;
    }
}