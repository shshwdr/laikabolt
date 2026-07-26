using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Scene special terrain from scene.csv <c>special</c>:
/// pointDamage / lineDamage / iceSkate / pointDamage2.
/// Point damage visuals load from Resources/prefab; other sprites from sceneItems.
/// </summary>
public class SceneSpecialSystem : MonoBehaviour
{
    enum SpecialKind
    {
        None,
        PointDamage,
        LineDamage,
        IceSkate,
        PointDamage2
    }

    enum HazardPhase
    {
        Warning,
        Active
    }

    struct ParsedSpecial
    {
        public SpecialKind Kind;
        public float Interval;
        public float WarnTime;
        public float ActiveTime;
        public float LossTime;
    }

    class ActiveHazard
    {
        public SpecialKind Kind;
        public HazardPhase Phase;
        public float PhaseLeft;
        public float LossTime;
        public bool IsRow;
        public int LineIndex;
        public Vector2Int Cell;
        public readonly List<GameObject> Visuals = new List<GameObject>();
        public readonly List<Vector2Int> OccupiedCells = new List<Vector2Int>();
        public readonly List<Vector2Int> TintedCells = new List<Vector2Int>();
    }

    GridBoard _board;
    GameData _data;
    GameManager _game;
    PlayerController _player;
    Transform _overlayRoot;
    ParsedSpecial _special;
    GameObject _pointPrePrefab;
    GameObject _pointDamagePrefab;
    Sprite _iceSkate;
    float _spawnTimer;
    bool _active;
    readonly List<ActiveHazard> _hazards = new List<ActiveHazard>(8);
    readonly HashSet<Vector2Int> _iceCells = new HashSet<Vector2Int>();
    readonly List<Vector2Int> _candidates = new List<Vector2Int>(64);

    public bool HasSpecial => _special.Kind != SpecialKind.None;

    public void Init(
        GridBoard board,
        GameData data,
        GameManager game,
        PlayerController player,
        List<string> specialTokens)
    {
        _board = board;
        _data = data;
        _game = game;
        _player = player;
        _special = Parse(specialTokens);

        _overlayRoot = new GameObject("SpecialOverlays").transform;
        _overlayRoot.SetParent(board.EntityRoot, false);

        if (!HasSpecial)
            return;

        _pointPrePrefab = PrefabUtil.Load("prefab/pointDamagePre");
        _pointDamagePrefab = PrefabUtil.Load("prefab/pointDamageDamage");
        _iceSkate = LoadSprite("iceSkate");
    }

    static Sprite LoadSprite(string name)
    {
        var s = Resources.Load<Sprite>("sceneItems/" + name);
        return s != null ? s : SpriteUtil.WhiteSprite();
    }

    static ParsedSpecial Parse(List<string> tokens)
    {
        var result = new ParsedSpecial { Kind = SpecialKind.None };
        if (tokens == null || tokens.Count == 0 || string.IsNullOrEmpty(tokens[0]))
            return result;

        string type = tokens[0].Trim();
        float Get(int i, float fallback = 0f)
        {
            if (i >= tokens.Count) return fallback;
            return float.TryParse(
                tokens[i],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float v)
                ? v
                : fallback;
        }

        switch (type)
        {
            case "pointDamage":
                result.Kind = SpecialKind.PointDamage;
                result.Interval = Mathf.Max(0.1f, Get(1, 3f));
                result.WarnTime = Mathf.Max(0f, Get(2, 0.5f));
                result.ActiveTime = Mathf.Max(0.1f, Get(3, 5f));
                result.LossTime = Mathf.Max(0f, Get(4, 3f));
                break;
            case "lineDamage":
                result.Kind = SpecialKind.LineDamage;
                result.Interval = Mathf.Max(0.1f, Get(1, 3f));
                result.WarnTime = Mathf.Max(0f, Get(2, 0.5f));
                result.ActiveTime = Mathf.Max(0.1f, Get(3, 5f));
                result.LossTime = Mathf.Max(0f, Get(4, 3f));
                break;
            case "iceSkate":
                // iceSkate|几秒出现一次|出现持续多少时间
                result.Kind = SpecialKind.IceSkate;
                result.Interval = Mathf.Max(0.1f, Get(1, 3f));
                result.ActiveTime = Mathf.Max(0.1f, Get(2, 5f));
                break;
            case "pointDamage2":
                result.Kind = SpecialKind.PointDamage2;
                result.Interval = Mathf.Max(0.1f, Get(1, 3f));
                result.WarnTime = Mathf.Max(0f, Get(2, 0.5f));
                result.ActiveTime = Mathf.Max(0.1f, Get(3, 5f));
                break;
            default:
                Debug.LogWarning("[SceneSpecialSystem] Unknown special: " + type);
                break;
        }

        return result;
    }

