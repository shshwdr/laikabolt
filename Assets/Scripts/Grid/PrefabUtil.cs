using UnityEngine;

public static class PrefabUtil
{
    public static GameObject Load(string resourcesPath)
    {
        var prefab = Resources.Load<GameObject>(resourcesPath);
        if (prefab == null)
            Debug.LogWarning("[PrefabUtil] Missing prefab: Resources/" + resourcesPath);
        return prefab;
    }

    public static GameObject Instantiate(string resourcesPath, Transform parent, string fallbackName = null)
    {
        var prefab = Load(resourcesPath);
        if (prefab != null)
        {
            var go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;
            return go;
        }

        var fallback = new GameObject(string.IsNullOrEmpty(fallbackName) ? resourcesPath : fallbackName);
        fallback.transform.SetParent(parent, false);
        return fallback;
    }

    public static void EnsureAnimPlayer(GameObject go)
    {
        if (go == null)
            return;
        if (go.GetComponent<AnimPlayer>() == null)
            go.AddComponent<AnimPlayer>();
    }
}
