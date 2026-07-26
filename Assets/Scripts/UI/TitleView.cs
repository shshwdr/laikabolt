using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title screen with a single Start button. Assign in the Inspector.
/// Optional diagonal wipe plays before the start callback (then story).
/// </summary>
public class TitleView : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Button startButton;
    [SerializeField] DiagonalWipeImage startWipe;

    Action onStart;
    bool _starting;

    void Awake()
    {
        ResolveWipe();
    }

    public void Setup(Action startCallback)
    {
        onStart = startCallback;
        _starting = false;
        ResolveWipe();

        if (startWipe != null)
            startWipe.ResetWipe();

        if (startButton != null)
        {
            startButton.interactable = true;
            startButton.onClick.RemoveListener(OnStartClicked);
            startButton.onClick.AddListener(OnStartClicked);
        }
    }

    public void Show()
    {
        SetPanelActive(true);
    }

    public void Hide()
    {
        SetPanelActive(false);
    }

    void SetPanelActive(bool active)
    {
        if (panel != null)
            panel.SetActive(active);
        else
            gameObject.SetActive(active);
    }

    void OnStartClicked()
    {
        if (_starting)
            return;
        _starting = true;

        if (startButton != null)
            startButton.interactable = false;

        ResolveWipe();
        if (startWipe != null)
        {
            Debug.Log("[TitleView] Playing start wipe, then enter story.");
            startWipe.Play(InvokeStart);
        }
        else
        {
            Debug.LogWarning("[TitleView] startWipe missing — entering story immediately.");
            InvokeStart();
        }
    }

    void InvokeStart()
    {
        onStart?.Invoke();
    }

    void ResolveWipe()
    {
        if (startWipe != null)
            return;
        startWipe = GetComponentInChildren<DiagonalWipeImage>(true);
    }
}
