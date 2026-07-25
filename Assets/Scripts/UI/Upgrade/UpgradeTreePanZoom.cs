using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeTreePanZoom : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    const float DragThresholdPixels = 8f;

    RectTransform viewport;
    RectTransform panTarget;
    Canvas canvas;
    float minScale = 0.4f;
    float maxScale = 2.5f;
    float zoomStep = 0.35f;
    bool pointerInside;
    bool pressActive;
    bool dragging;
    bool suppressClick;
    Vector2 pressScreenPos;
    Vector2 lastLocalPoint;
    float scale = 1f;

    public void Setup(
        RectTransform viewportRect,
        RectTransform target,
        float zoomStepValue = 0.35f,
        float minZoom = 0.4f,
        float maxZoom = 2.5f)
    {
        viewport = viewportRect;
        panTarget = target;
        canvas = viewport != null ? viewport.GetComponentInParent<Canvas>() : null;
        zoomStep = Mathf.Max(0.01f, zoomStepValue);
        minScale = Mathf.Max(0.01f, minZoom);
        maxScale = Mathf.Max(minScale, maxZoom);
        ResetView();
    }

    public void ResetView()
    {
        scale = maxScale;
        pressActive = false;
        dragging = false;
        suppressClick = false;
        if (panTarget != null)
        {
            panTarget.localScale = Vector3.one * scale;
            panTarget.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// Returns true once after a pan drag, so upgrade click handlers can ignore that release.
    /// </summary>
    public bool ConsumeSuppressClick()
    {
        if (!suppressClick)
            return false;
        suppressClick = false;
        return true;
    }

    void Update()
    {
        if (panTarget == null || viewport == null || !gameObject.activeInHierarchy)
            return;

        if (!pointerInside && !pressActive)
            return;

        if (Input.GetMouseButtonDown(0) && pointerInside)
        {
            if (TryGetLocalPoint(Input.mousePosition, out lastLocalPoint))
            {
                pressActive = true;
                dragging = false;
                suppressClick = false;
                pressScreenPos = Input.mousePosition;
            }
        }

        if (pressActive && Input.GetMouseButton(0))
        {
            if (!dragging)
            {
                float moved = Vector2.Distance(Input.mousePosition, pressScreenPos);
                if (moved >= DragThresholdPixels)
                {
                    dragging = true;
                    suppressClick = true;
                    CancelPressedButton();
                    if (TryGetLocalPoint(Input.mousePosition, out lastLocalPoint))
                    { }
                }
            }

            if (dragging && TryGetLocalPoint(Input.mousePosition, out var current))
            {
                panTarget.anchoredPosition += current - lastLocalPoint;
                lastLocalPoint = current;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            pressActive = false;
            dragging = false;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f && pointerInside)
        {
            float prevScale = scale;
            scale = Mathf.Clamp(scale + scroll * zoomStep, minScale, maxScale);
            if (Mathf.Approximately(prevScale, scale))
                return;

            ApplyZoom(prevScale);
        }
    }

    void ApplyZoom(float prevScale)
    {
        if (!TryGetLocalPoint(Input.mousePosition, out var localPoint))
            return;

        Vector2 focusOffset = localPoint - panTarget.anchoredPosition;
        float scaleRatio = scale / prevScale;
        panTarget.anchoredPosition -= focusOffset * (scaleRatio - 1f);
        panTarget.localScale = Vector3.one * scale;
    }

    bool TryGetLocalPoint(Vector2 screenPoint, out Vector2 localPoint)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport,
            screenPoint,
            GetEventCamera(),
            out localPoint);
    }

    Camera GetEventCamera()
    {
        if (canvas == null)
            canvas = viewport != null ? viewport.GetComponentInParent<Canvas>() : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    static void CancelPressedButton()
    {
        if (EventSystem.current == null)
            return;

        var pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            var button = result.gameObject.GetComponentInParent<Button>();
            if (button == null)
                continue;

            ExecuteEvents.Execute(button.gameObject, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointerData, ExecuteEvents.pointerExitHandler);
            break;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (!Input.GetMouseButton(0))
        {
            pressActive = false;
            dragging = false;
        }
    }
}
