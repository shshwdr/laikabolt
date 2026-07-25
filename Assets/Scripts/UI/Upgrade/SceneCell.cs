using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One planet entry on the upgrade screen. Wire references on the sceneCell prefab.
/// Player marker is a child placed in the prefab; code only toggles visibility.
/// </summary>
public class SceneCell : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text label;
    [SerializeField] GameObject player;

    public string Identifier { get; private set; }
    public Button Button => button;
    public Image Icon => icon;
    public TMP_Text Label => label;
    public GameObject Player => player;

    public void SetIdentifier(string identifier)
    {
        Identifier = identifier;
    }

    public void SetPlayerVisible(bool visible)
    {
        if (player != null)
            player.SetActive(visible);
    }
}
