using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CollectRobot : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public int StoredCount => _stored.Count;

    const float GrabDuration = 0.2f;

    static readonly Vector2Int[] Octants =
    {
        new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        new Vector2Int(-1, 0),                         new Vector2Int(1, 0),
        new Vector2Int(-1, 1),  new Vector2Int(0, 1),  new Vector2Int(1, 1)
    };

    GridBoard _board;
    GameData _data;
    GameManager _game;
    Transform _carryRoot;
    readonly List<FoodItem> _stored = new List<FoodItem>();
    float _checkTimer;
    bool _grabbing;
    bool _working;

    public void Setup(GridBoard board, GameData data, GameManager game, Vector2Int cell)
    {
        _board = board;
        _data = data;
        _game = game;
        GridPos = cell;
        transform.position = board.CellToWorld(cell);

        var sr = SpriteUtil.ResolveRenderer(gameObject);
        if (sr.sprite == null)
            sr.sprite = SpriteUtil.WhiteSprite();
        sr.sortingOrder = 7;
        MainGameObject.Fit(gameObject, sr, data.cellSize);

        var carryGo = new GameObject("CarryRoot");
        _carryRoot = carryGo.transform;
        _carryRoot.SetParent(transform, false);
        _carryRoot.localPosition = Vector3.zero;

        board.RegisterRobot(this);
        _working = false;
        _checkTimer = Mathf.Max(0.01f, data.machineCollectInterval);
    }

    public void StartWorking()
    {
        if (_working)
            return;

        _working = true;
        _checkTimer = Mathf.Max(0.01f, _data.machineCollectInterval);
    }

    void Update()
    {
        if (!_working || _game == null || !_game.IsPlaying || _grabbing)
            return;

        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f)
            return;

        _checkTimer = Mathf.Max(0.01f, _data.machineCollectInterval);
        TryGrabAdjacentFood();
    }

    void TryGrabAdjacentFood()
    {
        for (int i = 0; i < Octants.Length; i++)
        {
            var cell = GridPos + Octants[i];
            if (!_board.Map.InBounds(cell.x, cell.y))
                continue;
            if (!_board.TryGetFood(cell, out var food))
                continue;

            BeginGrab(food);
            return;
        }
    }

    void BeginGrab(FoodItem food)
    {
        _grabbing = true;
        int index = _stored.Count;
        food.BeginMachineCarry(_carryRoot, index, _data, GrabDuration, () =>
        {
            _stored.Add(food);
            _grabbing = false;
        });
    }

    /// <summary>Transfer as many stored foods as the player can hold.</summary>
    public void OfferToPlayer(PlayerController player)
    {
        if (player == null || _stored.Count == 0)
            return;

        bool tookAny = false;
        while (_stored.Count > 0 && player.CanCarryMore)
        {
            var food = _stored[0];
            _stored.RemoveAt(0);
            player.ReceiveFood(food);
            tookAny = true;
        }

        RestackVisuals();

        if (!tookAny && _stored.Count > 0)
            player.NotifyCarryFull();
    }

    void RestackVisuals()
    {
        for (int i = 0; i < _stored.Count; i++)
            _stored[i].SnapCarryStack(i, _data);
    }

    void OnDestroy()
    {
        if (_board != null)
            _board.UnregisterRobot(this);
        transform.DOKill();
    }
}
