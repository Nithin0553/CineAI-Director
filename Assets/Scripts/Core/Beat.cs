using System;

[Serializable]
public class Beat
{
    // ── Identity ─────────────────────────────────────────────────────
    public int scene_id;
    public int beat_id;

    // ── Narrative ────────────────────────────────────────────────────
    public string speaker;
    public string dialogue;
    public string action;
    public string emotion;
    public string intent;

    // ── Shot (semantic / AI-inferred) ────────────────────────────────
    public string shot_type;
    public string camera_angle;
    public string camera_movement;
    public string focus_target;
    public string secondary_target;

    // ── Timing ───────────────────────────────────────────────────────
    public float duration;
    public string transition;

    // ════════════════════════════════════════════════════════════════
    // EXPLICIT OVERRIDE PARAMETERS
    // When supplied, these values are used DIRECTLY by the engine and
    // bypass the inferred defaults completely. Leave at 0 / empty to
    // keep the normal AI-driven behaviour.
    // ════════════════════════════════════════════════════════════════

    // ── Camera exact position (world space).
    //    If camera_position_x/y/z are all 0 the engine infers position
    //    from shot_type + offset logic as before.
    public float camera_position_x;
    public float camera_position_y;
    public float camera_position_z;
    public bool use_exact_camera_position;   // set true to activate

    // ── Camera offset relative to follow-target (overrides per-shot defaults)
    public float camera_offset_x;
    public float camera_offset_y;
    public float camera_offset_z;
    public bool use_exact_camera_offset;     // set true to activate

    // ── Field of view override (0 = use shot-type default)
    public float fov_override;

    // ── Motion speeds (0 = use built-in defaults)
    public float orbit_speed_override;
    public float dolly_speed_override;
    public float pan_speed_override;

    // ── Character start position (world space, applied before the beat)
    public float char_start_x;
    public float char_start_y;
    public float char_start_z;
    public bool use_char_start_position;     // set true to activate

    // ── Character end / destination position (world space)
    //    Used for walk/run beats instead of snapping to the focus target.
    public float char_end_x;
    public float char_end_y;
    public float char_end_z;
    public bool use_char_end_position;       // set true to activate

    // ── Character facing direction (euler Y in degrees, -1 = auto)
    public float char_facing_y;
    public bool use_char_facing;             // set true to activate
}