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
    private bool includeInactiveEntities = false;

    [MenuItem("CineAI/Export Scene Context")]
    public static void ShowWindow()
    {
        GetWindow<SceneContextExporter>("Scene Context Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("CineAI Semantic Scene Context Exporter", EditorStyles.boldLabel);

        outputPath = EditorGUILayout.TextField("Output Path", outputPath);
        includeInactiveEntities = EditorGUILayout.Toggle("Include Inactive Entities", includeInactiveEntities);

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "Add CineAIEntity to story-relevant scene objects only: characters, props, and walkable environment surfaces. The exporter will not guess from names.",
            MessageType.Info
        );

        if (GUILayout.Button("Export Scene Context"))
            Export();
    }

    private void Export()
    {
        SceneContextData data = new SceneContextData();

        Scene activeScene = SceneManager.GetActiveScene();
        data.scene_name = activeScene.name;
        data.locations = new List<string> { activeScene.name };

        CineAIEntity[] entities = FindCineAIEntities(includeInactiveEntities);

        foreach (CineAIEntity entity in entities)
        {
            if (entity == null)
                continue;

            if (entity.role == CineAIEntityRole.Ignore)
                continue;

            if (!entity.gameObject.scene.IsValid())
                continue;

            SceneEntity exported = BuildEntity(entity);

            if (exported == null)
                continue;

            switch (entity.role)
            {
                case CineAIEntityRole.Character:
                    data.characters.Add(exported);
                    break;

                case CineAIEntityRole.Object:
                    data.objects.Add(exported);
                    break;

                case CineAIEntityRole.EnvironmentSurface:
                    data.environment_surfaces.Add(exported);
                    break;

                case CineAIEntityRole.Location:
                    data.locations.Add(entity.ExportName);
                    break;
            }
        }

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

    private static CineAIEntity[] FindCineAIEntities(bool includeInactive)
    {
        if (includeInactive)
            return Resources.FindObjectsOfTypeAll<CineAIEntity>();

        return UnityEngine.Object.FindObjectsByType<CineAIEntity>(FindObjectsSortMode.None);
    }

    private static SceneEntity BuildEntity(CineAIEntity marker)
    {
        GameObject obj = marker.gameObject;
        BoundsInfo bounds = CalculateBounds(obj);

        if (!bounds.hasAnyBounds && marker.role != CineAIEntityRole.Location)
        {
            Debug.LogWarning($"CineAIEntity '{marker.ExportName}' has no renderer/collider bounds. It may not produce useful context.");
        }

        SceneEntity entity = new SceneEntity();

        entity.name = marker.ExportName;
        entity.game_object_name = obj.name;
        entity.path = GetHierarchyPath(obj.transform);
        entity.kind = marker.role.ToString();
        entity.semantic_role = marker.role.ToString();

        entity.tag = SafeTag(obj);
        entity.layer = LayerMask.LayerToName(obj.layer);

        entity.can_be_focus_target = marker.canBeFocusTarget;
        entity.can_be_approached = marker.canBeApproached;
        entity.is_walkable_surface = marker.isWalkableSurface;

        entity.transform_position = ToVector(obj.transform.position);
        entity.transform_rotation_euler = ToVector(obj.transform.eulerAngles);
        entity.transform_forward = ToVector(obj.transform.forward);

        entity.has_renderer = bounds.hasRenderer;
        entity.has_collider = bounds.hasCollider;
        entity.has_animator = obj.GetComponent<Animator>() != null || obj.GetComponentInChildren<Animator>() != null;
        entity.has_navmesh_agent = obj.GetComponent<NavMeshAgent>() != null || obj.GetComponentInChildren<NavMeshAgent>() != null;
        entity.has_playable_director = obj.GetComponent<PlayableDirector>() != null || obj.GetComponentInChildren<PlayableDirector>() != null;

        entity.bounds_center = ToVector(bounds.center);
        entity.bounds_size = ToVector(bounds.size);
        entity.bounds_min = ToVector(bounds.min);
        entity.bounds_max = ToVector(bounds.max);

        Vector3 visual = bounds.hasAnyBounds ? bounds.center : obj.transform.position;
        entity.visual_position = ToVector(visual);

        entity.bottom_center = ToVector(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        entity.top_center = ToVector(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));

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
        float rayHeight = CalculateContextRayHeight(self.gameObject);
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

            CineAIEntity hitEntity = hit.transform.GetComponentInParent<CineAIEntity>();

            if (hitEntity != null && !hitEntity.isWalkableSurface)
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

    private static float CalculateContextRayHeight(GameObject obj)
    {
        BoundsInfo bounds = CalculateBounds(obj);
        float height = Mathf.Max(bounds.size.y, Mathf.Epsilon);
        return height;
    }

    private static List<GroundSample> BuildGroundSamples(SceneContextData data)
    {
        List<GroundSample> samples = new List<GroundSample>();

        foreach (SceneEntity surface in data.environment_surfaces)
        {
            if (!surface.is_walkable_surface)
                continue;

            GroundSample centerSample = new GroundSample();
            centerSample.near_entity = surface.name;
            centerSample.world_position = surface.bounds_center;
            samples.Add(centerSample);

            AddSurfaceCornerSamples(samples, surface);
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

    private static void AddSurfaceCornerSamples(List<GroundSample> samples, SceneEntity surface)
    {
        Vector3 min = FromVector(surface.bounds_min);
        Vector3 max = FromVector(surface.bounds_max);
        Vector3 center = FromVector(surface.bounds_center);

        Vector3[] points =
        {
            new Vector3(min.x, center.y, min.z),
            new Vector3(min.x, center.y, max.z),
            new Vector3(max.x, center.y, min.z),
            new Vector3(max.x, center.y, max.z)
        };

        foreach (Vector3 p in points)
        {
            GroundSample sample = new GroundSample();
            sample.near_entity = surface.name;
            sample.world_position = ToVector(p);
            samples.Add(sample);
        }
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
    public string game_object_name;
    public string path;
    public string kind;
    public string semantic_role;

    public string tag;
    public string layer;

    public bool can_be_focus_target;
    public bool can_be_approached;
    public bool is_walkable_surface;

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