using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CutsceneCharacterMover : MonoBehaviour
{
    [HideInInspector]
    public List<CharacterMoveData> moveSchedule = new List<CharacterMoveData>();

    public PlayableDirector playableDirector;

    [Header("Ground Safety")]
    public bool snapCharacterToGround = true;
    public LayerMask groundMask = ~0;
    public float groundRaycastHeight = 10.0f;
    public float groundRaycastDistance = 50.0f;
    public float groundOffset = 0.02f;

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
                    Debug.Log($"Root motion disabled on: {data.characterName}");
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
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
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
                    Debug.LogWarning($"Character mover could not find actor: {data.characterName}");

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

        Vector3 finalPosition;

        if (data.shouldMove)
        {
            finalPosition = Vector3.Lerp(data.startPosition, data.endPosition, smoothT);

            Vector3 dir = data.endPosition - data.startPosition;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
                actor.rotation = Quaternion.LookRotation(dir.normalized);
        }
        else
        {
            finalPosition = data.startPosition;

            if (data.facingY >= 0f)
                actor.rotation = Quaternion.Euler(0f, data.facingY, 0f);
        }

        if (snapCharacterToGround)
            finalPosition = SnapPositionToGround(finalPosition, actor);

        actor.position = finalPosition;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{data.characterName} position applied at time={now:F2}, " +
                $"position={finalPosition}, shouldMove={data.shouldMove}"
            );
        }
    }

    private Vector3 SnapPositionToGround(Vector3 position, Transform actor)
    {
        Vector3 rayStart = new Vector3(
            position.x,
            position.y + groundRaycastHeight,
            position.z
        );

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRaycastDistance, groundMask))
        {
            if (hit.transform == actor || hit.transform.IsChildOf(actor))
                return position;

            position.y = hit.point.y + groundOffset;
        }

        return position;
    }
}