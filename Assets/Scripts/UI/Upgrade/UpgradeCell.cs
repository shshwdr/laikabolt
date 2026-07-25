using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One upgrade node on the upgrade tree. Wire references on the upgradeCell prefab.
/// </summary>
public class UpgradeCell : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text label;

    [Header("Label Colors")]
    [SerializeField] Color labelColor = Color.black;
    [SerializeField] Color labelMaxedColor = new Color(0.45f, 0.45f, 0.48f, 1f);

    [Header("Icon Colors")]
    [SerializeField] Color iconMaxedColor = new Color(0.12f, 0.12f, 0.14f, 1f);
    [SerializeField] Color iconLockedColor = new Color(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] Color iconAffordableColor = new Color(0.25f, 0.45f, 0.8f, 1f);
    [SerializeField] Color iconCannotAffordColor = new Color(0.15f, 0.25f, 0.55f, 1f);

    public Button Button => button;
    public Image Icon => icon;
    public TMP_Text Label => label;

    public Color LabelColor => labelColor;
    public Color LabelMaxedColor => labelMaxedColor;
    public Color IconMaxedColor => iconMaxedColor;
    public Color IconLockedColor => iconLockedColor;
    public Color IconAffordableColor => iconAffordableColor;
    public Color IconCannotAffordColor => iconCannotAffordColor;

    public void Bind(Button buttonRef, Image iconRef, TMP_Text labelRef)
    {
        button = buttonRef;
        icon = iconRef;
        label = labelRef;
    }

    public void ApplyVisualState(bool maxed, bool locked, bool canBuy)
    {
        if (label != null)
            label.color = maxed ? labelMaxedColor : labelColor;

        if (icon == null)
            return;

        if (maxed)
            icon.color = iconMaxedColor;
        else if (locked)
            icon.color = iconLockedColor;
        else if (canBuy)
            icon.color = iconAffordableColor;
        else
            icon.color = iconCannotAffordColor;
    }
}
