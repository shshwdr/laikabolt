using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rectangular reveal hole: content stays completely still (sibling, not a child).
/// Four opaque panels cover everything except the hole rect. Move <see cref="hole"/> only.
///
/// Hierarchy:
///   Parent
///   ├── content          ← dog / image (NEVER moved by this system)
///   ├── hole             ← window size; add UiHoleFrame + TweenMove here
///   ├── start
///   └── end
/// Panels top/bottom/left/right are auto-created as siblings.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UiHoleFrame : MonoBehaviour
{
    [SerializeField] RectTransform hole;
    [SerializeField] RectTransform coverArea;
    [SerializeField] RectTransform top;
    [SerializeField] RectTransform bottom;
    [SerializeField] RectTransform left;
    [SerializeField] RectTransform right;
    [SerializeField] Color coverColor = new Color(0f, 0f, 0f, 0.85f);
    [SerializeField] bool raycastTarget = true;

    void Awake()
    {
        if (hole == null)
            hole = (RectTransform)transform;
        EnsurePanels();
        Refresh();
    }

    void LateUpdate()
    {
        Refresh();
    }

    public RectTransform Hole => hole != null ? hole : (RectTransform)transform;

    public void Refresh()
    {
        var h = Hole;
        var area = coverArea != null ? coverArea : h.parent as RectTransform;
        if (area == null || h == null || h.parent != area)
            return;

        EnsurePanels();

        Rect ar = area.rect;
        Vector2 holePos = h.localPosition;
        Vector2 holeSize = h.rect.size;

        float leftEdge = holePos.x - holeSize.x * h.pivot.x;
        float rightEdge = holePos.x + holeSize.x * (1f - h.pivot.x);
        float bottomEdge = holePos.y - holeSize.y * h.pivot.y;
        float topEdge = holePos.y + holeSize.y * (1f - h.pivot.y);

        SetPanel(top, ar.xMin, topEdge, ar.xMax, ar.yMax);
        SetPanel(bottom, ar.xMin, ar.yMin, ar.xMax, bottomEdge);
        SetPanel(left, ar.xMin, bottomEdge, leftEdge, topEdge);
        SetPanel(right, rightEdge, bottomEdge, ar.xMax, topEdge);
    }

    void SetPanel(RectTransform panel, float xMin, float yMin, float xMax, float yMax)
    {
        if (panel == null)
            return;

        float w = Mathf.Max(0f, xMax - xMin);
        float ht = Mathf.Max(0f, yMax - yMin);
        bool show = w > 0.01f && ht > 0.01f;
        panel.gameObject.SetActive(show);
        if (!show)
            return;

        if (panel.TryGetComponent<Image>(out var img))
        {
            img.color = coverColor;
            img.raycastTarget = raycastTarget;
        }

        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(w, ht);
        panel.localPosition = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f);
    }

    void EnsurePanels()
    {
        var parent = Hole.parent;
        if (parent == null)
            return;

        top = EnsurePanel(top, "hole_top", parent);
        bottom = EnsurePanel(bottom, "hole_bottom", parent);
        left = EnsurePanel(left, "hole_left", parent);
        right = EnsurePanel(right, "hole_right", parent);
    }

    RectTransform EnsurePanel(RectTransform current, string name, Transform parent)
    {
        if (current != null)
            return current;

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = coverColor;
        img.raycastTarget = raycastTarget;
        return rt;
    }
}
