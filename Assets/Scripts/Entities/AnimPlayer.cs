using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a simple frame animation when frames are defined.
/// Prefers SpriteRenderer; falls back to UI Image.
/// If no frames are assigned, does nothing.
/// </summary>
public class AnimPlayer : MonoBehaviour
{
    [SerializeField] Sprite[] frames;
    [SerializeField] float fps = 8f;
    [SerializeField] bool loop = true;
    [SerializeField] bool playOnEnable = true;

    SpriteRenderer _sr;
    Image _image;
    int _index;
    float _timer;
    bool _playing;
    Sprite _original;

    public bool HasAnimation => frames != null && frames.Length > 0;
    public bool IsPlaying => _playing;

    void Awake()
    {
        ResolveTarget();
    }

    void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        if (!HasAnimation)
            return;

        ResolveTarget();
        if (!HasTarget)
            return;

        if (!_playing)
            _original = GetSprite();

        _playing = true;
        _index = 0;
        _timer = 0f;
        ApplyFrame();
    }

    public void Stop()
    {
        if (!_playing)
            return;

        _playing = false;
        if (_original != null)
            SetSprite(_original);
    }

    void Update()
    {
        if (!_playing || !HasAnimation || fps <= 0f)
            return;

        _timer += Time.deltaTime;
        float frameDur = 1f / fps;
        while (_timer >= frameDur)
        {
            _timer -= frameDur;
            _index++;
            if (_index >= frames.Length)
            {
                if (!loop)
                {
                    _index = frames.Length - 1;
                    ApplyFrame();
                    _playing = false;
                    return;
                }

                _index = 0;
            }

            ApplyFrame();
        }
    }

    void ApplyFrame()
    {
        if (!HasTarget || frames == null || _index < 0 || _index >= frames.Length)
            return;
        if (frames[_index] != null)
            SetSprite(frames[_index]);
    }

    void ResolveTarget()
    {
        if (_sr == null)
            _sr = SpriteUtil.ResolveRenderer(gameObject, addIfMissing: false);

        if (_sr != null)
        {
            _image = null;
            return;
        }

        if (_image == null)
        {
            _image = GetComponent<Image>();
            if (_image == null)
                _image = GetComponentInChildren<Image>(true);
        }
    }

    bool HasTarget => _sr != null || _image != null;

    Sprite GetSprite()
    {
        if (_sr != null)
            return _sr.sprite;
        return _image != null ? _image.sprite : null;
    }

    void SetSprite(Sprite sprite)
    {
        if (_sr != null)
            _sr.sprite = sprite;
        else if (_image != null)
            _image.sprite = sprite;
    }
}
