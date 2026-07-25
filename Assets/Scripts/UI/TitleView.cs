using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title screen with a single Start button. Assign in the Inspector.
/// </summary>
public class TitleView : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Button startButton;

    Action onStart;

    public void Setup(Action startCallback)
    {
        onStart = startCallback;

        if (startButton != null)
        {
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
        onStart?.Invoke();
    }
}
