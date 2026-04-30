// Assets/Scripts/Timeline/TimelineBuilder.cs

using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TimelineBuilder : MonoBehaviour
{
    public string outputFolder = "Assets/GeneratedCutscenes";

    private AnimationLibraryResolver animationResolver;

    // ==============================
    // CREATE / LOAD TIMELINE
    // ==============================
    public TimelineAsset CreateTimelineAsset(string timelineName)
    {
#if UNITY_EDITOR
        EnsureFolderExists(outputFolder);

        string assetPath = Path.Combine(outputFolder, timelineName + ".playable").Replace("\\", "/");
        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);

        if (timeline == null)
        {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return timeline;
#else
        Debug.LogError("Timeline asset creation only works in the Unity Editor.");
        return null;
#endif
    }

    // ==============================
    // TRACK CREATION
    // ==============================
    public AnimationTrack CreateAnimationTrack(TimelineAsset timeline, string trackName)
    {
        return timeline.CreateTrack<AnimationTrack>(null, trackName);
    }

    public ActivationTrack CreateActivationTrack(TimelineAsset timeline, string trackName)
    {
        return timeline.CreateTrack<ActivationTrack>(null, trackName);
    }

    public SignalTrack CreateSignalTrack(TimelineAsset timeline, string trackName)
    {
        return timeline.CreateTrack<SignalTrack>(null, trackName);
    }

    public MarkerTrack CreateMarkerTrack(TimelineAsset timeline, string trackName)
    {
        return timeline.CreateTrack<MarkerTrack>(null, trackName);
    }

    public CinemachineTrack CreateCinemachineTrack(TimelineAsset timeline, string trackName)
    {
        return timeline.CreateTrack<CinemachineTrack>(null, trackName);
    }

    // ==============================
    // CLIP ADDERS
    // ==============================
    public void AddActivationClip(ActivationTrack track, double start, double duration)
    {
        TimelineClip clip = track.CreateDefaultClip();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = "Active";
    }

    public void AddAnimationClip(
        AnimationTrack track,
        double start,
        double duration,
        string animationName)
    {
        // Position movement is handled by CutsceneCharacterMover.
        // Timeline animation clips are used only for body/leg motion.
        track.trackOffset = TrackOffset.ApplyTransformOffsets;

        string safeAnimationName = string.IsNullOrWhiteSpace(animationName)
            ? "Idle"
            : animationName.Trim();

        TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = safeAnimationName;

        AnimationPlayableAsset asset = clip.asset as AnimationPlayableAsset;

        if (asset == null)
        {
            Debug.LogWarning($"⚠ Could not create AnimationPlayableAsset for '{safeAnimationName}'");
            return;
        }

        asset.applyFootIK = false;

        if (animationResolver == null)
            animationResolver = new AnimationLibraryResolver();

        AnimationClip anim = animationResolver.Resolve(safeAnimationName);

        if (anim == null)
        {
            Debug.LogWarning($"⚠ No animation clip assigned for '{safeAnimationName}'. Timeline clip will be empty.");
            return;
        }

        asset.clip = anim;
        Debug.Log($"✅ Animation clip assigned: requested='{safeAnimationName}' actual='{anim.name}'");
    }

    // ==============================
    // CINEMACHINE SHOTS
    // ==============================
    public CinemachineShot AddCinemachineShotClip(
        CinemachineTrack track,
        PlayableDirector director,
        double start,
        double duration,
        string clipName,
        CinemachineCamera cameraInstance)
    {
        TimelineClip clip = track.CreateClip<CinemachineShot>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = clipName;

        CinemachineShot shot = clip.asset as CinemachineShot;

        string guid = System.Guid.NewGuid().ToString();

        ExposedReference<CinemachineVirtualCameraBase> camRef =
            new ExposedReference<CinemachineVirtualCameraBase>();

        camRef.exposedName = guid;
        shot.VirtualCamera = camRef;

        director.SetReferenceValue(guid, cameraInstance);

        return shot;
    }

    // ==============================
    // SAVE TIMELINE
    // ==============================
    public void SaveTimeline(TimelineAsset timeline)
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif
    }

#if UNITY_EDITOR
    private void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
#endif
}