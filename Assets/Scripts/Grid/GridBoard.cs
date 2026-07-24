using System.Collections.Generic;
using UnityEngine;

public class GridBoard : MonoBehaviour
{
    static readonly Color HazardDangerColor = new Color(0.92f, 0.22f, 0.22f);

    public MapData Map { get; private set; }
    public GameData Data { get; private set; }

    readonly Dictionary<Vector2Int, List<FoodItem>> _food = new Dictionary<Vector2Int, List<FoodItem>>();
    readonly Dictionary<Vector2Int, EnemyItem> _enemies = new Dictionary<Vector2Int, EnemyItem>();
    readonly Dictionary<Vector2Int, SpriteRenderer> _tiles = new Dictionary<Vector2Int, SpriteRenderer>();
    readonly Dictionary<Vector2Int, Color> _tileBaseColors = new Dictionary<Vector2Int, Color>();
    readonly Dictionary<Vector2Int, int> _hazardTintRefs = new Dictionary<Vector2Int, int>();
    readonly HashSet<Vector2Int> _hazardOccupied = new HashSet<Vector2Int>();
    CollectRobot _robot;
    BossCollectFly _boss;

    Transform _tileRoot;
    Transform _entityRoot;

    public Transform EntityRoot => _entityRoot;
    public CollectRobot Robot => _robot;
    public BossCollectFly Boss => _boss;

    public void Init(MapData map, GameData data)
    {
        Map = map;
        Data = data;
        _tiles.Clear();
        _tileBaseColors.Clear();
        _hazardTintRefs.Clear();
        _hazardOccupied.Clear();
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
                var cellType = Map.GetCell(col, row);
                var cell = new Vector2Int(col, row);
                var go = new GameObject($"Tile_{col}_{row}");
                go.transform.SetParent(_tileRoot, false);
                go.transform.position = CellToWorld(col, row);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = tile;
                sr.sortingOrder = 0;
                float s = Data.cellSize * 0.95f;
                FitSprite(sr, s);

                Color baseColor;
                switch (cellType)
                {
                    case MapCellType.Blocked:
                        baseColor = new Color(0.25f, 0.25f, 0.28f);
                        break;
                    case MapCellType.Start:
                        baseColor = new Color(0.35f, 0.75f, 0.45f);
                        break;
                    default:
                        baseColor = new Color(0.55f, 0.6f, 0.68f);
                        break;
                }

                sr.color = baseColor;
                _tiles[cell] = sr;
                _tileBaseColors[cell] = baseColor;
            }
        }
    }

    public void AddHazardTint(Vector2Int cell)
    {
        if (!_tiles.TryGetValue(cell, out var sr))
            return;

        _hazardTintRefs.TryGetValue(cell, out int refs);
        refs++;
        _hazardTintRefs[cell] = refs;
        sr.color = HazardDangerColor;
    }

    public void RemoveHazardTint(Vector2Int cell)
    {
        if (!_hazardTintRefs.TryGetValue(cell, out int refs))
            return;

        refs--;
        if (refs <= 0)
        {
            _hazardTintRefs.Remove(cell);
            if (_tiles.TryGetValue(cell, out var sr) && _tileBaseColors.TryGetValue(cell, out var baseColor))
                sr.color = baseColor;
            return;
        }

        _hazardTintRefs[cell] = refs;
    }

    public void SetHazardOccupied(Vector2Int cell, bool occupied)
    {
        if (occupied)
            _hazardOccupied.Add(cell);
        else
            _hazardOccupied.Remove(cell);
    }

    public bool HasHazard(Vector2Int cell) => _hazardOccupied.Contains(cell);

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

    public bool HasFood(Vector2Int cell) =>
        _food.TryGetValue(cell, out var list) && list.Count > 0;

    public int GetFoodCount(Vector2Int cell) =>
        _food.TryGetValue(cell, out var list) ? list.Count : 0;

    public bool HasEnemy(Vector2Int cell) => _enemies.ContainsKey(cell);

    public bool TryGetFood(Vector2Int cell, out FoodItem food)
    {
        if (_food.TryGetValue(cell, out var list) && list.Count > 0)
        {
            food = list[list.Count - 1];
            return true;
        }

        food = null;
        return false;
    }

    public bool TryGetEnemy(Vector2Int cell, out EnemyItem enemy) => _enemies.TryGetValue(cell, out enemy);

    public int RegisterFood(Vector2Int cell, FoodItem food)
    {
        if (!_food.TryGetValue(cell, out var list))
        {
            list = new List<FoodItem>(2);
            _food[cell] = list;
        }

        list.Add(food);
        return list.Count - 1;
    }

    public void UnregisterFood(FoodItem food)
    {
        if (food == null)
            return;

        var cell = food.GridPos;
        if (!_food.TryGetValue(cell, out var list))
            return;

        if (!list.Remove(food))
            return;

        if (list.Count == 0)
        {
            _food.Remove(cell);
            return;
        }

        for (int i = 0; i < list.Count; i++)
            list[i].ApplyGroundStack(i);
    }

    public void RegisterEnemy(Vector2Int cell, EnemyItem enemy) => _enemies[cell] = enemy;
    public void UnregisterEnemy(Vector2Int cell) => _enemies.Remove(cell);

    public void RegisterRobot(CollectRobot robot) => _robot = robot;
    public void UnregisterRobot(CollectRobot robot)
    {
        if (_robot == robot)
            _robot = null;
    }

    public bool TryGetRobot(Vector2Int cell, out CollectRobot robot)
    {
        robot = _robot;
        return _robot != null && _robot.GridPos == cell;
    }

    public bool HasRobot(Vector2Int cell) => _robot != null && _robot.GridPos == cell;

    public void RegisterBoss(BossCollectFly boss) => _boss = boss;

    public void UnregisterBoss(BossCollectFly boss)
    {
        if (_boss == boss)
            _boss = null;
    }

    public bool HasBoss(Vector2Int cell) =>
        _boss != null && !_boss.IsCaught && _boss.GridPos == cell;

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
                if (HasRobot(cell) || HasBoss(cell) || HasHazard(cell))
                    continue;
                buffer.Add(cell);
            }
        }
    }

    public Vector2Int WrapCell(Vector2Int cell)
    {
        int w = Map.Width;
        int h = Map.Height;
        int x = ((cell.x % w) + w) % w;
        int y = ((cell.y % h) + h) % h;
        return new Vector2Int(x, y);
    }
}
