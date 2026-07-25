using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registers a tutorial target by identifier.
/// World objects: raise SpriteRenderer sorting to layer "tutorial", order 100.
/// Canvas UI objects: reparent under TutorialView.HighlightRoot for the active line, then restore.
/// </summary>
[DisallowMultipleComponent]
public class TutorialGameobject : MonoBehaviour
{
    const string TutorialSortingLayer = "tutorial";
    const int TutorialSortingOrder = 100;

    [SerializeField] string identifier;

    TutorialManager manager;
    Canvas cachedCanvas;
    readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    readonly List<string> savedLayers = new List<string>();
    readonly List<int> savedOrders = new List<int>();
    bool canvasRaised;
    bool canvasHadOverride;
    string savedCanvasLayer;
    int savedCanvasOrder;
    bool raised;

    Transform savedParent;
    int savedSiblingIndex;
    bool reparented;

    public string Identifier => identifier;

    public Canvas Canvas
    {
        get
        {
            EnsureCanvas();
            return cachedCanvas;
        }
    }

    public void SetIdentifier(string id)
    {
        identifier = id;
    }

    void Awake()
    {
        EnsureCanvas();
        CacheRenderers();
    }

    void OnEnable()
    {
        manager = FindObjectOfType<TutorialManager>(true);
        manager?.RegisterTutorialGameobject(this);
    }

    void OnDisable()
    {
        RestoreSorting();
        if (manager != null)
            manager.UnregisterTutorialGameobject(this);
    }

    void OnDestroy()
    {
        RestoreSorting();
        if (manager != null)
            manager.UnregisterTutorialGameobject(this);
    }

    public void RaiseSorting(Transform tutorialHost = null)
    {
        if (raised)
            return;

        if (IsUnderCanvas() && tutorialHost != null)
            ReparentToTutorial(tutorialHost);
        else
            RaiseWorldSorting();

        raised = true;
    }

    public void RestoreSorting()
    {
        if (!raised)
            return;

        if (reparented)
            RestoreParent();
        else
            RestoreWorldSorting();

        raised = false;
    }

    bool IsUnderCanvas()
    {
        return GetComponentInParent<Canvas>() != null;
    }

    void ReparentToTutorial(Transform tutorialHost)
    {
        if (tutorialHost == null || transform.parent == tutorialHost)
            return;

        savedParent = transform.parent;
        savedSiblingIndex = transform.GetSiblingIndex();
        // Keep on-screen position while moving into the tutorial canvas.
        transform.SetParent(tutorialHost, true);
        transform.SetAsLastSibling();
        reparented = true;
    }

    void RestoreParent()
    {
        if (!reparented)
            return;

        if (savedParent != null)
        {
            transform.SetParent(savedParent, true);
            int maxIndex = savedParent.childCount - 1;
            transform.SetSiblingIndex(Mathf.Clamp(savedSiblingIndex, 0, maxIndex));
        }

        savedParent = null;
        savedSiblingIndex = 0;
        reparented = false;
    }

    void RaiseWorldSorting()
    {
        CacheRenderers();
        savedLayers.Clear();
        savedOrders.Clear();
        for (int i = 0; i < renderers.Count; i++)
        {
            var sr = renderers[i];
            if (sr == null)
                continue;

            savedLayers.Add(sr.sortingLayerName);
            savedOrders.Add(sr.sortingOrder);
            sr.sortingLayerName = TutorialSortingLayer;
            sr.sortingOrder = TutorialSortingOrder;
        }

        EnsureCanvas();
        if (cachedCanvas != null)
        {
            canvasHadOverride = cachedCanvas.overrideSorting;
            savedCanvasLayer = cachedCanvas.sortingLayerName;
            savedCanvasOrder = cachedCanvas.sortingOrder;
            cachedCanvas.overrideSorting = true;
            cachedCanvas.sortingLayerName = TutorialSortingLayer;
            cachedCanvas.sortingOrder = TutorialSortingOrder;
            canvasRaised = true;
        }
    }

    void RestoreWorldSorting()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            var sr = renderers[i];
            if (sr == null || i >= savedLayers.Count)
                continue;

            sr.sortingLayerName = savedLayers[i];
            sr.sortingOrder = savedOrders[i];
        }

        if (canvasRaised && cachedCanvas != null)
        {
            cachedCanvas.sortingLayerName = savedCanvasLayer;
            cachedCanvas.sortingOrder = savedCanvasOrder;
            cachedCanvas.overrideSorting = canvasHadOverride;
        }

        canvasRaised = false;
    }

    void CacheRenderers()
    {
        renderers.Clear();
        GetComponentsInChildren(true, renderers);
    }

    void EnsureCanvas()
    {
        if (cachedCanvas == null)
            cachedCanvas = GetComponent<Canvas>();
    }
}
