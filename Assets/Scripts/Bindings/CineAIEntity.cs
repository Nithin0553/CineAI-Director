using UnityEngine;

public enum CineAIEntityRole
{
    Character,
    Object,
    EnvironmentSurface,
    Location,
    Ignore
}

[DisallowMultipleComponent]
public class CineAIEntity : MonoBehaviour
{
    public CineAIEntityRole role = CineAIEntityRole.Object;

    [Tooltip("Optional story-facing name. If empty, GameObject.name is used.")]
    public string displayName = "";

    [Tooltip("If enabled, this entity can be used as a cinematic look/focus target.")]
    public bool canBeFocusTarget = true;

    [Tooltip("If enabled, this entity can be used for character movement destination planning.")]
    public bool canBeApproached = true;

    [Tooltip("If enabled, this entity can be used as a walkable/ground surface.")]
    public bool isWalkableSurface = false;

    public string ExportName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();

            return gameObject.name;
        }
    }
}