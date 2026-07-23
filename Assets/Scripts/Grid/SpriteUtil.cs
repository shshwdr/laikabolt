using UnityEngine;

public static class SpriteUtil
{
    static Sprite _white;

    public static Sprite WhiteSprite()
    {
        if (_white != null) return _white;
        var tex = Texture2D.whiteTexture;
        _white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
        return _white;
    }

    public static Sprite LoadOr(Sprite overrideSprite, string resourcesPath)
    {
        if (overrideSprite != null) return overrideSprite;
        return Resources.Load<Sprite>(resourcesPath);
    }
}
