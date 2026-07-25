using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using FMODUnity;

/// <summary>
/// Boss: collectFly — UFO that flees on touch until hit count is reached, then can be carried to the hole.
/// </summary>
public class BossCollectFly : MonoBehaviour
{
    const float DropDuration = 0.35f;
    const float DropHeightCells = 2.2f;

    public Vector2Int GridPos { get; private set; }
    public bool IsCaught { get; private set; }
    public bool IsFlying => _flying;

    GridBoard _board;
    GameData _data;
    GameManager _game;
    PlayerController _player;
    SpriteRenderer _sr;
    int _hitsNeeded;
    int _minDistance;
    int _hits;
    bool _flying;
    readonly List<Vector2Int> _candidates = new List<Vector2Int>(64);

    public void Setup(
        GridBoard board,
        GameData data,
        GameManager game,
        PlayerController player,
        Vector2Int cell,
        int hitsNeeded,
        int minDistance)
    {
        _board = board;
        _data = data;
        _game = game;
        _player = player;
        _hitsNeeded = Mathf.Max(1, hitsNeeded);
        _minDistance = Mathf.Max(1, minDistance);
        GridPos = cell;

        Vector3 end = board.CellToWorld(cell);
        transform.position = end + Vector3.up * (data.cellSize * DropHeightCells);

        _sr = SpriteUtil.ResolveRenderer(gameObject);
        if (_sr.sprite == null)
            _sr.sprite = SpriteUtil.WhiteSprite();
        _sr.sortingOrder = 9;
        MainGameObject.Fit(gameObject, _sr, data.cellSize);

        board.RegisterBoss(this);

        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/NPC/sx_npc_ufo_spawn");

        _flying = true;
        transform.DOKill();
        transform.DOMove(end, DropDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => { _flying = false; });
    }

    /// <summary>Player stepped on / dashed through this cell. Returns true if caught this touch.</summary>
    public bool TryTouch(PlayerController player, bool requirePlayerOnCell = true)
    {
        if (IsCaught || _flying || player == null)
            return false;
        if (requirePlayerOnCell && player.GridPos != GridPos)
            return false;

        _hits++;
        if (_hits >= _hitsNeeded)
        {
            Catch(player);
            return true;
        }

        Flee();
        return false;
    }

    void Catch(PlayerController player)
    {
        IsCaught = true;
        transform.DOKill();
        if (_board != null)
            _board.UnregisterBoss(this);
        player.ReceiveBoss(this);
    }

    public void SnapCarryStack(int stackIndex, GameData data)
    {
        float y = data.carryBaseY + stackIndex * data.carryStackHeightStep;
        transform.localPosition = new Vector3(0f, y, 0f);
        if (_sr != null)
            _sr.sortingOrder = 12 + stackIndex;
    }

    public void AttachToCarry(Transform carryRoot, int stackIndex, GameData data)
    {
        transform.SetParent(carryRoot, true);
        transform.DOKill();

        Vector3 halfScale = transform.localScale * data.carryScale;
        float y = data.carryBaseY + stackIndex * data.carryStackHeightStep;
        var seq = DOTween.Sequence();
        seq.Join(transform.DOLocalMove(new Vector3(0f, y, 0f), 0.15f).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(halfScale, 0.15f).SetEase(Ease.OutQuad));
        if (_sr != null)
            _sr.sortingOrder = 12 + stackIndex;
    }

    public void DepositAndDestroy(Vector3 target, float duration)
    {
        transform.SetParent(null, true);
        transform.DOKill();
        var seq = DOTween.Sequence();
        seq.Join(transform.DOMove(target, duration).SetEase(Ease.InQuad));
        seq.Join(transform.DOScale(Vector3.zero, duration).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(gameObject));
    }

    void Flee()
    {
        Vector2Int from = _player != null ? _player.GridPos : GridPos;
        if (!TryPickFarCell(from, out var next))
            return;

        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/NPC/sx_npc_ufo_move");

        _flying = true;
        Vector3 world = _board.CellToWorld(next);
        transform.DOKill();
        transform.DOMove(world, 0.1f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                GridPos = next;
                _flying = false;
            });
    }

    bool IsBlockedCell(Vector2Int c)
    {
        if (!_board.Map.IsWalkable(c.x, c.y))
            return true;
        if (_board.Map.IsStart(c.x, c.y))
            return true;
        if (_board.HasFood(c) || _board.HasEnemy(c) || _board.HasHazard(c))
            return true;
        return false;
    }

    bool TryPickFarCell(Vector2Int from, out Vector2Int cell)
    {
        _candidates.Clear();
        int bestDist = -1;
        Vector2Int best = default;

        for (int row = 0; row < _board.Map.Height; row++)
        {
            for (int col = 0; col < _board.Map.Width; col++)
            {
                var c = new Vector2Int(col, row);
                if (c == from || c == GridPos)
                    continue;
                if (IsBlockedCell(c))
                    continue;

                int dist = Mathf.Abs(c.x - from.x) + Mathf.Abs(c.y - from.y);
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = c;
                }

                if (dist >= _minDistance)
                    _candidates.Add(c);
            }
        }

        if (_candidates.Count > 0)
        {
            cell = _candidates[Random.Range(0, _candidates.Count)];
            return true;
        }

        if (bestDist > 0)
        {
            cell = best;
            return true;
        }

        cell = default;
        return false;
    }

    public static bool TryPickSpawnCell(
        GridBoard board,
        Vector2Int from,
        int minDistance,
        out Vector2Int cell)
    {
        var far = new List<Vector2Int>(32);
        int bestDist = -1;
        Vector2Int best = default;

        for (int row = 0; row < board.Map.Height; row++)
        {
            for (int col = 0; col < board.Map.Width; col++)
            {
                var c = new Vector2Int(col, row);
                if (c == from)
                    continue;
                if (!board.Map.IsWalkable(col, row))
                    continue;
                if (board.Map.IsStart(col, row))
                    continue;
                if (board.HasFood(c) || board.HasEnemy(c) || board.HasRobot(c) || board.HasHazard(c))
                    continue;

                int dist = Mathf.Abs(c.x - from.x) + Mathf.Abs(c.y - from.y);
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = c;
                }

                if (dist >= minDistance)
                    far.Add(c);
            }
        }

        if (far.Count > 0)
        {
            cell = far[Random.Range(0, far.Count)];
            return true;
        }

        if (bestDist > 0)
        {
            cell = best;
            return true;
        }

        cell = default;
        return false;
    }

    void OnDestroy()
    {
        if (_board != null)
            _board.UnregisterBoss(this);
        transform.DOKill();
    }
}
