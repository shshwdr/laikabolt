using System;
using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// Persistent gameplay music. Waits for FMOD banks before starting —
/// required on WebGL where banks load asynchronously via UnityWebRequest
/// (same pattern as littleGuys GameBootstrap).
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] private EventReference musicEvent;

    EventInstance _musicInstance;
    Coroutine _initRoutine;
    float _pendingGameState;
    bool _hasPendingGameState;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _initRoutine = StartCoroutine(InitMusicWhenReady());
    }

    IEnumerator InitMusicWhenReady()
    {
        // WebGL loads banks asynchronously; CreateInstance in Awake is too early.
        // SFX work later because they play after banks have finished loading.
        while (!RuntimeManager.IsInitialized || !RuntimeManager.HaveAllBanksLoaded)
            yield return null;

        TryStartMusic();
        _initRoutine = null;
    }

    void TryStartMusic()
    {
        if (musicEvent.IsNull)
            return;

        if (_musicInstance.isValid())
        {
            PLAYBACK_STATE state;
            _musicInstance.getPlaybackState(out state);
            if (state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING)
            {
                ApplyPendingGameState();
                return;
            }

            StopMusicInstance();
        }

        try
        {
            _musicInstance = RuntimeManager.CreateInstance(musicEvent);
            _musicInstance.start();
            ApplyPendingGameState();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FMOD] Failed to start music: {e.Message}");
        }
    }

    public void SetGameState(float stateValue)
    {
        _pendingGameState = stateValue;
        _hasPendingGameState = true;

        // Banks may still be loading, or the browser may have blocked autoplay
        // until a user gesture — retry start when game flow requests a state.
        if (!_musicInstance.isValid())
        {
            if (RuntimeManager.IsInitialized && RuntimeManager.HaveAllBanksLoaded)
                TryStartMusic();
            return;
        }

        _musicInstance.setParameterByName("Game State", stateValue);
    }

    void ApplyPendingGameState()
    {
        if (!_hasPendingGameState || !_musicInstance.isValid())
            return;

        _musicInstance.setParameterByName("Game State", _pendingGameState);
    }

    void StopMusicInstance()
    {
        if (!_musicInstance.isValid())
            return;

        _musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _musicInstance.release();
        _musicInstance.clearHandle();
    }

    void OnDestroy()
    {
        if (_initRoutine != null)
        {
            StopCoroutine(_initRoutine);
            _initRoutine = null;
        }

        if (Instance == this)
        {
            StopMusicInstance();
            Instance = null;
        }
    }
}