    public void StartRunning()
    {
        if (!HasSpecial)
            return;
        _active = true;
        _spawnTimer = 0f;
    }

    public void Stop()
    {
        _active = false;
        ClearAllHazards();
    }

    void Update()
    {
        if (!_active || !HasSpecial || _game == null || !_game.IsPlaying)
            return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _special.Interval)
        {
            _spawnTimer = 0f;
            SpawnOnce();
        }

        for (int i = _hazards.Count - 1; i >= 0; i--)
        {
            var h = _hazards[i];
            h.PhaseLeft -= Time.deltaTime;
            if (h.PhaseLeft > 0f)
                continue;

            if (h.Phase == HazardPhase.Warning)
            {
                h.Phase = HazardPhase.Active;
                h.PhaseLeft = _special.ActiveTime;
                PlayStormBuildSfx();
                RefreshHazardVisuals(h);
                ApplyDangerTint(h);
                ApplyHazardIfPlayerStanding(h);
            }
            else
            {
                RemoveHazard(i);
            }
        }
    }

    void SpawnOnce()
    {
        switch (_special.Kind)
        {
            case SpecialKind.PointDamage:
            case SpecialKind.PointDamage2:
                SpawnPointHazard();
                break;
            case SpecialKind.LineDamage:
                SpawnLineHazard();
                break;
            case SpecialKind.IceSkate:
                SpawnIceSkate();
                break;
        }
    }

    void SpawnPointHazard()
    {
        if (!TryPickHazardCell(out var cell))
            return;

        var h = new ActiveHazard
        {
            Kind = _special.Kind,
            Phase = _special.WarnTime > 0f ? HazardPhase.Warning : HazardPhase.Active,
            PhaseLeft = _special.WarnTime > 0f ? _special.WarnTime : _special.ActiveTime,
            LossTime = _special.LossTime,
            Cell = cell
        };
        CollectOccupied(h);
        MarkOccupied(h, true);
        _hazards.Add(h);
        RefreshHazardVisuals(h);
        PlayHazardSpawnSfx(h);
        if (h.Phase == HazardPhase.Active)
        {
            ApplyDangerTint(h);
            ApplyHazardIfPlayerStanding(h);
        }
    }

    void SpawnLineHazard()
    {
        if (!TryPickLine(out bool isRow, out int index))
            return;

        var h = new ActiveHazard
        {
            Kind = SpecialKind.LineDamage,
            Phase = _special.WarnTime > 0f ? HazardPhase.Warning : HazardPhase.Active,
            PhaseLeft = _special.WarnTime > 0f ? _special.WarnTime : _special.ActiveTime,
            LossTime = _special.LossTime,
            IsRow = isRow,
            LineIndex = index
        };
        CollectOccupied(h);
        if (h.OccupiedCells.Count == 0)
            return;

        MarkOccupied(h, true);
        _hazards.Add(h);
        RefreshHazardVisuals(h);
        PlayHazardSpawnSfx(h);
        if (h.Phase == HazardPhase.Active)
        {
            ApplyDangerTint(h);
            ApplyHazardIfPlayerStanding(h);
        }
    }

    void SpawnIceSkate()
    {
        if (!TryPickHazardCell(out var cell))
            return;

        var h = new ActiveHazard
        {
            Kind = SpecialKind.IceSkate,
            Phase = HazardPhase.Active,
            PhaseLeft = _special.ActiveTime,
            Cell = cell
        };
        CollectOccupied(h);
        MarkOccupied(h, true);
        _iceCells.Add(cell);
        _hazards.Add(h);
        RefreshHazardVisuals(h);
    }

    bool TryPickHazardCell(out Vector2Int cell)
    {
        _candidates.Clear();
        Vector2Int? player = _player != null ? _player.GridPos : (Vector2Int?)null;
        for (int row = 0; row < _board.Map.Height; row++)
        {
            for (int col = 0; col < _board.Map.Width; col++)
            {
                var c = new Vector2Int(col, row);
                if (!IsValidHazardSpawn(c, player))
                    continue;
                _candidates.Add(c);
            }
        }

        if (_candidates.Count == 0)
        {
            cell = default;
            return false;
        }

        cell = _candidates[Random.Range(0, _candidates.Count)];
        return true;
    }

    bool TryPickLine(out bool isRow, out int index)
    {
        isRow = Random.value < 0.5f;
        if (!TryFillValidLines(isRow, out var valid))
        {
            // Prefer the other axis if the first choice has no safe lines.
            isRow = !isRow;
            if (!TryFillValidLines(isRow, out valid))
            {
                index = 0;
                return false;
            }
        }

        index = valid[Random.Range(0, valid.Count)];
        return true;
    }

    bool TryFillValidLines(bool isRow, out List<int> valid)
    {
        int max = isRow ? _board.Map.Height : _board.Map.Width;
        valid = new List<int>(max);

        int banned = -1;
        if (_board.Map.TryGetStart(out var start))
            banned = isRow ? start.y : start.x;

        for (int i = 0; i < max; i++)
        {
            if (i == banned)
                continue;
            if (LineContainsBoss(isRow, i))
                continue;
            if (LineHasWalkable(isRow, i))
                valid.Add(i);
        }

        return valid.Count > 0;
    }

    bool LineContainsBoss(bool isRow, int index)
    {
        if (isRow)
        {
            for (int col = 0; col < _board.Map.Width; col++)
            {
                if (_board.HasBoss(new Vector2Int(col, index)))
                    return true;
            }
        }
        else
        {
            for (int row = 0; row < _board.Map.Height; row++)
            {
                if (_board.HasBoss(new Vector2Int(index, row)))
                    return true;
            }
        }
        return false;
    }

    bool LineHasWalkable(bool isRow, int index)
    {
        if (isRow)
        {
            for (int col = 0; col < _board.Map.Width; col++)
            {
                if (_board.Map.IsWalkable(col, index))
                    return true;
            }
        }
        else
        {
            for (int row = 0; row < _board.Map.Height; row++)
            {
                if (_board.Map.IsWalkable(index, row))
                    return true;
            }
        }
        return false;
    }

    bool IsValidHazardSpawn(Vector2Int cell, Vector2Int? player)
    {
        if (!_board.Map.IsWalkable(cell.x, cell.y))
            return false;
        if (_board.Map.IsStart(cell.x, cell.y))
            return false;
        if (player.HasValue && player.Value == cell)
            return false;
        if (_board.HasFood(cell) || _board.HasEnemy(cell) || _board.HasBoss(cell) || _board.HasHazard(cell))
            return false;
        return true;
    }

    void CollectOccupied(ActiveHazard h)
    {
        h.OccupiedCells.Clear();
        if (h.Kind == SpecialKind.LineDamage)
        {
            if (h.IsRow)
            {
                for (int col = 0; col < _board.Map.Width; col++)
                {
                    var c = new Vector2Int(col, h.LineIndex);
                    if (_board.Map.IsWalkable(c.x, c.y))
                        h.OccupiedCells.Add(c);
                }
            }
            else
            {
                for (int row = 0; row < _board.Map.Height; row++)
                {
                    var c = new Vector2Int(h.LineIndex, row);
                    if (_board.Map.IsWalkable(c.x, c.y))
                        h.OccupiedCells.Add(c);
                }
            }
        }
        else
        {
            h.OccupiedCells.Add(h.Cell);
        }
    }

    void MarkOccupied(ActiveHazard h, bool occupied)
    {
        for (int i = 0; i < h.OccupiedCells.Count; i++)
            _board.SetHazardOccupied(h.OccupiedCells[i], occupied);
    }

    void ApplyDangerTint(ActiveHazard h)
    {
        if (!IsDamageKind(h.Kind) || h.Phase != HazardPhase.Active)
            return;

        ClearDangerTint(h);
        for (int i = 0; i < h.OccupiedCells.Count; i++)
        {
            var cell = h.OccupiedCells[i];
            _board.AddHazardTint(cell);
            h.TintedCells.Add(cell);
        }
    }

    void ClearDangerTint(ActiveHazard h)
    {
        for (int i = 0; i < h.TintedCells.Count; i++)
            _board.RemoveHazardTint(h.TintedCells[i]);
        h.TintedCells.Clear();
    }

    static bool IsDamageKind(SpecialKind kind) =>
        kind == SpecialKind.PointDamage
        || kind == SpecialKind.PointDamage2
        || kind == SpecialKind.LineDamage;

    void RefreshHazardVisuals(ActiveHazard h)
    {
        ClearVisuals(h);

        if (h.Kind == SpecialKind.PointDamage
            || h.Kind == SpecialKind.PointDamage2
            || h.Kind == SpecialKind.LineDamage)
        {
            var prefab = h.Phase == HazardPhase.Warning ? _pointPrePrefab : _pointDamagePrefab;
            string fallback = h.Phase == HazardPhase.Warning ? "pointDamagePre" : "pointDamageDamage";
            for (int i = 0; i < h.OccupiedCells.Count; i++)
                h.Visuals.Add(CreateOverlayFromPrefab(h.OccupiedCells[i], prefab, fallback));
            return;
        }

        if (h.Kind == SpecialKind.IceSkate)
        {
            var tint = new Color(0.7f, 0.9f, 1f, 0.95f);
            for (int i = 0; i < h.OccupiedCells.Count; i++)
                h.Visuals.Add(CreateOverlay(h.OccupiedCells[i], _iceSkate, tint));
        }
    }

    GameObject CreateOverlayFromPrefab(Vector2Int cell, GameObject prefab, string fallbackName)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, _overlayRoot);
            go.name = fallbackName + "_" + cell.x + "_" + cell.y;
        }
        else
        {
            go = new GameObject(fallbackName + "_" + cell.x + "_" + cell.y);
            go.transform.SetParent(_overlayRoot, false);
        }

        PrefabUtil.EnsureAnimPlayer(go);
        go.transform.position = _board.CellToWorld(cell);

        var sr = SpriteUtil.ResolveRenderer(go);
        if (sr.sprite == null)
            sr.sprite = SpriteUtil.WhiteSprite();
        sr.sortingOrder = 3;
        MainGameObject.Fit(go, sr, _data.cellSize);
        return go;
    }

    GameObject CreateOverlay(Vector2Int cell, Sprite sprite, Color tint)
    {
        var go = new GameObject("Hazard_" + cell.x + "_" + cell.y);
        go.transform.SetParent(_overlayRoot, false);
        go.transform.position = _board.CellToWorld(cell);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : SpriteUtil.WhiteSprite();
        sr.color = tint;
        sr.sortingOrder = 3;
        GridBoard.FitSprite(sr, _data.cellSize * 0.92f);
        return go;
    }

    static void ClearVisuals(ActiveHazard h)
    {
        for (int i = 0; i < h.Visuals.Count; i++)
        {
            if (h.Visuals[i] != null)
                Destroy(h.Visuals[i]);
        }
        h.Visuals.Clear();
    }

    void RemoveHazard(int index)
    {
        var h = _hazards[index];
        if (h.Kind == SpecialKind.IceSkate)
            _iceCells.Remove(h.Cell);
        ClearDangerTint(h);
        MarkOccupied(h, false);
        ClearVisuals(h);
        _hazards.RemoveAt(index);
    }

    void ClearAllHazards()
    {
        for (int i = _hazards.Count - 1; i >= 0; i--)
            RemoveHazard(i);
        _iceCells.Clear();
    }

    void ApplyHazardIfPlayerStanding(ActiveHazard h)
    {
        if (_player == null || _game == null || !_game.IsPlaying)
            return;
        if (HazardAffectsCell(h, _player.GridPos))
            TriggerHazard(h);
    }

    static bool HazardAffectsCell(ActiveHazard h, Vector2Int cell)
    {
        if (h.Phase != HazardPhase.Active)
            return false;

        switch (h.Kind)
        {
            case SpecialKind.PointDamage:
            case SpecialKind.PointDamage2:
                return h.Cell == cell;
            case SpecialKind.LineDamage:
                return h.IsRow ? cell.y == h.LineIndex : cell.x == h.LineIndex;
            default:
                return false;
        }
    }

    void TriggerHazard(ActiveHazard h)
    {
        if (h.Kind == SpecialKind.PointDamage || h.Kind == SpecialKind.LineDamage)
        {
            PlayStormDamageSfx();
            _game.ApplyTimeDamage(h.LossTime);
            return;
        }

        if (h.Kind == SpecialKind.PointDamage2 && _player != null)
        {
            PlayStormDamageSfx();
            _player.DropAllCarriedFood();
        }
    }

    static void PlayHazardSpawnSfx(ActiveHazard h)
    {
        if (!IsDamageKind(h.Kind))
            return;

        if (h.Phase == HazardPhase.Warning)
            RuntimeManager.PlayOneShot("event:/SFX/Environment/sx_env_storm");
        else if (h.Phase == HazardPhase.Active)
            PlayStormBuildSfx();
    }

    static void PlayStormBuildSfx() =>
        RuntimeManager.PlayOneShot("event:/SFX/Environment/sx_env_storm_build");

    static void PlayStormDamageSfx() =>
        RuntimeManager.PlayOneShot("event:/SFX/Environment/sx_env_storm_damage");

    public void OnPlayerArrived(Vector2Int cell)
    {
        if (!_active || !HasSpecial)
            return;

        for (int i = 0; i < _hazards.Count; i++)
        {
            var h = _hazards[i];
            if (HazardAffectsCell(h, cell))
                TriggerHazard(h);
        }
    }

    public bool IsIceSkate(Vector2Int cell) => _iceCells.Contains(cell);

    void OnDestroy()
    {
        ClearAllHazards();
    }
}
