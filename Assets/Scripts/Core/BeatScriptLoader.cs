using UnityEngine;

public class BeatScriptLoader : MonoBehaviour
{
    public Beat[] LoadBeats(string fileName)
    {
        // Load JSON from Resources/BeatScripts/
        TextAsset jsonFile = Resources.Load<TextAsset>("BeatScripts/" + fileName);

        if (jsonFile == null)
        {
            Debug.LogError("❌ Beat file NOT found: " + fileName);
            return null;
        }

        BeatListWrapper wrapper = JsonUtility.FromJson<BeatListWrapper>(jsonFile.text);

        if (wrapper == null || wrapper.beats == null || wrapper.beats.Length == 0)
        {
            Debug.LogError("❌ Invalid or empty beat script!");
            return null;
        }

        // ✅ VALIDATION
        foreach (Beat beat in wrapper.beats)
        {
            if (!ValidateBeat(beat))
            {
                Debug.LogError($"❌ Beat validation failed at Beat ID: {beat.beat_id}");
                return null;
            }
        }

        Debug.Log($"✅ Loaded {wrapper.beats.Length} beats successfully");

        return wrapper.beats;
    }

    private bool ValidateBeat(Beat beat)
    {
        if (string.IsNullOrEmpty(beat.shot_type))
        {
            Debug.LogError("Missing shot_type");
            return false;
        }

        if (string.IsNullOrEmpty(beat.camera_movement))
        {
            Debug.LogError("Missing camera_movement");
            return false;
        }

        if (beat.duration <= 0)
        {
            Debug.LogError("Invalid duration");
            return false;
        }

        if (string.IsNullOrEmpty(beat.speaker))
        {
            Debug.LogError("Missing speaker");
            return false;
        }

        return true;
    }
}
