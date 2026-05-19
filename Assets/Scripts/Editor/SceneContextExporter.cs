using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class SceneContextExporter : EditorWindow
{
    private string outputPath = "ai_director/scene_context.generated.json";

    [Header("Export Filters")]
    private bool includeInactiveObjects = false;
    private bool exportOnlyNamedSceneObjects = false;
    private string optionalCharacterNamesCsv = "";
    private string optionalObjectNamesCsv = "";

    [MenuItem("CineAI/Export Scene Context")]
    public static void ShowWindow()
    {
        GetWindow<SceneContextExporter>("Scene Context Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("CineAI Universal Scene Context Exporter", EditorStyles.boldLabel);

        outputPath = EditorGUILayout.TextField("Output Path", outputPath);

        GUILayout.Space(8);
        includeInactiveObjects = EditorGUILayout.Toggle("Include Inactive Objects", includeInactiveObjects);
        exportOnlyNamedSceneObjects = EditorGUILayout.Toggle("Only Export Listed Names", exportOnlyNamedSceneObjects);

        GUILayout.Space(8);
        GUILayout.Label("Optional Filters", EditorStyles.boldLabel);
        optionalCharacterNamesCsv = EditorGUILayout.TextField("Character Names", optionalCharacterNamesCsv);
        optionalObjectNamesCsv = EditorGUILayout.TextField("Object Names", optionalObjectNamesCsv);

        GUILayout.Space(12);

        if (GUILayout.Button("Export Universal Scene Context"))
            Export();
    }

    private void Export()
    {
        SceneContextData data = new SceneContextData();

        Scene activeScene = SceneManager.GetActiveScene();
        data.scene_name = activeScene.name;

        List<string> requestedCharacters = SplitCsv(optionalCharacterNamesCsv);
        List<string> requestedObjects = SplitCsv(optionalObjectNamesCsv);

        GameObject[] allObjects = GetSceneObjects(includeInactiveObjects);

        foreach (GameObject obj in allObjects)
        {
            if (obj == null)
                continue;

            if (!obj.scene.IsValid())
                continue;

            if (IsEditorOnly(obj))
                continue;

            SceneEntity entity = BuildEntity(obj);

            if (entity == null)
                continue;

            bool namedAsCharacter = requestedCharacters.Exists(n => NamesMatch(n, obj.name));
            bool namedAsObject = requestedObjects.Exists(n => NamesMatch(n, obj.name));

            bool detectedCharacter = IsLikelyCharacter(obj);
            bool detectedEnvironment = IsLikelyEnvironment(obj);
            bool detectedCamera = obj.GetComponentInChildren<Camera>() != null;
            bool detectedLight = obj.GetComponentInChildren<Light>() != null;

            if (exportOnlyNamedSceneObjects)
            {
                if (namedAsCharacter)
                    data.characters.Add(entity);
                else if (namedAsObject)
                    data.objects.Add(entity);

                continue;
            }

            if (namedAsCharacter || detectedCharacter)
            {
                data.characters.Add(entity);
            }
            else if (detectedEnvironment)
            {
                data.environment_surfaces.Add(entity);
            }
            else if (!detectedCamera && !detectedLight)
            {
                data.objects.Add(entity);
            }
        }

        data.characters = RemoveNestedDuplicates(data.characters);
        data.objects = RemoveNestedDuplicates(data.objects);
        data.environment_surfaces = RemoveNestedDuplicates(data.environment_surfaces);

        data.scene_bounds = CalculateSceneBounds(data);
        data.available_animations = FindAnimationClipNames();
        data.ground_samples = BuildGroundSamples(data);

        data.character_names = ExtractNames(data.characters);
        data.object_names = ExtractNames(data.objects);
        data.environment_surface_names = ExtractNames(data.environment_surfaces);
        data.locations = new List<string> { activeScene.name };

        WriteJson(data);

        Debug.Log($"Scene context exported to: {outputPath}");
        Debug.Log($"Characters exported: {data.characters.Count}");
        Debug.Log($"Objects exported: {data.objects.Count}");
        Debug.Log($"Environment surfaces exported: {data.environment_surfaces.Count}");
        Debug.Log($"Ground samples exported: {data.ground_samples.Count}");
    }

    private SceneEntity BuildEntity(GameObject obj)
    {
        BoundsInfo boundsInfo = CalculateBounds(obj);

        SceneEntity entity = new SceneEntity();
        entity.name = obj.name;
        entity.path = GetHierarchyPath(obj.transform);
        entity.tag = obj.tag;
        entity.layer = LayerMask.LayerToName(obj.layer);

        entity.transform_position = ToVector(obj.transform.position);
        entity.transform_rotation_euler = ToVector(obj.transform.eulerAngles);
        entity.transform_forward = ToVector(obj.transform.forward);

        entity.has_renderer = boundsInfo.hasRenderer;
        entity.has_collider = boundsInfo.hasCollider;
        entity.has_animator = obj.GetComponentInChildren<Animator>() != null;
        entity.has_navmesh_agent = obj.GetComponentInChildren<NavMeshAgent>() != null;

        entity.bounds_center = ToVector(boundsInfo.center);
        entity.bounds_size = ToVector(boundsInfo.size);
        entity.bounds_min = ToVector(boundsInfo.min);
        entity.bounds_max = ToVector(boundsInfo.max);

        entity.visual_position = ToVector(boundsInfo.hasAnyBounds ? boundsInfo.center : obj.transform.position);
        entity.bottom_center = ToVector(new Vector3(boundsInfo.center.x, boundsInfo.min.y, boundsInfo.center.z));
        entity.top_center = ToVector(new Vector3(boundsInfo.center.x, boundsInfo.max.y, boundsInfo.center.z));

        entity.ground_position = FindGroundBelow(boundsInfo.hasAnyBounds ? boundsInfo.center : obj.transform.position, obj.transform);
        entity.estimated_feet_position = entity.ground_position != null
            ? entity.ground_position
            : entity.bottom_center;

        entity.estimated_body_position = ToVector(Vector3.Lerp(
            FromVector(entity.bottom_center),
            FromVector(entity.top_center),
            0.5f
        ));

        entity.estimated_head_position = ToVector(Vector3.Lerp(
            FromVector(entity.bottom_center),
            FromVector(entity.top_center),
            0.9f
        ));

        entity.available_animation_clips = FindAnimatorClipNames(obj);

        return entity;
    }

    private static bool IsLikelyCharacter(GameObject obj)
    {
        if (obj.GetComponentInChildren<Animator>() != null)
            return true;

        string n = NormalizeName(obj.name);
        return n.Contains("character") || n.Contains("player") || n.Contains("npc") || n.Contains("hero");
    }

    private static bool IsLikelyEnvironment(GameObject obj)
    {
        if (obj.GetComponent<Terrain>() != null)
            return true;

        if (obj.GetComponent<TerrainCollider>() != null)
            return true;

        string n = NormalizeName(obj.name);
        return n.Contains("terrain") || n.Contains("ground") || n.Contains("floor") || n.Contains("landscape");
    }

    private static bool IsEditorOnly(GameObject obj)
    {
        return obj.CompareTag("EditorOnly");
    }

    private static BoundsInfo CalculateBounds(GameObject obj)
    {
        BoundsInfo info = new BoundsInfo();

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null)
                continue;

            if (!info.hasAnyBounds)
            {
                info.bounds = r.bounds;
                info.hasAnyBounds = true;
            }
            else
            {
                info.bounds.Encapsulate(r.bounds);
            }

            info.hasRenderer = true;
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders)
        {
            if (c == null)
                continue;

            if (!info.hasAnyBounds)
            {
                info.bounds = c.bounds;
                info.hasAnyBounds = true;
            }
            else
            {
                info.bounds.Encapsulate(c.bounds);
            }

            info.hasCollider = true;
        }

        if (!info.hasAnyBounds)
            info.bounds = new Bounds(obj.transform.position, Vector3.zero);

        return info;
    }

    private static SceneVector3 FindGroundBelow(Vector3 position, Transform self)
    {
        float rayHeight = Mathf.Max(CalculateObjectHeight(self.gameObject), Mathf.Epsilon);
        Vector3 rayStart = position + Vector3.up * rayHeight;
        float rayDistance = rayHeight * 3.0f;

        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, rayDistance, ~0);

        float bestY = float.NegativeInfinity;
        bool found = false;
        Vector3 bestPoint = position;

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == self || hit.transform.IsChildOf(self))
                continue;

            if (hit.point.y > position.y)
                continue;

            if (hit.point.y > bestY)
            {
                bestY = hit.point.y;
                bestPoint = hit.point;
                found = true;
            }
        }

        if (!found)
            return null;

        return ToVector(bestPoint);
    }

    private static float CalculateObjectHeight(GameObject obj)
    {
        BoundsInfo bounds = CalculateBounds(obj);
        return Mathf.Max(bounds.size.y, Mathf.Epsilon);
    }

    private List<GroundSample> BuildGroundSamples(SceneContextData data)
    {
        List<GroundSample> samples = new List<GroundSample>();

        foreach (SceneEntity obj in data.objects)
        {
            SceneVector3 visual = obj.visual_position;
            SceneVector3 ground = FindGroundBelow(FromVector(visual), FindTransformByPath(obj.path));

            if (ground == null)
                continue;

            GroundSample sample = new GroundSample();
            sample.near_entity = obj.name;
            sample.world_position = ground;
            samples.Add(sample);
        }

        foreach (SceneEntity character in data.characters)
        {
            SceneVector3 visual = character.visual_position;
            SceneVector3 ground = FindGroundBelow(FromVector(visual), FindTransformByPath(character.path));

            if (ground == null)
                continue;

            GroundSample sample = new GroundSample();
            sample.near_entity = character.name;
            sample.world_position = ground;
            samples.Add(sample);
        }

        return samples;
    }

    private static Transform FindTransformByPath(string path)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj == null)
                continue;

            if (!obj.scene.IsValid())
                continue;

            if (GetHierarchyPath(obj.transform) == path)
                return obj.transform;
        }

        return null;
    }

    private static SceneBounds CalculateSceneBounds(SceneContextData data)
    {
        bool found = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (SceneEntity entity in data.characters)
            IncludeEntityBounds(entity, ref bounds, ref found);

        foreach (SceneEntity entity in data.objects)
            IncludeEntityBounds(entity, ref bounds, ref found);

        foreach (SceneEntity entity in data.environment_surfaces)
            IncludeEntityBounds(entity, ref bounds, ref found);

        SceneBounds sceneBounds = new SceneBounds();
        sceneBounds.center = ToVector(bounds.center);
        sceneBounds.size = ToVector(bounds.size);
        sceneBounds.min = ToVector(bounds.min);
        sceneBounds.max = ToVector(bounds.max);
        return sceneBounds;
    }

    private static void IncludeEntityBounds(SceneEntity entity, ref Bounds bounds, ref bool found)
    {
        Vector3 center = FromVector(entity.bounds_center);
        Vector3 size = FromVector(entity.bounds_size);

        if (!found)
        {
            bounds = new Bounds(center, size);
            found = true;
        }
        else
        {
            bounds.Encapsulate(new Bounds(center, size));
        }
    }

    private static List<SceneEntity> RemoveNestedDuplicates(List<SceneEntity> input)
    {
        List<SceneEntity> output = new List<SceneEntity>();

        foreach (SceneEntity entity in input)
        {
            bool parentAlreadyIncluded = output.Exists(existing =>
                entity.path.StartsWith(existing.path + "/")
            );

            if (!parentAlreadyIncluded)
                output.Add(entity);
        }

        return output;
    }

    private static List<string> ExtractNames(List<SceneEntity> entities)
    {
        List<string> names = new List<string>();

        foreach (SceneEntity entity in entities)
        {
            if (!names.Contains(entity.name))
                names.Add(entity.name);
        }

        return names;
    }

    private static string[] FindAnimationClipNames()
    {
        List<string> names = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip != null && !names.Contains(clip.name))
                names.Add(clip.name);
        }

        return names.ToArray();
    }

    private static List<string> FindAnimatorClipNames(GameObject obj)
    {
        List<string> clips = new List<string>();
        Animator animator = obj.GetComponentInChildren<Animator>();

        if (animator == null || animator.runtimeAnimatorController == null)
            return clips;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && !clips.Contains(clip.name))
                clips.Add(clip.name);
        }

        return clips;
    }

    private void WriteJson(SceneContextData data)
    {
        string json = JsonUtility.ToJson(data, true);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.Combine(projectRoot, outputPath);
        string directory = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, json);
        AssetDatabase.Refresh();
    }

    private static GameObject[] GetSceneObjects(bool includeInactive)
    {
        if (includeInactive)
            return Resources.FindObjectsOfTypeAll<GameObject>();

        return UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
    }

    private static List<string> SplitCsv(string csv)
    {
        List<string> result = new List<string>();

        if (string.IsNullOrWhiteSpace(csv))
            return result;

        string[] parts = csv.Split(',');

        foreach (string p in parts)
        {
            string clean = p.Trim();
            if (!string.IsNullOrEmpty(clean))
                result.Add(clean);
        }

        return result;
    }

    private static bool NamesMatch(string a, string b)
    {
        return NormalizeName(a) == NormalizeName(b);
    }

    private static string NormalizeName(string value)
    {
        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace(" ", "")
            .Replace("-", "");
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "";

        string path = transform.name;

        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private static SceneVector3 ToVector(Vector3 v)
    {
        return new SceneVector3
        {
            x = v.x,
            y = v.y,
            z = v.z
        };
    }

    private static Vector3 FromVector(SceneVector3 v)
    {
        if (v == null)
            return Vector3.zero;

        return new Vector3(v.x, v.y, v.z);
    }

    private class BoundsInfo
    {
        public Bounds bounds;
        public bool hasAnyBounds;
        public bool hasRenderer;
        public bool hasCollider;

        public Vector3 center => bounds.center;
        public Vector3 size => bounds.size;
        public Vector3 min => bounds.min;
        public Vector3 max => bounds.max;
    }
}

