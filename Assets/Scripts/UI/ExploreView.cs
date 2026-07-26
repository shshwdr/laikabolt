using System;
using DG.Tweening;
using FMODUnity;
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
    static readonly Color TimerUrgent = new Color(1f, 0.2f, 0.2f, 1f);
    static readonly Color TimerBonus = new Color(0.25f, 0.95f, 0.4f, 1f);

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

    [Header("Score Tick")]
    [Tooltip("Seconds between each +1/-1 step while counting toward the target score.")]
    [SerializeField] float scoreTickInterval = 0.03f;

    [Header("Timer Punch (last 3s)")]
    [SerializeField] float timerPunchDuration = 0.28f;
    [SerializeField] float timerPunchBase = 0.18f;
    [SerializeField] float timerPunchStep = 0.14f;
    [SerializeField] int timerPunchVibrato = 6;
    [SerializeField] float timerPunchElasticity = 0.55f;

    [Header("Timer Damage Feedback")]
    [SerializeField] float timerDamageFlashDuration = 0.4f;
    [SerializeField] Vector2 timerDamagePunch = new Vector2(14f, 10f);
    [SerializeField] float timerDamagePunchDuration = 0.35f;
    [SerializeField] int timerDamagePunchVibrato = 14;
    [SerializeField] float timerDamagePunchElasticity = 0.7f;
    [SerializeField] float timerDamagePunchScale = 0.22f;

    [Header("Timer Bonus Feedback")]
    [SerializeField] float timerBonusFlashDuration = 0.55f;
    [SerializeField] float timerBonusPunchScale = 0.45f;
    [SerializeField] float timerBonusPunchDuration = 0.4f;
    [SerializeField] int timerBonusPunchVibrato = 8;
    [SerializeField] float timerBonusPunchElasticity = 0.65f;

    [Header("Carry Punch (full)")]
    [SerializeField] float carryPunchDuration = 0.35f;
    [SerializeField] float carryPunchStrength = 0.65f;
    [SerializeField] int carryPunchVibrato = 8;
    [SerializeField] float carryPunchElasticity = 0.6f;

    Action onEndGame;
    Tween _toastTween;
    Tween _scoreTween;
    Tween _timerPunchTween;
    Tween _timerDamageColorTween;
    Tween _timerDamageShakeTween;
    Tween _timerBonusColorTween;
    Tween _timerBonusPunchTween;
    Tween _carryPunchTween;

    int _displayedScore;
    int _targetScore;
    int _lastTimerDisplay = int.MinValue;
    Color _timerNormalColor = Color.white;
    Vector3 _timerBaseScale = Vector3.one;
    Vector2 _timerBaseAnchoredPos;
    bool _timerVisualCached;
    bool _timerDamageFlashActive;
    bool _timerBonusFlashActive;
    Vector3 _carryBaseScale = Vector3.one;
    bool _carryVisualCached;

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

        CacheTimerVisuals();
        CacheCarryVisuals();
        ResetTimerVisual();
        SetScore(0, immediate: true);
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

    public void SetSceneBackgroundVisible(bool visible)
    {
        if (sceneBK == null)
            return;
        sceneBK.gameObject.SetActive(visible);
    }

    public void SetTimer(float seconds)
    {
        if (timerText == null)
            return;

        CacheTimerVisuals();

        float clamped = Mathf.Max(0f, seconds);
        int display = Mathf.CeilToInt(clamped);
        bool changed = display != _lastTimerDisplay;

        timerText.text = display.ToString();
        if (!_timerDamageFlashActive && !_timerBonusFlashActive)
            timerText.color = display <= 5 ? TimerUrgent : _timerNormalColor;

        if (changed && display > 0 && display <= 5)
            RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_countdown");

        if (changed && display > 0 && display <= 3)
            PunchTimer(display);

        _lastTimerDisplay = display;
    }

    /// <summary>Brief red flash + punch-shake when time damage is applied.</summary>
    public void FlashTimerDamage()
    {
        if (timerText == null)
            return;

        CacheTimerVisuals();
        ClearTimerBonusFlash();
        _timerDamageFlashActive = true;
        timerText.color = TimerUrgent;

        if (_timerDamageColorTween != null && _timerDamageColorTween.IsActive())
            _timerDamageColorTween.Kill();
        if (_timerDamageShakeTween != null && _timerDamageShakeTween.IsActive())
            _timerDamageShakeTween.Kill(true);

        var rt = timerText.rectTransform;
        rt.anchoredPosition = _timerBaseAnchoredPos;
        timerText.transform.localScale = _timerBaseScale;

        var seq = DOTween.Sequence().SetLink(gameObject).SetUpdate(true);
        seq.Join(rt.DOPunchAnchorPos(timerDamagePunch, timerDamagePunchDuration, timerDamagePunchVibrato, timerDamagePunchElasticity));
        seq.Join(timerText.transform.DOPunchScale(
            Vector3.one * timerDamagePunchScale,
            timerDamagePunchDuration,
            timerDamagePunchVibrato,
            timerDamagePunchElasticity));
        seq.OnKill(() =>
        {
            if (rt != null)
                rt.anchoredPosition = _timerBaseAnchoredPos;
            if (timerText != null)
                timerText.transform.localScale = _timerBaseScale;
        });
        _timerDamageShakeTween = seq;

        float flashDur = Mathf.Max(0.05f, timerDamageFlashDuration);
        _timerDamageColorTween = DOVirtual.DelayedCall(flashDur, EndTimerDamageFlash)
            .SetLink(gameObject)
            .SetUpdate(true);
    }

    void EndTimerDamageFlash()
    {
        _timerDamageFlashActive = false;
        _timerDamageColorTween = null;
        if (timerText == null || _timerBonusFlashActive)
            return;
        timerText.color = _lastTimerDisplay <= 5 ? TimerUrgent : _timerNormalColor;
    }

    /// <summary>Green flash + punch when bonus time is added (endless boss deposit).</summary>
    public void FlashTimerBonus()
    {
        if (timerText == null)
            return;

        CacheTimerVisuals();
        ClearTimerDamageFlash();
        _timerBonusFlashActive = true;
        timerText.color = TimerBonus;

        if (_timerBonusColorTween != null && _timerBonusColorTween.IsActive())
            _timerBonusColorTween.Kill();
        if (_timerBonusPunchTween != null && _timerBonusPunchTween.IsActive())
            _timerBonusPunchTween.Kill(true);
        if (_timerPunchTween != null && _timerPunchTween.IsActive())
            _timerPunchTween.Kill(true);

        timerText.transform.localScale = _timerBaseScale;
        _timerBonusPunchTween = timerText.transform
            .DOPunchScale(Vector3.one * timerBonusPunchScale, timerBonusPunchDuration, timerBonusPunchVibrato, timerBonusPunchElasticity)
            .SetLink(gameObject)
            .SetUpdate(true)
            .OnKill(() =>
            {
                if (timerText != null)
                    timerText.transform.localScale = _timerBaseScale;
            });

        float flashDur = Mathf.Max(0.05f, timerBonusFlashDuration);
        _timerBonusColorTween = DOVirtual.DelayedCall(flashDur, EndTimerBonusFlash)
            .SetLink(gameObject)
            .SetUpdate(true);
    }

    void EndTimerBonusFlash()
    {
        _timerBonusFlashActive = false;
        _timerBonusColorTween = null;
        if (timerText == null || _timerDamageFlashActive)
            return;
        timerText.color = _lastTimerDisplay <= 5 ? TimerUrgent : _timerNormalColor;
    }

    void ClearTimerDamageFlash()
    {
        _timerDamageFlashActive = false;
        if (_timerDamageColorTween != null && _timerDamageColorTween.IsActive())
            _timerDamageColorTween.Kill();
        _timerDamageColorTween = null;
        if (_timerDamageShakeTween != null && _timerDamageShakeTween.IsActive())
            _timerDamageShakeTween.Kill(true);
        _timerDamageShakeTween = null;
    }

    void ClearTimerBonusFlash()
    {
        _timerBonusFlashActive = false;
        if (_timerBonusColorTween != null && _timerBonusColorTween.IsActive())
            _timerBonusColorTween.Kill();
        _timerBonusColorTween = null;
        if (_timerBonusPunchTween != null && _timerBonusPunchTween.IsActive())
            _timerBonusPunchTween.Kill(true);
        _timerBonusPunchTween = null;
    }

    public void SetScore(int score, bool immediate = false)
    {
        if (scoreText == null)
            return;

        _targetScore = Mathf.Max(0, score);

        if (immediate || scoreTickInterval <= 0f)
        {
            KillScoreTween();
            _displayedScore = _targetScore;
            scoreText.text = _displayedScore.ToString();
            return;
        }

        if (_displayedScore == _targetScore)
            return;

        if (_scoreTween != null && _scoreTween.IsActive())
            return;

        StartScoreTick();
    }

    public void SetCarry(int carry, int maxCarry)
    {
        if (carryText == null)
            return;

        CacheCarryVisuals();
        carryText.text = $"{carry}/{maxCarry}";
        carryText.color = carry >= maxCarry ? CarryFull : CarryNormal;
    }

    public void PunchCarryFull()
    {
        if (carryText == null)
            return;

        CacheCarryVisuals();

        var t = carryText.transform;
        if (_carryPunchTween != null && _carryPunchTween.IsActive())
            _carryPunchTween.Kill(true);

        t.localScale = _carryBaseScale;
        _carryPunchTween = t
            .DOPunchScale(Vector3.one * carryPunchStrength, carryPunchDuration, carryPunchVibrato, carryPunchElasticity)
            .SetLink(gameObject)
            .SetUpdate(true)
            .OnKill(() =>
            {
                if (t != null)
                    t.localScale = _carryBaseScale;
            });
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

    void StartScoreTick()
    {
        KillScoreTween();
        TickScoreOnce();
    }

    void TickScoreOnce()
    {
        if (_displayedScore == _targetScore)
        {
            _scoreTween = null;
            return;
        }

        _displayedScore += _displayedScore < _targetScore ? 1 : -1;
        if (scoreText != null)
            scoreText.text = _displayedScore.ToString();

        if (_displayedScore == _targetScore)
        {
            _scoreTween = null;
            return;
        }

        float interval = Mathf.Max(0.001f, scoreTickInterval);
        _scoreTween = DOVirtual.DelayedCall(interval, TickScoreOnce)
            .SetLink(gameObject)
            .SetUpdate(true);
    }

    void KillScoreTween()
    {
        if (_scoreTween != null && _scoreTween.IsActive())
            _scoreTween.Kill();
        _scoreTween = null;
    }

    void PunchTimer(int displaySeconds)
    {
        if (timerText == null)
            return;

        var t = timerText.transform;
        if (_timerPunchTween != null && _timerPunchTween.IsActive())
            _timerPunchTween.Kill(true);

        t.localScale = _timerBaseScale;

        // 3 -> smallest punch, 2 -> medium, 1 -> largest
        float strength = timerPunchBase + timerPunchStep * (3 - displaySeconds);
        _timerPunchTween = t
            .DOPunchScale(Vector3.one * strength, timerPunchDuration, timerPunchVibrato, timerPunchElasticity)
            .SetLink(gameObject)
            .SetUpdate(true)
            .OnKill(() =>
            {
                if (t != null)
                    t.localScale = _timerBaseScale;
            });
    }

    void CacheTimerVisuals()
    {
        if (_timerVisualCached || timerText == null)
            return;

        _timerNormalColor = timerText.color;
        _timerBaseScale = timerText.transform.localScale;
        _timerBaseAnchoredPos = timerText.rectTransform.anchoredPosition;
        _timerVisualCached = true;
    }

    void CacheCarryVisuals()
    {
        if (_carryVisualCached || carryText == null)
            return;

        _carryBaseScale = carryText.transform.localScale;
        _carryVisualCached = true;
    }

    void ResetTimerVisual()
    {
        _lastTimerDisplay = int.MinValue;
        ClearTimerDamageFlash();
        ClearTimerBonusFlash();
        if (timerText == null)
            return;

        if (_timerPunchTween != null && _timerPunchTween.IsActive())
            _timerPunchTween.Kill(true);
        _timerPunchTween = null;

        timerText.color = _timerNormalColor;
        timerText.transform.localScale = _timerBaseScale;
        timerText.rectTransform.anchoredPosition = _timerBaseAnchoredPos;
    }

    void OnEndGameClicked()
    {
        RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_select");
        onEndGame?.Invoke();
    }

    /// <summary>Registers HUD elements referenced by tutorial.csv higherSort (time, carry).</summary>
    public void RegisterTutorialHudTargets()
    {
        EnsureHudTutorialTarget(timerText, "time");
        EnsureHudTutorialTarget(carryText, "carry");
    }

    static void EnsureHudTutorialTarget(Component component, string identifier)
    {
        if (component == null || string.IsNullOrEmpty(identifier))
            return;

        var go = component.gameObject;
        if (go.GetComponent<Canvas>() == null)
        {
            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = false;
        }

        var target = go.GetComponent<TutorialGameobject>();
        if (target == null)
            target = go.AddComponent<TutorialGameobject>();
        target.SetIdentifier(identifier);
    }

    void OnDestroy()
    {
        if (_toastTween != null && _toastTween.IsActive())
            _toastTween.Kill();
        KillScoreTween();
        if (_timerPunchTween != null && _timerPunchTween.IsActive())
            _timerPunchTween.Kill();
        if (_timerDamageColorTween != null && _timerDamageColorTween.IsActive())
            _timerDamageColorTween.Kill();
        if (_timerDamageShakeTween != null && _timerDamageShakeTween.IsActive())
            _timerDamageShakeTween.Kill();
        if (_timerBonusColorTween != null && _timerBonusColorTween.IsActive())
            _timerBonusColorTween.Kill();
        if (_timerBonusPunchTween != null && _timerBonusPunchTween.IsActive())
            _timerBonusPunchTween.Kill();
        if (_carryPunchTween != null && _carryPunchTween.IsActive())
            _carryPunchTween.Kill();
    }
}
