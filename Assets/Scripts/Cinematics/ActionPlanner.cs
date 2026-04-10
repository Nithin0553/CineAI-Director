using UnityEngine;

public class ActionPlanner : MonoBehaviour
{
    public PlannedAction PlanAction(Beat beat, Transform speaker, Transform focus)
    {
        PlannedAction action = new PlannedAction();

        action.duration = beat.duration;
        action.lookTarget = focus;

        // FIX: guard against null action field in JSON
        string act = string.IsNullOrEmpty(beat.action) ? "" : beat.action.ToLower();

        // ── Walking ──────────────────────────────────────────────────
        if (act.Contains("walk") || act.Contains("approach") || act.Contains("move"))
        {
            action.animationState = "Walking";
            action.useRootMotion = true;
            action.moveTarget = focus;
        }
        // ── Sitting ──────────────────────────────────────────────────
        else if (act.Contains("sit"))
        {
            action.animationState = "Sitting";
            action.useRootMotion = false;
        }
        // ── Running ──────────────────────────────────────────────────
        else if (act.Contains("run"))
        {
            action.animationState = "Running";
            action.useRootMotion = true;
            action.moveTarget = focus;
        }
        // ── Talking / dialogue ───────────────────────────────────────
        // FIX: beats that have dialogue should use a Talking animation,
        //      not just snap to Idle
        else if (!string.IsNullOrEmpty(beat.dialogue) && beat.dialogue.Length > 0)
        {
            action.animationState = "Talking";
            action.useRootMotion = false;
        }
        // ── Look / observe ────────────────────────────────────────────
        else if (act.Contains("look") || act.Contains("watch") || act.Contains("stare"))
        {
            action.animationState = "Idle";
            action.useRootMotion = false;
            // Character should face the focus target
            if (speaker != null && focus != null)
            {
                Vector3 dir = (focus.position - speaker.position);
                dir.y = 0;
                if (dir != Vector3.zero)
                    speaker.rotation = Quaternion.LookRotation(dir);
            }
        }
        // ── Stop / stand ──────────────────────────────────────────────
        else if (act.Contains("stop") || act.Contains("stand") || act.Contains("idle"))
        {
            action.animationState = "Idle";
            action.useRootMotion = false;
        }
        // ── Default ──────────────────────────────────────────────────
        else
        {
            action.animationState = "Idle";
            action.useRootMotion = false;
        }

        Debug.Log($"🎭 Beat {beat.beat_id} → Action: '{beat.action}' → Anim: {action.animationState}");
        return action;
    }
}