using DG.Tweening;
using UnityEngine;
using FMODUnity;

public class EnemyItem : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public int HitsLeft { get; private set; }
    public bool IsAlive => HitsLeft > 0 && !_flying;

    SpriteRenderer _sr;
    GridBoard _board;
    GameData _data;
    System.Action<Vector2Int> _onKilled;
    EnemyHpBar _hpBar;
    bool _flying;

    public void Setup(
        GridBoard board,
        GameData data,
        Vector2Int cell,
        System.Action<Vector2Int> onKilled = null)
    {
        _board = board;
        _data = data;
        _onKilled = onKilled;
        GridPos = cell;
        HitsLeft = data.enemyHitsToKill;
        transform.position = board.CellToWorld(cell);

        _sr = SpriteUtil.ResolveRenderer(gameObject);
        if (_sr.sprite == null)
            _sr.sprite = SpriteUtil.WhiteSprite();
        _sr.sortingOrder = 8;
        MainGameObject.Fit(gameObject, _sr, data.cellSize);

        board.RegisterEnemy(cell, this);

        _hpBar = GetComponent<EnemyHpBar>();
        if (_hpBar == null)
            _hpBar = gameObject.AddComponent<EnemyHpBar>();
        _hpBar.Setup(HitsLeft);
    }

    /// <summary>Apply damage; returns true if the enemy died and flies away.</summary>
    public bool TakeHit(Vector2Int fromCell, int damage = 1)
    {
        if (!IsAlive) return true;

        int applied = Mathf.Max(1, damage);
        HitsLeft -= applied;
        transform.DOKill(false);
        transform.DOPunchScale(Vector3.one * 0.15f, 0.12f, 4, 0.5f);
        DamageNumber.Spawn(transform.position, applied);
        _hpBar?.SetHp(Mathf.Max(0, HitsLeft));

        if (HitsLeft > 0)
        {
                FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/NPC/sx_npc_enemy_damage");
            return false;
        }

            FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/NPC/sx_npc_enemy_death");

        FlyAway(fromCell);
        return true;
    }

    void FlyAway(Vector2Int fromCell)
    {
        _flying = true;
        _hpBar?.SetVisible(false);
        var dropCell = GridPos;
        _board.UnregisterEnemy(GridPos);
        _onKilled?.Invoke(dropCell);

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
