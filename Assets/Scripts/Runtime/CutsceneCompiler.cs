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

        Dictionary<string, AnimationTrack> actorTracks = new Dictionary<string, AnimationTrack>();
        Dictionary<string, ActivationTrack> activationTracks = new Dictionary<string, ActivationTrack>();

        double currentTime = 0;

        foreach (Beat beat in beats)
        {
            Transform speaker = sceneBindingResolver.ResolveSpeaker(beat.speaker);
            Transform focus = sceneBindingResolver.ResolveFocus(beat.focus_target);

            PlannedShot shot = shotPlanner.PlanShot(beat, speaker, focus);
            PlannedAction action = actionPlanner.PlanAction(beat, speaker, focus);

            CinemachineCamera cam = CreateShotCamera(beat, shot);

            timelineBuilder.AddCinemachineShotClip(
                cineTrack,
                playableDirector,
                currentTime,
                beat.duration,
                "Shot_" + beat.beat_id,
                cam
            );

            // 🎭 FIXED ANIMATION BLOCK
            if (speaker != null)
            {
                string key = speaker.name;

                if (!actorTracks.ContainsKey(key))
                    actorTracks.Add(key,
                        timelineBuilder.CreateAnimationTrack(timeline, key + "_Animation"));

                if (!activationTracks.ContainsKey(key))
                    activationTracks.Add(key,
                        timelineBuilder.CreateActivationTrack(timeline, key + "_Activation"));

                // ✅ USE NEW FUNCTION
                timelineBuilder.AddAnimationClip(
                    actorTracks[key],
                    currentTime,
                    action.duration,
                    action.animationState
                );

                timelineBuilder.AddActivationClip(
                    activationTracks[key],
                    currentTime,
                    action.duration
                );
            }

            currentTime += beat.duration;
        }

        BindTracks(actorTracks, activationTracks);

        timelineBuilder.SaveTimeline(timeline);

        playableDirector.playableAsset = timeline;
        playableDirector.RebuildGraph();

        Debug.Log("✅ CUTSCENE GENERATED");
    }

    private CinemachineCamera CreateShotCamera(Beat beat, PlannedShot shot)
    {
        GameObject camObject = new GameObject("VCam_" + beat.beat_id);

        if (generatedCameraRoot != null)
            camObject.transform.SetParent(generatedCameraRoot);

        spawnedCameraObjects.Add(camObject);

        CinemachineCamera cam = camObject.AddComponent<CinemachineCamera>();

        if (shot.movementType != "orbit")
        {
            var follow = camObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = shot.offset;
        }

        cam.Follow = shot.followTarget;
        cam.LookAt = shot.lookTarget;

        if (shot.followTarget != null)
            camObject.transform.position = shot.followTarget.position + shot.offset;

        if (shot.lookTarget != null)
            camObject.transform.LookAt(shot.lookTarget);

        CameraMotionController motion = camObject.AddComponent<CameraMotionController>();
        motion.Initialize(shot.followTarget, shot.movementType, shot.offset);

        return cam;
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
            Debug.LogError("❌ Missing references!");
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