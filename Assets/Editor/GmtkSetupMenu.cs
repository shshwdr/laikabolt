#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GmtkSetupMenu
{
    const string MapPath = "Assets/Resources/Maps/DefaultMap.asset";
    const string DataPath = "Assets/Resources/GameData.asset";

    [MenuItem("GMTK/Create Default Assets")]
    public static void CreateDefaultAssets()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Maps");

        var map = AssetDatabase.LoadAssetAtPath<MapData>(MapPath);
        if (map == null)
        {
            map = ScriptableObject.CreateInstance<MapData>();
            map.ApplyAscii(new[] { "xxx", "soxx", "xxxx" });
            AssetDatabase.CreateAsset(map, MapPath);
        }
        else
        {
            map.ApplyAscii(new[] { "xxx", "soxx", "xxxx" });
            EditorUtility.SetDirty(map);
        }

        var data = AssetDatabase.LoadAssetAtPath<GameData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<GameData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GMTK] Default MapData / GameData created.");
        Selection.activeObject = map;
    }

    [MenuItem("GMTK/Setup Sample Scene")]
    public static void SetupSampleScene()
    {
        CreateDefaultAssets();

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<GameManager>() != null)
                Object.DestroyImmediate(root);
        }

        // Ensure camera is orthographic
        var cam = Object.FindObjectOfType<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
        }

        var go = new GameObject("Game");
        var explore = new GameObject("ExploreRoot");
        explore.transform.SetParent(go.transform, false);
        var upgrade = new GameObject("UpgradeRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        upgrade.transform.SetParent(go.transform, false);
        var upgradeCanvas = upgrade.GetComponent<Canvas>();
        upgradeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        upgrade.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        upgrade.SetActive(false);

        var gm = go.AddComponent<GameManager>();
        var map = AssetDatabase.LoadAssetAtPath<MapData>(MapPath);
        var data = AssetDatabase.LoadAssetAtPath<GameData>(DataPath);
        var so = new SerializedObject(gm);
        so.FindProperty("mapData").objectReferenceValue = map;
        so.FindProperty("gameData").objectReferenceValue = data;
        so.FindProperty("exploreRoot").objectReferenceValue = explore;
        so.FindProperty("upgradeRoot").objectReferenceValue = upgrade;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GMTK] SampleScene ready with GameManager + Explore/Upgrade roots. Wire ExploreView / GameOverView manually.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent ?? "Assets", name);
    }
}

#endif
