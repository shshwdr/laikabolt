using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TutorialManager : MonoBehaviour
{
    [SerializeField] bool enableTutorial = true;
    [SerializeField] TutorialView tutorialView;

    readonly Dictionary<string, TutorialGameobject> targetsByIdentifier =
        new Dictionary<string, TutorialGameobject>();
    readonly List<TutorialGameobject> activeHighlights = new List<TutorialGameobject>();
    readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    readonly List<Selectable> disabledSelectables = new List<Selectable>();
    readonly HashSet<string> finishedGroups = new HashSet<string>();

    Coroutine runningRoutine;
    TutorialGameobject allowedClickTarget;
    Canvas allowedClickCanvas;
    bool waitingForClick;
    bool requireHighlightTargetClick;
    bool disableAllButtonsActive;
    bool waitingForLineInput;
    bool inTimePassGap;
    float resumeTimeScale = 1f;
    string currentTutorialId;

    public static TutorialManager Instance { get; private set; }

    public bool IsTutorialCompleted => MetaSaveService.Load().TutorialCompleted;
    public bool IsPlaying => runningRoutine != null;

    /// <summary>True while any tutorial is playing (blocks WASD / buttons).</summary>
    public bool BlocksGameplayInput => IsPlaying;

    public static bool IsGameplayBlocked =>
        Instance != null && Instance.BlocksGameplayInput;

    void Awake()
    {
        Instance = this;
        if (tutorialView == null)
            tutorialView = GetComponent<TutorialView>()
                ?? GetComponentInChildren<TutorialView>(true);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void TryShowTutorial(string identifier)
    {
        if (!enableTutorial)
        {
            HideTutorialView();
            return;
        }

        if (IsTutorialCompleted)
        {
            HideTutorialView();
            return;
        }

        ReloadFinishedGroups();
        CSVLoader.Init();
        if (IsTutorialGroupFinished(identifier))
        {
            HideTutorialView();
            return;
        }

        ShowTutorial(identifier);
    }

    public void ShowTutorial(string identifier)
    {
        if (!enableTutorial || IsTutorialCompleted)
        {
            HideTutorialView();
            return;
        }

        ReloadFinishedGroups();
        CSVLoader.Init();

        if (IsTutorialGroupFinished(identifier))
        {
            HideTutorialView();
            return;
        }

        var rows = CSVLoader.GetTutorialRows(identifier);
        if (rows.Count == 0)
        {
            Debug.LogWarning($"Tutorial '{identifier}' has 0 rows in tutorial.csv.");
            HideTutorialView();
            return;
        }

        EnsureAllTutorialTargetsRegistered();

        if (runningRoutine != null)
            StopCoroutine(runningRoutine);

        CleanupCurrentLineState(restoreTime: false, removeBlocker: false);
        resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        currentTutorialId = identifier;
        runningRoutine = StartCoroutine(PlayTutorial(identifier));
    }

    public void RegisterTutorialGameobject(TutorialGameobject target)
    {
        if (target == null || string.IsNullOrEmpty(target.Identifier))
            return;

        targetsByIdentifier[target.Identifier] = target;
    }

    public void UnregisterTutorialGameobject(TutorialGameobject target)
    {
        if (target == null || string.IsNullOrEmpty(target.Identifier))
            return;

        if (targetsByIdentifier.TryGetValue(target.Identifier, out var existing)
            && existing == target)
            targetsByIdentifier.Remove(target.Identifier);
    }

    void Update()
    {
        if (!waitingForClick || !Input.GetMouseButtonDown(0))
            return;

        if (!TryRaycastUi(out var results))
        {
            // No EventSystem: still allow click-to-advance when not requiring a UI target.
            if (!requireHighlightTargetClick)
            {
                RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_select");
                waitingForClick = false;
            }
            return;
        }

        if (requireHighlightTargetClick)
        {
            if (!TryGetTopHitUnderClickTarget(results, out var hitObject))
                return;

            RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_select");
            PropagatePointerClick(hitObject);
            CaptureResumeTimeScaleFromCurrent();
            waitingForClick = false;
            return;
        }

        RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_select");
        PropagateTopRaycastClick(results);
        CaptureResumeTimeScaleFromCurrent();
        waitingForClick = false;
    }

    void LateUpdate()
    {
        if (!disableAllButtonsActive || !IsPlaying)
            return;

        RefreshInputBlock(allowedClickCanvas);
    }

    IEnumerator PlayTutorial(string identifier)
    {
        var rows = CSVLoader.GetTutorialRows(identifier);
        if (rows.Count == 0)
        {
            FinishTutorial();
            runningRoutine = null;
            yield break;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            BeginLine(row);

            waitingForClick = true;
            waitingForLineInput = true;
            yield return new WaitUntil(() => !waitingForClick);
            yield return null;

            waitingForLineInput = false;
            EndLine();
            ExecuteLogic(row.logicAfter, true);
            if (!string.IsNullOrEmpty(row.finishGroup))
                MarkGroupFinished(row.finishGroup);

            if (row.isEnd != 0)
            {
                // Prefer finishGroup; otherwise persist the row's group so TryShow skips next time.
                if (string.IsNullOrEmpty(row.finishGroup) && !string.IsNullOrEmpty(row.group))
                    MarkGroupFinished(row.group);

                if (identifier == "start")
                    MarkTutorialCompleted();
                break;
            }

            Time.timeScale = resumeTimeScale;

            if (row.timePass > 0f)
            {
                inTimePassGap = true;
                if (disableAllButtonsActive)
                    ShowBlockerOverlay();
                HideTutorialView();
                yield return new WaitForSeconds(row.timePass);
                inTimePassGap = false;
            }
        }

        FinishTutorial();
        runningRoutine = null;
    }

    void BeginLine(TutorialInfo row)
    {
        Time.timeScale = 0f;

        ExecuteLogic(row.logic, true);

        if (tutorialView != null)
            tutorialView.Show(row.text ?? string.Empty);
        else
            ShowTutorialView();

        EnsureAllTutorialTargetsRegistered();

        allowedClickTarget = ResolveTarget(row.click);
        allowedClickCanvas = allowedClickTarget != null ? allowedClickTarget.Canvas : null;
        if (!string.IsNullOrEmpty(row.click) && allowedClickTarget == null)
            Debug.LogWarning($"Tutorial click target '{row.click}' was not found/registered.");

        requireHighlightTargetClick = allowedClickCanvas != null;

        var higherSortTarget = ResolveTarget(row.higherSort);
        if (higherSortTarget != null && higherSortTarget != allowedClickTarget)
            EnableHighlight(higherSortTarget);

        if (allowedClickTarget != null)
            EnableHighlight(allowedClickTarget);

        if (disableAllButtonsActive)
            ShowBlockerOverlay();

        RefreshInputBlock(allowedClickCanvas);
    }

    void EndLine()
    {
        ClearAllowedClickTarget();
        EndHigherSortHighlights();
        requireHighlightTargetClick = false;

        if (disableAllButtonsActive)
            RefreshInputBlock(null);
    }

    void FinishTutorial()
    {
        ClearAllowedClickTarget();
        CleanupCurrentLineState(restoreTime: true, removeBlocker: true);
        HideTutorialView();
        currentTutorialId = null;
    }

    void MarkTutorialCompleted()
    {
        var meta = MetaSaveService.Load();
        if (meta.TutorialCompleted)
            return;

        meta.TutorialCompleted = true;
        MetaSaveService.Save(meta);
        enableTutorial = false;
    }

    bool IsTutorialGroupFinished(string identifier)
    {
        var rows = CSVLoader.GetTutorialRows(identifier);
        for (int i = 0; i < rows.Count; i++)
        {
            string group = rows[i].group;
            if (string.IsNullOrEmpty(group))
                continue;

            if (finishedGroups.Contains(group))
                return true;
        }

        return false;
    }

    void MarkGroupFinished(string group)
    {
        if (string.IsNullOrEmpty(group))
            return;

        ReloadFinishedGroups();
        if (!finishedGroups.Add(group))
            return;

        var meta = MetaSaveService.Load();
        var list = new List<string>(finishedGroups);
        meta.FinishedTutorialGroups = list.ToArray();
        MetaSaveService.Save(meta);
    }

    void ReloadFinishedGroups()
    {
        finishedGroups.Clear();
        var meta = MetaSaveService.Load();
        if (meta.FinishedTutorialGroups == null)
            return;

        foreach (var group in meta.FinishedTutorialGroups)
        {
            if (!string.IsNullOrEmpty(group))
                finishedGroups.Add(group);
        }
    }

    void CaptureResumeTimeScaleFromCurrent()
    {
        if (Time.timeScale > 0f)
            resumeTimeScale = Time.timeScale;
    }

    void CleanupCurrentLineState(bool restoreTime, bool removeBlocker)
    {
        waitingForClick = false;
        waitingForLineInput = false;
        inTimePassGap = false;

        if (removeBlocker)
            ExecuteLogic("removeDisableAllButtons", true);
        else
            EndLine();

        if (restoreTime)
            Time.timeScale = resumeTimeScale;
    }

    void ExecuteLogic(string logic, bool apply)
    {
        if (!apply || string.IsNullOrEmpty(logic))
            return;

        if (logic == "addDisableAllButtons")
        {
            disableAllButtonsActive = true;
            ShowBlockerOverlay();
            return;
        }

        if (logic == "removeDisableAllButtons")
        {
            disableAllButtonsActive = false;
            ReleaseGameplayInput();
        }
    }

    void ReleaseGameplayInput()
    {
        ClearAllowedClickTarget();
        ClearInputBlock();
        HideBlockerOverlay();
    }

    void ShowBlockerOverlay()
    {
        if (!disableAllButtonsActive)
            return;

        if (tutorialView != null)
            tutorialView.SetDisableAllActive(true);
    }

    void HideBlockerOverlay()
    {
        if (tutorialView != null)
            tutorialView.SetDisableAllActive(false);
    }

    void RefreshInputBlock(Canvas allowed)
    {
        ClearInputBlock();
        if (!disableAllButtonsActive)
            return;

        var selectables = FindObjectsOfType<Selectable>(true);
        foreach (var selectable in selectables)
        {
            if (selectable == null || !selectable.interactable)
                continue;

            if (IsBlockerHit(selectable.transform))
                continue;

            if (allowed != null && IsUnderCanvas(selectable.transform, allowed))
                continue;

            selectable.interactable = false;
            disabledSelectables.Add(selectable);
        }
    }

    void ClearInputBlock()
    {
        for (int i = disabledSelectables.Count - 1; i >= 0; i--)
        {
            var selectable = disabledSelectables[i];
            if (selectable != null)
                selectable.interactable = true;
        }

        disabledSelectables.Clear();
    }

    void EnableHighlight(TutorialGameobject target)
    {
        if (target == null || activeHighlights.Contains(target))
            return;

        Transform host = tutorialView != null ? tutorialView.HighlightRoot : null;
        target.RaiseSorting(host);
        activeHighlights.Add(target);
    }

    void EndHigherSortHighlights()
    {
        for (int i = activeHighlights.Count - 1; i >= 0; i--)
        {
            var target = activeHighlights[i];
            if (target == null || target == allowedClickTarget)
                continue;

            target.RestoreSorting();
            activeHighlights.RemoveAt(i);
        }
    }

    void ClearAllowedClickTarget()
    {
        if (allowedClickTarget != null)
        {
            allowedClickTarget.RestoreSorting();
            activeHighlights.Remove(allowedClickTarget);
        }

        allowedClickTarget = null;
        allowedClickCanvas = null;
    }

    void EnsureAllTutorialTargetsRegistered()
    {
        var targets = FindObjectsOfType<TutorialGameobject>(true);
        for (int i = 0; i < targets.Length; i++)
            RegisterTutorialGameobject(targets[i]);
    }

    TutorialGameobject ResolveTarget(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return null;

        targetsByIdentifier.TryGetValue(identifier, out var target);
        return target;
    }

    bool TryGetTopHitUnderClickTarget(List<RaycastResult> results, out GameObject hitObject)
    {
        hitObject = null;
        if (allowedClickCanvas == null || results == null || results.Count == 0)
            return false;

        for (int i = 0; i < results.Count; i++)
        {
            var hit = results[i].gameObject;
            if (hit == null)
                continue;

            if (IsUnderCanvas(hit.transform, allowedClickCanvas))
            {
                hitObject = hit;
                return true;
            }
        }

        return false;
    }

    void PropagateTopRaycastClick(List<RaycastResult> results)
    {
        if (results == null || results.Count == 0)
            return;

        for (int i = 0; i < results.Count; i++)
        {
            var hit = results[i].gameObject;
            if (hit == null || IsBlockerHit(hit.transform))
                continue;

            PropagatePointerClick(hit);
            return;
        }
    }

    bool TryRaycastUi(out List<RaycastResult> results)
    {
        results = raycastResults;
        results.Clear();

        if (EventSystem.current == null)
            return false;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition,
            button = PointerEventData.InputButton.Left
        };
        EventSystem.current.RaycastAll(pointerData, results);
        return true;
    }

    void PropagatePointerClick(GameObject hitObject)
    {
        if (hitObject == null || EventSystem.current == null)
            return;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition,
            button = PointerEventData.InputButton.Left
        };

        var button = hitObject.GetComponentInParent<Button>();
        if (button != null && button.interactable && button.gameObject.activeInHierarchy)
        {
            ExecuteEvents.Execute(button.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
            return;
        }

        var selectable = hitObject.GetComponentInParent<Selectable>();
        if (selectable != null && selectable.interactable && selectable.gameObject.activeInHierarchy)
            ExecuteEvents.Execute(selectable.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
    }

    static bool IsUnderCanvas(Transform transform, Canvas canvas)
    {
        if (transform == null || canvas == null)
            return false;

        return transform == canvas.transform || transform.IsChildOf(canvas.transform);
    }

    bool IsBlockerHit(Transform transform)
    {
        var blocker = tutorialView != null ? tutorialView.DisableAllButton : null;
        return blocker != null
            && blocker.activeInHierarchy
            && (transform == blocker.transform || transform.IsChildOf(blocker.transform));
    }

    void ShowTutorialView()
    {
        if (tutorialView != null)
            tutorialView.Show(string.Empty);
    }

    void HideTutorialView()
    {
        if (tutorialView != null)
            tutorialView.Hide();
    }

    void OnDisable()
    {
        if (runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
            runningRoutine = null;
        }

        CleanupCurrentLineState(restoreTime: true, removeBlocker: true);
    }
}
