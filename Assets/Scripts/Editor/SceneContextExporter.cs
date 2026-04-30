using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class SceneContextExporter : EditorWindow
{
    private string outputPath = "ai_director/scene_context.generated.json";

    private string charactersCsv = "UNCLE_BEN";
    private string objectsCsv = "ROCK";
    private string locationsCsv = "DefaultLocation";

    private float approachStopDistance = 2.0f;
    private float approachMiddleDistance = 6.0f;
    private float approachStartDistance = 12.0f;

    [MenuItem("CineAI/Export Scene Context")]
    public static void ShowWindow()
    {
        GetWindow<SceneContextExporter>("Scene Context Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("CineAI Scene Context Exporter", EditorStyles.boldLabel);

        outputPath = EditorGUILayout.TextField("Output Path", outputPath);

        GUILayout.Space(10);
        GUILayout.Label("Scene Names", EditorStyles.boldLabel);

        charactersCsv = EditorGUILayout.TextField("Characters", charactersCsv);
        objectsCsv = EditorGUILayout.TextField("Objects", objectsCsv);
        locationsCsv = EditorGUILayout.TextField("Locations", locationsCsv);

        GUILayout.Space(10);
        GUILayout.Label("Approach Blocking", EditorStyles.boldLabel);

        approachStopDistance = EditorGUILayout.FloatField("Stop Distance", approachStopDistance);
        approachMiddleDistance = EditorGUILayout.FloatField("Middle Distance", approachMiddleDistance);
        approachStartDistance = EditorGUILayout.FloatField("Start Distance", approachStartDistance);

        GUILayout.Space(15);

        if (GUILayout.Button("Export Scene Context"))
        {
            Export();
        }
    }

    private void Export()
    {
        string[] characters = SplitCsv(charactersCsv);
        string[] objects = SplitCsv(objectsCsv);
        string[] locations = SplitCsv(locationsCsv);

        if (characters.Length == 0)
        {
            Debug.LogError("❌ No characters provided.");
            return;
        }

        if (objects.Length == 0)
        {
            Debug.LogError("❌ No objects provided.");
            return;
        }

        GameObject characterObj = FindSceneObjectFlexible(characters[0]);
        GameObject objectObj = FindSceneObjectFlexible(objects[0]);

        if (characterObj == null)
        {
            Debug.LogError($"❌ Character not found in scene: {characters[0]}");
            return;
        }

        if (objectObj == null)
        {
            Debug.LogError($"❌ Object not found in scene: {objects[0]}");
            return;
        }

        SceneContextData data = new SceneContextData();
        data.characters = characters;
        data.objects = objects;
        data.locations = locations;

        data.default_character_start = VectorFromTransform(characterObj.transform);
        data.default_object_position = VectorFromTransform(objectObj.transform);

        data.approach_stop_distance = approachStopDistance;
        data.approach_middle_distance = approachMiddleDistance;
        data.approach_start_distance = approachStartDistance;

        data.available_animations = FindAnimationClipNames();

        string json = JsonUtility.ToJson(data, true);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.Combine(projectRoot, outputPath);
        string directory = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, json);

        AssetDatabase.Refresh();

        Debug.Log($"✅ Scene context exported: {fullPath}");
        Debug.Log($"✅ Character match: requested='{characters[0]}' found='{characterObj.name}' position={characterObj.transform.position}");
        Debug.Log($"✅ Object match: requested='{objects[0]}' found='{objectObj.name}' position={objectObj.transform.position}");
    }

    private static GameObject FindSceneObjectFlexible(string requestedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
            return null;

        string requested = NormalizeName(requestedName);

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj == null)
                continue;

            if (!obj.scene.IsValid())
                continue;

            string candidate = NormalizeName(obj.name);

            if (candidate == requested)
            {
                Debug.Log($"✅ Exact scene object match: requested='{requestedName}' → found='{obj.name}'");
                return obj;
            }
        }

        foreach (GameObject obj in allObjects)
        {
            if (obj == null)
                continue;

            if (!obj.scene.IsValid())
                continue;

            string candidate = NormalizeName(obj.name);

            if (candidate.Contains(requested) || requested.Contains(candidate))
            {
                Debug.Log($"✅ Flexible scene object match: requested='{requestedName}' → found='{obj.name}'");
                return obj;
            }
        }

        Debug.LogWarning($"⚠ Could not find scene object for requested name: {requestedName}");
        return null;
    }

    private static string NormalizeName(string value)
    {
        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace(" ", "")
            .Replace("-", "");
    }

    private static SceneVector3 VectorFromTransform(Transform transform)
    {
        return new SceneVector3
        {
            x = transform.position.x,
            y = transform.position.y,
            z = transform.position.z
        };
    }

    private static string[] SplitCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new string[0];

        string[] raw = csv.Split(',');
        List<string> cleaned = new List<string>();

        foreach (string item in raw)
        {
            string trimmed = item.Trim();

            if (!string.IsNullOrEmpty(trimmed))
                cleaned.Add(trimmed);
        }

        return cleaned.ToArray();
    }

    private static string[] FindAnimationClipNames()
    {
        List<string> names = new List<string>();

        string[] guids = AssetDatabase.FindAssets(
            "t:AnimationClip",
            new[] { "Assets/Resources/Animations" }
        );

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip != null && !names.Contains(clip.name))
                names.Add(clip.name);
        }

        return names.ToArray();
    }
}

[System.Serializable]
public class SceneContextData
{
    public string[] characters;
    public string[] objects;
    public string[] locations;

    public SceneVector3 default_character_start;
    public SceneVector3 default_object_position;

    public float approach_stop_distance;
    public float approach_middle_distance;
    public float approach_start_distance;

    public string[] available_animations;
}

[System.Serializable]
public class SceneVector3
{
    public float x;
    public float y;
    public float z;

    public override string ToString()
    {
        return $"({x}, {y}, {z})";
    }
}