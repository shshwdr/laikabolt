using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Starts FMOD gameplay music after banks are ready.
/// WebGL loads banks asynchronously via UnityWebRequest — must wait for HaveAllBanksLoaded.
/// Pattern matches littleGuys GameBootstrap music bootstrap.
/// </summary>
public class FmodMusicPlayer : MonoBehaviour
{
    [Header("FMOD Music")]
    [SerializeField] FMODUnity.EventReference gameplayMusicEvent;
    [SerializeField] string gameplayMusicPath = "event:/Music/mus_gameplay";

    FMOD.Studio.EventInstance musicInstance;
    Coroutine musicRoutine;

    void Start()
    {
        musicRoutine = StartCoroutine(StartMusicWhenReady());
    }

    void OnDestroy()
    {
        StopMusic();
    }

    IEnumerator StartMusicWhenReady()
    {
        // WebGL loads banks asynchronously; music in Awake/Start is often too early.
        while (!FMODUnity.RuntimeManager.IsInitialized || !FMODUnity.RuntimeManager.HaveAllBanksLoaded)
            yield return null;

        TryStartMusic();
        musicRoutine = null;
    }

    void TryStartMusic()
    {
        if (musicInstance.isValid())
        {
            FMOD.Studio.PLAYBACK_STATE state;
            musicInstance.getPlaybackState(out state);
            if (state == FMOD.Studio.PLAYBACK_STATE.PLAYING)
                return;
            StopMusicInstance();
        }

        try
        {
            if (!gameplayMusicEvent.IsNull)
                musicInstance = FMODUnity.RuntimeManager.CreateInstance(gameplayMusicEvent);
            else if (!string.IsNullOrEmpty(gameplayMusicPath))
                musicInstance = FMODUnity.RuntimeManager.CreateInstance(gameplayMusicPath);
            else
                return;

            musicInstance.start();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FMOD] Failed to start music: {e.Message}");
        }
    }

    void StopMusic()
    {
        if (musicRoutine != null)
        {
            StopCoroutine(musicRoutine);
            musicRoutine = null;
        }
        StopMusicInstance();
    }

    void StopMusicInstance()
    {
        if (!musicInstance.isValid())
            return;

        musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        musicInstance.release();
        musicInstance.clearHandle();
    }
}
