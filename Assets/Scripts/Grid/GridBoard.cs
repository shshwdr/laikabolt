using System.Collections.Generic;
using UnityEngine;

public class GridBoard : MonoBehaviour
{
    public MapData Map { get; private set; }
    public GameData Data { get; private set; }

    readonly Dictionary<Vector2Int, FoodItem> _food = new Dictionary<Vector2Int, FoodItem>();
    readonly Dictionary<Vector2Int, EnemyItem> _enemies = new Dictionary<Vector2Int, EnemyItem>();

    Transform _tileRoot;
    Transform _entityRoot;

    public Transform EntityRoot => _entityRoot;

    public void Init(MapData map, GameData data)
    {
        Map = map;
        Data = data;
        _tileRoot = new GameObject("Tiles").transform;
        _tileRoot.SetParent(transform, false);
        _entityRoot = new GameObject("Entities").transform;
        _entityRoot.SetParent(transform, false);
        BuildTiles();
    }

    void BuildTiles()
    {
        Sprite tile = Data.tileSprite != null
            ? Data.tileSprite
            : SpriteUtil.WhiteSprite();

        for (int row = 0; row < Map.Height; row++)
        {
            for (int col = 0; col < Map.Width; col++)
            {
                var cell = Map.GetCell(col, row);
                var go = new GameObject($"Tile_{col}_{row}");
                go.transform.SetParent(_tileRoot, false);
                go.transform.position = CellToWorld(col, row);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = tile;
                sr.sortingOrder = 0;
                float s = Data.cellSize * 0.95f;
                FitSprite(sr, s);

                switch (cell)
                {
                    case MapCellType.Blocked:
                        sr.color = new Color(0.25f, 0.25f, 0.28f);
                        break;
                    case MapCellType.Start:
                        sr.color = new Color(0.35f, 0.75f, 0.45f);
                        break;
                    default:
                        sr.color = new Color(0.55f, 0.6f, 0.68f);
                        break;
                }
            }
        }
    }

    public Vector3 CellToWorld(int col, int row)
    {
        float x = col * Data.cellSize;
        float y = (Map.Height - 1 - row) * Data.cellSize;
        return new Vector3(x, y, 0f);
    }

    public Vector3 CellToWorld(Vector2Int cell) => CellToWorld(cell.x, cell.y);

    public static void FitSprite(SpriteRenderer sr, float worldSize)
    {
        if (sr.sprite == null) return;
        var b = sr.sprite.bounds.size;
        if (b.x < 0.0001f || b.y < 0.0001f) return;
        float scale = worldSize / Mathf.Max(b.x, b.y);
        sr.transform.localScale = new Vector3(scale, scale, 1f);
    }

    public bool HasFood(Vector2Int cell) => _food.ContainsKey(cell);
    public bool HasEnemy(Vector2Int cell) => _enemies.ContainsKey(cell);

    public bool TryGetFood(Vector2Int cell, out FoodItem food) => _food.TryGetValue(cell, out food);
    public bool TryGetEnemy(Vector2Int cell, out EnemyItem enemy) => _enemies.TryGetValue(cell, out enemy);

    public void RegisterFood(Vector2Int cell, FoodItem food) => _food[cell] = food;
    public void UnregisterFood(Vector2Int cell) => _food.Remove(cell);

    public void RegisterEnemy(Vector2Int cell, EnemyItem enemy) => _enemies[cell] = enemy;
    public void UnregisterEnemy(Vector2Int cell) => _enemies.Remove(cell);

    public void GetSpawnCandidates(List<Vector2Int> buffer, Vector2Int? playerCell)
    {
        buffer.Clear();
        for (int row = 0; row < Map.Height; row++)
        {
            for (int col = 0; col < Map.Width; col++)
            {
                var cell = new Vector2Int(col, row);
                if (Map.GetCell(col, row) != MapCellType.Walkable)
                    continue;
                if (playerCell.HasValue && playerCell.Value == cell)
                    continue;
                if (_food.ContainsKey(cell) || _enemies.ContainsKey(cell))
                    continue;
                buffer.Add(cell);
            }
        }
    }
}
