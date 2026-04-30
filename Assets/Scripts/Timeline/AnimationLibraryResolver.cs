using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimationMapData
{
    public string default_idle = "Idle";
    public AnimationAliasEntry[] aliases;
}

[Serializable]
public class AnimationAliasEntry
{
    public string action;
    public string[] clips;
}

public class AnimationLibraryResolver
{
    private const string MapPath = "AnimationLibrary/animation_map";

    private AnimationMapData mapData;
    private readonly Dictionary<string, string[]> aliasLookup = new Dictionary<string, string[]>();

    public AnimationLibraryResolver()
    {
        LoadMap();
    }

    public AnimationClip Resolve(string requestedAction)
    {
        string action = NormalizeAction(requestedAction);

        List<string> candidates = new List<string>();

        if (aliasLookup.TryGetValue(action, out string[] mappedClips))
            candidates.AddRange(mappedClips);

        candidates.Add(requestedAction);
        candidates.Add(action);
        candidates.Add(mapData != null ? mapData.default_idle : "Idle");

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            AnimationClip clip = Resources.Load<AnimationClip>("Animations/" + candidate.Trim());

            if (clip != null)
            {
                Debug.Log($"✅ Animation resolved: action='{requestedAction}' → clip='{candidate}'");
                return clip;
            }
        }

        Debug.LogWarning($"⚠ Could not resolve animation action='{requestedAction}'. Add it to Resources/AnimationLibrary/animation_map.json");
        return null;
    }

    private void LoadMap()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(MapPath);

        if (jsonFile == null)
        {
            Debug.LogWarning("⚠ Animation map not found at Resources/AnimationLibrary/animation_map.json. Using direct animation names only.");
            mapData = new AnimationMapData();
            return;
        }

        mapData = JsonUtility.FromJson<AnimationMapData>(jsonFile.text);

        if (mapData == null)
        {
            Debug.LogWarning("⚠ Failed to parse animation_map.json. Using direct animation names only.");
            mapData = new AnimationMapData();
            return;
        }

        aliasLookup.Clear();

        if (mapData.aliases == null)
            return;

        foreach (AnimationAliasEntry entry in mapData.aliases)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.action))
                continue;

            string key = NormalizeAction(entry.action);
            aliasLookup[key] = entry.clips ?? Array.Empty<string>();
        }

        Debug.Log($"✅ Animation map loaded with {aliasLookup.Count} action aliases.");
    }

    private string NormalizeAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return "idle";

        return action.Trim().ToLowerInvariant().Replace(" ", "_");
    }
}