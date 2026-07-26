using System;
using System.IO;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CheatManager : MonoBehaviour
{
    const int GoldCheatAmount = 10;
    const int FoodCheatAmount = 10;
    const float CheatHelpHoldSeconds = 4.5f;

    const string CheatHelpText =
        "R: Reset all progress and reload\n" +
        "G: Add 10 global gold\n" +
        "F: Deposit 10 food into the hole\n" +
        "U: Unlock all scenes\n" +
#if UNITY_EDITOR
        "B: Take screenshot (Editor only)\n" +
#endif
        "C: Show this cheat help";

    GameManager _game;
    CanvasGroup _toastGroup;
    Text _toastText;
    Tween _toastTween;

    void Awake()
    {
        _game = GetComponent<GameManager>();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.B))
            TakeEditorScreenshot();
#endif

        if (_game != null && _game.IsStoryPlaying)
            return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            MetaSaveService.Reset();
            SceneFlowService.ReloadActiveScene();
            return;
        }

        if (Input.GetKeyDown(KeyCode.G))
            AddGold(GoldCheatAmount);

        if (Input.GetKeyDown(KeyCode.F))
            DepositFood(FoodCheatAmount);

        if (Input.GetKeyDown(KeyCode.U))
            UnlockAllScenes();

        if (Input.GetKeyDown(KeyCode.C))
            ShowToast(CheatHelpText, CheatHelpHoldSeconds);
    }

#if UNITY_EDITOR
    void TakeEditorScreenshot()
    {
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
        Directory.CreateDirectory(dir);
        var fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var path = Path.Combine(dir, fileName);
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[CheatManager] Screenshot saved: {path}");
        ShowToast($"Screenshot saved:\n{fileName}", 2f);
    }
#endif

    void AddGold(int amount)
    {
        var save = MetaSaveService.Load();
        save.MetaGold += amount;
        MetaSaveService.Save(save);
        RefreshUpgradePanel();
    }

    void DepositFood(int amount)
    {
        if (_game == null)
            _game = GetComponent<GameManager>();
        if (_game == null)
            return;

        _game.CheatDepositFood(amount);
    }

    void UnlockAllScenes()
    {
        CSVLoader.Init();
        var save = MetaSaveService.Load();
        save.MaxUnlockedSceneId = CSVLoader.GetMaxSceneId();
        MetaSaveService.Save(save);
        RefreshUpgradePanel();
    }

    static void RefreshUpgradePanel()
    {
        var panel = FindObjectOfType<UpgradePanelView>();
        if (panel != null)
            panel.ReloadSave();
    }

    void ShowToast(string message, float holdSeconds)
    {
        EnsureToast();
        if (_toastText == null || _toastGroup == null)
            return;

        _toastText.text = message;
        if (_toastTween != null && _toastTween.IsActive())
            _toastTween.Kill();

        _toastGroup.alpha = 0f;
        _toastTween = DOTween.Sequence()
            .Append(_toastGroup.DOFade(1f, 0.12f))
            .AppendInterval(Mathf.Max(0.5f, holdSeconds))
            .Append(_toastGroup.DOFade(0f, 0.25f))
            .SetLink(gameObject);
    }

    void EnsureToast()
    {
        if (_toastGroup != null)
            return;

        var canvasGo = new GameObject("CheatToastCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        var go = new GameObject("Toast");
        go.transform.SetParent(canvasGo.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -80f);
        rt.sizeDelta = new Vector2(720f, 220f);

        _toastGroup = go.AddComponent<CanvasGroup>();
        _toastGroup.alpha = 0f;
        _toastGroup.blocksRaycasts = false;
        _toastGroup.interactable = false;

        _toastText = go.AddComponent<Text>();
        _toastText.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        _toastText.fontSize = 24;
        _toastText.alignment = TextAnchor.UpperCenter;
        _toastText.color = Color.white;
        _toastText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _toastText.verticalOverflow = VerticalWrapMode.Overflow;
        _toastText.raycastTarget = false;
    }

    void OnDestroy()
    {
        if (_toastTween != null && _toastTween.IsActive())
            _toastTween.Kill();
    }
}
