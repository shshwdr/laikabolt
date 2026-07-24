using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public bool IsBusy { get; private set; }
    public int CarryCount => _carried.Count;
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
    bool _suppressIceSlide;

    // Dash hold input (only used when _data.dash).
    Vector2Int _holdDir;
    float _holdTime;
    bool _holdActive;
    bool _dashFiredThisHold;
    readonly List<Vector2Int> _dashPath = new List<Vector2Int>(32);
    readonly List<EnemyItem> _enemyBuffer = new List<EnemyItem>(16);
    readonly List<FoodItem> _attractFoodBuffer = new List<FoodItem>(8);

    static readonly Vector2Int[] AttractOctants =
    {
        new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        new Vector2Int(-1, 0),                         new Vector2Int(1, 0),
        new Vector2Int(-1, 1),  new Vector2Int(0, 1),  new Vector2Int(1, 1)
    };

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
        if (_game == null || !_game.IsPlaying)
            return;

        // Dash hold must accumulate during move tweens (IsBusy), so a press
        // started mid-move still counts toward dashHoldSeconds after landing.
        if (_data != null && _data.dash)
            UpdateDashInput();
        else if (!IsBusy)
            UpdateStepInput();
    }

    void UpdateStepInput()
    {
        Vector2Int dir = ReadDirectionKeyDown();
        if (dir == Vector2Int.zero)
            return;

        _iceChainDepth = 0;
        TryMove(dir);
    }

    void UpdateDashInput()
    {
        Vector2Int pressed = ReadDirectionKeyDown();
        if (pressed != Vector2Int.zero)
        {
            _holdDir = pressed;
            _holdTime = 0f;
            _holdActive = true;
            _dashFiredThisHold = false;
        }

        if (!_holdActive)
            return;

        if (!IsDirectionHeld(_holdDir))
        {
            // Only step on release when idle; release mid-tween just cancels the hold.
            if (!_dashFiredThisHold && !IsBusy)
            {
                _iceChainDepth = 0;
                TryMove(_holdDir);
            }

            _holdActive = false;
            _holdTime = 0f;
            return;
        }

        _holdTime += Time.deltaTime;
        if (IsBusy || _dashFiredThisHold)
            return;

        float holdNeed = _data.dashHoldSeconds > 0f ? _data.dashHoldSeconds : 0.5f;
        if (_holdTime < holdNeed)
            return;

        _dashFiredThisHold = true;
        _iceChainDepth = 0;
        TryDash(_holdDir);
    }

    static Vector2Int ReadDirectionKeyDown()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            return new Vector2Int(0, -1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            return new Vector2Int(0, 1);
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            return new Vector2Int(-1, 0);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            return new Vector2Int(1, 0);
        return Vector2Int.zero;
    }

    static bool IsDirectionHeld(Vector2Int dir)
    {
        if (dir.y < 0)
            return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        if (dir.y > 0)
            return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        if (dir.x < 0)
            return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        if (dir.x > 0)
            return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        return false;
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
            if (jump && TryBeginJumpAttack(landing))
                return;
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
        if (TryBeginJumpAttack(jumpLanding))
            return;

        if (jumpWrapped)
            JumpToWrapped(jumpLanding, dir);
        else
            JumpTo(jumpLanding);
    }

    /// <summary>
    /// Dash in a straight line until hole, enemy, blocked wall, or map edge.
    /// Never wraps (ignores passBorder). Food / robots / boss on the path are collected like stepping on them.
    /// With dashAttack, an enemy ahead becomes a 2x-damage attack destination.
    /// </summary>
    bool TryDash(Vector2Int dir)
    {
        if (dir == Vector2Int.zero || _board == null || _data == null)
            return false;

        if (!BuildDashPath(dir, out var end, out var attackEnemy))
            return false;

        _game.NotifyPlayerActed();
        _lastMoveDir = dir;
        _suppressIceSlide = true;

        // Collect along the dash path (ground food, robot stores, boss) — same as stepping on cells.
        for (int i = 0; i < _dashPath.Count; i++)
        {
            var cell = _dashPath[i];
            while (_board.TryGetFood(cell, out var food) && CanCarryMore)
                TryPickup(food);

            if (_board.TryGetRobot(cell, out var robot))
                robot.OfferToPlayer(this);

            if (_board.HasBoss(cell) && _board.Boss != null)
                _board.Boss.TryTouch(this, requirePlayerOnCell: false);
        }

        IsBusy = true;
        Vector2Int attackFrom = end;
        float dashDur = Mathf.Max(0.02f, _data.dashDuration);
        transform.DOKill();
        var seq = DOTween.Sequence();

        if (_dashPath.Count > 0)
        {
            Vector3 endWorld = _board.CellToWorld(end);
            seq.Append(transform.DOMove(endWorld, dashDur).SetEase(Ease.Linear));
            seq.AppendCallback(() => { GridPos = end; });
        }

        if (attackEnemy != null)
        {
            Vector3 standWorld = _board.CellToWorld(end);
            Vector3 enemyWorld = _board.CellToWorld(attackEnemy.GridPos);
            int damage = Mathf.Max(1, _data.playerHitDamage) * 2;
            seq.Append(transform.DOMove(enemyWorld, dashDur).SetEase(Ease.Linear));
            seq.AppendCallback(() =>
            {
                if (attackEnemy != null && attackEnemy.IsAlive)
                    ApplyEnemyHit(attackEnemy, attackFrom, damage);
            });
            seq.Append(transform.DOMove(standWorld, dashDur).SetEase(Ease.Linear));
        }

        seq.OnComplete(() =>
        {
            GridPos = end;
            IsBusy = false;
            AfterArrive();
        });
        return true;
    }

    bool BuildDashPath(Vector2Int dir, out Vector2Int end, out EnemyItem attackEnemy)
    {
        _dashPath.Clear();
        end = GridPos;
        attackEnemy = null;
        var cell = GridPos;
        int guard = _board.Map.Width * _board.Map.Height + 2;

        for (int step = 0; step < guard; step++)
        {
            var next = cell + dir;

            // Always stop at map edge — no wrap even with passBorder.
            if (!_board.Map.InBounds(next.x, next.y))
                break;

            var type = _board.Map.GetCell(next.x, next.y);
            if (type == MapCellType.Blocked)
                break;

            if (_board.TryGetEnemy(next, out var enemy) && enemy.IsAlive)
            {
                if (_data.dashAttack)
                    attackEnemy = enemy;
                break;
            }

            // Boss does not block dash — treated like a collectable on the path.
            if (!_board.Map.IsWalkable(next.x, next.y))
                break;

            cell = next;
            _dashPath.Add(cell);
            end = cell;

            // Stop on hole / spaceship.
            if (_board.Map.IsStart(cell.x, cell.y))
                break;
        }

        return _dashPath.Count > 0 || attackEnemy != null;
    }

    /// <summary>Ice auto-slide uses half duration (faster) vs player input moves.</summary>
    const float IceSlideDurationScale = 0.5f;

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
            if (jump && TryBeginJumpAttack(landing))
                return;
            if (jump)
                JumpToWrapped(landing, dir, IceSlideDurationScale);
            else
                MoveToWrapped(landing, dir, IceSlideDurationScale);
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
            MoveTo(target, IceSlideDurationScale);
            return;
        }

        if (_board.Map.GetCell(target.x, target.y) != MapCellType.Blocked)
            return;

        if (!TryFindJumpLanding(dir, out var jumpLanding, out bool jumpWrapped))
            return;

        _iceChainDepth++;
        _lastMoveDir = dir;
        if (TryBeginJumpAttack(jumpLanding))
            return;

        if (jumpWrapped)
            JumpToWrapped(jumpLanding, dir, IceSlideDurationScale);
        else
            JumpTo(jumpLanding, IceSlideDurationScale);
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

        if (_board.TryGetEnemy(cell, out var enemy) && enemy.IsAlive && !_data.jumpAttack)
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

        if (_board.TryGetEnemy(cell, out var enemy) && enemy.IsAlive && !_data.jumpAttack)
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

        if (_board.TryGetEnemy(cell, out var enemy) && enemy.IsAlive && !_data.jumpAttack)
            return false;

        landing = cell;
        return true;
    }

    void MoveTo(Vector2Int target, float durationScale = 1f)
    {
        IsBusy = true;
        Vector3 world = _board.CellToWorld(target);
        float duration = Mathf.Max(0.01f, _data.moveDuration * durationScale);
        transform.DOKill();
        transform.DOMove(world, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                GridPos = target;
                IsBusy = false;
                AfterArrive();
            });
    }

    void JumpTo(Vector2Int target, float durationScale = 1f)
    {
        IsBusy = true;
        Vector3 world = _board.CellToWorld(target);
        float duration = Mathf.Max(0.01f, _data.jumpDuration * durationScale);
        transform.DOKill();
        transform.DOJump(world, _data.jumpPower, 1, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                GridPos = target;
                IsBusy = false;
                AfterArrive();
            });
    }

    void MoveToWrapped(Vector2Int target, Vector2Int dir, float durationScale = 1f)
    {
        IsBusy = true;
        Vector3 worldDir = DirToWorld(dir);
        Vector3 exit = transform.position + worldDir * (_data.cellSize * 0.55f);
        Vector3 end = _board.CellToWorld(target);
        Vector3 enter = end - worldDir * (_data.cellSize * 0.55f);
        float half = Mathf.Max(0.01f, _data.moveDuration * 0.5f * durationScale);

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

    void JumpToWrapped(Vector2Int target, Vector2Int dir, float durationScale = 1f)
    {
        IsBusy = true;
        Vector3 worldDir = DirToWorld(dir);
        Vector3 exit = transform.position + worldDir * (_data.cellSize * 0.55f);
        Vector3 end = _board.CellToWorld(target);
        Vector3 enter = end - worldDir * (_data.cellSize * 0.55f);
        float half = Mathf.Max(0.01f, _data.jumpDuration * 0.5f * durationScale);

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

        if (_suppressIceSlide)
        {
            _suppressIceSlide = false;
            _iceChainDepth = 0;
            return;
        }

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

        // lastMinute: picking one food from the ground puts two in hand.
        if (_game != null && _game.IsLastMinuteActive && CanCarryMore)
        {
            var bonus = _game.CreateLastMinuteFood();
            if (bonus != null)
                ReceiveFood(bonus);
        }
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
            bool fullCarriage = n >= _data.holdItemCount;
            Vector3 startWorld = _board.CellToWorld(GridPos);
            for (int i = 0; i < _carried.Count; i++)
                _carried[i].DepositAndDestroy(startWorld + Vector3.down * 0.1f, 0.2f);

            _carried.Clear();
            int score = n * (1 + Mathf.Max(0, _data.foodCollectAmount));
            int progress = n;
            if (fullCarriage && _data.fullRewardBonus > 0)
            {
                score += _data.fullRewardBonus;
                progress += _data.fullRewardBonus;
            }

            _game.AddScore(score);
            _game.AddFoodProgress(progress);
            _game.NotifyCarryChanged();

            if (_data.homeAttack)
                HomeAttackAllEnemies();
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

    void HomeAttackAllEnemies()
    {
        if (_board == null || _data == null)
            return;

        int damage = Mathf.Max(1, _data.playerHitDamage);
        Vector2Int from = GridPos;
        _board.GetEnemies(_enemyBuffer);
        for (int i = 0; i < _enemyBuffer.Count; i++)
        {
            var enemy = _enemyBuffer[i];
            if (enemy != null && enemy.IsAlive)
                enemy.TakeHit(from, damage);
        }
    }

    void BumpEnemy(Vector2Int enemyCell, EnemyItem enemy)
    {
        IsBusy = true;
        Vector3 origin = transform.position;
        Vector3 bump = _board.CellToWorld(enemyCell);
        int damage = Mathf.Max(1, _data.playerHitDamage);
        Vector2Int fromCell = GridPos;

        transform.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(transform.DOMove(bump, _data.moveDuration * 0.6f).SetEase(Ease.OutQuad));
        seq.AppendCallback(() => ApplyEnemyHit(enemy, fromCell, damage));
        seq.Append(transform.DOMove(origin, _data.moveDuration * 0.6f).SetEase(Ease.InQuad));
        seq.OnComplete(() => { IsBusy = false; });
    }

    /// <summary>Jump onto enemy for 2x damage, then jump back to the takeoff cell.</summary>
    bool TryBeginJumpAttack(Vector2Int enemyCell)
    {
        if (_data == null || !_data.jumpAttack)
            return false;
        if (!_board.TryGetEnemy(enemyCell, out var enemy) || !enemy.IsAlive)
            return false;

        JumpAttackEnemy(enemyCell, enemy);
        return true;
    }

    void JumpAttackEnemy(Vector2Int enemyCell, EnemyItem enemy)
    {
        IsBusy = true;
        Vector3 origin = transform.position;
        Vector3 bump = _board.CellToWorld(enemyCell);
        Vector2Int fromCell = GridPos;
        int damage = Mathf.Max(1, _data.playerHitDamage) * 2;
        float duration = Mathf.Max(0.01f, _data.jumpDuration);

        transform.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(transform.DOJump(bump, _data.jumpPower, 1, duration).SetEase(Ease.OutQuad));
        seq.AppendCallback(() => ApplyEnemyHit(enemy, fromCell, damage));
        seq.Append(transform.DOJump(origin, _data.jumpPower, 1, duration).SetEase(Ease.OutQuad));
        seq.OnComplete(() => { IsBusy = false; });
    }

    void ApplyEnemyHit(EnemyItem enemy, Vector2Int fromCell, int damage)
    {
        if (enemy == null || !enemy.IsAlive)
            return;

        Vector2Int enemyCell = enemy.GridPos;
        enemy.TakeHit(fromCell, damage);

        if (_data != null && _data.attackAttract)
            TryAttractAdjacentFood(enemyCell);
    }

    /// <summary>Pull one food adjacent to the hit enemy into the player's hands, if any.</summary>
    void TryAttractAdjacentFood(Vector2Int enemyCell)
    {
        if (!CanCarryMore || _board == null)
            return;

        _attractFoodBuffer.Clear();
        for (int i = 0; i < AttractOctants.Length; i++)
        {
            var cell = enemyCell + AttractOctants[i];
            if (!_board.Map.InBounds(cell.x, cell.y))
                continue;
            if (_board.TryGetFood(cell, out var food) && food != null && !food.IsCarried)
                _attractFoodBuffer.Add(food);
        }

        if (_attractFoodBuffer.Count == 0)
            return;

        var chosen = _attractFoodBuffer[Random.Range(0, _attractFoodBuffer.Count)];
        TryPickup(chosen);
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}
