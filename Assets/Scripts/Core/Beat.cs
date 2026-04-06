using System;

[Serializable]
public class Beat
{
    public int scene_id;
    public int beat_id;

    public string speaker;
    public string dialogue;
    public string action;

    public string emotion;
    public string intent;

    public string shot_type;
    public string camera_angle;
    public string camera_movement;

    public string focus_target;
    public string secondary_target;

    public float duration;
    public string transition;
}