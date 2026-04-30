using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attached to the same GameObject as PlayableDirector.
/// It reads the move schedule built by CutsceneCompiler and translates characters
/// along their paths using the current PlayableDirector time.
///
/// Important:
/// - Works during normal Play mode playback.
/// - Also works while scrubbing/previewing the Timeline in the Unity Editor.
/// - Root motion is disabled so this script owns character world movement.
/// </summary>
[ExecuteAlways]
public class CutsceneCharacterMover : MonoBehaviour
{
    [HideInInspector]
    public List<CharacterMoveData> moveSchedule = new List<CharacterMoveData>();

    public PlayableDirector playableDirector;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    private double lastAppliedTime = -999.0;

    private void Awake()
    {
        if (playableDirector == null)
            playableDirector = GetComponent<PlayableDirector>();

        DisableRootMotionOnAllActors();
    }

    private void OnEnable()
    {
        if (playableDirector == null)
            playableDirector = GetComponent<PlayableDirector>();

        DisableRootMotionOnAllActors();
    }

    public void DisableRootMotionOnAllActors()
    {
        if (moveSchedule == null)
            return;

        foreach (CharacterMoveData data in moveSchedule)
        {
            if (data == null || string.IsNullOrEmpty(data.characterName))
                continue;

            GameObject actor = GameObject.Find(data.characterName);

            if (actor == null)
                continue;

            Animator anim = actor.GetComponent<Animator>();

            if (anim != null)
            {
                anim.applyRootMotion = false;

                if (enableDebugLogs)
                    Debug.Log($"🔒 Root motion disabled on: {data.characterName}");
            }
        }
    }

    private void Update()
    {
        ApplyCurrentDirectorTime();
    }

    private void LateUpdate()
    {
        ApplyCurrentDirectorTime();
    }

    private void ApplyCurrentDirectorTime()
    {
        if (playableDirector == null)
            return;

        if (playableDirector.playableAsset == null)
            return;

        if (moveSchedule == null || moveSchedule.Count == 0)
            return;

        double directorTime = playableDirector.time;

        if (Application.isPlaying)
        {
            if (playableDirector.state != PlayState.Playing)
                return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // In editor preview/scrub mode, Timeline changes playableDirector.time
            // without entering PlayState.Playing. So we still apply movement.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // Avoid applying twice for the same timeline time while editor is idle.
                if (Mathf.Approximately((float)directorTime, (float)lastAppliedTime))
                    return;
            }
        }
#endif

        ApplyAtTime((float)directorTime);
        lastAppliedTime = directorTime;
    }

    public void ApplyAtTime(float now)
    {
        if (moveSchedule == null)
            return;

        foreach (CharacterMoveData data in moveSchedule)
        {
            if (data == null || string.IsNullOrEmpty(data.characterName))
                continue;

            bool isInsideBeat = now >= data.startTime && now <= data.startTime + data.duration;

            if (!isInsideBeat)
                continue;

            GameObject actor = GameObject.Find(data.characterName);

            if (actor == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"⚠ Character mover could not find actor: {data.characterName}");

                continue;
            }

            ApplyMoveData(actor.transform, data, now);
        }
    }

    private void ApplyMoveData(Transform actor, CharacterMoveData data, float now)
    {
        float safeDuration = Mathf.Max(data.duration, 0.001f);
        float t = Mathf.Clamp01((now - data.startTime) / safeDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        if (data.shouldMove)
        {
            actor.position = Vector3.Lerp(data.startPosition, data.endPosition, smoothT);

            Vector3 dir = data.endPosition - data.startPosition;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
                actor.rotation = Quaternion.LookRotation(dir.normalized);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"🚶 Moving {data.characterName} time={now:F2} t={t:F2} " +
                    $"from={data.startPosition} to={data.endPosition}"
                );
            }
        }
        else
        {
            actor.position = data.startPosition;

            if (data.facingY >= 0f)
                actor.rotation = Quaternion.Euler(0f, data.facingY, 0f);

            if (enableDebugLogs)
                Debug.Log($"🧍 Holding {data.characterName} at {data.startPosition}");
        }
    }
}