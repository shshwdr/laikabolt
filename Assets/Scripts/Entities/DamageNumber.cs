using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// World-space TMP damage popup: diagonal bounce up + scale punch, then fade out.
/// </summary>
public class DamageNumber : MonoBehaviour
{
    const float FontSize = 4f;
    const float Duration = 0.55f;
    const float JumpPower = 0.55f;
    const float Travel = 0.9f;
    const float PeakScale = 1.35f;

    static readonly Color NumberColor = new Color(1f, 0.35f, 0.25f, 1f);

    TextMeshPro _tmp;

    public static void Spawn(Vector3 worldPos, int damage)
    {
        var go = new GameObject("DamageNumber");
        go.transform.position = worldPos + Vector3.up * 0.25f;
        var dn = go.AddComponent<DamageNumber>();
        dn.Play(Mathf.Max(1, damage));
    }

    void Play(int damage)
    {
        _tmp = gameObject.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null)
            _tmp.font = TMP_Settings.defaultFontAsset;
        _tmp.text = damage.ToString();
        _tmp.fontSize = FontSize;
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.color = NumberColor;
        _tmp.enableWordWrapping = false;
        _tmp.raycastTarget = false;
        _tmp.sortingOrder = 30;
        _tmp.outlineWidth = 0.2f;
        _tmp.outlineColor = new Color(0.15f, 0.05f, 0.05f, 0.9f);

        // Random diagonal: up with ±15°..±55° horizontal lean.
        float side = Random.value < 0.5f ? -1f : 1f;
        float angle = side * Random.Range(15f, 55f);
        Vector3 end = transform.position + (Quaternion.Euler(0f, 0f, angle) * Vector3.up) * Travel;

        transform.localScale = Vector3.zero;

        var seq = DOTween.Sequence().SetLink(gameObject);
        seq.Append(transform.DOJump(end, JumpPower, 1, Duration).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(PeakScale, Duration * 0.25f).SetEase(Ease.OutBack));
        seq.Insert(Duration * 0.55f, transform.DOScale(0f, Duration * 0.45f).SetEase(Ease.InQuad));
        seq.Insert(Duration * 0.55f, DOTween.To(() => _tmp.alpha, a => _tmp.alpha = a, 0f, Duration * 0.45f));
        seq.OnComplete(() => Destroy(gameObject));
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}
