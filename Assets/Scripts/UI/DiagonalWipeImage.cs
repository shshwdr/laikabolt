using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 45° diagonal wipe between two same-sized UI images (light-sweep look).
/// Hang on the Image that should stay visible (fromImage). Assign toImage + wipe material.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class DiagonalWipeImage : MonoBehaviour
{
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int SoftId = Shader.PropertyToID("_Soft");
    static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
    static readonly int SecondTexId = Shader.PropertyToID("_SecondTex");
    static readonly int AngleId = Shader.PropertyToID("_Angle");

    [Header("Images")]
    [Tooltip("Shown at progress 0 (uses this Image's sprite as A). Usually this GameObject's Image.")]
    [SerializeField] Image fromImage;
    [Tooltip("Source of sprite B. Will be hidden; wipe draws B through the shader.")]
    [SerializeField] Image toImage;

    [Header("Material")]
    [SerializeField] Material wipeMaterialTemplate;

    [Header("Wipe")]
    [SerializeField] float duration = 0.85f;
    [SerializeField] Ease ease = Ease.InOutSine;
    [SerializeField] float softEdge = 0.045f;
    [SerializeField] Color glowColor = new Color(1f, 0.95f, 0.75f, 1f);
    [SerializeField] float glowStrength = 0.35f;
    [Tooltip("0 = left→right, 45 = classic diagonal, 90 = bottom→top, 135 = other diagonal, etc.")]
    [SerializeField] [Range(0f, 360f)] float angleDegrees = 45f;
    [SerializeField] bool useUnscaledTime = true;

    Material _mat;
    Tween _tween;
    bool _playing;

    public bool IsPlaying => _playing;

    void Awake()
    {
        if (fromImage == null)
            fromImage = GetComponent<Image>();
        EnsureMaterial();
        ResetWipe();
    }

    void OnDestroy()
    {
        KillTween();
        if (_mat != null)
            Destroy(_mat);
    }

    public void ResetWipe()
    {
        KillTween();
        _playing = false;
        EnsureMaterial();
        if (_mat != null)
            _mat.SetFloat(ProgressId, 0f);
        if (toImage != null)
            toImage.enabled = false;
        DirtyGraphic();
    }

    /// <summary>Plays A→B wipe, then invokes onComplete.</summary>
    public void Play(Action onComplete = null)
    {
        EnsureMaterial();
        if (_mat == null || fromImage == null)
        {
            Debug.LogWarning("[DiagonalWipeImage] Cannot play wipe (missing material or fromImage).", this);
            onComplete?.Invoke();
            return;
        }

        KillTween();
        _playing = true;
        if (toImage != null)
            toImage.enabled = false;

        ApplySettings();
        SetProgress(0f);

        _tween = DOTween.To(() => _mat.GetFloat(ProgressId), SetProgress, 1f, Mathf.Max(0.01f, duration))
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                _playing = false;
                _tween = null;
                onComplete?.Invoke();
            });
    }

    void SetProgress(float value)
    {
        if (_mat == null)
            return;
        _mat.SetFloat(ProgressId, value);
        DirtyGraphic();
    }

    void DirtyGraphic()
    {
        if (fromImage != null)
            fromImage.SetMaterialDirty();
    }

    void EnsureMaterial()
    {
        if (fromImage == null)
            return;

        if (_mat == null)
        {
            if (wipeMaterialTemplate == null)
            {
                // Fallback: load from Assets/Materials if reference was lost in scene.
                wipeMaterialTemplate = Resources.Load<Material>("UIDiagonalWipe");
            }
            if (wipeMaterialTemplate == null)
            {
                var shader = Shader.Find("UI/DiagonalWipe");
                if (shader != null)
                    wipeMaterialTemplate = new Material(shader);
            }
            if (wipeMaterialTemplate == null)
            {
                Debug.LogWarning("[DiagonalWipeImage] Assign wipeMaterialTemplate (UIDiagonalWipe).", this);
                return;
            }
            _mat = Instantiate(wipeMaterialTemplate);
            _mat.name = wipeMaterialTemplate.name + " (Instance)";
        }

        fromImage.material = _mat;
        ApplySecondTexture();
        ApplySettings();
        DirtyGraphic();
    }

    void ApplySecondTexture()
    {
        if (_mat == null)
            return;

        Texture tex = null;
        if (toImage != null && toImage.sprite != null)
            tex = toImage.sprite.texture;
        if (tex != null)
            _mat.SetTexture(SecondTexId, tex);
    }

    void ApplySettings()
    {
        if (_mat == null)
            return;
        _mat.SetFloat(SoftId, softEdge);
        _mat.SetColor(GlowColorId, glowColor);
        _mat.SetFloat(GlowStrengthId, glowStrength);
        _mat.SetFloat(AngleId, angleDegrees);
    }

    void KillTween()
    {
        if (_tween != null && _tween.IsActive())
            _tween.Kill();
        _tween = null;
    }
}
