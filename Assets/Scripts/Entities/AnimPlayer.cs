using UnityEngine;

/// <summary>
/// Plays a simple frame animation on a SpriteRenderer when frames are defined.
/// If no frames are assigned, does nothing.
/// </summary>
public class AnimPlayer : MonoBehaviour
{
    [SerializeField] Sprite[] frames;
    [SerializeField] float fps = 8f;
    [SerializeField] bool loop = true;
    [SerializeField] bool playOnEnable = true;

    SpriteRenderer _sr;
    int _index;
    float _timer;
    bool _playing;
    Sprite _original;

    public bool HasAnimation => frames != null && frames.Length > 0;
    public bool IsPlaying => _playing;

    void Awake()
    {
        _sr = SpriteUtil.ResolveRenderer(gameObject, addIfMissing: false);
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

        if (_sr == null)
            _sr = SpriteUtil.ResolveRenderer(gameObject, addIfMissing: false);
        if (_sr == null)
            return;

        if (!_playing)
            _original = _sr.sprite;

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
        if (_sr != null && _original != null)
            _sr.sprite = _original;
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
        if (_sr == null || frames == null || _index < 0 || _index >= frames.Length)
            return;
        if (frames[_index] != null)
            _sr.sprite = frames[_index];
    }
}
