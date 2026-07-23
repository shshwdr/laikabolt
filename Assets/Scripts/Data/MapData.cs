using UnityEngine;

[CreateAssetMenu(fileName = "MapData", menuName = "GMTK/Map Data", order = 0)]
public class MapData : ScriptableObject
{
    [SerializeField] int width = 4;
    [SerializeField] int height = 3;
    [SerializeField] MapCellType[] cells = new MapCellType[12];

    public int Width => width;
    public int Height => height;

    void OnEnable()
    {
        EnsureCells();
    }

    public void EnsureCells()
    {
        int count = Mathf.Max(1, width) * Mathf.Max(1, height);
        if (cells != null && cells.Length == count)
            return;
        cells = new MapCellType[count];
        for (int i = 0; i < count; i++)
            cells[i] = MapCellType.Blocked;
    }

    public void Resize(int newWidth, int newHeight, MapCellType fill = MapCellType.Blocked)
    {
        newWidth = Mathf.Max(1, newWidth);
        newHeight = Mathf.Max(1, newHeight);
        var next = new MapCellType[newWidth * newHeight];
        for (int i = 0; i < next.Length; i++)
            next[i] = fill;

        if (cells != null)
        {
            int copyW = Mathf.Min(width, newWidth);
            int copyH = Mathf.Min(height, newHeight);
            for (int row = 0; row < copyH; row++)
            {
                for (int col = 0; col < copyW; col++)
                    next[row * newWidth + col] = cells[row * width + col];
            }
        }

        width = newWidth;
        height = newHeight;
        cells = next;
    }

    public static int Index(int col, int row, int w) => row * w + col;

    public bool InBounds(int col, int row)
    {
        return (uint)col < (uint)width && (uint)row < (uint)height;
    }

    public MapCellType GetCell(int col, int row)
    {
        EnsureCells();
        if (!InBounds(col, row))
            return MapCellType.Blocked;
        return cells[Index(col, row, width)];
    }

    public void SetCell(int col, int row, MapCellType type)
    {
        EnsureCells();
        if (!InBounds(col, row))
            return;
        cells[Index(col, row, width)] = type;
    }

    public bool IsWalkable(int col, int row)
    {
        var t = GetCell(col, row);
        return t == MapCellType.Walkable || t == MapCellType.Start;
    }

    public bool IsStart(int col, int row) => GetCell(col, row) == MapCellType.Start;

    public bool TryGetStart(out Vector2Int cell)
    {
        EnsureCells();
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                if (cells[Index(col, row, width)] == MapCellType.Start)
                {
                    cell = new Vector2Int(col, row);
                    return true;
                }
            }
        }
        cell = default;
        return false;
    }

    /// <summary>Write from ASCII rows; short rows pad Blocked(o) on the right. row0 = top.</summary>
    public void ApplyAscii(string[] lines)
    {
        if (lines == null || lines.Length == 0)
            return;

        int h = lines.Length;
        int w = 0;
        for (int i = 0; i < lines.Length; i++)
            w = Mathf.Max(w, lines[i] != null ? lines[i].Length : 0);
        w = Mathf.Max(1, w);

        Resize(w, h, MapCellType.Blocked);
        for (int row = 0; row < h; row++)
        {
            string line = lines[row] ?? string.Empty;
            for (int col = 0; col < w; col++)
            {
                char c = col < line.Length ? line[col] : 'o';
                cells[Index(col, row, width)] = CharToCell(c);
            }
        }
    }

    public static MapCellType CharToCell(char c)
    {
        switch (char.ToLowerInvariant(c))
        {
            case 's': return MapCellType.Start;
            case 'o': return MapCellType.Blocked;
            default: return MapCellType.Walkable;
        }
    }

    public static char CellToChar(MapCellType t)
    {
        switch (t)
        {
            case MapCellType.Start: return 's';
            case MapCellType.Blocked: return 'o';
            default: return 'x';
        }
    }
}
