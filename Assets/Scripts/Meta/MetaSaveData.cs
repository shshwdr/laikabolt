using System;
using System.Collections.Generic;

[Serializable]
public class UpgradeLevelEntry
{
    public string id;
    public int level;
}

[Serializable]
public class MetaSaveData
{
    public int MetaGold;
    public UpgradeLevelEntry[] UpgradeLevels = Array.Empty<UpgradeLevelEntry>();
    /// <summary>Highest scene id the player may select (0 = first planet only).</summary>
    public int MaxUnlockedSceneId;
    /// <summary>Scene currently selected for the next explore run.</summary>
    public string SelectedSceneId = "0";

    [NonSerialized] Dictionary<string, int> levelCache;

    public int GetLevel(string id)
    {
        EnsureCache();
        return levelCache.TryGetValue(id, out int level) ? level : 0;
    }

    public void SetLevel(string id, int level)
    {
        EnsureCache();
        levelCache[id] = level;
        SyncEntries();
    }

    public static MetaSaveData CreateDefault()
    {
        return new MetaSaveData
        {
            UpgradeLevels = Array.Empty<UpgradeLevelEntry>(),
            MaxUnlockedSceneId = 0,
            SelectedSceneId = "0"
        };
    }

    public bool IsSceneUnlocked(int sceneId)
    {
        return sceneId <= MaxUnlockedSceneId;
    }

    public bool IsSceneUnlocked(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return false;

        var info = CSVLoader.GetScene(identifier);
        if (info == null)
            return false;

        return IsSceneUnlocked(info.SceneId);
    }

    void EnsureCache()
    {
        if (levelCache != null)
            return;

        levelCache = new Dictionary<string, int>();
        if (UpgradeLevels == null)
            return;

        foreach (var entry in UpgradeLevels)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id))
                continue;

            levelCache[entry.id] = entry.level;
        }
    }

    void SyncEntries()
    {
        var entries = new List<UpgradeLevelEntry>();
        foreach (var pair in levelCache)
        {
            if (pair.Value <= 0)
                continue;

            entries.Add(new UpgradeLevelEntry { id = pair.Key, level = pair.Value });
        }

        UpgradeLevels = entries.ToArray();
    }
}
