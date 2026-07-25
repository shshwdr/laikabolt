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

    public Button Button => button;
    public Image Icon => icon;
    public TMP_Text Label => label;

    public void Bind(Button buttonRef, Image iconRef, TMP_Text labelRef)
    {
        button = buttonRef;
        icon = iconRef;
        label = labelRef;
    }
}
