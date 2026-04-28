using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Attached to the same GameObject as PlayableDirector.
/// At runtime it reads the move schedule built by CutsceneCompiler
/// and physically translates characters along their paths each frame.
///
/// FIX #1: Disables applyRootMotion on all tracked characters in Awake()
///         so this script owns the transform exclusively and doesn't fight
///         the Animation Track that Timeline is also driving.
/// </summary>
public class CutsceneCharacterMover : MonoBehaviour
{
    [HideInInspector]
    public List<CharacterMoveData> moveSchedule = new List<CharacterMoveData>();

    public PlayableDirector playableDirector;

    // ── FIX #1: Disable root motion before playback starts ────────────
    private void Awake()
    {
        DisableRootMotionOnAllActors();
    }

    // Also call when schedule is rebuilt at editor time
    public void DisableRootMotionOnAllActors()
    {
        foreach (CharacterMoveData data in moveSchedule)
        {
            GameObject actor = GameObject.Find(data.characterName);
            if (actor == null) continue;

            Animator anim = actor.GetComponent<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                Debug.Log($"🔒 Root motion disabled on: {data.characterName}");
            }
        }
    }

    private void Update()
    {
        if (playableDirector == null) return;
        if (playableDirector.state != PlayState.Playing) return;

        float now = (float)playableDirector.time;

        foreach (CharacterMoveData data in moveSchedule)
        {
            // Only active during this beat's time window
            if (now < data.startTime || now > data.startTime + data.duration)
                continue;

            GameObject actor = GameObject.Find(data.characterName);
            if (actor == null) continue;

            float t = Mathf.Clamp01((now - data.startTime) / data.duration);

            if (data.shouldMove)
            {
                // Smoothed movement along path
                actor.transform.position = Vector3.Lerp(
                    data.startPosition, data.endPosition,
                    Mathf.SmoothStep(0f, 1f, t));

                // Face direction of travel
                Vector3 dir = data.endPosition - data.startPosition;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                    actor.transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
            else
            {
                // Snap to start and apply facing
                actor.transform.position = data.startPosition;
                if (data.facingY >= 0)
                    actor.transform.rotation = Quaternion.Euler(0, data.facingY, 0);
            }
        }
    }
}