using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-wired explore-mode HUD. Assign texts / buttons / sceneBK in the Inspector.
/// </summary>
public class ExploreView : MonoBehaviour
{
    static readonly Color CarryNormal = Color.white;
    static readonly Color CarryFull = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Texts")]
    [SerializeField] TMP_Text timerText;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text carryText;
    [SerializeField] TMP_Text progressText;
    [SerializeField] TMP_Text toastText;

    [Header("Progress")]
    [SerializeField] Image progressFill;

    [Header("Buttons")]
    [SerializeField] Button endGameButton;

    [Header("Toast")]
    [SerializeField] CanvasGroup toastGroup;

    [Header("Scene Background")]
    [SerializeField] Image sceneBK;

    Action onEndGame;
    Tween _toastTween;

    public void Setup(Action endGameCallback, string sceneId = null)
    {
        onEndGame = endGameCallback;

        if (endGameButton != null)
        {
            endGameButton.onClick.RemoveListener(OnEndGameClicked);
            endGameButton.onClick.AddListener(OnEndGameClicked);
        }

        if (!string.IsNullOrEmpty(sceneId))
            ApplySceneBackground(sceneId);

        if (toastGroup != null)
        {
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
        }
    }

    public void ApplySceneBackground(string sceneId)
    {
        if (sceneBK == null || string.IsNullOrEmpty(sceneId))
            return;

        var sprite = Resources.Load<Sprite>("scene/" + sceneId);
        if (sprite == null)
        {
            Debug.LogWarning($"ExploreView: scene background not found at Resources/scene/{sceneId}.");
            return;
        }

        sceneBK.sprite = sprite;
        sceneBK.preserveAspect = true;
        sceneBK.color = Color.white;
    }

    public void SetTimer(float seconds)
    {
        if (timerText != null)
            timerText.text = Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString();
    }

    public void SetScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"Score {score}";
    }

    public void SetCarry(int carry, int maxCarry)
    {
        if (carryText == null)
            return;

        carryText.text = $"Carry {carry}/{maxCarry}";
        carryText.color = carry >= maxCarry ? CarryFull : CarryNormal;
    }

    public void SetFoodProgress(int current, int target)
    {
        target = Mathf.Max(1, target);
        current = Mathf.Clamp(current, 0, target);
        float t = (float)current / target;

        if (progressText != null)
            progressText.text = $"{current}/{target}";

        if (progressFill == null)
            return;

        var rt = progressFill.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(t, 1f);
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(t >= 0.999f ? -4f : 0f, -4f);
    }

    public void ShowToast(string message)
    {
        if (toastText == null)
            return;

        toastText.text = message;

        if (toastGroup == null)
            return;

        if (_toastTween != null && _toastTween.IsActive())
            _toastTween.Kill();

        toastGroup.alpha = 0f;
        _toastTween = DOTween.Sequence()
            .Append(toastGroup.DOFade(1f, 0.12f))
            .AppendInterval(0.85f)
            .Append(toastGroup.DOFade(0f, 0.25f))
            .SetLink(gameObject);
    }

    void OnEndGameClicked()
    {
        onEndGame?.Invoke();
    }

    void OnDestroy()
    {
        if (_toastTween != null && _toastTween.IsActive())
            _toastTween.Kill();
    }
}
