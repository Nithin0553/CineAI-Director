using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

public class CutsceneCompiler : MonoBehaviour
{
    public BeatScriptLoader beatScriptLoader;
    public SceneBindingResolver sceneBindingResolver;
    public ShotPlanner shotPlanner;
    public ActionPlanner actionPlanner;
    public TimelineBuilder timelineBuilder;
    public PlayableDirector playableDirector;

    public string beatScriptName = "scene1";
    public string timelineAssetName = "scene1";

    public Transform generatedCameraRoot;

    // FIX 2: assign your MainCamera here so the Cinemachine track is
    // bound automatically — no more manual selection in the Bindings panel
    [Tooltip("Drag your MainCamera (with CinemachineBrain) here")]
    public Camera mainCamera;

    [Tooltip("How far in front of the focus target a walking character stops")]
    public float walkStopDistance = 1.5f;

    private readonly List<GameObject> spawnedCameraObjects = new List<GameObject>();

    [ContextMenu("Generate Cutscene Timeline")]
    public void GenerateCutscene()
    {
        if (!ValidateDependencies()) return;

        Beat[] beats = beatScriptLoader.LoadBeats(beatScriptName);
        if (beats == null || beats.Length == 0) return;

        TimelineAsset timeline = timelineBuilder.CreateTimelineAsset(timelineAssetName);
        ClearExistingTracks(timeline);
        ClearSpawnedCameras();

        CinemachineTrack cineTrack =
            timelineBuilder.CreateCinemachineTrack(timeline, "Cinemachine Shots");

        // FIX 2: Bind the Cinemachine track to the CinemachineBrain automatically.
        // Without this, the track has no brain to drive and only the first
        // VCam ever activates — all shot switching is silently ignored.
        CinemachineBrain brain = null;
        if (mainCamera != null)
            brain = mainCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
            brain = FindFirstObjectByType<CinemachineBrain>();

        if (brain != null)
        {
            playableDirector.SetGenericBinding(cineTrack, brain);
            Debug.Log("✅ Bound Cinemachine track to: " + brain.gameObject.name);
        }
        else
            Debug.LogError("❌ No CinemachineBrain found! Add one to MainCamera.");

        Dictionary<string, AnimationTrack> actorTracks = new Dictionary<string, AnimationTrack>();
        Dictionary<string, ActivationTrack> activationTracks = new Dictionary<string, ActivationTrack>();
        Dictionary<string, Vector3> actorPositions = new Dictionary<string, Vector3>();

        double currentTime = 0;

        foreach (Beat beat in beats)
        {
            Transform speaker = sceneBindingResolver.ResolveSpeaker(beat.speaker);
            Transform focus = sceneBindingResolver.ResolveFocus(beat.focus_target);

            if (speaker != null && beat.speaker.ToUpper() != "ENVIRONMENT")
                PrePositionActor(beat, speaker, focus, actorPositions);

            PlannedShot shot = shotPlanner.PlanShot(beat, speaker, focus);
            PlannedAction action = actionPlanner.PlanAction(beat, speaker, focus);

            CinemachineCamera cam = CreateShotCamera(beat, shot);

            timelineBuilder.AddCinemachineShotClip(
                cineTrack, playableDirector,
                currentTime, beat.duration,
                "Shot_" + beat.beat_id, cam);

            // FIX 1: Skip animation/activation tracks for ENVIRONMENT beats.
            // ENVIRONMENT has no Animator — creating tracks for it produced a
            // "None (Animator)" binding which caused the NullReferenceException
            // and the SerializedProperty disposed error in the Bindings panel.
            bool isCharacterBeat = beat.speaker.ToUpper() != "ENVIRONMENT" && speaker != null;

            if (isCharacterBeat)
            {
                string key = speaker.name;

                if (!actorTracks.ContainsKey(key))
                    actorTracks.Add(key, timelineBuilder.CreateAnimationTrack(timeline, key + "_Animation"));

                if (!activationTracks.ContainsKey(key))
                    activationTracks.Add(key, timelineBuilder.CreateActivationTrack(timeline, key + "_Activation"));

                timelineBuilder.AddAnimationClip(actorTracks[key], currentTime, action.duration, action.animationState);
                timelineBuilder.AddActivationClip(activationTracks[key], currentTime, action.duration);
            }

            currentTime += beat.duration;
        }

        BindTracks(actorTracks, activationTracks);
        timelineBuilder.SaveTimeline(timeline);

        playableDirector.playableAsset = timeline;
        playableDirector.RebuildGraph();

        Debug.Log("✅ CUTSCENE GENERATED");
    }

