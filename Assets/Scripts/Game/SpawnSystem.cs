using System.Collections.Generic;
using UnityEngine;

public class SpawnSystem : MonoBehaviour
{
    const string FoodPrefabPath = "prefab/food";
    const string MonsterPrefabPath = "prefab/monster";
    const string RobotPrefabPath = "prefab/robot";

    static readonly Vector2Int[] Cardinals =
    {
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0)
    };

    static readonly Vector2Int[] Octants =
    {
        new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        new Vector2Int(-1, 0),                         new Vector2Int(1, 0),
        new Vector2Int(-1, 1),  new Vector2Int(0, 1),  new Vector2Int(1, 1)
    };

    GridBoard _board;
    GameData _data;
    PlayerController _player;
    GameManager _game;
    float _foodTimer;
    float _enemyTimer;
    readonly List<Vector2Int> _candidates = new List<Vector2Int>(32);
    bool _active;

    public void Init(
        GridBoard board,
        GameData data,
        PlayerController player,
        GameManager game)
    {
        _board = board;
        _data = data;
        _player = player;
        _game = game;
    }

    public void SpawnInitial()
    {
        int robotCount = Mathf.Max(0, _data.machineCollectCount);
        for (int i = 0; i < robotCount; i++)
            TrySpawnRobot();

        for (int i = 0; i < _data.initialCollectables; i++)
            TrySpawnFood();
        for (int i = 0; i < _data.initialEnemies; i++)
            TrySpawnEnemy();
    }

    public void StartTimedSpawning()
    {
        _active = true;
        _foodTimer = 0f;
        _enemyTimer = 0f;

        if (_board != null)
            _board.StartAllRobotsWorking();
    }

    public void Stop()
    {
        _active = false;
    }

    void Update()
    {
        if (!_active) return;

        _foodTimer += Time.deltaTime;
        _enemyTimer += Time.deltaTime;

        if (_foodTimer >= _data.collectableSpawnInterval)
        {
            _foodTimer = 0f;
            TrySpawnFood();
        }

        if (_enemyTimer >= _data.enemySpawnInterval)
        {
            _enemyTimer = 0f;
            TrySpawnEnemy();
        }
    }

    void TrySpawnFood()
    {
        if (!TryPickCell(out var cell)) return;
        SpawnFoodAt(cell);
    }

    void TrySpawnEnemy()
    {
        if (!TryPickCell(out var cell)) return;
        var go = PrefabUtil.Instantiate(MonsterPrefabPath, _board.EntityRoot, "Enemy");
        PrefabUtil.EnsureAnimPlayer(go);
        var enemy = go.GetComponent<EnemyItem>();
        if (enemy == null)
            enemy = go.AddComponent<EnemyItem>();
        enemy.Setup(_board, _data, cell, DropFoodFromEnemy);
    }

    void DropFoodFromEnemy(Vector2Int origin)
    {
        int count = Mathf.Max(0, _data.enemyFoodDrop);
        if (count <= 0)
            return;

        // Stack all drops on the death cell (board supports multi-food per cell).
        for (int i = 0; i < count; i++)
            CreateFood(origin);
    }

    public void SpawnFoodDrops(Vector2Int origin, int count)
    {
        if (count <= 0)
            return;

        for (int i = 0; i < count; i++)
            CreateFood(origin);
    }

    void SpawnFoodAt(Vector2Int cell)
    {
        SpawnFoodAt(cell, true);
    }

    void SpawnFoodAt(Vector2Int cell, bool rollBonus)
    {
        CreateFood(cell);

        if (!rollBonus || _data.bonusGenerateChance <= 0)
            return;

        if (Random.Range(0f, 100f) < _data.bonusGenerateChance)
            CreateFood(cell);
    }

    void CreateFood(Vector2Int cell)
    {
        var go = PrefabUtil.Instantiate(FoodPrefabPath, _board.EntityRoot, "Food");
        PrefabUtil.EnsureAnimPlayer(go);
        var food = go.GetComponent<FoodItem>();
        if (food == null)
            food = go.AddComponent<FoodItem>();
        food.Setup(_board, cell, _data.cellSize);
    }

    /// <summary>Creates a food that goes straight into the player's hand (no board cell).</summary>
    public FoodItem CreateCarryOnlyFood()
    {
        var go = PrefabUtil.Instantiate(FoodPrefabPath, _board.EntityRoot, "Food");
        PrefabUtil.EnsureAnimPlayer(go);
        var food = go.GetComponent<FoodItem>();
        if (food == null)
            food = go.AddComponent<FoodItem>();
        food.SetupUnregistered(_data.cellSize);
        return food;
    }

    void TrySpawnRobot()
    {
        CollectRobotCandidates(_candidates);
        if (_candidates.Count == 0)
        {
            Debug.LogWarning("[SpawnSystem] No valid cell for collect robot.");
            return;
        }

        var cell = _candidates[Random.Range(0, _candidates.Count)];
        var go = PrefabUtil.Instantiate(RobotPrefabPath, _board.EntityRoot, "CollectRobot");
        PrefabUtil.EnsureAnimPlayer(go);
        var robot = go.GetComponent<CollectRobot>();
        if (robot == null)
            robot = go.AddComponent<CollectRobot>();
        robot.Setup(_board, _data, _game, cell);
    }

    void CollectRobotCandidates(List<Vector2Int> buffer)
    {
        buffer.Clear();
        Vector2Int? playerCell = _player != null ? _player.GridPos : (Vector2Int?)null;

        for (int row = 0; row < _board.Map.Height; row++)
        {
            for (int col = 0; col < _board.Map.Width; col++)
            {
                var cell = new Vector2Int(col, row);
                if (_board.Map.GetCell(col, row) != MapCellType.Walkable)
                    continue;
                if (playerCell.HasValue && playerCell.Value == cell)
                    continue;
                if (_board.HasFood(cell) || _board.HasEnemy(cell) || _board.HasBoss(cell) || _board.HasHazard(cell))
                    continue;
                // 3x3 around this cell must not already contain another robot.
                if (_board.HasRobotInNeighborhood(cell))
                    continue;
                if (!TouchesWall(cell))
                    continue;
                if (CountAbsorbableNeighbors(cell) < 3)
                    continue;
                buffer.Add(cell);
            }
        }
    }

    bool TouchesWall(Vector2Int cell)
    {
        for (int i = 0; i < Cardinals.Length; i++)
        {
            var n = cell + Cardinals[i];
            if (!_board.Map.InBounds(n.x, n.y))
                continue;
            if (_board.Map.GetCell(n.x, n.y) == MapCellType.Blocked)
                return true;
        }
        return false;
    }

    int CountAbsorbableNeighbors(Vector2Int cell)
    {
        int count = 0;
        for (int i = 0; i < Octants.Length; i++)
        {
            var n = cell + Octants[i];
            if (!_board.Map.InBounds(n.x, n.y))
                continue;
            // Cells that can hold food (same rule as spawn candidates).
            if (_board.Map.GetCell(n.x, n.y) == MapCellType.Walkable)
                count++;
        }
        return count;
    }

    bool TryPickCell(out Vector2Int cell)
    {
        _board.GetSpawnCandidates(_candidates, _player != null ? _player.GridPos : (Vector2Int?)null);
        if (_candidates.Count == 0)
        {
            cell = default;
            return false;
        }
        cell = _candidates[Random.Range(0, _candidates.Count)];
        return true;
    }
}
