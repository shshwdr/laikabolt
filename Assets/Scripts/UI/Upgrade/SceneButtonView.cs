using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One planet entry on the upgrade screen. Player marker offset is local to this button.
/// </summary>
public class SceneButtonView : MonoBehaviour
{
    [SerializeField] Vector2 playerMarkerOffset = new Vector2(48f, 36f);

    public string Identifier { get; private set; }
    public Button Button { get; private set; }
    public Image Icon { get; private set; }
    public TMP_Text Label { get; private set; }
    public Vector2 PlayerMarkerOffset => playerMarkerOffset;

    public void Bind(string identifier, Button button, Image icon, TMP_Text label, Vector2 markerOffset)
    {
        Identifier = identifier;
        Button = button;
        Icon = icon;
        Label = label;
        playerMarkerOffset = markerOffset;
    }

    public void SetPlayerMarkerOffset(Vector2 offset)
    {
        playerMarkerOffset = offset;
    }
}
