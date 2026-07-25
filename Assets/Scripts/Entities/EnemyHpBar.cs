using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space HP progress bar for monsters. Assign ProgressBar fill Image on the prefab,
/// or leave empty to auto-create a simple bar at runtime.
/// </summary>
public class EnemyHpBar : MonoBehaviour
{
    static readonly Color BgColor = new Color(0.12f, 0.12f, 0.14f, 0.9f);
    static readonly Color FillColor = new Color(0.85f, 0.25f, 0.25f, 1f);

    [SerializeField] Image progressBar;
    [SerializeField] Vector3 localOffset = new Vector3(0f, 0.75f, 0f);
    [SerializeField] Vector2 barSize = new Vector2(80f, 12f);
    [SerializeField] float worldScale = 0.01f;

    int _maxHp = 1;
    GameObject _runtimeRoot;

    public Image ProgressBar => progressBar;

    public void Setup(int maxHp)
    {
        _maxHp = Mathf.Max(1, maxHp);
        //EnsureProgressBar();
       // SetVisible(true);
        SetHp(_maxHp);
    }

    public void SetHp(int current)
    {
        EnsureProgressBar();
        if (progressBar == null)
            return;

        float t = Mathf.Clamp01((float)Mathf.Max(0, current) / _maxHp);
        ApplyFill(t);
    }

    public void SetVisible(bool visible)
    {
        if (progressBar != null)
            progressBar.gameObject.SetActive(visible);

        if (_runtimeRoot != null)
            _runtimeRoot.SetActive(visible);
    }

    void EnsureProgressBar()
    {
        if (progressBar != null)
            return;

        BuildRuntimeBar();
    }

    void ApplyFill(float t)
    {
        if (progressBar.type == Image.Type.Filled)
        {
            progressBar.fillAmount = t;
            return;
        }

        var rt = progressBar.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(t, 1f);
        rt.offsetMin = new Vector2(2f, 2f);
        rt.offsetMax = new Vector2(t >= 0.999f ? -2f : 0f, -2f);
    }

    void BuildRuntimeBar()
    {
        _runtimeRoot = new GameObject("HpBar");
        _runtimeRoot.transform.SetParent(transform, false);
        _runtimeRoot.transform.localPosition = localOffset;
        _runtimeRoot.transform.localScale = Vector3.one * worldScale;

        var canvas = _runtimeRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        var canvasRt = _runtimeRoot.GetComponent<RectTransform>();
        canvasRt.sizeDelta = barSize;

        var bgGo = new GameObject("Bg");
        bgGo.transform.SetParent(_runtimeRoot.transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bg = bgGo.AddComponent<Image>();
        bg.sprite = SpriteUtil.WhiteSprite();
        bg.color = BgColor;
        bg.raycastTarget = false;

        var fillGo = new GameObject("ProgressBar");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        progressBar = fillGo.AddComponent<Image>();
        progressBar.sprite = SpriteUtil.WhiteSprite();
        progressBar.color = FillColor;
        progressBar.type = Image.Type.Filled;
        progressBar.fillMethod = Image.FillMethod.Horizontal;
        progressBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressBar.fillAmount = 1f;
        progressBar.raycastTarget = false;
    }
}
