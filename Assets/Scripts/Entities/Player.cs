using UnityEngine;

/// <summary>
/// Player visual: switches among 8 direction sprites (idle + dash).
/// Sprites load from Resources/player/{up,down,left,right}[_dash].
/// </summary>
public class Player : MonoBehaviour
{
    const string ResourceRoot = "player/";

    SpriteRenderer _sr;
    Sprite _up;
    Sprite _down;
    Sprite _left;
    Sprite _right;
    Sprite _upDash;
    Sprite _downDash;
    Sprite _leftDash;
    Sprite _rightDash;

    Vector2Int _facing = new Vector2Int(0, 1);
    bool _dash;

    public SpriteRenderer Renderer => _sr;

    public void Init(float cellSize)
    {
        _sr = SpriteUtil.ResolveRenderer(gameObject);

        LoadSprites();
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

    void LoadSprites()
    {
        _up = Resources.Load<Sprite>(ResourceRoot + "up");
        _down = Resources.Load<Sprite>(ResourceRoot + "down");
        _left = Resources.Load<Sprite>(ResourceRoot + "left");
        _right = Resources.Load<Sprite>(ResourceRoot + "right");
        _upDash = Resources.Load<Sprite>(ResourceRoot + "up_dash");
        _downDash = Resources.Load<Sprite>(ResourceRoot + "down_dash");
        _leftDash = Resources.Load<Sprite>(ResourceRoot + "left_dash");
        _rightDash = Resources.Load<Sprite>(ResourceRoot + "right_dash");
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
            return dash ? Prefer(_upDash, _up) : _up;
        if (dir.y > 0)
            return dash ? Prefer(_downDash, _down) : _down;
        if (dir.x < 0)
            return dash ? Prefer(_leftDash, _left) : _left;
        if (dir.x > 0)
            return dash ? Prefer(_rightDash, _right) : _right;
        return dash ? Prefer(_downDash, _down) : _down;
    }

    static Sprite Prefer(Sprite preferred, Sprite fallback) =>
        preferred != null ? preferred : fallback;
}
