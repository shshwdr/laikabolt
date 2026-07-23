using DG.Tweening;
using UnityEngine;

public class FoodItem : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }

    SpriteRenderer _sr;
    GridBoard _board;
    bool _carried;

    public void Setup(GridBoard board, Vector2Int cell, Sprite sprite, float cellSize)
    {
        _board = board;
        GridPos = cell;
        transform.position = board.CellToWorld(cell);

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = sprite;
        _sr.sortingOrder = 5;
        GridBoard.FitSprite(_sr, cellSize * 0.7f);

        board.RegisterFood(cell, this);
    }

    public void BeginCarry(Transform carryRoot, int stackIndex, GameData data)
    {
        if (_carried) return;
        _carried = true;
        _board.UnregisterFood(GridPos);

        transform.SetParent(carryRoot, true);
        transform.DOKill();

        Vector3 halfScale = transform.localScale * data.carryScale;
        float y = data.carryBaseY + stackIndex * data.carryStackHeightStep;
        var seq = DOTween.Sequence();
        seq.Join(transform.DOLocalMove(new Vector3(0f, y, 0f), 0.15f).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(halfScale, 0.15f).SetEase(Ease.OutQuad));
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
