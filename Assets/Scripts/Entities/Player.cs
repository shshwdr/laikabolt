using UnityEngine;

/// <summary>
/// Player visual: switches among 8 direction sprites (idle + dash).
/// Assign in Inspector; unset slots fall back to Resources/player/{up,down,left,right}[_dash].
/// </summary>
public class Player : MonoBehaviour
{
    const string ResourceRoot = "player/";

    [Header("Idle")]
    [SerializeField] Sprite up;
    [SerializeField] Sprite down;
    [SerializeField] Sprite left;
    [SerializeField] Sprite right;

    [Header("Dash")]
    [SerializeField] Sprite upDash;
    [SerializeField] Sprite downDash;
    [SerializeField] Sprite leftDash;
    [SerializeField] Sprite rightDash;

    SpriteRenderer _sr;
    Vector2Int _facing = new Vector2Int(0, 1);
    bool _dash;

    public SpriteRenderer Renderer => _sr;

    public void Init(float cellSize)
    {
        _sr = SpriteUtil.ResolveRenderer(gameObject);

        ResolveSprites();
        _sr.sortingOrder = 10;
        ApplySprite();
        MainGameObject.Fit(gameObject, _sr, cellSize);
    }

    public void SetFacing(Vector2Int dir)
    {
        if (dir == Vector2Int.zero)
            return;
        if (_facing == dir)
            return;
        _facing = dir;
        ApplySprite();
    }

    public void SetDash(bool dash)
    {
        if (_dash == dash)
            return;
        _dash = dash;
        ApplySprite();
    }

    public void SetVisual(Vector2Int dir, bool dash)
    {
        if (dir != Vector2Int.zero)
            _facing = dir;
        _dash = dash;
        ApplySprite();
    }

    void ResolveSprites()
    {
        up = SpriteUtil.LoadOr(up, ResourceRoot + "up");
        down = SpriteUtil.LoadOr(down, ResourceRoot + "down");
        left = SpriteUtil.LoadOr(left, ResourceRoot + "left");
        right = SpriteUtil.LoadOr(right, ResourceRoot + "right");
        upDash = SpriteUtil.LoadOr(upDash, ResourceRoot + "up_dash");
        downDash = SpriteUtil.LoadOr(downDash, ResourceRoot + "down_dash");
        leftDash = SpriteUtil.LoadOr(leftDash, ResourceRoot + "left_dash");
        rightDash = SpriteUtil.LoadOr(rightDash, ResourceRoot + "right_dash");
    }

    void ApplySprite()
    {
        if (_sr == null)
            return;

        Sprite sprite = ResolveSprite(_facing, _dash);
        if (sprite == null)
            sprite = SpriteUtil.WhiteSprite();
        _sr.sprite = sprite;
    }

    Sprite ResolveSprite(Vector2Int dir, bool dash)
    {
        if (dir.y < 0)
            return dash ? Prefer(upDash, up) : up;
        if (dir.y > 0)
            return dash ? Prefer(downDash, down) : down;
        if (dir.x < 0)
            return dash ? Prefer(leftDash, left) : left;
        if (dir.x > 0)
            return dash ? Prefer(rightDash, right) : right;
        return dash ? Prefer(downDash, down) : down;
    }

    static Sprite Prefer(Sprite preferred, Sprite fallback) =>
        preferred != null ? preferred : fallback;
}
