using UnityEngine;

public class ActionPlanner : MonoBehaviour
{
    public PlannedAction PlanAction(Beat beat, Transform speaker, Transform focus)
    {
        PlannedAction action = new PlannedAction();

        action.duration = beat.duration;
        action.lookTarget = focus;

        string act = beat.action.ToLower();

        // 🚶 WALK
        if (act.Contains("walk"))
        {
            action.animationState = "Walking";
            action.useRootMotion = true;
            action.moveTarget = focus;
        }
        // 🧍 IDLE
        else if (act.Contains("idle") || act.Contains("stand"))
        {
            action.animationState = "Idle";
            action.useRootMotion = false;
        }
        // 👀 LOOK
        else if (act.Contains("look"))
        {
            action.animationState = "Idle";
            action.useRootMotion = false;
        }
        else
        {
            action.animationState = "Idle";
            action.useRootMotion = false;
        }

        Debug.Log($"🎭 Planned Action → {action.animationState}");

        return action;
    }
}