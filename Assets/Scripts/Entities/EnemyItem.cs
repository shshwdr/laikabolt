using DG.Tweening;
using UnityEngine;

public class EnemyItem : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public int HitsLeft { get; private set; }
    public bool IsAlive => HitsLeft > 0 && !_flying;

    SpriteRenderer _sr;
    GridBoard _board;
    GameData _data;
    bool _flying;

    public void Setup(GridBoard board, GameData data, Vector2Int cell, Sprite sprite)
    {
        _board = board;
        _data = data;
        GridPos = cell;
        HitsLeft = data.enemyHitsToKill;
        transform.position = board.CellToWorld(cell);

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = sprite;
        _sr.sortingOrder = 8;
        GridBoard.FitSprite(_sr, data.cellSize * 0.85f);

        board.RegisterEnemy(cell, this);
    }

    /// <summary>Apply hit; returns true if the enemy died and flies away.</summary>
    public bool TakeHit(Vector2Int fromCell)
    {
        if (!IsAlive) return true;

        HitsLeft--;
        transform.DOKill(false);
        transform.DOPunchScale(Vector3.one * 0.15f, 0.12f, 4, 0.5f);

        if (HitsLeft > 0)
            return false;

        FlyAway(fromCell);
        return true;
    }

    void FlyAway(Vector2Int fromCell)
    {
        _flying = true;
        _board.UnregisterEnemy(GridPos);

        Vector2 dir = (Vector2)(GridPos - fromCell);
        if (dir.sqrMagnitude < 0.01f)
            dir = Vector2.up;
        dir.Normalize();

        // Convert grid direction to world (row flips Y).
        Vector3 worldDir = new Vector3(dir.x, -dir.y, 0f).normalized;
        Vector3 end = transform.position + worldDir * _data.enemyFlyDistance;

        transform.DOKill();
        var seq = DOTween.Sequence();
        seq.Join(transform.DOJump(end, _data.enemyFlyJumpPower, 1, _data.enemyFlyDuration).SetEase(Ease.OutQuad));
        seq.Join(_sr.DOFade(0f, _data.enemyFlyDuration));
        seq.Join(transform.DOScale(transform.localScale * 0.3f, _data.enemyFlyDuration));
        seq.OnComplete(() => Destroy(gameObject));
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}
