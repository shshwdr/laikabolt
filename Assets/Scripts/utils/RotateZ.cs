using UnityEngine;

/// <summary>
/// Continuously rotates this transform around the local Z axis.
/// </summary>
public class RotateZ : MonoBehaviour
{
    [SerializeField] float degreesPerSecond = 90f;
    [SerializeField] bool playOnEnable = true;
    [SerializeField] bool useUnscaledTime;

    bool _playing;

    void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        _playing = true;
    }

    public void Stop()
    {
        _playing = false;
    }

    void Update()
    {
        if (!_playing)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        transform.Rotate(0f, 0f, degreesPerSecond * dt, Space.Self);
    }
}
