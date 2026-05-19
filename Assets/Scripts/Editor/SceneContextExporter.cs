using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class SceneContextExporter : EditorWindow
{
    private string outputPath = "ai_director/scene_context.generated.json";

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
        GUILayout.Label("Optional Semantic Filters", EditorStyles.boldLabel);
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
        data.locations = new List<string> { activeScene.name };

        List<string> requestedCharacters = SplitCsv(optionalCharacterNamesCsv);
        List<string> requestedObjects = SplitCsv(optionalObjectNamesCsv);

        GameObject[] sceneObjects = GetSceneObjects(includeInactiveObjects);

        HashSet<Transform> characterRoots = FindCharacterRoots(sceneObjects, requestedCharacters);
        HashSet<Transform> characterHierarchy = BuildHierarchySet(characterRoots);

        foreach (Transform characterRoot in characterRoots)
        {
            if (characterRoot == null)
                continue;

            SceneEntity entity = BuildEntity(characterRoot.gameObject, EntityKind.Character, null);

            if (entity != null)
                data.characters.Add(entity);
        }

        foreach (GameObject obj in sceneObjects)
        {
            if (obj == null)
                continue;

            if (!IsSceneObject(obj))
                continue;

            if (IsIgnoredSystemObject(obj))
                continue;

            if (characterHierarchy.Contains(obj.transform))
                continue;

            bool namedObject = requestedObjects.Exists(n => NamesMatch(n, obj.name));

            if (exportOnlyNamedSceneObjects && !namedObject)
                continue;

            if (IsEnvironmentSurface(obj))
            {
                SceneEntity env = BuildEntity(obj, EntityKind.EnvironmentSurface, null);

                if (env != null)
                    data.environment_surfaces.Add(env);

                continue;
            }

            if (!namedObject && !IsExportableProp(obj))
                continue;

            SceneEntity prop = BuildEntity(obj, EntityKind.Object, null);

            if (prop != null)
                data.objects.Add(prop);
        }

        data.characters = RemoveNestedEntities(data.characters);
        data.objects = RemoveNestedEntities(data.objects);
        data.environment_surfaces = RemoveNestedEntities(data.environment_surfaces);

        data.character_names = ExtractNames(data.characters);
        data.object_names = ExtractNames(data.objects);
        data.environment_surface_names = ExtractNames(data.environment_surfaces);

        data.scene_bounds = CalculateSceneBounds(data);
        data.available_animations = FindAnimationClipNames();
        data.ground_samples = BuildGroundSamples(data);

        WriteJson(data);

        Debug.Log($"Scene context exported to: {outputPath}");
        Debug.Log($"Characters exported: {data.characters.Count}");
        Debug.Log($"Objects exported: {data.objects.Count}");
        Debug.Log($"Environment surfaces exported: {data.environment_surfaces.Count}");
        Debug.Log($"Ground samples exported: {data.ground_samples.Count}");
    }

    private static HashSet<Transform> FindCharacterRoots(GameObject[] sceneObjects, List<string> requestedCharacters)
    {
        HashSet<Transform> roots = new HashSet<Transform>();

        foreach (GameObject obj in sceneObjects)
        {
            if (obj == null)
                continue;

            if (!IsSceneObject(obj))
                continue;

            if (IsIgnoredSystemObject(obj))
                continue;

            bool namedCharacter = requestedCharacters.Exists(n => NamesMatch(n, obj.name));

            Animator animator = obj.GetComponent<Animator>();

            if (animator == null && !namedCharacter)
                continue;

            if (!HasUsefulVisibleRenderer(obj))
                continue;

            Transform root = obj.transform;

            if (namedCharacter)
                root = obj.transform;
            else if (animator != null)
                root = animator.transform;

            if (HasAncestorAnimator(root))
                continue;

            roots.Add(root);
        }

        return roots;
    }

    private static bool HasAncestorAnimator(Transform transform)
    {
        if (transform == null)
            return false;

        Transform parent = transform.parent;

        while (parent != null)
        {
            if (parent.GetComponent<Animator>() != null)
                return true;

            parent = parent.parent;
        }

        return false;
    }

    private static HashSet<Transform> BuildHierarchySet(HashSet<Transform> roots)
    {
        HashSet<Transform> all = new HashSet<Transform>();

        foreach (Transform root in roots)
            AddHierarchy(root, all);

        return all;
    }

    private static void AddHierarchy(Transform root, HashSet<Transform> set)
    {
        if (root == null)
            return;

        if (!set.Add(root))
            return;

        foreach (Transform child in root)
            AddHierarchy(child, set);
    }

    private static SceneEntity BuildEntity(GameObject obj, EntityKind kind, string semanticRole)
    {
        BoundsInfo boundsInfo = CalculateBounds(obj);

        if (!boundsInfo.hasAnyBounds && kind != EntityKind.Character)
            return null;

        SceneEntity entity = new SceneEntity();

        entity.name = obj.name;
        entity.path = GetHierarchyPath(obj.transform);
        entity.kind = kind.ToString();
        entity.semantic_role = semanticRole ?? kind.ToString();

        entity.tag = SafeTag(obj);
        entity.layer = LayerMask.LayerToName(obj.layer);

        entity.transform_position = ToVector(obj.transform.position);
        entity.transform_rotation_euler = ToVector(obj.transform.eulerAngles);
        entity.transform_forward = ToVector(obj.transform.forward);

        entity.has_renderer = boundsInfo.hasRenderer;
        entity.has_collider = boundsInfo.hasCollider;
        entity.has_animator = obj.GetComponent<Animator>() != null || obj.GetComponentInChildren<Animator>() != null;
        entity.has_navmesh_agent = obj.GetComponent<NavMeshAgent>() != null || obj.GetComponentInChildren<NavMeshAgent>() != null;
        entity.has_playable_director = obj.GetComponent<PlayableDirector>() != null || obj.GetComponentInChildren<PlayableDirector>() != null;

        entity.bounds_center = ToVector(boundsInfo.center);
        entity.bounds_size = ToVector(boundsInfo.size);
        entity.bounds_min = ToVector(boundsInfo.min);
        entity.bounds_max = ToVector(boundsInfo.max);

        Vector3 visual = boundsInfo.hasAnyBounds ? boundsInfo.center : obj.transform.position;
        entity.visual_position = ToVector(visual);

        entity.bottom_center = ToVector(new Vector3(boundsInfo.center.x, boundsInfo.min.y, boundsInfo.center.z));
        entity.top_center = ToVector(new Vector3(boundsInfo.center.x, boundsInfo.max.y, boundsInfo.center.z));

        entity.ground_position = FindGroundBelow(visual, obj.transform);
        entity.estimated_feet_position = entity.ground_position != null ? entity.ground_position : entity.bottom_center;

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

    private static bool IsExportableProp(GameObject obj)
    {
        if (obj == null)
            return false;

        if (IsIgnoredSystemObject(obj))
            return false;

        if (obj.GetComponent<Animator>() != null || obj.GetComponentInChildren<Animator>() != null)
            return false;

        BoundsInfo bounds = CalculateBounds(obj);

        if (!bounds.hasAnyBounds)
            return false;

        if (!bounds.hasRenderer && !bounds.hasCollider)
            return false;

        if (IsTrivialBounds(bounds))
            return false;

        if (IsPureChildPart(obj))
            return false;

        return true;
    }

    private static bool IsEnvironmentSurface(GameObject obj)
    {
        if (obj == null)
            return false;

        if (obj.GetComponent<Terrain>() != null)
            return true;

        if (obj.GetComponent<TerrainCollider>() != null)
            return true;

        Collider collider = obj.GetComponent<Collider>();

        if (collider == null)
            return false;

        Bounds b = collider.bounds;

        bool broadSurface = b.size.x > b.size.y && b.size.z > b.size.y;
        bool hasRendererOrCollider = HasUsefulVisibleRenderer(obj) || collider != null;

        return broadSurface && hasRendererOrCollider;
    }

    private static bool IsPureChildPart(GameObject obj)
    {
        if (obj == null)
            return true;

        if (obj.transform.parent == null)
            return false;

        bool hasOwnUsefulRenderer = HasOwnUsefulRenderer(obj);
        bool hasOwnUsefulCollider = HasOwnUsefulCollider(obj);

        if (!hasOwnUsefulRenderer && !hasOwnUsefulCollider)
            return true;

        if (HasAncestorWithAnimator(obj.transform))
            return true;

        return false;
    }

    private static bool HasAncestorWithAnimator(Transform transform)
    {
        Transform parent = transform.parent;

        while (parent != null)
        {
            if (parent.GetComponent<Animator>() != null)
                return true;

            parent = parent.parent;
        }

        return false;
    }

    private static bool IsIgnoredSystemObject(GameObject obj)
    {
        if (obj == null)
            return true;

        if (!IsSceneObject(obj))
            return true;

        if (obj.GetComponent<Camera>() != null || obj.GetComponentInChildren<Camera>() != null)
            return true;

        if (obj.GetComponent<Light>() != null || obj.GetComponentInChildren<Light>() != null)
            return true;

        if (obj.GetComponent<PlayableDirector>() != null)
            return true;

        if (obj.GetComponent<AudioListener>() != null || obj.GetComponent<AudioSource>() != null)
            return true;

        MonoBehaviour[] behaviours = obj.GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;

            bool looksLikeManagerOrGenerator =
                typeName.Contains("Manager") ||
                typeName.Contains("Builder") ||
                typeName.Contains("Compiler") ||
                typeName.Contains("Resolver") ||
                typeName.Contains("Planner") ||
                typeName.Contains("Loader") ||
                typeName.Contains("Exporter") ||
                typeName.Contains("Runner") ||
                typeName.Contains("Validator") ||
                typeName.Contains("MotionExtension");

            if (looksLikeManagerOrGenerator)
                return true;
        }

        return false;
    }

    private static bool HasUsefulVisibleRenderer(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!renderer.enabled)
                continue;

            if (IsTrivialBounds(renderer.bounds))
                continue;

            return true;
        }

        return false;
    }

    private static bool HasOwnUsefulRenderer(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();

        if (renderer == null || !renderer.enabled)
            return false;

        return !IsTrivialBounds(renderer.bounds);
    }

    private static bool HasOwnUsefulCollider(GameObject obj)
    {
        Collider collider = obj.GetComponent<Collider>();

        if (collider == null || !collider.enabled)
            return false;

        return !IsTrivialBounds(collider.bounds);
    }

    private static bool IsTrivialBounds(BoundsInfo bounds)
    {
        return IsTrivialBounds(bounds.bounds);
    }

    private static bool IsTrivialBounds(Bounds bounds)
    {
        float volume = bounds.size.x * bounds.size.y * bounds.size.z;
        float maxAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

        return volume <= Mathf.Epsilon || maxAxis <= Mathf.Epsilon;
    }

    private static bool IsSceneObject(GameObject obj)
    {
        return obj != null && obj.scene.IsValid();
    }

    private static bool IsEditorOnly(GameObject obj)
    {
        try
        {
            return obj.CompareTag("EditorOnly");
        }
        catch
        {
            return false;
        }
    }

    private static BoundsInfo CalculateBounds(GameObject obj)
    {
        BoundsInfo info = new BoundsInfo();

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!renderer.enabled)
                continue;

            if (!info.hasAnyBounds)
            {
                info.bounds = renderer.bounds;
                info.hasAnyBounds = true;
            }
            else
            {
                info.bounds.Encapsulate(renderer.bounds);
            }

            info.hasRenderer = true;
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            if (!collider.enabled)
                continue;

            if (!info.hasAnyBounds)
            {
                info.bounds = collider.bounds;
                info.hasAnyBounds = true;
            }
            else
            {
                info.bounds.Encapsulate(collider.bounds);
            }

            info.hasCollider = true;
        }

        if (!info.hasAnyBounds)
            info.bounds = new Bounds(obj.transform.position, Vector3.zero);

        return info;
    }

    private static SceneVector3 FindGroundBelow(Vector3 position, Transform self)
    {
        BoundsInfo bounds = CalculateBounds(self.gameObject);
        float rayHeight = Mathf.Max(bounds.size.y, Mathf.Epsilon);
        Vector3 rayStart = position + Vector3.up * rayHeight;
        float rayDistance = rayHeight * 3.0f;

        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, rayDistance, ~0);

        bool found = false;
        float bestY = float.NegativeInfinity;
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

    private static List<GroundSample> BuildGroundSamples(SceneContextData data)
    {
        List<GroundSample> samples = new List<GroundSample>();

        foreach (SceneEntity surface in data.environment_surfaces)
        {
            GroundSample sample = new GroundSample();
            sample.near_entity = surface.name;
            sample.world_position = surface.bounds_center;
            samples.Add(sample);
        }

        foreach (SceneEntity character in data.characters)
        {
            if (character.ground_position == null)
                continue;

            GroundSample sample = new GroundSample();
            sample.near_entity = character.name;
            sample.world_position = character.ground_position;
            samples.Add(sample);
        }

        foreach (SceneEntity obj in data.objects)
        {
            if (obj.ground_position == null)
                continue;

            GroundSample sample = new GroundSample();
            sample.near_entity = obj.name;
            sample.world_position = obj.ground_position;
            samples.Add(sample);
        }

        return samples;
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
        if (entity == null || entity.bounds_center == null || entity.bounds_size == null)
            return;

        Bounds entityBounds = new Bounds(
            FromVector(entity.bounds_center),
            FromVector(entity.bounds_size)
        );

        if (!found)
        {
            bounds = entityBounds;
            found = true;
        }
        else
        {
            bounds.Encapsulate(entityBounds);
        }
    }

    private static List<SceneEntity> RemoveNestedEntities(List<SceneEntity> input)
    {
        List<SceneEntity> output = new List<SceneEntity>();

        foreach (SceneEntity entity in input)
        {
            if (entity == null)
                continue;

            bool isNestedInsideExisting = output.Exists(existing =>
                !string.IsNullOrEmpty(entity.path) &&
                !string.IsNullOrEmpty(existing.path) &&
                entity.path.StartsWith(existing.path + "/")
            );

            if (!isNestedInsideExisting)
                output.Add(entity);
        }

        return output;
    }

    private static List<string> ExtractNames(List<SceneEntity> entities)
    {
        List<string> names = new List<string>();

        foreach (SceneEntity entity in entities)
        {
            if (entity == null)
                continue;

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
        Animator animator = obj.GetComponent<Animator>();

        if (animator == null)
            animator = obj.GetComponentInChildren<Animator>();

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

        foreach (string part in parts)
        {
            string clean = part.Trim();

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
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace(" ", "")
            .Replace("-", "");
    }

    private static string SafeTag(GameObject obj)
    {
        try
        {
            return obj.tag;
        }
        catch
        {
            return "";
        }
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

    private enum EntityKind
    {
        Character,
        Object,
        EnvironmentSurface
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
    public string kind;
    public string semantic_role;

    public string tag;
    public string layer;

    public SceneVector3 transform_position;
    public SceneVector3 transform_rotation_euler;
    public SceneVector3 transform_forward;

    public bool has_renderer;
    public bool has_collider;
    public bool has_animator;
    public bool has_navmesh_agent;
    public bool has_playable_director;

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