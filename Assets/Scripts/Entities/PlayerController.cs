using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public bool IsBusy { get; private set; }
    public int CarryCount => _carried.Count;

    GridBoard _board;
    GameData _data;
    GameManager _game;
    Transform _carryRoot;
    readonly List<FoodItem> _carried = new List<FoodItem>();
    SpriteRenderer _sr;

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

    void Update()
    {
        if (_game == null || !_game.IsPlaying || IsBusy)
            return;

        Vector2Int dir = Vector2Int.zero;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            dir = new Vector2Int(0, -1); // editor row decreases toward top visually = world +y, row-1
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            dir = new Vector2Int(0, 1);
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            dir = new Vector2Int(-1, 0);
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            dir = new Vector2Int(1, 0);

        if (dir != Vector2Int.zero)
            TryMove(dir);
    }

    void TryMove(Vector2Int dir)
    {
        var target = GridPos + dir;
        if (!_board.Map.InBounds(target.x, target.y))
            return;
        if (!_board.Map.IsWalkable(target.x, target.y))
            return;

        if (_board.TryGetEnemy(target, out var enemy) && enemy.IsAlive)
        {
            BumpEnemy(target, enemy);
            return;
        }

        MoveTo(target);
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
                AfterArrive();
                IsBusy = false;
            });
    }

    void AfterArrive()
    {
        if (_board.TryGetFood(GridPos, out var food))
            Pickup(food);

        if (_board.Map.IsStart(GridPos.x, GridPos.y) && _carried.Count > 0)
            Deposit();
    }

    void Pickup(FoodItem food)
    {
        int index = _carried.Count;
        food.BeginCarry(_carryRoot, index, _data);
        _carried.Add(food);
        _game.NotifyCarryChanged();
    }

    void Deposit()
    {
        int n = _carried.Count;
        if (n == 0) return;

        Vector3 startWorld = _board.CellToWorld(GridPos);
        for (int i = 0; i < _carried.Count; i++)
            _carried[i].DepositAndDestroy(startWorld + Vector3.down * 0.1f, 0.2f);

        _carried.Clear();
        _game.AddScore(n);
        _game.NotifyCarryChanged();
    }

    void BumpEnemy(Vector2Int enemyCell, EnemyItem enemy)
    {
        IsBusy = true;
        Vector3 origin = transform.position;
        Vector3 bump = _board.CellToWorld(enemyCell);

        transform.DOKill();
        var seq = DOTween.Sequence();
        seq.Append(transform.DOMove(bump, _data.moveDuration * 0.6f).SetEase(Ease.OutQuad));
        seq.AppendCallback(() => enemy.TakeHit(GridPos));
        seq.Append(transform.DOMove(origin, _data.moveDuration * 0.6f).SetEase(Ease.InQuad));
        seq.OnComplete(() => { IsBusy = false; });
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}
