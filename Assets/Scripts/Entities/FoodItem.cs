using DG.Tweening;
using UnityEngine;

public class FoodItem : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public bool IsCarried => _carried;

    SpriteRenderer _sr;
    GridBoard _board;
    bool _carried;

    public void Setup(GridBoard board, Vector2Int cell, Sprite sprite, float cellSize)
    {
        _board = board;
        GridPos = cell;

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = sprite;
        GridBoard.FitSprite(_sr, cellSize * 0.7f);

        int stackIndex = board.RegisterFood(cell, this);
        ApplyGroundStack(stackIndex);
    }

    public void ApplyGroundStack(int stackIndex)
    {
        if (_board == null || _carried)
            return;

        float step = _board.Data != null ? _board.Data.groundStackHeightStep : 0.25f;
        Vector3 basePos = _board.CellToWorld(GridPos);
        transform.position = basePos + Vector3.up * (stackIndex * step);
        if (_sr != null)
            _sr.sortingOrder = 5 + stackIndex;
    }

    public void BeginCarry(Transform carryRoot, int stackIndex, GameData data)
    {
        if (_carried) return;
        _carried = true;
        if (_board != null)
            _board.UnregisterFood(this);

        transform.SetParent(carryRoot, true);
        transform.DOKill();

        Vector3 halfScale = transform.localScale * data.carryScale;
        float y = data.carryBaseY + stackIndex * data.carryStackHeightStep;
        var seq = DOTween.Sequence();
        seq.Join(transform.DOLocalMove(new Vector3(0f, y, 0f), 0.15f).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(halfScale, 0.15f).SetEase(Ease.OutQuad));
        _sr.sortingOrder = 12 + stackIndex;
    }

    /// <summary>
    /// Immediately leaves the board cell, then animates onto a machine stack.
    /// </summary>
    public void BeginMachineCarry(Transform carryRoot, int stackIndex, GameData data, float duration, System.Action onComplete)
    {
        if (_carried) return;
        _carried = true;
        if (_board != null)
            _board.UnregisterFood(this);

        transform.SetParent(carryRoot, true);
        transform.DOKill();

        Vector3 halfScale = transform.localScale * data.carryScale;
        float y = data.carryBaseY + stackIndex * data.carryStackHeightStep;
        float dur = Mathf.Max(0.01f, duration);
        var seq = DOTween.Sequence();
        seq.Join(transform.DOLocalMove(new Vector3(0f, y, 0f), dur).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(halfScale, dur).SetEase(Ease.OutQuad));
        seq.OnComplete(() => onComplete?.Invoke());
        _sr.sortingOrder = 12 + stackIndex;
    }

    public void TransferToCarry(Transform carryRoot, int stackIndex, GameData data)
    {
        _carried = true;
        transform.SetParent(carryRoot, true);
        transform.DOKill();
        SnapCarryStack(stackIndex, data);
    }

    public void SnapCarryStack(int stackIndex, GameData data)
    {
        float y = data.carryBaseY + stackIndex * data.carryStackHeightStep;
        transform.localPosition = new Vector3(0f, y, 0f);
        if (_sr != null)
            _sr.sortingOrder = 12 + stackIndex;
    }

    public void DepositAndDestroy(Vector3 target, float duration)
    {
        transform.SetParent(null, true);
        transform.DOKill();
        var seq = DOTween.Sequence();
        seq.Join(transform.DOMove(target, duration).SetEase(Ease.InQuad));
        seq.Join(transform.DOScale(Vector3.zero, duration).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(gameObject));
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}
