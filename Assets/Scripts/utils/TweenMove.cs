using DG.Tweening;
using UnityEngine;

/// <summary>
/// Tweens a target between start → end (sibling markers).
/// For reveal windows: put UiHoleFrame on the hole, assign target = hole, leave MovingMask empty.
/// Content should be a sibling and must NOT be parented under the moving hole.
/// </summary>
public class TweenMove : MonoBehaviour
{
    [SerializeField] Transform start;
    [SerializeField] Transform end;
    [SerializeField] RectTransform target;
    [SerializeField] float duration = 1f;
    [SerializeField] Ease ease = Ease.InOutSine;
    [SerializeField] bool playOnEnable = true;
    [SerializeField] bool loop = true;
    [SerializeField] bool useUnscaledTime;

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
        if (start == null || end == null)
            return;

        var moveTarget = target != null ? target : transform as RectTransform;
        if (moveTarget == null)
            return;

        Stop();

        // Relative path from current pose — no teleport.
        Vector2 from = moveTarget.localPosition;
        Vector2 delta;
        if (start.parent == moveTarget.parent && end.parent == moveTarget.parent)
            delta = (Vector2)(end.localPosition - start.localPosition);
        else if (moveTarget.parent != null)
        {
            Vector3 a = moveTarget.parent.InverseTransformPoint(start.position);
            Vector3 b = moveTarget.parent.InverseTransformPoint(end.position);
            delta = (Vector2)(b - a);
        }
        else
            delta = (Vector2)(end.position - start.position);

        Vector2 to = from + delta;
        moveTarget.localPosition = from;

        _tween = moveTarget
            .DOLocalMove(to, Mathf.Max(0.01f, duration))
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject);

        if (loop)
            _tween.SetLoops(-1, LoopType.Yoyo);
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
