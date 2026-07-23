using System.Collections.Generic;
using UnityEngine;

public class SpawnSystem : MonoBehaviour
{
    GridBoard _board;
    GameData _data;
    PlayerController _player;
    Sprite _foodSprite;
    Sprite _monsterSprite;
    float _foodTimer;
    float _enemyTimer;
    readonly List<Vector2Int> _candidates = new List<Vector2Int>(32);
    bool _active;

    public void Init(GridBoard board, GameData data, PlayerController player, Sprite food, Sprite monster)
    {
        _board = board;
        _data = data;
        _player = player;
        _foodSprite = food;
        _monsterSprite = monster;
    }

    public void StartSpawning()
    {
        _active = true;
        _foodTimer = 0f;
        _enemyTimer = 0f;

        for (int i = 0; i < _data.initialCollectables; i++)
            TrySpawnFood();
        for (int i = 0; i < _data.initialEnemies; i++)
            TrySpawnEnemy();
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
        var go = new GameObject("Food");
        go.transform.SetParent(_board.EntityRoot, false);
        var food = go.AddComponent<FoodItem>();
        food.Setup(_board, cell, _foodSprite, _data.cellSize);
    }

    void TrySpawnEnemy()
    {
        if (!TryPickCell(out var cell)) return;
        var go = new GameObject("Enemy");
        go.transform.SetParent(_board.EntityRoot, false);
        var enemy = go.AddComponent<EnemyItem>();
        enemy.Setup(_board, _data, cell, _monsterSprite);
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
