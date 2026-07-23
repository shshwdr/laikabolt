#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapData))]
public class MapDataEditor : Editor
{
    const float CellPx = 40f;

    MapCellType _brush = MapCellType.Walkable;
    int _editWidth = 4;
    int _editHeight = 3;
    string _asciiDraft =
        "xxx\n" +
        "soxx\n" +
        "xxxx";

    void OnEnable()
    {
        var map = (MapData)target;
        if (map != null)
        {
            _editWidth = map.Width;
            _editHeight = map.Height;
        }
    }

    public override void OnInspectorGUI()
    {
        var map = (MapData)target;
        map.EnsureCells();

        EditorGUILayout.HelpBox(
            "x=walkable  o=blocked  s=start. Short rows pad with o on the right. Editor row0 is top; runtime Y is flipped. Left-click to paint.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        _editWidth = EditorGUILayout.IntField("Width", _editWidth);
        _editHeight = EditorGUILayout.IntField("Height", _editHeight);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Apply Size", GUILayout.Height(24f)))
        {
            Undo.RecordObject(map, "Resize MapData");
            map.Resize(_editWidth, _editHeight, MapCellType.Blocked);
            EditorUtility.SetDirty(map);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        BrushButton("x Walkable", MapCellType.Walkable, new Color(0.65f, 0.78f, 0.95f));
        BrushButton("o Blocked", MapCellType.Blocked, new Color(0.45f, 0.45f, 0.48f));
        BrushButton("s Start", MapCellType.Start, new Color(0.45f, 0.85f, 0.55f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        Rect gridRect = GUILayoutUtility.GetRect(
            map.Width * CellPx,
            map.Height * CellPx,
            GUILayout.ExpandWidth(false));

        HandleGrid(map, gridRect);
        if (Event.current.type == EventType.Repaint)
            DrawGrid(map, gridRect);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("ASCII Import (short rows pad with o)", EditorStyles.boldLabel);
        _asciiDraft = EditorGUILayout.TextArea(_asciiDraft, GUILayout.MinHeight(60f));
        if (GUILayout.Button("Apply ASCII"))
        {
            var lines = _asciiDraft.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            Undo.RecordObject(map, "Apply ASCII Map");
            map.ApplyAscii(lines);
            _editWidth = map.Width;
            _editHeight = map.Height;
            EditorUtility.SetDirty(map);
        }

        if (GUILayout.Button("Fill Default xxxo/soxx/xxxx"))
        {
            Undo.RecordObject(map, "Default Map");
            map.ApplyAscii(new[] { "xxx", "soxx", "xxxx" });
            _editWidth = map.Width;
            _editHeight = map.Height;
            EditorUtility.SetDirty(map);
        }

        if (GUI.changed)
            EditorUtility.SetDirty(map);
    }

    void BrushButton(string label, MapCellType type, Color tint)
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = _brush == type ? Color.Lerp(tint, Color.white, 0.35f) : tint;
        if (GUILayout.Button(label, GUILayout.Height(24f)))
            _brush = type;
        GUI.backgroundColor = prev;
    }

    void HandleGrid(MapData map, Rect gridRect)
    {
        Event e = Event.current;
        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag)
            return;
        if (e.button != 0)
            return;
        if (!gridRect.Contains(e.mousePosition) && e.type != EventType.MouseDrag)
            return;

        float lx = (e.mousePosition.x - gridRect.x) / CellPx;
        float ly = (e.mousePosition.y - gridRect.y) / CellPx;
        int col = Mathf.Clamp(Mathf.FloorToInt(lx), 0, map.Width - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt(ly), 0, map.Height - 1);

        if (!gridRect.Contains(e.mousePosition))
            return;

        if (map.GetCell(col, row) != _brush)
        {
            Undo.RecordObject(map, "Paint Map Cell");
            map.SetCell(col, row, _brush);
            EditorUtility.SetDirty(map);
            GUI.changed = true;
        }
        e.Use();
        Repaint();
    }

    static void DrawGrid(MapData map, Rect gridRect)
    {
        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                var cellRect = new Rect(
                    gridRect.x + col * CellPx,
                    gridRect.y + row * CellPx,
                    CellPx,
                    CellPx);
                EditorGUI.DrawRect(cellRect, CellColor(map.GetCell(col, row)));

                var line = new Color(0f, 0f, 0f, 0.35f);
                EditorGUI.DrawRect(new Rect(cellRect.xMax - 1f, cellRect.yMin, 1f, cellRect.height), line);
                EditorGUI.DrawRect(new Rect(cellRect.xMin, cellRect.yMax - 1f, cellRect.width, 1f), line);

                var style = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    normal = { textColor = Color.black }
                };
                GUI.Label(cellRect, MapData.CellToChar(map.GetCell(col, row)).ToString(), style);
            }
        }
    }

    static Color CellColor(MapCellType t)
    {
        switch (t)
        {
            case MapCellType.Blocked: return new Color(0.42f, 0.42f, 0.45f);
            case MapCellType.Start: return new Color(0.4f, 0.8f, 0.5f);
            default: return new Color(0.72f, 0.76f, 0.82f);
        }
    }
}

#endif
