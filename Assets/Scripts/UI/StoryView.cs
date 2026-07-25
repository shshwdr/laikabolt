using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Reads Resources/story/start.txt. Blank lines split pages; each page fades in,
/// click fades out then shows the next. When finished, fades out and invokes callback.
/// </summary>
public class StoryView : MonoBehaviour
{
    const string StoryResourcePath = "story/start";

    [SerializeField] GameObject panel;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] TMP_Text storyText;
    [SerializeField] float fadeDuration = 0.45f;

    Action onComplete;
    readonly List<string> _pages = new List<string>();
    int _pageIndex;
    bool _busy;
    bool _listening;
    Tween _tween;

    public void Setup(Action completeCallback)
    {
        onComplete = completeCallback;
        HideImmediate();
    }

    public void Play()
    {
        KillTween();
        _pages.Clear();
        _pageIndex = 0;
        _busy = false;
        _listening = false;

        if (!TryLoadPages())
        {
            HideImmediate();
            onComplete?.Invoke();
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

        ShowPage(0);
    }

    public void HideImmediate()
    {
        KillTween();
        _listening = false;
        _busy = false;
        SetPanelActive(false);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        // Keep the whole story root off until Play() so it never blocks Title clicks.
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_listening || _busy)
            return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            Advance();
    }

    void Advance()
    {
        if (_busy)
            return;

        int next = _pageIndex + 1;
        if (next < _pages.Count)
            TransitionToPage(next);
        else
            Finish();
    }

    void ShowPage(int index)
    {
        _pageIndex = index;
        _busy = true;
        _listening = false;

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
            .OnComplete(() => ShowPage(index));
    }

    void Finish()
    {
        _busy = true;
        _listening = false;
        KillTween();

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
            onComplete?.Invoke();
        });

        _tween = seq;
    }

    bool TryLoadPages()
    {
        var asset = Resources.Load<TextAsset>(StoryResourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"StoryView: missing Resources/{StoryResourcePath}.txt");
            return false;
        }

        ParsePages(asset.text, _pages);
        if (_pages.Count == 0)
        {
            Debug.LogWarning("StoryView: story file has no content.");
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
