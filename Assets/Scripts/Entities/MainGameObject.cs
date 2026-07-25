using UnityEngine;

/// <summary>
/// Shared prefab root settings. sizeScale is relative to cell size (1.3 = 1.3x cell).
/// </summary>
public class MainGameObject : MonoBehaviour
{
    [SerializeField] float sizeScale = 1f;

    public float SizeScale => sizeScale;

    public float WorldSize(float cellSize) => cellSize * sizeScale;

    public void FitRenderer(SpriteRenderer sr, float cellSize)
    {
        if (sr == null)
            return;
        GridBoard.FitSprite(sr, WorldSize(cellSize));
    }

    /// <summary>
    /// Fit using MainGameObject on <paramref name="go"/> if present; otherwise defaultScale.
    /// </summary>
    public static void Fit(GameObject go, SpriteRenderer sr, float cellSize, float defaultScale = 1f)
    {
        if (sr == null)
            return;

        float scale = defaultScale;
        if (go != null)
        {
            var main = go.GetComponent<MainGameObject>();
            if (main != null)
                scale = main.SizeScale;
        }

        GridBoard.FitSprite(sr, cellSize * scale);
    }
}
