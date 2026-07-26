using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    static readonly Color CarryNormal = Color.white;
    static readonly Color CarryFull = new Color(1f, 0.25f, 0.25f, 1f);
    static readonly Color ProgressFill = new Color(0.35f, 0.85f, 0.45f, 1f);
    static readonly Color ProgressBg = new Color(0.12f, 0.12f, 0.14f, 0.85f);

    Text _timerText;
    Text _scoreText;
    Text _carryText;
    Text _progressText;
    Image _progressFill;
    Text _toastText;
    CanvasGroup _toastGroup;
    Tween _toastTween;

    public void Build(System.Action onEndGame = null)
    {
        EnsureEventSystem();

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        _timerText = CreateLabel(canvasGo.transform, "Timer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), 36, TextAnchor.UpperCenter);
        _scoreText = CreateLabel(canvasGo.transform, "Score", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -30f), 28, TextAnchor.UpperLeft);
        _carryText = CreateLabel(canvasGo.transform, "Carry", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20f, -30f), 28, TextAnchor.UpperRight);

        BuildFoodProgress(canvasGo.transform);
        BuildToast(canvasGo.transform);
        BuildEndGameButton(canvasGo.transform, onEndGame);
    }

    void BuildFoodProgress(Transform canvas)
    {
        var root = new GameObject("FoodProgress");
        root.transform.SetParent(canvas, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0f);
        rootRt.anchorMax = new Vector2(0.5f, 0f);
        rootRt.pivot = new Vector2(0.5f, 0f);
        rootRt.anchoredPosition = new Vector2(0f, 24f);
        rootRt.sizeDelta = new Vector2(420f, 36f);

        var bg = root.AddComponent<Image>();
        bg.color = ProgressBg;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(root.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.sizeDelta = new Vector2(0f, 0f);
        fillRt.offsetMin = new Vector2(4f, 4f);
        fillRt.offsetMax = new Vector2(4f, -4f);
        _progressFill = fillGo.AddComponent<Image>();
        _progressFill.color = ProgressFill;
        _progressFill.raycastTarget = false;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        var labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        _progressText = labelGo.AddComponent<Text>();
        _progressText.font = Font.CreateDynamicFontFromOSFont("Arial", 20);
        _progressText.fontSize = 20;
        _progressText.alignment = TextAnchor.MiddleCenter;
        _progressText.color = Color.white;
        _progressText.raycastTarget = false;
    }

    void BuildEndGameButton(Transform canvas, System.Action onEndGame)
    {
        if (onEndGame == null)
            return;

        var go = new GameObject("EndGameButton");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-20f, 20f);
        rt.sizeDelta = new Vector2(160f, 48f);

        var image = go.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.18f, 0.85f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_select");
            onEndGame.Invoke();
        });

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<Text>();
        label.font = Font.CreateDynamicFontFromOSFont("Arial", 22);
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "End Game";
        label.raycastTarget = false;
    }

    void BuildToast(Transform canvas)
    {
        var go = new GameObject("Toast");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -120f);
        rt.sizeDelta = new Vector2(600f, 60f);

        _toastGroup = go.AddComponent<CanvasGroup>();
        _toastGroup.alpha = 0f;
        _toastGroup.blocksRaycasts = false;
        _toastGroup.interactable = false;

        _toastText = go.AddComponent<Text>();
        _toastText.font = Font.CreateDynamicFontFromOSFont("Arial", 32);
        _toastText.fontSize = 32;
        _toastText.alignment = TextAnchor.MiddleCenter;
        _toastText.color = Color.white;
        _toastText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _toastText.verticalOverflow = VerticalWrapMode.Overflow;
        _toastText.raycastTarget = false;
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static Text CreateLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, int fontSize, TextAnchor align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(400f, 80f);

        var text = go.AddComponent<Text>();
        text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    public void SetTimer(float seconds)
    {
        _timerText.text = Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString();
    }

    public void SetScore(int score)
    {
        _scoreText.text = $"Score {score}";
    }

    public void SetCarry(int carry, int maxCarry)
    {
        _carryText.text = $"Carry {carry}/{maxCarry}";
        _carryText.color = carry >= maxCarry ? CarryFull : CarryNormal;
    }

    public void SetFoodProgress(int current, int target)
    {
        target = Mathf.Max(1, target);
        current = Mathf.Clamp(current, 0, target);
        float t = (float)current / target;

        if (_progressText != null)
            _progressText.text = $"{current}/{target}";

        if (_progressFill == null)
            return;

        var rt = _progressFill.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(t, 1f);
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(t >= 0.999f ? -4f : 0f, -4f);
    }

    public void ShowToast(string message)
    {
        if (_toastText == null || _toastGroup == null)
            return;

        _toastText.text = message;
        if (_toastTween != null && _toastTween.IsActive())
            _toastTween.Kill();

        _toastGroup.alpha = 0f;
        _toastTween = DOTween.Sequence()
            .Append(_toastGroup.DOFade(1f, 0.12f))
            .AppendInterval(0.85f)
            .Append(_toastGroup.DOFade(0f, 0.25f))
            .SetLink(gameObject);
    }

    void OnDestroy()
    {
        if (_toastTween != null && _toastTween.IsActive())
            _toastTween.Kill();
    }
}
