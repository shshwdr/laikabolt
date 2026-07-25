using DG.Tweening;
using UnityEngine;
using FMODUnity;

public class FoodItem : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public bool IsCarried => _carried;
    public bool IsAttractFlying { get; private set; }

    SpriteRenderer _sr;
    GridBoard _board;
    bool _carried;

    public void Setup(GridBoard board, Vector2Int cell, float cellSize)
    {
        _board = board;
        GridPos = cell;

        BindRenderer(cellSize);

        int stackIndex = board.RegisterFood(cell, this);
        ApplyGroundStack(stackIndex);
    }

    /// <summary>Food that never sat on the board (e.g. lastMinute duplicate).</summary>
    public void SetupUnregistered(float cellSize)
    {
        _board = null;
        _carried = false;
        GridPos = default;

        BindRenderer(cellSize);
    }

    void BindRenderer(float cellSize)
    {
        _sr = SpriteUtil.ResolveRenderer(gameObject);
        if (_sr.sprite == null)
            _sr.sprite = SpriteUtil.WhiteSprite();
        MainGameObject.Fit(gameObject, _sr, cellSize);
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

        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/Interactables/sx_int_stone_pickUp");

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
    /// Attack-attract: fly from the ore's world cell onto Laika's carry stack.
    /// </summary>
    public void BeginAttractCarry(Transform carryRoot, int stackIndex, GameData data)
    {
        if (_carried) return;
        _carried = true;
        IsAttractFlying = true;
        if (_board != null)
            _board.UnregisterFood(this);

        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/Interactables/sx_int_stone_pickUp");

        transform.SetParent(carryRoot, true);
        transform.DOKill();

        Vector3 halfScale = transform.localScale * data.carryScale;
        float y = data.carryBaseY + stackIndex * data.carryStackHeightStep;
        float dur = 0.28f;
        float jump = data != null ? data.cellSize * 0.55f : 0.55f;
        int capturedIndex = stackIndex;
        var seq = DOTween.Sequence();
        seq.Join(transform.DOLocalJump(new Vector3(0f, y, 0f), jump, 1, dur).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(halfScale, dur).SetEase(Ease.OutQuad));
        seq.OnComplete(() =>
        {
            IsAttractFlying = false;
            SnapCarryStack(capturedIndex, data);
        });
        if (_sr != null)
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

        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/NPC/sx_npc_robot_pickUp");

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
        IsAttractFlying = false;
        transform.SetParent(carryRoot, true);
        transform.DOKill();
        SnapCarryStack(stackIndex, data);
    }

    public void SnapCarryStack(int stackIndex, GameData data)
    {
        if (IsAttractFlying)
        {
            if (_sr != null)
                _sr.sortingOrder = 12 + stackIndex;
            return;
        }

        float y = data.carryBaseY + stackIndex * data.carryStackHeightStep;
        transform.localPosition = new Vector3(0f, y, 0f);
        if (_sr != null)
            _sr.sortingOrder = 12 + stackIndex;
    }

    public void DepositAndDestroy(Vector3 target, float duration)
    {
        transform.SetParent(null, true);
        transform.DOKill();

        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/Interactables/sx_int_stone_dropOff");

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
