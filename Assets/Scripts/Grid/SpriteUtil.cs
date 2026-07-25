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

    /// <summary>
    /// Prefer a SpriteRenderer on a child; fall back to self; optionally add one.
    /// </summary>
    public static SpriteRenderer ResolveRenderer(GameObject go, bool addIfMissing = true)
    {
        if (go == null)
            return null;

        var all = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject != go)
                return all[i];
        }

        var self = go.GetComponent<SpriteRenderer>();
        if (self != null)
            return self;

        return addIfMissing ? go.AddComponent<SpriteRenderer>() : null;
    }
}
