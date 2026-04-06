// Assets/Scripts/Runtime/CutsceneCompiler.cs

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

public class CutsceneCompiler : MonoBehaviour
{
    [Header("Core Systems")]
    public BeatScriptLoader beatScriptLoader;
    public SceneBindingResolver sceneBindingResolver;
    public ShotPlanner shotPlanner;
    public ActionPlanner actionPlanner;
    public TimelineBuilder timelineBuilder;
    public PlayableDirector playableDirector;

    [Header("Input")]
    public string beatScriptName = "scene1";
    public string timelineAssetName = "scene1";

    [Header("Generated Cameras")]
    public Transform generatedCameraRoot;

    private readonly List<GameObject> spawnedCameraObjects = new List<GameObject>();

    [ContextMenu("Generate Cutscene Timeline")]
    public void GenerateCutscene()
    {
        if (!ValidateDependencies())
            return;

        Beat[] beats = beatScriptLoader.LoadBeats(beatScriptName);
        if (beats == null || beats.Length == 0)
        {
            Debug.LogError("No beats available.");
            return;
        }

        TimelineAsset timeline = timelineBuilder.CreateTimelineAsset(timelineAssetName);
        if (timeline == null)
            return;

        ClearExistingTracks(timeline);
        ClearSpawnedCameras();

        CinemachineTrack cinemachineTrack =
            timelineBuilder.CreateCinemachineTrack(timeline, "Cinemachine Shots");

        Dictionary<string, AnimationTrack> actorTracks = new Dictionary<string, AnimationTrack>();
        Dictionary<string, ActivationTrack> activationTracks = new Dictionary<string, ActivationTrack>();

        double currentTime = 0;

        foreach (Beat beat in beats)
        {
            Transform speaker = sceneBindingResolver.ResolveSpeaker(beat.speaker);
            Transform focus = sceneBindingResolver.ResolveFocus(beat.focus_target);

            PlannedShot plannedShot = shotPlanner.PlanShot(beat, speaker, focus);
            PlannedAction plannedAction = actionPlanner.PlanAction(beat, speaker, focus);

            Debug.Log($"🎬 Beat {beat.beat_id} → {plannedShot.shotType}");

            // 🎥 CREATE CAMERA
            CinemachineCamera vcam = CreateShotCamera(beat, plannedShot);

            // 🎬 ADD TO TIMELINE (FIXED)
            timelineBuilder.AddCinemachineShotClip(
                cinemachineTrack,
                playableDirector,
                currentTime,
                beat.duration,
                "Shot_" + beat.beat_id,
                vcam
            );

            // 🎭 ANIMATION TRACKS
            if (speaker != null)
            {
                string actorKey = speaker.name;

                if (!actorTracks.ContainsKey(actorKey))
                    actorTracks.Add(actorKey,
                        timelineBuilder.CreateAnimationTrack(timeline, actorKey + "_Animation"));

                if (!activationTracks.ContainsKey(actorKey))
                    activationTracks.Add(actorKey,
                        timelineBuilder.CreateActivationTrack(timeline, actorKey + "_Activation"));

                timelineBuilder.AddAnimationPlaceholderClip(
                    actorTracks[actorKey],
                    currentTime,
                    plannedAction.duration,
                    plannedAction.animationState + "_Beat_" + beat.beat_id
                );

                timelineBuilder.AddActivationClip(
                    activationTracks[actorKey],
                    currentTime,
                    plannedAction.duration
                );
            }

            currentTime += beat.duration;
        }

        BindTimelineTracks(actorTracks, activationTracks);

        timelineBuilder.SaveTimeline(timeline);

        playableDirector.playableAsset = timeline;
        playableDirector.RebuildGraph();

        Debug.Log("✅ CUTSCENE GENERATED SUCCESSFULLY");
    }

    // 🎥 CAMERA CREATION (AUTOMATIC)
    private CinemachineCamera CreateShotCamera(Beat beat, PlannedShot plannedShot)
    {
        GameObject camObject = new GameObject($"VCam_Beat_{beat.beat_id}_{plannedShot.shotType}");

        if (generatedCameraRoot != null)
            camObject.transform.SetParent(generatedCameraRoot);

        spawnedCameraObjects.Add(camObject);

        CinemachineCamera vcam = camObject.AddComponent<CinemachineCamera>();
        CinemachineFollow follow = camObject.AddComponent<CinemachineFollow>();

        vcam.Follow = plannedShot.followTarget;
        vcam.LookAt = plannedShot.lookTarget;

        follow.FollowOffset = plannedShot.offset;

        // 🎯 POSITION INITIAL
        if (plannedShot.followTarget != null)
            camObject.transform.position = plannedShot.followTarget.position + plannedShot.offset;

        if (plannedShot.lookTarget != null)
            camObject.transform.LookAt(plannedShot.lookTarget);

        // 🔥 ADD CINEMATIC MOTION
        CameraMotionController motion = camObject.AddComponent<CameraMotionController>();
        motion.Initialize(
            plannedShot.followTarget,
            plannedShot.movementType,
            plannedShot.offset
        );

        return vcam;
    }

    private bool ValidateDependencies()
    {
        if (beatScriptLoader == null ||
            sceneBindingResolver == null ||
            shotPlanner == null ||
            actionPlanner == null ||
            timelineBuilder == null ||
            playableDirector == null)
        {
            Debug.LogError("❌ Missing references in CutsceneCompiler");
            return false;
        }
        return true;
    }

    private void ClearExistingTracks(TimelineAsset timeline)
    {
        var tracks = timeline.GetOutputTracks();
        List<TrackAsset> toDelete = new List<TrackAsset>();

        foreach (var t in tracks)
            toDelete.Add(t);

        foreach (var t in toDelete)
            timeline.DeleteTrack(t);
    }

    private void BindTimelineTracks(
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

    private void ClearSpawnedCameras()
    {
        for (int i = spawnedCameraObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedCameraObjects[i] != null)
                DestroyImmediate(spawnedCameraObjects[i]);
        }

        spawnedCameraObjects.Clear();
    }
}