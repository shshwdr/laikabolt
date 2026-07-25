using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] private EventReference musicEvent;

    private EventInstance _musicInstance;

    private void Awake()
    {
        // Keep only one instance playing across scene reloads/transitions
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitMusic();
    }

    private void InitMusic()
    {
        if (musicEvent.IsNull) return;

        _musicInstance = RuntimeManager.CreateInstance(musicEvent);
        _musicInstance.start();

        // Start in Upgrade mode (0)
        SetGameState(0f);
    }

    public void SetGameState(float stateValue)
    {
        if (_musicInstance.isValid())
        {
            _musicInstance.setParameterByName("Game State", stateValue);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this && _musicInstance.isValid())
        {
            _musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _musicInstance.release();
        }
    }
}