    // ── Camera factory ────────────────────────────────────────────────
    private CinemachineCamera CreateShotCamera(Beat beat, PlannedShot shot)
    {
        GameObject camObject = new GameObject("VCam_" + beat.beat_id);

        if (generatedCameraRoot != null)
            camObject.transform.SetParent(generatedCameraRoot);

        spawnedCameraObjects.Add(camObject);

        CinemachineCamera cam = camObject.AddComponent<CinemachineCamera>();
        cam.Follow = shot.followTarget;
        cam.LookAt = shot.lookTarget;

        // Snap to initial world position
        if (shot.followTarget != null)
            camObject.transform.position = shot.followTarget.position + shot.offset;
        if (shot.lookTarget != null)
            camObject.transform.LookAt(shot.lookTarget);

        if (shot.movementType == "static" || shot.movementType == "follow")
        {
            // FIX 3: Pair CinemachineFollow WITH CinemachineRotationComposer.
            // CinemachineFollow positions the camera at the offset.
            // CinemachineRotationComposer rotates it to actually aim at LookAt.
            // Without the composer, position is correct but the camera stares
            // at world-forward instead of at the subject.
            var follow = camObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = shot.offset;

            var composer = camObject.AddComponent<CinemachineRotationComposer>();
            composer.Composition.ScreenPosition = new Vector2(0.5f, 0.45f);
        }
        else
        {
            // Motion extension handles both position AND orientation internally
            var ext = camObject.AddComponent<CinemachineMotionExtension>();
            ext.target = shot.followTarget;
            ext.initialOffset = shot.offset;
            ext.motionType = MapMotionType(shot.movementType);
        }

        return cam;
    }

    private CinemachineMotionExtension.MotionType MapMotionType(string movement)
    {
        switch (movement)
        {
            case "orbit": return CinemachineMotionExtension.MotionType.Orbit;
            case "dolly_in": return CinemachineMotionExtension.MotionType.DollyIn;
            case "dolly_out": return CinemachineMotionExtension.MotionType.DollyOut;
            case "follow": return CinemachineMotionExtension.MotionType.Follow;
            case "pan": return CinemachineMotionExtension.MotionType.Pan;
            default: return CinemachineMotionExtension.MotionType.Static;
        }
    }

    // ── Pre-position actors ───────────────────────────────────────────
    private void PrePositionActor(
        Beat beat, Transform actor, Transform focus,
        Dictionary<string, Vector3> actorPositions)
    {
        string key = actor.name;
        if (actorPositions.ContainsKey(key))
            actor.position = actorPositions[key];

        string act = string.IsNullOrEmpty(beat.action) ? "" : beat.action.ToLower();

        if ((act.Contains("walk") || act.Contains("approach")) && focus != null)
        {
            Vector3 dir = (actor.position - focus.position).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward;
            Vector3 dest = focus.position + dir * walkStopDistance;
            dest.y = actor.position.y;
            actor.position = dest;

            Vector3 lookDir = focus.position - actor.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                actor.rotation = Quaternion.LookRotation(lookDir);
        }
        else if ((act.Contains("stop") || act.Contains("stand")) && focus != null)
        {
            Vector3 lookDir = focus.position - actor.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                actor.rotation = Quaternion.LookRotation(lookDir);
        }

        actorPositions[key] = actor.position;
        Debug.Log($"📍 Pre-positioned {actor.name} → {actor.position} for beat {beat.beat_id}");
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private bool ValidateDependencies()
    {
        if (beatScriptLoader == null || sceneBindingResolver == null ||
            shotPlanner == null || actionPlanner == null ||
            timelineBuilder == null || playableDirector == null)
        {
            Debug.LogError("❌ Missing references on CutsceneCompiler!");
            return false;
        }
        return true;
    }

    private void ClearExistingTracks(TimelineAsset timeline)
    {
        foreach (var track in timeline.GetOutputTracks())
            timeline.DeleteTrack(track);
    }

    private void ClearSpawnedCameras()
    {
        for (int i = spawnedCameraObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedCameraObjects[i] != null)
                DestroyImmediate(spawnedCameraObjects[i]);
        }
        spawnedCameraObjects.Clear();
    }

    private void BindTracks(
        Dictionary<string, AnimationTrack> actorTracks,
        Dictionary<string, ActivationTrack> activationTracks)
    {
        foreach (var kvp in actorTracks)
        {
            GameObject actor = GameObject.Find(kvp.Key);
            if (actor == null) continue;
            Animator animator = actor.GetComponent<Animator>();
            if (animator != null)
                playableDirector.SetGenericBinding(kvp.Value, animator);
        }

        foreach (var kvp in activationTracks)
        {
            GameObject actor = GameObject.Find(kvp.Key);
            if (actor == null) continue;
            playableDirector.SetGenericBinding(kvp.Value, actor);
        }
    }
}