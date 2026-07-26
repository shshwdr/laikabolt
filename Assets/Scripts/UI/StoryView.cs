using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reads a Resources/story/*.txt. Blank lines split pages; each page fades in,
/// click advances. ESC skips only when the skip hint is visible. When finished,
/// fades out and invokes callback. Keyboard (except that ESC) is ignored.
/// </summary>
public class StoryView : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] TMP_Text storyText;
    [SerializeField] Image storyImage;
    [Tooltip("Parent of named RectTransforms (e.g. start1). Missing names keep the previous layout.")]
    [SerializeField] Transform textPositions;
    [SerializeField] GameObject skipHint;
    [SerializeField] float fadeDuration = 0.45f;
    [Header("Page Reveal")]
    [Tooltip("Shown when GameManager reveals it (e.g. 5th start-story page).")]
    [SerializeField] GameObject pageRevealObject;

    Action onComplete;
    Action<int> onPageChanged;
    readonly List<string> _pages = new List<string>();
    int _pageIndex;
    bool _busy;
    bool _listening;
    bool _playing;
    bool _finished;
    Tween _tween;
    string _storyKey;

    public bool IsPlaying => _playing;

    public void SetPageRevealVisible(bool visible)
    {
        if (pageRevealObject != null)
            pageRevealObject.SetActive(visible);
    }

    public void Setup()
    {
        HideImmediate();
    }

    /// <param name="resourcePath">Path under Resources without extension, e.g. "story/start".</param>
    /// <param name="showSkipHint">True when the player has seen this story before.</param>
    /// <param name="pageChangedCallback">Optional; invoked with 0-based page index when a page is shown.</param>
    public void Play(string resourcePath, bool showSkipHint, Action completeCallback, Action<int> pageChangedCallback = null)
    {
        KillTween();
        _pages.Clear();
        _pageIndex = 0;
        _busy = false;
        _listening = false;
        _finished = false;
        _playing = false;
        onComplete = completeCallback;
        onPageChanged = pageChangedCallback;
        _storyKey = ExtractStoryKey(resourcePath);
        SetPageRevealVisible(false);

        if (skipHint != null)
            skipHint.SetActive(showSkipHint);

        if (!TryLoadPages(resourcePath))
        {
            HideImmediate();
            InvokeComplete();
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        SetPanelActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        if (storyText != null)
        {
            storyText.text = string.Empty;
            SetTextAlpha(0f);
        }

        _playing = true;
        ShowPage(0);
    }

    public void HideImmediate()
    {
        KillTween();
        _listening = false;
        _busy = false;
        _playing = false;
        SetPageRevealVisible(false);
        if (skipHint != null)
            skipHint.SetActive(false);
        SetPanelActive(false);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_playing || _finished)
            return;

        // ESC skips only when the skip hint is shown (story already seen once).
        if (skipHint != null && skipHint.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            Finish();
            return;
        }

        // Advance by mouse only — Space/Enter must not drive story or gameplay.
        if (!_listening || _busy)
            return;

        if (Input.GetMouseButtonDown(0))
            Advance();
    }

    void Advance()
    {
        if (_busy || _finished)
            return;

        int next = _pageIndex + 1;
        if (next < _pages.Count)
            TransitionToPage(next);
        else
            Finish();
    }

    void ShowPage(int index)
    {
        if (_finished)
            return;

        _pageIndex = index;
        _busy = true;
        _listening = false;
        onPageChanged?.Invoke(index);

        ApplyPageVisuals(index);

        if (storyText != null)
        {
            storyText.text = _pages[index];
            SetTextAlpha(0f);
            _tween = storyText
                .DOFade(1f, fadeDuration)
                .SetLink(gameObject)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (_finished)
                        return;
                    _busy = false;
                    _listening = true;
                });
        }
        else
        {
            _busy = false;
            _listening = true;
        }
    }

    void TransitionToPage(int index)
    {
        if (_finished)
            return;

        _busy = true;
        _listening = false;
        KillTween();

        if (storyText == null)
        {
            ShowPage(index);
            return;
        }

        _tween = storyText
            .DOFade(0f, fadeDuration)
            .SetLink(gameObject)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (!_finished)
                    ShowPage(index);
            });
    }

    void ApplyPageVisuals(int index)
    {
        string pageKey = BuildPageKey(index);

        if (storyImage != null && !string.IsNullOrEmpty(pageKey))
        {
            var sprite = Resources.Load<Sprite>("storyImage/" + pageKey);
            if (sprite != null)
                storyImage.sprite = sprite;
        }

        ApplyTextPosition(pageKey);
    }

    void ApplyTextPosition(string pageKey)
    {
        if (storyText == null || textPositions == null || string.IsNullOrEmpty(pageKey))
            return;

        var target = FindNamedRect(pageKey);
        if (target == null)
            return;

        CopyRectTransform(target, storyText.rectTransform);
    }

    RectTransform FindNamedRect(string name)
    {
        if (textPositions == null || string.IsNullOrEmpty(name))
            return null;

        for (int i = 0; i < textPositions.childCount; i++)
        {
            var child = textPositions.GetChild(i) as RectTransform;
            if (child != null && child.name == name)
                return child;
        }

        return null;
    }

    static void CopyRectTransform(RectTransform source, RectTransform dest)
    {
        dest.anchorMin = source.anchorMin;
        dest.anchorMax = source.anchorMax;
        dest.pivot = source.pivot;
        dest.anchoredPosition = source.anchoredPosition;
        dest.sizeDelta = source.sizeDelta;
        dest.localRotation = source.localRotation;
        dest.localScale = source.localScale;
    }

    string BuildPageKey(int index)
    {
        if (string.IsNullOrEmpty(_storyKey))
            return null;
        return _storyKey + (index + 1);
    }

    static string ExtractStoryKey(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;

        int slash = resourcePath.LastIndexOf('/');
        return slash >= 0 && slash < resourcePath.Length - 1
            ? resourcePath.Substring(slash + 1)
            : resourcePath;
    }

    void Finish()
    {
        if (_finished)
            return;

        _finished = true;
        _busy = true;
        _listening = false;
        _playing = false;
        KillTween();

        if (skipHint != null)
            skipHint.SetActive(false);

        SetPageRevealVisible(false);

        Sequence seq = DOTween.Sequence().SetLink(gameObject).SetUpdate(true);

        if (storyText != null)
            seq.Append(storyText.DOFade(0f, fadeDuration));

        if (canvasGroup != null)
            seq.Join(canvasGroup.DOFade(0f, fadeDuration));
        else if (storyText == null)
            seq.AppendInterval(fadeDuration);

        seq.OnComplete(() =>
        {
            SetPanelActive(false);
            _busy = false;
            gameObject.SetActive(false);
            InvokeComplete();
        });

        _tween = seq;
    }

    void InvokeComplete()
    {
        var cb = onComplete;
        onComplete = null;
        onPageChanged = null;
        cb?.Invoke();
    }

    bool TryLoadPages(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return false;

        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"StoryView: missing Resources/{resourcePath}.txt");
            return false;
        }

        ParsePages(asset.text, _pages);
        if (_pages.Count == 0)
        {
            Debug.LogWarning($"StoryView: {resourcePath} has no content.");
            return false;
        }

        return true;
    }

    static void ParsePages(string raw, List<string> pages)
    {
        pages.Clear();
        if (string.IsNullOrEmpty(raw))
            return;

        string normalized = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        var buf = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0)
            {
                if (buf.Length > 0)
                {
                    pages.Add(buf.ToString());
                    buf.Length = 0;
                }
                continue;
            }

            if (buf.Length > 0)
                buf.Append('\n');
            buf.Append(line);
        }

        if (buf.Length > 0)
            pages.Add(buf.ToString());
    }

    void SetTextAlpha(float a)
    {
        if (storyText == null)
            return;
        var c = storyText.color;
        c.a = a;
        storyText.color = c;
    }

    void SetPanelActive(bool active)
    {
        if (panel != null)
            panel.SetActive(active);
        else
            gameObject.SetActive(active);
    }

    void KillTween()
    {
        if (_tween != null && _tween.IsActive())
            _tween.Kill();
        _tween = null;
    }

    void OnDestroy()
    {
        KillTween();
    }
}
