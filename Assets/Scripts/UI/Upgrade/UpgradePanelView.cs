using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanelView : MonoBehaviour
{
    const float DefaultButtonSize = 160f;
    const float SceneButtonSize = 96f;

    [Header("Texts (place & position in scene)")]
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text summaryText;
    [SerializeField] TMP_Text goldText;

    [Header("Buttons (place & position in scene)")]
    [SerializeField] Button startNextRunButton;

    [Header("Upgrade Tree (place containers in scene)")]
    [SerializeField] RectTransform treeViewport;
    [SerializeField] RectTransform treeRoot;
    [SerializeField] RectTransform lineRoot;
    [SerializeField] float treeScrollSensitivity = 0.35f;

    [Header("Scenes (optional; created at runtime if null)")]
    [SerializeField] RectTransform sceneRow;
    [SerializeField] Vector2 defaultPlayerMarkerOffset = new Vector2(52f, 40f);

    MetaSaveData metaSave;
    Sprite buttonSprite;
    [SerializeField] GameObject upgradeCellPrefab;
    UpgradeTreePanZoom panZoom;
    bool built;
    System.Action onMetaGoldChanged;
    System.Action onStartNextRun;
    System.Action<string> onSceneSelected;
    float buttonSize = DefaultButtonSize;

    readonly Dictionary<string, UpgradeNodeView> nodeViews = new Dictionary<string, UpgradeNodeView>();
    readonly List<SceneButtonView> sceneButtons = new List<SceneButtonView>();
    RectTransform playerMarker;
    Image playerMarkerImage;

    class UpgradeNodeView
    {
        public string Id;
        public UpgradeCell Cell;
    }

    public void Setup(
        MetaSaveData save,
        System.Action startNextRun,
        System.Action metaGoldChanged = null,
        System.Action<string> sceneSelected = null)
    {
        metaSave = save;
        onStartNextRun = startNextRun;
        onMetaGoldChanged = metaGoldChanged;
        onSceneSelected = sceneSelected;

        if (startNextRunButton != null)
        {
            startNextRunButton.onClick.RemoveListener(OnStartNextRunClicked);
            startNextRunButton.onClick.AddListener(OnStartNextRunClicked);
        }
    }

    void OnStartNextRunClicked()
    {
        onStartNextRun?.Invoke();
    }

    public void EnsureBuilt()
    {
        if (built)
            return;

        built = true;
        CSVLoader.Init();
        buttonSprite = SpriteUtil.WhiteSprite();
        if (upgradeCellPrefab == null)
            Debug.LogError($"UpgradePanelView: prefab not found.");
        else
        {
            var prefabRect = upgradeCellPrefab.GetComponent<RectTransform>();
            if (prefabRect != null)
            {
                float size = Mathf.Max(prefabRect.sizeDelta.x, prefabRect.sizeDelta.y);
                if (size > 0.01f)
                    buttonSize = size;
            }
        }

        BuildTreeNodes();
        BuildSceneRow();
        RefreshAll();
    }

    public void OnShown()
    {
        if (summaryText != null)
            summaryText.gameObject.SetActive(false);

        EnsureBuilt();
        panZoom?.ResetView();
        ReloadSave();

        // Default to the newest unlocked planet; player can still switch afterward.
        if (MetaSaveService.SelectLatestUnlockedScene(metaSave))
            NotifySceneSelected(MetaSaveService.GetSelectedSceneId(metaSave));

        RefreshSceneButtons();
    }

    void BuildTreeNodes()
    {
        if (treeRoot == null)
        {
            Debug.LogError("UpgradePanelView: treeRoot is not assigned.");
            return;
        }

        EnsureLineRootUnderTree();

        var positions = BuildLayoutPositions();
        foreach (var pair in positions)
        {
            try
            {
                CreateUpgradeNode(pair.Key, pair.Value);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"UpgradePanelView: failed to draw upgrade '{pair.Key}'. {ex.Message}\n{ex.StackTrace}");
            }
        }

        foreach (var info in CSVLoader.GetAll())
        {
            if (string.IsNullOrEmpty(info.prev))
                continue;
            if (!info.IsVisible())
                continue;
            if (!nodeViews.ContainsKey(info.prev) || !nodeViews.ContainsKey(info.identifier))
            {
                Debug.LogError(
                    $"UpgradePanelView: failed to draw connection '{info.prev}' -> '{info.identifier}' because at least one node was not drawn.");
                continue;
            }

            if (!positions.TryGetValue(info.prev, out var from)
                || !positions.TryGetValue(info.identifier, out var to))
            {
                Debug.LogError(
                    $"UpgradePanelView: failed to draw connection '{info.prev}' -> '{info.identifier}' because layout position is missing.");
                continue;
            }

            CreateConnectionLine(from, to);
        }

        if (treeViewport != null)
        {
            panZoom = treeViewport.GetComponent<UpgradeTreePanZoom>();
            if (panZoom == null)
                panZoom = treeViewport.gameObject.AddComponent<UpgradeTreePanZoom>();
            panZoom.Setup(treeViewport, treeRoot, treeScrollSensitivity);
        }
    }

    void EnsureLineRootUnderTree()
    {
        if (lineRoot == null)
        {
            var lineGo = new GameObject("Lines", typeof(RectTransform));
            lineRoot = (RectTransform)lineGo.transform;
        }

        // Pan/zoom moves treeRoot only — lines must be children so they follow.
        if (lineRoot.parent != treeRoot)
            lineRoot.SetParent(treeRoot, false);

        lineRoot.anchorMin = Vector2.zero;
        lineRoot.anchorMax = Vector2.one;
        lineRoot.pivot = new Vector2(0.5f, 0.5f);
        lineRoot.anchoredPosition = Vector2.zero;
        lineRoot.sizeDelta = Vector2.zero;
        lineRoot.offsetMin = Vector2.zero;
        lineRoot.offsetMax = Vector2.zero;
        lineRoot.localScale = Vector3.one;
        lineRoot.SetAsFirstSibling();
    }

    Dictionary<string, Vector2> BuildLayoutPositions()
    {
        UpgradeTreeLayout.TryBuild(buttonSize * 2f, out var positions);
        return positions;
    }

    void CreateUpgradeNode(string identifier, Vector2 position)
    {
        var info = CSVLoader.Get(identifier);
        if (info == null)
        {
            Debug.LogError($"UpgradePanelView: failed to draw upgrade '{identifier}' because CSV data was not found.");
            return;
        }
        if (!info.IsVisible())
            return;

        if (upgradeCellPrefab == null)
        {
            Debug.LogError($"UpgradePanelView: failed to draw upgrade '{identifier}' because upgradeCell prefab is missing.");
            return;
        }

        var buttonGo = Instantiate(upgradeCellPrefab, treeRoot);
        buttonGo.name = "Upgrade_" + identifier;

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        if (buttonRect == null)
            buttonRect = buttonGo.AddComponent<RectTransform>();

        Vector2 size = buttonRect.sizeDelta;
        if (size.sqrMagnitude < 0.01f)
            size = new Vector2(buttonSize, buttonSize);
        SetupCenterRect(buttonRect, position, size);

        var cell = buttonGo.GetComponent<UpgradeCell>();
        if (cell == null)
        {
            Debug.LogError(
                $"UpgradePanelView: upgradeCell prefab is missing UpgradeCell. Attach it on the prefab root and wire Button / Icon / Label.");
            Destroy(buttonGo);
            return;
        }

        if (cell.Label != null)
        {
            if (TMP_Settings.defaultFontAsset != null)
                cell.Label.font = TMP_Settings.defaultFontAsset;
            cell.Label.text = BuildUpgradeLabel(info);
        }

        if (cell.Button != null)
        {
            string capturedId = identifier;
            cell.Button.onClick.RemoveAllListeners();
            cell.Button.onClick.AddListener(() => OnUpgradeClicked(capturedId));
        }

        nodeViews[identifier] = new UpgradeNodeView
        {
            Id = identifier,
            Cell = cell
        };
    }

    void BuildSceneRow()
    {
        EnsureSceneRow();
        sceneButtons.Clear();

        for (int i = sceneRow.childCount - 1; i >= 0; i--)
            Destroy(sceneRow.GetChild(i).gameObject);

        foreach (var info in CSVLoader.GetAllScenes())
            CreateSceneButton(info);

        EnsurePlayerMarker();
        RefreshSceneButtons();
    }

    void EnsureSceneRow()
    {
        if (sceneRow != null)
            return;

        var go = new GameObject("SceneRow", typeof(RectTransform));
        sceneRow = (RectTransform)go.transform;
        sceneRow.SetParent(transform, false);
        sceneRow.anchorMin = new Vector2(0.5f, 0f);
        sceneRow.anchorMax = new Vector2(0.5f, 0f);
        sceneRow.pivot = new Vector2(0.5f, 0f);
        sceneRow.anchoredPosition = new Vector2(0f, 70f);
        sceneRow.sizeDelta = new Vector2(900f, 140f);

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 18f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    void CreateSceneButton(SceneInfo info)
    {
        string id = info.ResolvedIdentifier;

        var go = new GameObject("Scene_" + id, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(sceneRow, false);
        rt.sizeDelta = new Vector2(SceneButtonSize + 24f, SceneButtonSize + 36f);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = SceneButtonSize + 24f;
        layout.preferredHeight = SceneButtonSize + 36f;

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.SetParent(rt, false);
        iconRt.anchorMin = new Vector2(0.5f, 1f);
        iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.anchoredPosition = new Vector2(0f, -4f);
        iconRt.sizeDelta = new Vector2(SceneButtonSize, SceneButtonSize);

        var icon = iconGo.AddComponent<Image>();
        var sprite = Resources.Load<Sprite>("scene/" + id);
        icon.sprite = sprite != null ? sprite : buttonSprite;
        icon.preserveAspect = true;
        icon.color = sprite != null ? Color.white : new Color(0.35f, 0.55f, 0.85f, 1f);

        var button = iconGo.AddComponent<Button>();
        button.targetGraphic = icon;

        var labelGo = new GameObject("Name", typeof(RectTransform));
        var labelRt = (RectTransform)labelGo.transform;
        labelRt.SetParent(rt, false);
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 0f);
        labelRt.pivot = new Vector2(0.5f, 0f);
        labelRt.anchoredPosition = Vector2.zero;
        labelRt.sizeDelta = new Vector2(0f, 28f);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.text = string.IsNullOrEmpty(info.name) ? id : info.name;
        label.raycastTarget = false;

        var view = go.AddComponent<SceneButtonView>();
        view.Bind(id, button, icon, label, defaultPlayerMarkerOffset);

        string captured = id;
        button.onClick.AddListener(() => OnSceneClicked(captured));
        sceneButtons.Add(view);
    }

    void EnsurePlayerMarker()
    {
        if (playerMarker != null)
            return;

        var go = new GameObject("PlayerMarker", typeof(RectTransform));
        playerMarker = (RectTransform)go.transform;
        playerMarker.SetParent(transform, false);
        playerMarker.sizeDelta = new Vector2(40f, 40f);
        playerMarker.anchorMin = new Vector2(0.5f, 0.5f);
        playerMarker.anchorMax = new Vector2(0.5f, 0.5f);
        playerMarker.pivot = new Vector2(0.5f, 0.5f);

        playerMarkerImage = go.AddComponent<Image>();
        var playerSprite = Resources.Load<Sprite>("player/down");
        playerMarkerImage.sprite = playerSprite != null ? playerSprite : buttonSprite;
        playerMarkerImage.preserveAspect = true;
        playerMarkerImage.raycastTarget = false;
        playerMarker.gameObject.SetActive(false);
    }

    void OnSceneClicked(string identifier)
    {
        if (!MetaSaveService.TrySelectScene(metaSave, identifier))
            return;

        NotifySceneSelected(identifier);
        RefreshSceneButtons();
    }

    void NotifySceneSelected(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return;

        onSceneSelected?.Invoke(identifier);
    }

    void CreateConnectionLine(Vector2 from, Vector2 to)
    {
        if (lineRoot == null)
            return;

        var go = new GameObject("Line");
        go.transform.SetParent(lineRoot, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);

        float distance = Vector2.Distance(from, to);
        float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
        rect.anchoredPosition = from;
        rect.sizeDelta = new Vector2(distance, 2f);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);

        var image = go.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.type = Image.Type.Simple;
        image.color = new Color(0.55f, 0.55f, 0.6f, 0.85f);
        image.raycastTarget = false;
    }

    static void SetupCenterRect(RectTransform rect, Vector2 anchoredPos, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
    }

    void OnUpgradeClicked(string identifier)
    {
        if (!MetaSaveService.TryPurchase(metaSave, identifier))
            return;

        RefreshAll();
        onMetaGoldChanged?.Invoke();
    }

    public void ReloadSave()
    {
        metaSave = MetaSaveService.Load();
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (goldText != null && metaSave != null)
            goldText.text = $"Gold {metaSave.MetaGold}";

        foreach (var pair in nodeViews)
            RefreshUpgradeButton(pair.Value);

        RefreshSceneButtons();
    }

    void RefreshSceneButtons()
    {
        if (metaSave == null)
            return;

        string selected = MetaSaveService.GetSelectedSceneId(metaSave);
        SceneButtonView selectedView = null;

        foreach (var view in sceneButtons)
        {
            if (view == null)
                continue;

            var info = CSVLoader.GetScene(view.Identifier);
            bool unlocked = info != null && metaSave.IsSceneUnlocked(info.SceneId);
            bool isSelected = view.Identifier == selected;

            if (view.Button != null)
                view.Button.interactable = unlocked;

            if (view.Icon != null)
            {
                if (!unlocked)
                    view.Icon.color = new Color(0.25f, 0.25f, 0.28f, 1f);
                else if (isSelected)
                    view.Icon.color = Color.white;
                else
                    view.Icon.color = new Color(0.75f, 0.75f, 0.8f, 1f);
            }

            if (view.Label != null)
            {
                view.Label.color = unlocked ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
                string name = info != null && !string.IsNullOrEmpty(info.name) ? info.name : view.Identifier;
                view.Label.text = unlocked ? name : $"{name}\nLocked";
            }

            if (isSelected && unlocked)
                selectedView = view;
        }

        UpdatePlayerMarker(selectedView);
    }

    void UpdatePlayerMarker(SceneButtonView selected)
    {
        if (playerMarker == null)
            return;

        if (selected == null || selected.Icon == null)
        {
            playerMarker.gameObject.SetActive(false);
            return;
        }

        playerMarker.gameObject.SetActive(true);
        playerMarker.SetParent(selected.Icon.rectTransform, false);
        playerMarker.anchorMin = new Vector2(0.5f, 0.5f);
        playerMarker.anchorMax = new Vector2(0.5f, 0.5f);
        playerMarker.pivot = new Vector2(0.5f, 0.5f);
        playerMarker.anchoredPosition = selected.PlayerMarkerOffset;
        playerMarker.localScale = Vector3.one;
        playerMarker.SetAsLastSibling();
    }

    void RefreshUpgradeButton(UpgradeNodeView node)
    {
        var info = CSVLoader.Get(node.Id);
        if (info == null || node.Cell == null)
            return;

        bool canBuy = MetaSaveService.CanPurchase(metaSave, node.Id);
        bool maxed = metaSave.GetLevel(node.Id) >= info.maxLevel;
        bool locked = MetaSaveService.IsLocked(metaSave, info);

        if (node.Cell.Label != null)
        {
            node.Cell.Label.text = BuildUpgradeLabel(info);
            node.Cell.Label.color = maxed
                ? new Color(0.45f, 0.45f, 0.48f, 1f)
                : Color.white;
        }

        if (node.Cell.Button != null)
            node.Cell.Button.interactable = canBuy;

        if (node.Cell.Icon == null)
            return;

        if (maxed)
            node.Cell.Icon.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        else if (locked)
            node.Cell.Icon.color = new Color(0.22f, 0.22f, 0.22f, 1f);
        else if (canBuy)
            node.Cell.Icon.color = new Color(0.25f, 0.45f, 0.8f, 1f);
        else
            node.Cell.Icon.color = new Color(0.45f, 0.28f, 0.28f, 1f);
    }

    string BuildUpgradeLabel(UpgradeInfo info)
    {
        int level = metaSave.GetLevel(info.identifier);
        bool maxed = level >= info.maxLevel;
        bool locked = MetaSaveService.IsLocked(metaSave, info);
        string title = info.GetDisplayText();

        if (locked)
            return $"{title}\n({level}/{info.maxLevel})\nLocked";

        if (maxed)
            return $"{title}\n({level}/{info.maxLevel})\nMAX";

        return $"{title}\n({level}/{info.maxLevel})\n{info.cost} Gold";
    }
}
