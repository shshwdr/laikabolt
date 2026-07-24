using UnityEngine;

public static class MetaSaveService
{
    const string SaveKey = "MetaSaveData";

    static MetaSaveData cached;

    public static MetaSaveData Load()
    {
        EnsureCsvLoaded();

        if (cached != null)
            return cached;

        if (!PlayerPrefs.HasKey(SaveKey))
        {
            cached = MetaSaveData.CreateDefault();
            return cached;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        var data = JsonUtility.FromJson<MetaSaveData>(json);
        cached = data ?? MetaSaveData.CreateDefault();
        return cached;
    }

    public static void Save(MetaSaveData data)
    {
        if (data == null)
            return;

        cached = data;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static void Reset()
    {
        cached = null;
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    /// <summary>Returns a runtime copy of GameData with upgrade effects applied.</summary>
    public static GameData ApplyUpgrades(GameData baseData, MetaSaveData meta)
    {
        EnsureCsvLoaded();
        var config = Object.Instantiate(baseData);
        float foodGenerateBonusPercent = 0f;
        float moveSpeedBonusPercent = 0f;

        foreach (var info in CSVLoader.GetAll())
        {
            int level = meta.GetLevel(info.identifier);
            if (level <= 0)
                continue;

            switch (info.effect)
            {
                case "holdItemCount":
                    config.holdItemCount += info.value * level;
                    break;
                case "time":
                    config.roundDuration += info.value * level;
                    break;
                case "hitDamage":
                    config.playerHitDamage += info.value * level;
                    break;
                case "jumpDistance":
                    config.jumpDistance += info.value * level;
                    break;
                case "passBorder":
                    config.passBorder = true;
                    break;
                case "enemyFood":
                    config.enemyFoodDrop += info.value * level;
                    break;
                case "foodCollectAmount":
                    config.foodCollectAmount += info.value * level;
                    break;
                case "machineCollect":
                    config.machineCollectCount += level;
                    config.machineCollect = config.machineCollectCount > 0;
                    config.machineCollectInterval = info.value * level;
                    break;
                case "bonusGenerate":
                    config.bonusGenerateChance += info.value * level;
                    break;
                case "foodGenerate":
                    foodGenerateBonusPercent += info.value * level;
                    break;
                case "startFood":
                    config.initialCollectables += info.value * level;
                    break;
                case "finalSafe":
                    config.finalSafePercent += info.value * level;
                    break;
                case "fullReward":
                    config.fullRewardBonus += info.value * level;
                    break;
                case "lastMinute":
                    config.lastMinute = true;
                    break;
                case "moveSpeed":
                    moveSpeedBonusPercent += info.value * level;
                    break;
            }
        }

        if (foodGenerateBonusPercent > 0f)
            config.collectableSpawnInterval /= 1f + foodGenerateBonusPercent / 100f;

        if (moveSpeedBonusPercent > 0f)
        {
            float speedFactor = 1f + moveSpeedBonusPercent / 100f;
            config.moveDuration /= speedFactor;
            config.jumpDuration /= speedFactor;
        }

        return config;
    }

    public static bool IsLocked(MetaSaveData meta, UpgradeInfo info)
    {
        if (info == null)
            return true;

        if (string.IsNullOrEmpty(info.prev))
            return false;

        return meta.GetLevel(info.prev) < 1;
    }

    public static bool CanPurchase(MetaSaveData meta, string identifier)
    {
        var info = CSVLoader.Get(identifier);
        if (info == null)
            return false;

        int level = meta.GetLevel(identifier);
        if (level >= info.maxLevel)
            return false;

        if (IsLocked(meta, info))
            return false;

        return meta.MetaGold >= info.cost;
    }

    public static bool TryPurchase(MetaSaveData meta, string identifier)
    {
        if (!CanPurchase(meta, identifier))
            return false;

        var info = CSVLoader.Get(identifier);
        meta.MetaGold -= info.cost;
        meta.SetLevel(identifier, meta.GetLevel(identifier) + 1);
        Save(meta);
        return true;
    }

    public static string GetSelectedSceneId(MetaSaveData meta)
    {
        EnsureCsvLoaded();
        if (meta == null)
            return "0";

        if (!string.IsNullOrEmpty(meta.SelectedSceneId) && CSVLoader.GetScene(meta.SelectedSceneId) != null)
            return meta.SelectedSceneId;

        return "0";
    }

    /// <summary>Highest unlocked scene id (Maps / Resources/scene key).</summary>
    public static string GetLatestUnlockedSceneId(MetaSaveData meta)
    {
        EnsureCsvLoaded();
        if (meta == null)
            return "0";

        var info = CSVLoader.GetScene(meta.MaxUnlockedSceneId);
        return info != null ? info.ResolvedIdentifier : meta.MaxUnlockedSceneId.ToString();
    }

    public static bool TrySelectScene(MetaSaveData meta, string identifier)
    {
        EnsureCsvLoaded();
        if (meta == null || string.IsNullOrEmpty(identifier))
            return false;

        var info = CSVLoader.GetScene(identifier);
        if (info == null || !meta.IsSceneUnlocked(info.SceneId))
            return false;

        meta.SelectedSceneId = identifier;
        Save(meta);
        return true;
    }

    /// <summary>Selects the newest unlocked scene for the next run.</summary>
    public static bool SelectLatestUnlockedScene(MetaSaveData meta)
    {
        return TrySelectScene(meta, GetLatestUnlockedSceneId(meta));
    }

    /// <summary>Marks a scene cleared and unlocks the next one if any.</summary>
    public static void ClearScene(MetaSaveData meta, string identifier)
    {
        EnsureCsvLoaded();
        if (meta == null)
            return;

        var info = CSVLoader.GetScene(identifier);
        if (info == null)
            return;

        int nextId = info.SceneId + 1;
        if (nextId > meta.MaxUnlockedSceneId && CSVLoader.GetScene(nextId) != null)
            meta.MaxUnlockedSceneId = nextId;

        Save(meta);
    }

    public static MapData LoadMapForScene(string identifier)
    {
        EnsureCsvLoaded();
        if (string.IsNullOrEmpty(identifier))
            identifier = "0";

        var map = Resources.Load<MapData>("Maps/" + identifier);
        if (map != null)
            return map;

        Debug.LogWarning($"[MetaSaveService] Map not found at Resources/Maps/{identifier}, falling back to Maps/0.");
        return Resources.Load<MapData>("Maps/0");
    }

    static void EnsureCsvLoaded()
    {
        if (CSVLoader.IsInitialized)
            return;

        CSVLoader.Init();
    }
}
