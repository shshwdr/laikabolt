using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public bool IsBusy { get; private set; }
    public int CarryCount => _carried.Count + (_carriedBoss != null ? 1 : 0);
    public bool CanCarryMore => CarryCount < _data.holdItemCount;
    public bool HasBoss => _carriedBoss != null;

    GridBoard _board;
    GameData _data;
    GameManager _game;
    SceneSpecialSystem _specials;
    Transform _carryRoot;
    readonly List<FoodItem> _carried = new List<FoodItem>();
    BossCollectFly _carriedBoss;
    SpriteRenderer _sr;
    Vector2Int _lastMoveDir;
    int _iceChainDepth;

    public void Setup(GridBoard board, GameData data, GameManager game, Vector2Int start, Sprite sprite)
    {
        _board = board;
        _data = data;
        _game = game;
        GridPos = start;
        transform.position = board.CellToWorld(start);

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = sprite;
        _sr.sortingOrder = 10;
        GridBoard.FitSprite(_sr, data.cellSize * 0.9f);

        var carryGo = new GameObject("CarryRoot");
        _carryRoot = carryGo.transform;
        _carryRoot.SetParent(transform, false);
        _carryRoot.localPosition = Vector3.zero;
    }

    public void BindSpecials(SceneSpecialSystem specials)
    {
        _specials = specials;
    }

    void Update()
    {
        if (_game == null || !_game.IsPlaying || IsBusy)
            return;

        Vector2Int dir = Vector2Int.zero;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            dir = new Vector2Int(0, -1);
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            dir = new Vector2Int(0, 1);
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            dir = new Vector2Int(-1, 0);
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            dir = new Vector2Int(1, 0);

        if (dir != Vector2Int.zero)
        {
            _iceChainDepth = 0;
            TryMove(dir);
        }
    }

    void TryMove(Vector2Int dir)
    {
        var target = GridPos + dir;
        bool outOfBounds = !_board.Map.InBounds(target.x, target.y);

        if (outOfBounds)
        {
            if (!_data.passBorder)
                return;
            if (!TryResolveWrappedMove(dir, out var landing, out bool jump))
                return;

            _game.NotifyPlayerActed();
            _lastMoveDir = dir;
            if (jump)
                JumpToWrapped(landing, dir);
            else
                MoveToWrapped(landing, dir);
            return;
        }

        if (_board.Map.IsWalkable(target.x, target.y))
        {
            if (_board.TryGetEnemy(target, out var enemy) && enemy.IsAlive)
            {
                _game.NotifyPlayerActed();
                _lastMoveDir = dir;
                BumpEnemy(target, enemy);
                return;
            }

            _game.NotifyPlayerActed();
            _lastMoveDir = dir;
            MoveTo(target);
            return;
        }

        if (_board.Map.GetCell(target.x, target.y) != MapCellType.Blocked)
            return;

        if (!TryFindJumpLanding(dir, out var jumpLanding, out bool jumpWrapped))
            return;

        _game.NotifyPlayerActed();
        _lastMoveDir = dir;
        if (jumpWrapped)
            JumpToWrapped(jumpLanding, dir);
        else
            JumpTo(jumpLanding);
    }

    /// <summary>Ice skate auto-slide in the arrival direction.</summary>
    void TryIceSlide(Vector2Int dir)
    {
        if (dir == Vector2Int.zero || IsBusy || _game == null || !_game.IsPlaying)
            return;

        int maxChain = _board.Map.Width * _board.Map.Height + 2;
        if (_iceChainDepth >= maxChain)
        {
            _iceChainDepth = 0;
            return;
        }

        var target = GridPos + dir;
        bool outOfBounds = !_board.Map.InBounds(target.x, target.y);

        if (outOfBounds)
        {
            if (!_data.passBorder)
                return;

            var wrapped = _board.WrapCell(GridPos + dir);
            if (wrapped != GridPos
                && _board.Map.IsWalkable(wrapped.x, wrapped.y)
                && _board.TryGetEnemy(wrapped, out var wrapEnemy)
                && wrapEnemy.IsAlive)
            {
                _iceChainDepth = 0;
                _lastMoveDir = dir;
                BumpEnemy(wrapped, wrapEnemy);
                return;
            }

            if (!TryResolveWrappedMove(dir, out var landing, out bool jump))
                return;

            _iceChainDepth++;
            _lastMoveDir = dir;
            if (jump)
                JumpToWrapped(landing, dir);
            else
                MoveToWrapped(landing, dir);
            return;
        }

        if (_board.Map.IsWalkable(target.x, target.y))
        {
            if (_board.TryGetEnemy(target, out var enemy) && enemy.IsAlive)
            {
                _iceChainDepth = 0;
                _lastMoveDir = dir;
                BumpEnemy(target, enemy);
                return;
            }

            _iceChainDepth++;
            _lastMoveDir = dir;
            MoveTo(target);
            return;
        }

        if (_board.Map.GetCell(target.x, target.y) != MapCellType.Blocked)
            return;

        if (!TryFindJumpLanding(dir, out var jumpLanding, out bool jumpWrapped))
            return;

        _iceChainDepth++;
        _lastMoveDir = dir;
        if (jumpWrapped)
            JumpToWrapped(jumpLanding, dir);
        else
            JumpTo(jumpLanding);
    }

    bool TryResolveWrappedMove(Vector2Int dir, out Vector2Int landing, out bool jump)
    {
        landing = default;
        jump = false;

        var cell = _board.WrapCell(GridPos + dir);
        if (cell == GridPos)
            return false;

        if (_board.Map.IsWalkable(cell.x, cell.y))
        {
            if (_board.TryGetEnemy(cell, out var enemy) && enemy.IsAlive)
                return false;

            landing = cell;
            jump = false;
            return true;
        }

        if (_board.Map.GetCell(cell.x, cell.y) != MapCellType.Blocked)
            return false;

        return TryFindJumpLandingWrapped(dir, out landing, out jump);
    }

    bool TryFindJumpLandingWrapped(Vector2Int dir, out Vector2Int landing, out bool jump)
    {
        landing = default;
        jump = true;
        int maxSkip = Mathf.Max(0, _data.jumpDistance);
        if (maxSkip <= 0)
            return false;

        int blockedCount = 0;
        var cell = _board.WrapCell(GridPos + dir);
        int guard = _board.Map.Width * _board.Map.Height + 2;

        while (blockedCount < guard)
        {
            if (_board.Map.GetCell(cell.x, cell.y) != MapCellType.Blocked)
                break;

            blockedCount++;
            if (blockedCount > maxSkip)
                return false;

            cell = _board.WrapCell(cell + dir);
            if (cell == GridPos)
                return false;
        }

        if (blockedCount == 0 || blockedCount > maxSkip)
            return false;

        if (!_board.Map.IsWalkable(cell.x, cell.y))
            return false;

        if (_board.TryGetEnemy(cell, out var enemy) && enemy.IsAlive)
            return false;

        landing = cell;
        return true;
    }

    bool TryFindJumpLanding(Vector2Int dir, out Vector2Int landing, out bool wrapped)
    {
        landing = default;
        wrapped = false;
        int maxSkip = Mathf.Max(0, _data.jumpDistance);
        if (maxSkip <= 0)
            return false;

        int blockedCount = 0;
        var cell = GridPos + dir;
        while (_board.Map.InBounds(cell.x, cell.y)
               && _board.Map.GetCell(cell.x, cell.y) == MapCellType.Blocked)
        {
            blockedCount++;
            cell += dir;
            if (blockedCount > maxSkip)
                return false;
        }

        if (blockedCount == 0 || blockedCount > maxSkip)
            return false;

        if (!_board.Map.InBounds(cell.x, cell.y))
        {
            if (!_data.passBorder)
                return false;

            if (!TryFindJumpLandingFrom(cell, dir, blockedCount, maxSkip, out landing))
                return false;
            wrapped = true;
            return true;
        }

        if (!_board.Map.IsWalkable(cell.x, cell.y))
            return false;

        if (_board.TryGetEnemy(cell, out var enemy) && enemy.IsAlive)
            return false;

        landing = cell;
        return true;
    }

    bool TryFindJumpLandingFrom(Vector2Int startCell, Vector2Int dir, int blockedSoFar, int maxSkip, out Vector2Int landing)
    {
        landing = default;
        int blockedCount = blockedSoFar;
        var cell = _board.WrapCell(startCell);
        int guard = _board.Map.Width * _board.Map.Height + 2;

        while (blockedCount < guard)
        {
            var type = _board.Map.GetCell(cell.x, cell.y);
            if (type != MapCellType.Blocked)
                break;

            blockedCount++;
            if (blockedCount > maxSkip)
                return false;

            cell = _board.WrapCell(cell + dir);
            if (cell == GridPos)
                return false;
        }

        if (blockedCount == 0 || blockedCount > maxSkip)
            return false;

        if (!_board.Map.IsWalkable(cell.x, cell.y))
            return false;

        if (_board.TryGetEnemy(cell, out var enemy) && enemy.IsAlive)
            return false;

        landing = cell;
        return true;
    }

    void MoveTo(Vector2Int target)
    {
        IsBusy = true;
        Vector3 world = _board.CellToWorld(target);
        transform.DOKill();
        transform.DOMove(world, _data.moveDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                GridPos = target;
                IsBusy = false;
                AfterArrive();
            });
    }

    void JumpTo(Vector2Int target)
    {
        IsBusy = true;
        Vector3 world = _board.CellToWorld(target);
        transform.DOKill();
        transform.DOJump(world, _data.jumpPower, 1, _data.jumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                GridPos = target;
                IsBusy = false;
                AfterArrive();
            });
    }

    void MoveToWrapped(Vector2Int target, Vector2Int dir)
    {
        IsBusy = true;
        Vector3 worldDir = DirToWorld(dir);
        Vector3 exit = transform.position + worldDir * (_data.cellSize * 0.55f);
        Vector3 end = _board.CellToWorld(target);
        Vector3 enter = end - worldDir * (_data.cellSize * 0.55f);
        float half = Mathf.Max(0.01f, _data.moveDuration * 0.5f);

        transform.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(transform.DOMove(exit, half).SetEase(Ease.InQuad));
        seq.AppendCallback(() => transform.position = enter);
        seq.Append(transform.DOMove(end, half).SetEase(Ease.OutQuad));
        seq.OnComplete(() =>
        {
            GridPos = target;
            IsBusy = false;
            AfterArrive();
        });
    }

    void JumpToWrapped(Vector2Int target, Vector2Int dir)
    {
        IsBusy = true;
        Vector3 worldDir = DirToWorld(dir);
        Vector3 exit = transform.position + worldDir * (_data.cellSize * 0.55f);
        Vector3 end = _board.CellToWorld(target);
        Vector3 enter = end - worldDir * (_data.cellSize * 0.55f);
        float half = Mathf.Max(0.01f, _data.jumpDuration * 0.5f);

        transform.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(transform.DOJump(exit, _data.jumpPower * 0.6f, 1, half).SetEase(Ease.InQuad));
        seq.AppendCallback(() => transform.position = enter);
        seq.Append(transform.DOJump(end, _data.jumpPower * 0.6f, 1, half).SetEase(Ease.OutQuad));
        seq.OnComplete(() =>
        {
            GridPos = target;
            IsBusy = false;
            AfterArrive();
        });
    }

    static Vector3 DirToWorld(Vector2Int dir) => new Vector3(dir.x, -dir.y, 0f);

    void AfterArrive()
    {
        if (_specials != null)
            _specials.OnPlayerArrived(GridPos);

        if (_game == null || !_game.IsPlaying)
            return;

        if (_game != null)
            _game.TryTouchBoss(this);

        if (!_game.IsPlaying)
            return;

        while (_board.TryGetFood(GridPos, out var food) && CanCarryMore)
            TryPickup(food);

        if (_board.TryGetRobot(GridPos, out var robot))
            robot.OfferToPlayer(this);

        if (_board.Map.IsStart(GridPos.x, GridPos.y) && (_carried.Count > 0 || _carriedBoss != null))
            Deposit();

        if (!_game.IsPlaying)
            return;

        if (_specials != null
            && _specials.IsIceSkate(GridPos)
            && _lastMoveDir != Vector2Int.zero
            && !IsBusy)
        {
            TryIceSlide(_lastMoveDir);
        }
        else
        {
            _iceChainDepth = 0;
        }
    }

    void TryPickup(FoodItem food)
    {
        if (!CanCarryMore)
        {
            NotifyCarryFull();
            return;
        }

        ReceiveFood(food);
    }

    public void ReceiveFood(FoodItem food)
    {
        if (food == null || !CanCarryMore)
            return;

        int index = _carried.Count;
        if (food.IsCarried)
            food.TransferToCarry(_carryRoot, index, _data);
        else
            food.BeginCarry(_carryRoot, index, _data);

        _carried.Add(food);
        RestackCarryVisuals();
        _game.NotifyCarryChanged();
    }

    public void ReceiveBoss(BossCollectFly boss)
    {
        if (boss == null || _carriedBoss != null)
            return;

        _carriedBoss = boss;
        boss.AttachToCarry(_carryRoot, _carried.Count, _data);
        RestackCarryVisuals();
        if (_game != null)
            _game.ShowBossCaughtToast();
        _game.NotifyCarryChanged();
    }

    void RestackCarryVisuals()
    {
        for (int i = 0; i < _carried.Count; i++)
        {
            if (_carried[i] != null)
                _carried[i].SnapCarryStack(i, _data);
        }

        if (_carriedBoss != null)
            _carriedBoss.SnapCarryStack(_carried.Count, _data);
    }

    public void DropAllCarriedFood()
    {
        if (_carried.Count == 0)
        {
            RestackCarryVisuals();
            return;
        }

        for (int i = 0; i < _carried.Count; i++)
        {
            var food = _carried[i];
            if (food == null)
                continue;
            food.transform.DOKill();
            Destroy(food.gameObject);
        }

        _carried.Clear();
        RestackCarryVisuals();
        _game.NotifyCarryChanged();
    }

    public void NotifyCarryFull()
    {
        _game.ShowFullToast();
        ShakeFull();
    }

    void ShakeFull()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        transform.DOKill(false);
        transform.DOShakePosition(0.18f, _data.cellSize * 0.12f, 18, 90f, false, true)
            .OnComplete(() => { IsBusy = false; });
    }

    void Deposit()
    {
        int n = _carried.Count;
        if (n > 0)
        {
            Vector3 startWorld = _board.CellToWorld(GridPos);
            for (int i = 0; i < _carried.Count; i++)
                _carried[i].DepositAndDestroy(startWorld + Vector3.down * 0.1f, 0.2f);

            _carried.Clear();
            int score = n * (1 + Mathf.Max(0, _data.foodCollectAmount));
            _game.AddScore(score);
            _game.AddFoodProgress(n);
            _game.NotifyCarryChanged();
        }

        if (_carriedBoss != null)
        {
            var boss = _carriedBoss;
            _carriedBoss = null;
            Vector3 hole = _board.CellToWorld(GridPos) + Vector3.down * 0.1f;
            boss.DepositAndDestroy(hole, 0.25f);
            _game.NotifyCarryChanged();
            _game.NotifyBossDeposited();
        }
    }

    void BumpEnemy(Vector2Int enemyCell, EnemyItem enemy)
    {
        IsBusy = true;
        Vector3 origin = transform.position;
        Vector3 bump = _board.CellToWorld(enemyCell);
        int damage = Mathf.Max(1, _data.playerHitDamage);

        transform.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(transform.DOMove(bump, _data.moveDuration * 0.6f).SetEase(Ease.OutQuad));
        seq.AppendCallback(() => enemy.TakeHit(GridPos, damage));
        seq.Append(transform.DOMove(origin, _data.moveDuration * 0.6f).SetEase(Ease.InQuad));
        seq.OnComplete(() => { IsBusy = false; });
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}
