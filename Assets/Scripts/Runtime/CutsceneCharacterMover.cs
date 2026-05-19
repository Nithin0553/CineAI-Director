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

    [Header("Universal Ground Safety")]
    public bool snapCharacterToGround = true;
    public LayerMask groundMask = ~0;

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
                anim.applyRootMotion = false;
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
        float safeDuration = Mathf.Max(data.duration, Mathf.Epsilon);
        float t = Mathf.Clamp01((now - data.startTime) / safeDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        Vector3 finalPosition;

        if (data.shouldMove)
        {
            finalPosition = Vector3.Lerp(data.startPosition, data.endPosition, smoothT);

            Vector3 dir = data.endPosition - data.startPosition;
            dir.y = 0f;

            if (dir.sqrMagnitude > Mathf.Epsilon)
                actor.rotation = Quaternion.LookRotation(dir.normalized);
        }
        else
        {
            finalPosition = data.startPosition;

            if (data.facingY >= 0f)
                actor.rotation = Quaternion.Euler(0f, data.facingY, 0f);
        }

        actor.position = finalPosition;

        if (snapCharacterToGround)
            SnapActorVisualBottomToGround(actor);

        if (enableDebugLogs)
            Debug.Log($"{data.characterName} applied at time={now:F2}, root={actor.position}");
    }

    private void SnapActorVisualBottomToGround(Transform actor)
    {
        Bounds actorBounds;

        if (!TryGetActorVisualBounds(actor, out actorBounds))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"Ground snap failed for {actor.name}: no Renderer or Collider bounds found.");

            return;
        }

        float groundY;

        if (!TryFindGroundY(actor, actorBounds, out groundY))
            return;

        Bounds updatedBounds;

        if (TryGetActorVisualBounds(actor, out updatedBounds))
            actorBounds = updatedBounds;

        float visualBottomY = actorBounds.min.y;
        float correctionY = groundY - visualBottomY;

        actor.position += Vector3.up * correctionY;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"Ground snap {actor.name}: groundY={groundY:F3}, " +
                $"visualBottomY={visualBottomY:F3}, correctionY={correctionY:F3}, " +
                $"finalRoot={actor.position}"
            );
        }
    }

    private bool TryFindGroundY(Transform actor, Bounds actorBounds, out float groundY)
    {
        groundY = actor.position.y;

        float actorHeight = Mathf.Max(actorBounds.size.y, Mathf.Epsilon);
        float rayStartY = actorBounds.max.y + actorHeight;
        float rayDistance = actorHeight * 3.0f;

        Vector3 rayStart = new Vector3(
            actorBounds.center.x,
            rayStartY,
            actorBounds.center.z
        );

        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            Vector3.down,
            rayDistance,
            groundMask
        );

        if (hits == null || hits.Length == 0)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"Ground snap failed for {actor.name}: no ground hit. " +
                    $"rayStart={rayStart}, rayDistance={rayDistance}, " +
                    $"actorBoundsMinY={actorBounds.min.y}, actorBoundsMaxY={actorBounds.max.y}. " +
                    $"Check if the terrain/floor has a Collider and if Ground Mask includes its layer."
                );
            }

            return false;
        }

        bool foundGround = false;
        float nearestGroundY = float.NegativeInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == actor || hit.transform.IsChildOf(actor))
            {
                if (enableDebugLogs)
                    Debug.Log($"Ground snap ignored self-hit: {hit.transform.name}");

                continue;
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"Ground snap ray hit: object={hit.transform.name}, " +
                    $"layer={LayerMask.LayerToName(hit.transform.gameObject.layer)}, " +
                    $"point={hit.point}"
                );
            }

            if (hit.point.y > nearestGroundY)
            {
                nearestGroundY = hit.point.y;
                foundGround = true;
            }
        }

        if (!foundGround)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"Ground snap failed for {actor.name}: hits existed, but all were ignored.");

            return false;
        }

        groundY = nearestGroundY;
        return true;
    }

    private bool TryGetActorVisualBounds(Transform actor, out Bounds bounds)
    {
        Renderer[] renderers = actor.GetComponentsInChildren<Renderer>();

        bounds = new Bounds(actor.position, Vector3.zero);
        bool found = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!renderer.enabled)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (found)
            return true;

        Collider[] colliders = actor.GetComponentsInChildren<Collider>();

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            if (!collider.enabled)
                continue;

            if (!found)
            {
                bounds = collider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return found;
    }
}