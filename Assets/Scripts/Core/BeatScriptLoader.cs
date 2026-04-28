using UnityEngine;

public class BeatScriptLoader : MonoBehaviour
{
    public Beat[] LoadBeats(string fileName)
    {
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
        // ── speaker is OPTIONAL for pure camera/environment beats ─────
        // An empty speaker means this is a camera-only shot (e.g. aerial
        // orbit around a rock) with no character active. This is valid.
        // Only validate speaker content if one is actually provided.
        bool hasSpeaker = !string.IsNullOrEmpty(beat.speaker);

        // ── duration is always required ───────────────────────────────
        if (beat.duration <= 0)
        {
            Debug.LogError($"Beat {beat.beat_id}: 'duration' must be > 0");
            return false;
        }

        // ── shot_type: required UNLESS exact camera position/offset given
        bool hasExactCamPos = beat.use_exact_camera_position;
        bool hasExactCamOffset = beat.use_exact_camera_offset;

        if (string.IsNullOrEmpty(beat.shot_type) && !hasExactCamPos && !hasExactCamOffset)
        {
            Debug.LogError($"Beat {beat.beat_id}: 'shot_type' is required when " +
                           "neither 'use_exact_camera_position' nor 'use_exact_camera_offset' is set");
            return false;
        }

        // ── camera_movement: required UNLESS exact position/offset given
        if (string.IsNullOrEmpty(beat.camera_movement) && !hasExactCamPos && !hasExactCamOffset)
        {
            Debug.LogError($"Beat {beat.beat_id}: 'camera_movement' is required when " +
                           "no exact camera override is set");
            return false;
        }

        // ── focus_target: warn if missing on a camera-only beat ───────
        if (!hasSpeaker && string.IsNullOrEmpty(beat.focus_target))
        {
            Debug.LogWarning($"Beat {beat.beat_id}: No speaker and no focus_target set. " +
                             "Camera will have no target to point at.");
        }

        // ── warn on suspicious zero positions ─────────────────────────
        if (beat.use_exact_camera_position &&
            beat.camera_position_x == 0 && beat.camera_position_y == 0 && beat.camera_position_z == 0)
        {
            Debug.LogWarning($"Beat {beat.beat_id}: 'use_exact_camera_position' is true but " +
                             "all position values are 0 — camera will be placed at world origin.");
        }

        if (beat.use_char_start_position &&
            beat.char_start_x == 0 && beat.char_start_y == 0 && beat.char_start_z == 0)
        {
            Debug.LogWarning($"Beat {beat.beat_id}: 'use_char_start_position' is true but " +
                             "all values are 0 — character will be moved to world origin.");
        }

        if (beat.fov_override < 0)
        {
            Debug.LogError($"Beat {beat.beat_id}: 'fov_override' cannot be negative");
            return false;
        }

        return true;
    }
}