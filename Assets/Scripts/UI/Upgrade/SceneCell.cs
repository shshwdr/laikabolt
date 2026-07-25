using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One planet entry on the upgrade screen. Wire references on the sceneCell prefab.
/// Player marker offset is local to Icon.
/// </summary>
public class SceneCell : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text label;
    [SerializeField] Vector2 playerMarkerOffset = new Vector2(48f, 36f);

    public string Identifier { get; private set; }
    public Button Button => button;
    public Image Icon => icon;
    public TMP_Text Label => label;
    public Vector2 PlayerMarkerOffset => playerMarkerOffset;

    public void SetIdentifier(string identifier)
    {
        Identifier = identifier;
    }
}
