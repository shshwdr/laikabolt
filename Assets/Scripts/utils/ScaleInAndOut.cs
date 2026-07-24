using DG.Tweening;
using UnityEngine;

public class ScaleInAndOut : MonoBehaviour
{
    [SerializeField] Vector3 startScale = Vector3.one;
    [SerializeField] Vector3 endScale = Vector3.one * 1.2f;
    [SerializeField] float duration = 0.5f;
    [SerializeField] Ease ease = Ease.InOutSine;
    [SerializeField] bool playOnEnable = true;

    Tween _tween;

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
        Stop();
        transform.localScale = startScale;
        _tween = transform
            .DOScale(endScale, Mathf.Max(0.01f, duration))
            .SetEase(ease)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    public void Stop()
    {
        if (_tween != null && _tween.IsActive())
            _tween.Kill();
        _tween = null;
    }

    void OnDestroy()
    {
        Stop();
    }
}
