using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

/// <summary>
/// Scene-wired game over panel. Assign panel / continue button / score text in the Inspector.
/// </summary>
public class GameOverView : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Button continueButton;
    [SerializeField] TMP_Text scoreText;

    Action onContinue;

    public void Setup(Action continueCallback)
    {
        onContinue = continueCallback;

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        Hide();
    }

    public void Show(int score, bool cleared = false)
    {
        if (scoreText != null)
        {
            scoreText.text = cleared
                ? $"Cleared!\nCollected: {score}"
                : $"Time's up!\nCollected: {score}";
        }

        SetPanelActive(true);

        RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_gameEnd");
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

    void OnContinueClicked()
    {
        RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_select");
        Hide();
        onContinue?.Invoke();
    }
}
