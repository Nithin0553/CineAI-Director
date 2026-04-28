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
        {
            Debug.LogError("❌ No CinemachineBrain found! Add one to MainCamera.");
        }

        var actorTracks = new Dictionary<string, AnimationTrack>();
        var activationTracks = new Dictionary<string, ActivationTrack>();
        var actorPositions = new Dictionary<string, Vector3>();
        var moveSchedule = new List<CharacterMoveData>();

        double currentTime = 0;

        foreach (Beat beat in beats)
        {
            Transform speaker = sceneBindingResolver.ResolveSpeaker(beat.speaker);
            Transform focus = sceneBindingResolver.ResolveFocus(beat.focus_target);

            if (speaker != null && !IsEnvironmentBeat(beat))
                PrePositionActor(beat, speaker, focus, actorPositions);

            PlannedShot shot = shotPlanner.PlanShot(beat, speaker, focus);
            PlannedAction action = actionPlanner.PlanAction(beat, speaker, focus);

            if (action.useExactStartPosition && speaker != null)
            {
                speaker.position = action.exactStartPosition;
                Debug.Log($"📍 Beat {beat.beat_id} → '{speaker.name}' snapped to {action.exactStartPosition}");
            }

            if (action.useExactFacing && speaker != null)
            {
                speaker.rotation = Quaternion.Euler(0, action.exactFacingY, 0);
                Debug.Log($"🧭 Beat {beat.beat_id} → '{speaker.name}' facing set to Y={action.exactFacingY}");
            }

            if (speaker != null && !IsEnvironmentBeat(beat))
            {
                CharacterMoveData moveData = BuildMoveData(beat, action, speaker, focus, currentTime);
                moveSchedule.Add(moveData);
            }

            CinemachineCamera cam = CreateShotCamera(beat, shot);

            timelineBuilder.AddCinemachineShotClip(
                cineTrack,
                playableDirector,
                currentTime,
                beat.duration,
                "Shot_" + beat.beat_id,
                cam
            );

            bool isCharacterBeat = speaker != null && !IsEnvironmentBeat(beat);

            if (isCharacterBeat)
            {
                string key = speaker.name;

                if (!actorTracks.ContainsKey(key))
                    actorTracks.Add(key, timelineBuilder.CreateAnimationTrack(timeline, key + "_Animation"));

                if (!activationTracks.ContainsKey(key))
                    activationTracks.Add(key, timelineBuilder.CreateActivationTrack(timeline, key + "_Activation"));

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

        CutsceneCharacterMover mover =
            playableDirector.gameObject.GetComponent<CutsceneCharacterMover>();

        if (mover == null)
            mover = playableDirector.gameObject.AddComponent<CutsceneCharacterMover>();

        mover.playableDirector = playableDirector;
        mover.moveSchedule = moveSchedule;
        mover.DisableRootMotionOnAllActors();

        Debug.Log($"✅ CUTSCENE GENERATED — {moveSchedule.Count} character move entries");
    }

    private CharacterMoveData BuildMoveData(
        Beat beat,
        PlannedAction action,
        Transform speaker,
        Transform focus,
        double currentTime)
    {
        bool shouldMove = beat.use_char_end_position;

        CharacterMoveData moveData = new CharacterMoveData();
        moveData.characterName = speaker.name;
        moveData.startTime = (float)currentTime;
        moveData.duration = beat.duration;
        moveData.facingY = action.useExactFacing ? action.exactFacingY : -1f;
        moveData.shouldMove = shouldMove;

        if (action.useExactStartPosition)
            moveData.startPosition = action.exactStartPosition;
        else
            moveData.startPosition = speaker.position;

        if (shouldMove)
        {
            if (action.useExactEndPosition)
            {
                moveData.endPosition = action.exactEndPosition;
            }
            else if (focus != null)
            {
                Vector3 dir = (speaker.position - focus.position).normalized;

                if (dir == Vector3.zero)
                    dir = Vector3.forward;

                moveData.endPosition = focus.position + dir * walkStopDistance;
                moveData.endPosition.y = speaker.position.y;
            }
            else
            {
                moveData.endPosition = moveData.startPosition;
            }
        }
        else
        {
            moveData.endPosition = moveData.startPosition;
        }

        return moveData;
    }

    private CinemachineCamera CreateShotCamera(Beat beat, PlannedShot shot)
    {
        GameObject camObject = new GameObject("VCam_" + beat.beat_id);

        if (generatedCameraRoot != null)
            camObject.transform.SetParent(generatedCameraRoot);

        spawnedCameraObjects.Add(camObject);

        CinemachineCamera cam = camObject.AddComponent<CinemachineCamera>();

        cam.Follow = shot.followTarget;
        cam.LookAt = shot.lookTarget;

        if (shot.useExactPosition)
        {
            camObject.transform.position = shot.exactPosition;
            Debug.Log($"📷 VCam_{beat.beat_id} placed at AI EXACT world pos {shot.exactPosition}");
        }
        else if (shot.followTarget != null)
        {
            Quaternion facingRot = shot.followTarget.rotation;
            camObject.transform.position = shot.followTarget.position + facingRot * shot.offset;
            Debug.Log($"📷 VCam_{beat.beat_id} placed at offset {shot.offset} from {shot.followTarget.name}");
        }

        if (beat.use_exact_camera_rotation)
        {
            camObject.transform.rotation = Quaternion.Euler(
                beat.camera_rotation_x,
                beat.camera_rotation_y,
                beat.camera_rotation_z
            );

            Debug.Log($"🎬 VCam_{beat.beat_id} rotation set from AI: {camObject.transform.rotation.eulerAngles}");
        }
        else if (shot.lookTarget != null)
        {
            camObject.transform.LookAt(shot.lookTarget);
        }

        bool exactStaticCamera =
            shot.useExactPosition &&
            beat.use_exact_camera_rotation &&
            shot.movementType == "static";

        if (exactStaticCamera)
        {
            cam.Lens.FieldOfView = shot.fov;
            Debug.Log($"🎥 VCam_{beat.beat_id} using pure AI exact static transform.");
            return cam;
        }

        if (shot.movementType == "static" || shot.movementType == "follow")
        {
            if (shot.followTarget != null)
            {
                var follow = camObject.AddComponent<CinemachineFollow>();
                follow.FollowOffset = shot.offset;
            }

            var composer = camObject.AddComponent<CinemachineRotationComposer>();
            composer.Composition.ScreenPosition = new Vector2(0.5f, 0.45f);
        }
        else
        {
            if (shot.lookTarget == null && shot.followTarget == null)
            {
                Debug.LogWarning($"⚠️ VCam_{beat.beat_id}: no follow/look target found. Skipping motion extension.");
                cam.Lens.FieldOfView = shot.fov;
                return cam;
            }

            var ext = camObject.AddComponent<CinemachineMotionExtension>();

            Transform orbitTarget = shot.followTarget != null ? shot.followTarget : shot.lookTarget;
            ext.target = orbitTarget;
            ext.lookTarget = shot.lookTarget;

            if (shot.movementType == "orbit")
            {
                SetupOrbitMotion(ext, beat, shot);
            }
            else
            {
                ext.useWorldAnchor = false;

                if (shot.followTarget != null)
                    ext.initialOffset = shot.followTarget.rotation * shot.offset;
                else
                    ext.initialOffset = shot.offset;
            }

            ext.motionType = MapMotionType(shot.movementType);

            if (ext.target == null && !ext.useWorldAnchor && ext.lookTarget == null)
                Debug.LogWarning($"⚠️ VCam_{beat.beat_id}: motion target is null! Set focus_target.");

            if (shot.orbitSpeedOverride > 0)
                ext.orbitSpeed = shot.orbitSpeedOverride;

            if (shot.dollySpeedOverride > 0)
                ext.dollySpeed = shot.dollySpeedOverride;

            if (shot.panSpeedOverride > 0)
                ext.panSpeed = shot.panSpeedOverride;

            if (shot.movementType == "pan" && shot.followTarget != null)
            {
                Vector3 camToTarget = camObject.transform.position - shot.followTarget.position;
                camToTarget.y = 0;

                ext.panRadius = Mathf.Max(camToTarget.magnitude, 0.5f);
                ext.initialOffset = camObject.transform.position - shot.followTarget.position;

                Debug.Log($"🎥 VCam_{beat.beat_id} PAN radius={ext.panRadius:F2} seeded from spawn pos");
            }
        }

        cam.Lens.FieldOfView = shot.fov;
        return cam;
    }

    private void SetupOrbitMotion(CinemachineMotionExtension ext, Beat beat, PlannedShot shot)
    {
        if (shot.useExactPosition && shot.lookTarget != null)
        {
            ext.useWorldAnchor = true;
            ext.worldAnchor = shot.lookTarget.position;

            Vector3 toCam = shot.exactPosition - shot.lookTarget.position;
            float horizDist = new Vector2(toCam.x, toCam.z).magnitude;

            ext.orbitRadius = horizDist > 0.1f ? horizDist : 15f;
            ext.initialOffset = new Vector3(0f, toCam.y, 0f);
        }
        else
        {
            Transform orbitTarget = shot.followTarget != null ? shot.followTarget : shot.lookTarget;

            if (orbitTarget != null)
            {
                ext.useWorldAnchor = false;

                float xzRadius = new Vector2(shot.offset.x, shot.offset.z).magnitude;
                ext.orbitRadius = xzRadius > 0.1f ? xzRadius : 15f;
                ext.initialOffset = new Vector3(0f, shot.offset.y, 0f);
            }
        }

        Debug.Log($"🌀 VCam_{beat.beat_id} ORBIT radius={ext.orbitRadius} anchor={ext.worldAnchor} height={ext.initialOffset.y}");
    }

    private CinemachineMotionExtension.MotionType MapMotionType(string movement)
    {
        switch (movement)
        {
            case "orbit":
                return CinemachineMotionExtension.MotionType.Orbit;

            case "dolly_in":
                return CinemachineMotionExtension.MotionType.DollyIn;

            case "dolly_out":
                return CinemachineMotionExtension.MotionType.DollyOut;

            case "follow":
                return CinemachineMotionExtension.MotionType.Follow;

            case "pan":
                return CinemachineMotionExtension.MotionType.Pan;

            default:
                return CinemachineMotionExtension.MotionType.Static;
        }
    }

    private void PrePositionActor(
        Beat beat,
        Transform actor,
        Transform focus,
        Dictionary<string, Vector3> actorPositions)
    {
        string key = actor.name;

        if (actorPositions.ContainsKey(key))
            actor.position = actorPositions[key];

        if (beat.use_char_end_position)
        {
            actor.position = new Vector3(
                beat.char_end_x,
                beat.char_end_y,
                beat.char_end_z
            );
        }
        else if (beat.use_char_start_position)
        {
            actor.position = new Vector3(
                beat.char_start_x,
                beat.char_start_y,
                beat.char_start_z
            );
        }
        else if (focus != null)
        {
            Vector3 dir = (actor.position - focus.position).normalized;

            if (dir == Vector3.zero)
                dir = Vector3.forward;

            Vector3 dest = focus.position + dir * walkStopDistance;
            dest.y = actor.position.y;
            actor.position = dest;
        }

        if (beat.use_char_facing)
        {
            actor.rotation = Quaternion.Euler(0, beat.char_facing_y, 0);
        }
        else if (focus != null)
        {
            Vector3 lookDir = focus.position - actor.position;
            lookDir.y = 0;

            if (lookDir != Vector3.zero)
                actor.rotation = Quaternion.LookRotation(lookDir);
        }

        actorPositions[key] = actor.position;
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
            Debug.LogError("❌ Missing references on CutsceneCompiler!");
            return false;
        }

        return true;
    }

    private bool IsEnvironmentBeat(Beat beat)
    {
        return !string.IsNullOrEmpty(beat.speaker) &&
               beat.speaker.ToUpper() == "ENVIRONMENT";
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

            if (actor == null)
                continue;

            Animator anim = actor.GetComponent<Animator>();

            if (anim != null)
                playableDirector.SetGenericBinding(kvp.Value, anim);
        }

        foreach (var kvp in activationTracks)
        {
            GameObject actor = GameObject.Find(kvp.Key);

            if (actor == null)
                continue;

            playableDirector.SetGenericBinding(kvp.Value, actor);
        }
    }
}