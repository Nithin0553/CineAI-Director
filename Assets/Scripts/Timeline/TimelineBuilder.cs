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

    // ✅ CREATE / LOAD TIMELINE
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

    // ✅ TRACK CREATION
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

    // ✅ CLIP ADDERS

    public void AddActivationClip(ActivationTrack track, double start, double duration)
    {
        var clip = track.CreateDefaultClip();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = "Active";
    }

    public void AddAnimationPlaceholderClip(AnimationTrack track, double start, double duration, string displayName)
    {
        var clip = track.CreateClip<AnimationPlayableAsset>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = displayName;
    }

    // ✅ 🔥 FIXED CINEMACHINE SHOT CREATION
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

        // ✅ Create unique reference ID
        string guid = System.Guid.NewGuid().ToString();

        // ✅ Use correct type for Timeline
        ExposedReference<CinemachineVirtualCameraBase> camRef =
            new ExposedReference<CinemachineVirtualCameraBase>();

        camRef.exposedName = guid;

        // ✅ Assign to shot
        shot.VirtualCamera = camRef;

        // ✅ Bind actual camera instance
        director.SetReferenceValue(guid, cameraInstance);

        return shot;
    }

    // ✅ SAVE TIMELINE
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