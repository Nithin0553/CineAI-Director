// Assets/Scripts/Runtime/CutsceneRunner.cs
using UnityEngine;
using UnityEngine.Playables;

public class CutsceneRunner : MonoBehaviour
{
    public PlayableDirector playableDirector;
    public bool playOnStart = false;

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Play()
    {
        if (playableDirector == null)
        {
            Debug.LogError("PlayableDirector missing on CutsceneRunner.");
            return;
        }

        if (playableDirector.playableAsset == null)
        {
            Debug.LogError("No playable asset assigned to PlayableDirector.");
            return;
        }

        playableDirector.time = 0;
        playableDirector.Evaluate();
        playableDirector.Play();
        Debug.Log("🎬 Cutscene playback started.");
    }

    public void Stop()
    {
        if (playableDirector == null)
            return;

        playableDirector.Stop();
        Debug.Log("⏹️ Cutscene playback stopped.");
    }
}