[Serializable]
public class SceneContextData
{
    public string scene_name;

    public List<string> character_names = new List<string>();
    public List<string> object_names = new List<string>();
    public List<string> environment_surface_names = new List<string>();
    public List<string> locations = new List<string>();

    public List<SceneEntity> characters = new List<SceneEntity>();
    public List<SceneEntity> objects = new List<SceneEntity>();
    public List<SceneEntity> environment_surfaces = new List<SceneEntity>();

    public SceneBounds scene_bounds;
    public List<GroundSample> ground_samples = new List<GroundSample>();

    public string[] available_animations;
}

[Serializable]
public class SceneEntity
{
    public string name;
    public string path;
    public string tag;
    public string layer;

    public SceneVector3 transform_position;
    public SceneVector3 transform_rotation_euler;
    public SceneVector3 transform_forward;

    public bool has_renderer;
    public bool has_collider;
    public bool has_animator;
    public bool has_navmesh_agent;

    public SceneVector3 bounds_center;
    public SceneVector3 bounds_size;
    public SceneVector3 bounds_min;
    public SceneVector3 bounds_max;

    public SceneVector3 visual_position;
    public SceneVector3 bottom_center;
    public SceneVector3 top_center;

    public SceneVector3 ground_position;
    public SceneVector3 estimated_feet_position;
    public SceneVector3 estimated_body_position;
    public SceneVector3 estimated_head_position;

    public List<string> available_animation_clips = new List<string>();
}

[Serializable]
public class SceneBounds
{
    public SceneVector3 center;
    public SceneVector3 size;
    public SceneVector3 min;
    public SceneVector3 max;
}

[Serializable]
public class GroundSample
{
    public string near_entity;
    public SceneVector3 world_position;
}

[Serializable]
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