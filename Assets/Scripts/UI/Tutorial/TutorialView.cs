using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial UI: text panel + full-screen input blocker.
/// Wire in Inspector, or Awake builds a Screen Space Overlay fallback.
/// </summary>
[DisallowMultipleComponent]
public class TutorialView : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text tutorialText;
    [SerializeField] GameObject disableAllButton;
    [SerializeField] RectTransform highlightRoot;

    bool builtFallback;

    public GameObject DisableAllButton => disableAllButton;

    /// <summary>UI higherSort targets are reparented here (above the dimmer) for the active line.</summary>
    public Transform HighlightRoot
    {
        get
        {
            EnsureUi();
            return highlightRoot != null ? highlightRoot : transform;
        }
    }

    void Awake()
    {
        EnsureUi();
        Hide();
        if (disableAllButton != null)
            disableAllButton.SetActive(false);
    }

    public void Show(string text)
    {
        EnsureUi();
        if (tutorialText != null)
            tutorialText.text = text ?? string.Empty;

        SetPanelActive(true);
    }

    public void Hide()
    {
        SetPanelActive(false);
    }

    public void SetDisableAllActive(bool active)
    {
        EnsureUi();
        if (disableAllButton == null)
            return;

        disableAllButton.SetActive(active);
        if (!active)
            return;

        if (disableAllButton.TryGetComponent<Image>(out var image))
            image.raycastTarget = true;

        var canvas = disableAllButton.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    void SetPanelActive(bool active)
    {
        if (panel != null)
            panel.SetActive(active);
        else
            gameObject.SetActive(active);
    }

    void EnsureUi()
    {
        if (panel != null && tutorialText != null && disableAllButton != null)
        {
            EnsureHighlightRoot();
            return;
        }

        if (!builtFallback)
            BuildFallbackOverlay();
    }

    void EnsureHighlightRoot()
    {
        if (highlightRoot != null)
            return;

        Transform canvasTf = null;
        if (disableAllButton != null)
        {
            var c = disableAllButton.GetComponentInParent<Canvas>();
            if (c != null)
                canvasTf = c.transform;
        }

        if (canvasTf == null && panel != null)
        {
            var c = panel.GetComponentInParent<Canvas>();
            if (c != null)
                canvasTf = c.transform;
        }

        if (canvasTf == null)
            canvasTf = transform;

        var hostGo = new GameObject("HighlightRoot");
        hostGo.transform.SetParent(canvasTf, false);
        // Sit above the dimmer, below the text panel when possible.
        if (disableAllButton != null && disableAllButton.transform.parent == canvasTf)
            hostGo.transform.SetSiblingIndex(disableAllButton.transform.GetSiblingIndex() + 1);
        if (panel != null && panel.transform.parent == canvasTf)
            panel.transform.SetAsLastSibling();

        highlightRoot = hostGo.AddComponent<RectTransform>();
        StretchFull(highlightRoot);
        var hostImage = hostGo.AddComponent<Image>();
        hostImage.color = Color.clear;
        hostImage.raycastTarget = false;
    }

    void BuildFallbackOverlay()
    {
        builtFallback = true;

        var canvasGo = new GameObject("TutorialOverlay");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        if (disableAllButton == null)
        {
            var blockerGo = new GameObject("DisableAllButton");
            blockerGo.transform.SetParent(canvasGo.transform, false);
            var blockerRt = blockerGo.AddComponent<RectTransform>();
            StretchFull(blockerRt);
            var blockerImage = blockerGo.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.35f);
            blockerImage.raycastTarget = true;
            disableAllButton = blockerGo;
            blockerGo.SetActive(false);
        }

        if (highlightRoot == null)
        {
            var hostGo = new GameObject("HighlightRoot");
            hostGo.transform.SetParent(canvasGo.transform, false);
            highlightRoot = hostGo.AddComponent<RectTransform>();
            StretchFull(highlightRoot);
            // Pass-through: only children receive raycasts.
            var hostImage = hostGo.AddComponent<Image>();
            hostImage.color = Color.clear;
            hostImage.raycastTarget = false;
        }

        if (panel == null)
        {
            var panelGo = new GameObject("TutorialPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.1f, 0.08f);
            panelRt.anchorMax = new Vector2(0.9f, 0.28f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);
            panelImage.raycastTarget = false;
            panel = panelGo;
        }

        if (tutorialText == null)
        {
            var textGo = new GameObject("TutorialText");
            textGo.transform.SetParent(panel.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            StretchFull(textRt);
            textRt.offsetMin = new Vector2(16f, 12f);
            textRt.offsetMax = new Vector2(-16f, -12f);
            tutorialText = textGo.AddComponent<TextMeshProUGUI>();
            tutorialText.fontSize = 28f;
            tutorialText.alignment = TextAlignmentOptions.Center;
            tutorialText.color = Color.white;
            tutorialText.raycastTarget = false;
        }
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
