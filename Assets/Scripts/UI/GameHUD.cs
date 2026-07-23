using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    Text _timerText;
    Text _scoreText;
    Text _carryText;
    Text _endText;
    GameObject _endPanel;

    public void Build()
    {
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

        _endPanel = new GameObject("EndPanel");
        _endPanel.transform.SetParent(canvasGo.transform, false);
        var rt = _endPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = _endPanel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);

        _endText = CreateLabel(_endPanel.transform, "End", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, 48, TextAnchor.MiddleCenter);
        _endPanel.SetActive(false);
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

    public void SetCarry(int carry)
    {
        _carryText.text = $"Carry {carry}";
    }

    public void ShowEnd(int score)
    {
        _endPanel.SetActive(true);
        _endText.text = $"Time's up!\nCollected: {score}";
    }
}
