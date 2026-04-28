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

        // Bind Cinemachine track to brain
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

        var actorTracks = new Dictionary<string, AnimationTrack>();
        var activationTracks = new Dictionary<string, ActivationTrack>();
        var actorPositions = new Dictionary<string, Vector3>();

        // ── Build character move schedule for runtime mover ───────────
        var moveSchedule = new List<CharacterMoveData>();

        double currentTime = 0;

        foreach (Beat beat in beats)
        {
            Transform speaker = sceneBindingResolver.ResolveSpeaker(beat.speaker);
            Transform focus = sceneBindingResolver.ResolveFocus(beat.focus_target);

            // Pre-position actor in editor (for camera offset calculation)
            if (speaker != null && beat.speaker.ToUpper() != "ENVIRONMENT")
                PrePositionActor(beat, speaker, focus, actorPositions);

            PlannedShot shot = shotPlanner.PlanShot(beat, speaker, focus);
            PlannedAction action = actionPlanner.PlanAction(beat, speaker, focus);

            // Apply exact start position / facing immediately (editor-time snap)
            if (action.useExactStartPosition && speaker != null)
            {
                speaker.position = action.exactStartPosition;
                Debug.Log($"📍 Beat {beat.beat_id} → '{speaker.name}' snapped to {action.exactStartPosition}");
            }
            if (action.useExactFacing && speaker != null)
                speaker.rotation = Quaternion.Euler(0, action.exactFacingY, 0);

            // ── Build move entry for this beat ────────────────────────
            if (speaker != null && beat.speaker.ToUpper() != "ENVIRONMENT")
            {
                // FIX #3: Use the explicit JSON flag instead of fragile text parsing.
                // beat.use_char_end_position = true means the beat has a destination
                // and the character should physically move there.
                bool isWalking = beat.use_char_end_position;

                CharacterMoveData moveData = new CharacterMoveData();
                moveData.characterName = speaker.name;
                moveData.startTime = (float)currentTime;
                moveData.duration = beat.duration;
                moveData.facingY = action.useExactFacing ? action.exactFacingY : -1f;
                moveData.shouldMove = isWalking;

                if (action.useExactStartPosition)
                    moveData.startPosition = action.exactStartPosition;
                else
                    moveData.startPosition = speaker.position;

                if (isWalking)
                {
                    if (action.useExactEndPosition)
                        moveData.endPosition = action.exactEndPosition;
                    else if (focus != null)
                    {
                        Vector3 dir = (speaker.position - focus.position).normalized;
                        if (dir == Vector3.zero) dir = Vector3.forward;
                        moveData.endPosition = focus.position + dir * walkStopDistance;
                        moveData.endPosition.y = speaker.position.y;
                    }
                    else
                        moveData.endPosition = moveData.startPosition;
                }
                else
                {
                    moveData.endPosition = moveData.startPosition;
                }

                moveSchedule.Add(moveData);
            }

            // ── Build camera ──────────────────────────────────────────
            CinemachineCamera cam = CreateShotCamera(beat, shot);

            timelineBuilder.AddCinemachineShotClip(
                cineTrack, playableDirector,
                currentTime, beat.duration,
                "Shot_" + beat.beat_id, cam);

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

        // ── Attach / update CutsceneCharacterMover ────────────────────
        CutsceneCharacterMover mover =
            playableDirector.gameObject.GetComponent<CutsceneCharacterMover>();
        if (mover == null)
            mover = playableDirector.gameObject.AddComponent<CutsceneCharacterMover>();

        mover.playableDirector = playableDirector;
        mover.moveSchedule = moveSchedule;

        // FIX #1: Immediately disable root motion now that schedule is built
        mover.DisableRootMotionOnAllActors();

        Debug.Log($"✅ CUTSCENE GENERATED — {moveSchedule.Count} character move entries");
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

        // ── Snap to initial world position ────────────────────────────
        if (shot.useExactPosition)
        {
            // FIX: For exact position beats, place camera directly at the
            // specified world position. Do NOT add any offset on top of it.
            camObject.transform.position = shot.exactPosition;
            Debug.Log($"📷 VCam_{beat.beat_id} placed at EXACT world pos {shot.exactPosition}");
        }
        else if (shot.followTarget != null)
        {
            // FIX #2: Rotate offset by the character's facing direction so that
            // camera_offset_z = -3.5 means "behind the character" regardless
            // of which way they face. World-space addition was placing cameras
            // in completely wrong positions when char_facing_y != 0.
            Quaternion facingRot = shot.followTarget.rotation;
            camObject.transform.position = shot.followTarget.position + facingRot * shot.offset;
            Debug.Log($"📷 VCam_{beat.beat_id} placed at offset {shot.offset} from {shot.followTarget.name}");
        }

        if (shot.lookTarget != null)
            camObject.transform.LookAt(shot.lookTarget);

        if (shot.movementType == "static" || shot.movementType == "follow")
        {
            var follow = camObject.AddComponent<CinemachineFollow>();

            // FIX (Beat 3): CinemachineFollow.FollowOffset is interpreted in
            // TARGET-LOCAL space by the default LockToTargetWithWorldUp binding.
            // The JSON defines offsets in character-local space (e.g. offset_z=+5.5
            // means "5.5 units in front of the character"). This is exactly what
            // Cinemachine expects, so we assign shot.offset directly — no rotation
            // needed. The initial camera spawn (line above) applies facingRot to get
            // the correct WORLD position, but the Follow component independently
            // re-derives world pos as followTarget.position + followTarget.rotation * FollowOffset
            // each frame, which gives the same result. Both operations are consistent.
            //
            // However: for TIER 2 beats that use use_exact_camera_offset the offset
            // was authored relative to character-local axes. If the character has
            // char_facing_y=180, local +Z is world -Z, so offset_z=+5.5 (local-front)
            // IS world -Z (correct). Cinemachine also interprets +5.5 on Z as
            // local-forward of the target, which at facing=180 is world -Z. So the
            // offset stays correct as-is.
            follow.FollowOffset = shot.offset;

            var composer = camObject.AddComponent<CinemachineRotationComposer>();
            composer.Composition.ScreenPosition = new Vector2(0.5f, 0.45f);
        }
        else
        {
            var ext = camObject.AddComponent<CinemachineMotionExtension>();

            Transform orbitTarget = shot.followTarget != null ? shot.followTarget : shot.lookTarget;
            ext.target = orbitTarget;

            // FIX: Wire the explicit look-at so OTS/reveal pans (camera
            // pivots around the character but aims at a different focus,
            // e.g. the rock) actually look at the focus.
            ext.lookTarget = shot.lookTarget;

            if (shot.movementType == "orbit")
            {
                // Aerial / TIER 1 exact-position orbit around lookTarget
                if (shot.useExactPosition && shot.lookTarget != null)
                {
                    ext.useWorldAnchor = true;
                    ext.worldAnchor = shot.lookTarget.position;

                    // FIX: Derive the actual orbit radius from where the
                    // camera was placed (exactPosition vs anchor) instead
                    // of the hardcoded 15 units. The old hardcoded value
                    // caused the camera to teleport to a 15-unit-radius
                    // ring on the first frame regardless of where the
                    // user positioned it.
                    Vector3 toCam = shot.exactPosition - shot.lookTarget.position;
                    float horizDist = new Vector2(toCam.x, toCam.z).magnitude;
                    ext.orbitRadius = horizDist > 0.1f ? horizDist : 15f;
                    ext.initialOffset = new Vector3(0f, toCam.y, 0f);
                }
                else if (orbitTarget != null)
                {
                    ext.useWorldAnchor = false;

                    // shot.offset is in TARGET-LOCAL space; XZ magnitude is
                    // rotation-invariant, so we can use it directly as the
                    // orbit radius without converting to world coordinates.
                    float xzRadius = new Vector2(shot.offset.x, shot.offset.z).magnitude;
                    ext.orbitRadius = xzRadius > 0.1f ? xzRadius : 15f;
                    ext.initialOffset = new Vector3(0f, shot.offset.y, 0f);
                }

                Debug.Log($"🌀 VCam_{beat.beat_id} ORBIT radius={ext.orbitRadius} anchor={ext.worldAnchor} height={ext.initialOffset.y}");
            }
            else
            {
                ext.useWorldAnchor = false;

                // FIX: shot.offset is in TARGET-LOCAL space, but DoDolly /
                // DoFollow / DoPan all compute `target.position + initialOffset`
                // which expects WORLD-space. Convert here by rotating into
                // the follow target's frame so the motion plays out at the
                // correct world position regardless of character facing.
                if (shot.followTarget != null)
                    ext.initialOffset = shot.followTarget.rotation * shot.offset;
                else
                    ext.initialOffset = shot.offset;
            }

            ext.motionType = MapMotionType(shot.movementType);

            if (ext.target == null && !ext.useWorldAnchor && ext.lookTarget == null)
                Debug.LogWarning($"⚠️ VCam_{beat.beat_id}: motion target is null! Set focus_target.");

            if (shot.orbitSpeedOverride > 0) ext.orbitSpeed = shot.orbitSpeedOverride;
            if (shot.dollySpeedOverride > 0) ext.dollySpeed = shot.dollySpeedOverride;
            if (shot.panSpeedOverride > 0) ext.panSpeed = shot.panSpeedOverride;

            // FIX (Beat 4): Set panRadius = horizontal distance from the spawned
            // camera position to the follow target. This gives DoPan a natural
            // arc radius that matches where the camera actually starts, rather than
            // deriving it from the XZ magnitude of initialOffset (which is nearly 0
            // for behind-shoulder shots like beat 4).
            if (shot.movementType == "pan" && shot.followTarget != null)
            {
                Vector3 camToTarget = camObject.transform.position - shot.followTarget.position;
                camToTarget.y = 0;
                ext.panRadius = Mathf.Max(camToTarget.magnitude, 0.5f);
                // Seed the pan angle from the camera's actual position so it
                // starts exactly where it spawned (no snap on frame 1).
                Vector3 toCam = camObject.transform.position - shot.followTarget.position;
                // _orbitAngle is set in OnEnable from initialOffset, but we need to
                // re-seed from the WORLD position. Store the world-space initial
                // offset so OnEnable can read it.
                ext.initialOffset = camObject.transform.position - shot.followTarget.position;
                Debug.Log($"🎥 VCam_{beat.beat_id} PAN radius={ext.panRadius:F2} seeded from spawn pos");
            }
        }

        cam.Lens.FieldOfView = shot.fov;
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

    // ── Pre-position actors (editor-time, for camera offset calc) ─────
    private void PrePositionActor(
        Beat beat, Transform actor, Transform focus,
        Dictionary<string, Vector3> actorPositions)
    {
        string key = actor.name;
        if (actorPositions.ContainsKey(key))
            actor.position = actorPositions[key];

        // FIX #5 (also mirrors FIX #3): Use the reliable JSON flag
        // instead of fragile action text parsing for pre-positioning.
        // Previously a beat with use_char_end_position=true but without
        // "walk" in the action text would skip pre-positioning entirely,
        // causing the camera offset to be calculated from the wrong position.
        if (beat.use_char_end_position)
        {
            actor.position = new Vector3(beat.char_end_x, beat.char_end_y, beat.char_end_z);
        }
        else if (beat.use_char_start_position)
        {
            actor.position = new Vector3(beat.char_start_x, beat.char_start_y, beat.char_start_z);
        }
        else if (focus != null)
        {
            Vector3 dir = (actor.position - focus.position).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward;
            Vector3 dest = focus.position + dir * walkStopDistance;
            dest.y = actor.position.y;
            actor.position = dest;
        }

        if (beat.use_char_facing)
            actor.rotation = Quaternion.Euler(0, beat.char_facing_y, 0);
        else if (focus != null)
        {
            Vector3 lookDir = focus.position - actor.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                actor.rotation = Quaternion.LookRotation(lookDir);
        }

        actorPositions[key] = actor.position;
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
            Animator anim = actor.GetComponent<Animator>();
            if (anim != null)
                playableDirector.SetGenericBinding(kvp.Value, anim);
        }

        foreach (var kvp in activationTracks)
        {
            GameObject actor = GameObject.Find(kvp.Key);
            if (actor == null) continue;
            playableDirector.SetGenericBinding(kvp.Value, actor);
        }
    }
}