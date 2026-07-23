using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] MapData mapData;
    [SerializeField] GameData gameData;

    public bool IsPlaying { get; private set; }
    public int Score { get; private set; }

    GridBoard _board;
    PlayerController _player;
    SpawnSystem _spawn;
    GameHUD _hud;
    float _timeLeft;

    public void Configure(MapData map, GameData data)
    {
        mapData = map;
        gameData = data;
    }

    void Start()
    {
        if (mapData == null)
            mapData = Resources.Load<MapData>("Maps/DefaultMap");
        if (gameData == null)
            gameData = Resources.Load<GameData>("GameData");

        if (mapData == null || gameData == null)
        {
            Debug.LogError("[GameManager] Missing MapData or GameData (Resources/Maps/DefaultMap, Resources/GameData).");
            enabled = false;
            return;
        }

        if (!mapData.TryGetStart(out var start))
        {
            Debug.LogError("[GameManager] MapData has no Start(s) cell.");
            enabled = false;
            return;
        }

        BeginGame(start);
    }

    void BeginGame(Vector2Int start)
    {
        var boardGo = new GameObject("Board");
        boardGo.transform.SetParent(transform, false);
        _board = boardGo.AddComponent<GridBoard>();
        _board.Init(mapData, gameData);

        var playerSprite = SpriteUtil.LoadOr(gameData.playerSprite, "render/player");
        var foodSprite = SpriteUtil.LoadOr(gameData.foodSprite, "render/food");
        var monsterSprite = SpriteUtil.LoadOr(gameData.monsterSprite, "render/monster");

        if (playerSprite == null || foodSprite == null || monsterSprite == null)
            Debug.LogWarning("[GameManager] Some sprites failed to load from Resources/render.");

        var playerGo = new GameObject("Player");
        playerGo.transform.SetParent(_board.EntityRoot, false);
        _player = playerGo.AddComponent<PlayerController>();
        _player.Setup(_board, gameData, this, start, playerSprite != null ? playerSprite : SpriteUtil.WhiteSprite());

        _spawn = gameObject.AddComponent<SpawnSystem>();
        _spawn.Init(_board, gameData, _player,
            foodSprite != null ? foodSprite : SpriteUtil.WhiteSprite(),
            monsterSprite != null ? monsterSprite : SpriteUtil.WhiteSprite());

        var hudGo = new GameObject("HUD");
        hudGo.transform.SetParent(transform, false);
        _hud = hudGo.AddComponent<GameHUD>();
        _hud.Build();
        _hud.SetScore(0);
        _hud.SetCarry(0);

        CenterCamera();

        _timeLeft = gameData.roundDuration;
        IsPlaying = true;
        _spawn.StartSpawning();
        _hud.SetTimer(_timeLeft);
    }

    void CenterCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        float w = (mapData.Width - 1) * gameData.cellSize;
        float h = (mapData.Height - 1) * gameData.cellSize;
        cam.transform.position = new Vector3(w * 0.5f, h * 0.5f, -10f);
        float halfH = mapData.Height * gameData.cellSize * 0.5f + 0.75f;
        float halfW = mapData.Width * gameData.cellSize * 0.5f + 0.75f;
        float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
        cam.orthographicSize = Mathf.Max(halfH, halfW / aspect);
    }

    void Update()
    {
        if (!IsPlaying) return;

        _timeLeft -= Time.deltaTime;
        _hud.SetTimer(_timeLeft);
        if (_timeLeft <= 0f)
            EndGame();
    }

    public void AddScore(int amount)
    {
        Score += amount;
        _hud.SetScore(Score);
    }

    public void NotifyCarryChanged()
    {
        if (_player != null)
            _hud.SetCarry(_player.CarryCount);
    }

    void EndGame()
    {
        IsPlaying = false;
        _spawn.Stop();
        _hud.ShowEnd(Score);
    }
}
