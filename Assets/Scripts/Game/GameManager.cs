using FMODUnity;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    /// <summary>Once the title Start is clicked, TitleView stays hidden for the rest of this play session (including scene reloads).</summary>
    static bool s_titleDone;

    [Header("Data")]
    [SerializeField] MapData mapData;
    [SerializeField] GameData gameData;

    [Header("Phase Roots (assign in scene)")]
    [SerializeField] GameObject exploreRoot;
    [SerializeField] GameObject upgradeRoot;

    [Header("UI (assign in scene)")]
    [SerializeField] TitleView titleView;
    [SerializeField] StoryView storyView;
    [SerializeField] ExploreView exploreView;
    [SerializeField] GameOverView gameOverView;
    [SerializeField] TutorialManager tutorialManager;

    public bool IsPlaying { get; private set; }
    public int Score { get; private set; }
    public int FoodProgress { get; private set; }
    public int FoodTarget { get; private set; }
    public string CurrentSceneId { get; private set; }
    public bool SceneCleared { get; private set; }
    public bool BossPhaseActive { get; private set; }
    public bool IsStoryPlaying => storyView != null && storyView.IsPlaying;

    GridBoard _board;
    PlayerController _player;
    SpawnSystem _spawn;
    SceneSpecialSystem _specials;
    BossCollectFly _boss;
    UpgradePanelView _upgradePanel;
    GameData _runtimeData;
    MetaSaveData _metaSave;
    SceneInfo _sceneInfo;
    float _timeLeft;
    bool _timerStarted;
    bool _runGoldSettled;
    bool _hasCollectFlyBoss;
    int _bossHitsNeeded = 3;
    int _bossMinDistance = 4;
    Vector2Int _pendingStart;
    float _storyResumeTimeScale = 1f;
    bool _storyPausedTime;

    public void Configure(MapData map, GameData data)
    {
        mapData = map;
        gameData = data;
    }

    void Start()
    {
        CSVLoader.Init();
        _metaSave = MetaSaveService.Load();
        CurrentSceneId = MetaSaveService.GetSelectedSceneId(_metaSave);
        _sceneInfo = CSVLoader.GetScene(CurrentSceneId);
        FoodTarget = _sceneInfo != null ? Mathf.Max(1, _sceneInfo.full) : 20;
        ParseBossConfig();

        var loadedMap = MetaSaveService.LoadMapForScene(CurrentSceneId);
        if (loadedMap != null)
            mapData = loadedMap;
        else if (mapData == null)
            mapData = Resources.Load<MapData>("Maps/DefaultMap");

        if (gameData == null)
            gameData = Resources.Load<GameData>("GameData");

        if (mapData == null || gameData == null)
        {
            Debug.LogError("[GameManager] Missing MapData or GameData (Resources/Maps/{scene}, Resources/GameData).");
            enabled = false;
            return;
        }

        if (!mapData.TryGetStart(out var start))
        {
            Debug.LogError("[GameManager] MapData has no Start(s) cell.");
            enabled = false;
            return;
        }

        _runtimeData = MetaSaveService.ApplyUpgrades(gameData, _metaSave);
        if (_sceneInfo != null && _sceneInfo.monsterHP > 0)
            _runtimeData.enemyHitsToKill = _sceneInfo.monsterHP;

        EnsureTitleStoryViews();
        EnsureExploreView();
        EnsureUpgradePanel();
        EnsureCheatManager();
        EnsureTutorialManager();

        if (gameOverView != null)
            gameOverView.Setup(OnGameOverContinue);

        _pendingStart = start;

        // Always start with story/explore hidden; reveal when the flow needs them.
        HideStoryAndExplore();
        if (s_titleDone && titleView != null)
            titleView.Hide();

        if (titleView != null && !s_titleDone)
            BeginTitleFlow();
        else
            BeginExploreRun(start);
    }

    void ParseBossConfig()
    {
        _hasCollectFlyBoss = false;
        BossPhaseActive = false;
        if (_sceneInfo == null || string.IsNullOrEmpty(_sceneInfo.boss))
            return;

        string[] parts = _sceneInfo.boss.Split('|');
        if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
            return;

        if (parts[0].Trim() != "collectFly")
            return;

        _hasCollectFlyBoss = true;
        if (parts.Length > 1 && int.TryParse(parts[1], out int hits))
            _bossHitsNeeded = Mathf.Max(1, hits);
        if (parts.Length > 2 && int.TryParse(parts[2], out int dist))
            _bossMinDistance = Mathf.Max(1, dist);
    }

    void BeginTitleFlow()
    {
        if (upgradeRoot != null)
            upgradeRoot.SetActive(false);

        if (MusicManager.Instance != null)
            MusicManager.Instance.SetGameState(0f);

        HideStoryAndExplore();

        if (storyView != null)
            storyView.Setup();

        titleView.Setup(OnTitleStartClicked);
        titleView.Show();
    }

    void OnTitleStartClicked()
    {
        s_titleDone = true;
        RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_select");

        if (titleView != null)
            titleView.Hide();

        if (storyView != null)
            PlayStartStory();
        else
            OnStartStoryComplete();
    }

    /// <summary>Skip title (and start story) and go straight into explore gameplay.</summary>
    void BeginExploreRun(Vector2Int start)
    {
        if (titleView != null)
            titleView.Hide();

        if (storyView != null)
            storyView.HideImmediate();

        if (MusicManager.Instance != null)
            MusicManager.Instance.SetGameState(1f);

        ApplyExploreMode();
        BeginGame(start);
    }

    /// <summary>0-based index: 5th blank-line group in story/start.txt (reveal StoryView object).</summary>
    const int StartStorySceneBkPage = 4;

    void PlayStartStory()
    {
        var save = MetaSaveService.Load();
        bool seen = save != null && save.HasSeenStartStory;
        PlayStory("story/start", seen, OnStartStoryComplete, OnStartStoryPage);
    }

    void OnStartStoryPage(int pageIndex)
    {
        if (pageIndex < StartStorySceneBkPage || storyView == null)
            return;

        storyView.SetPageRevealVisible(true);
    }

    void OnStartStoryComplete()
    {
        MarkStartStorySeen();

        if (storyView != null)
            storyView.HideImmediate();

        if (MusicManager.Instance != null)
            MusicManager.Instance.SetGameState(1f);

        ApplyExploreMode();
        BeginGame(_pendingStart);
    }

    void MarkStartStorySeen()
    {
        var save = MetaSaveService.Load();
        if (save == null || save.HasSeenStartStory)
            return;
        save.HasSeenStartStory = true;
        MetaSaveService.Save(save);
        _metaSave = save;
    }

    void OnGameOverContinue()
    {
        if (ShouldPlayEndStory())
            PlayEndStory();
        else
            EnterUpgradeMode();
    }

    /// <summary>Third planet = scene id 2 (Mars=0, Jupiter=1, Saturn=2).</summary>
    bool ShouldPlayEndStory()
    {
        return SceneCleared && CurrentSceneId == "2";
    }

    void PlayEndStory()
    {
        if (upgradeRoot != null)
            upgradeRoot.SetActive(false);

        if (storyView == null)
        {
            EnterUpgradeMode();
            return;
        }

        var save = MetaSaveService.Load();
        bool seen = save != null && save.HasSeenEndStory;
        PlayStory("story/end", seen, OnEndStoryComplete);
    }

    void OnEndStoryComplete()
    {
        MarkEndStorySeen();

        if (storyView != null)
            storyView.HideImmediate();

        EnterUpgradeMode();
    }

    void MarkEndStorySeen()
    {
        var save = MetaSaveService.Load();
        if (save == null || save.HasSeenEndStory)
            return;
        save.HasSeenEndStory = true;
        MetaSaveService.Save(save);
        _metaSave = save;

        if (MusicManager.Instance != null)
            MusicManager.Instance.SetGameState(0f);
    }

    void HideExploreView()
    {
        if (exploreRoot != null)
            exploreRoot.SetActive(false);
        if (exploreView != null && exploreView.gameObject != exploreRoot)
            exploreView.gameObject.SetActive(false);
    }

    void HideStoryAndExplore()
    {
        HideExploreView();

        if (storyView != null)
            storyView.HideImmediate();
    }

    /// <summary>Hides explore HUD, then plays a story. All story entry points go through here.</summary>
    void PlayStory(string resourcePath, bool showSkipHint, System.Action completeCallback, System.Action<int> pageChangedCallback = null)
    {
        HideExploreView();
        if (storyView == null)
        {
            completeCallback?.Invoke();
            return;
        }

        storyView.gameObject.SetActive(true);
        storyView.Play(resourcePath, showSkipHint, completeCallback, pageChangedCallback);
    }

    void EnsureTitleStoryViews()
    {
        if (titleView == null)
            titleView = FindObjectOfType<TitleView>(true);
        if (storyView == null)
            storyView = FindObjectOfType<StoryView>(true);
    }

    void EnsureExploreView()
    {
        if (exploreView == null && exploreRoot != null)
            exploreView = exploreRoot.GetComponent<ExploreView>()
                ?? exploreRoot.GetComponentInChildren<ExploreView>(true);

        if (exploreView != null)
            exploreView.Setup(EndGame, CurrentSceneId);
    }

    void EnsureUpgradePanel()
    {
        if (upgradeRoot == null)
            return;

        _upgradePanel = upgradeRoot.GetComponent<UpgradePanelView>();
        if (_upgradePanel == null)
            _upgradePanel = upgradeRoot.AddComponent<UpgradePanelView>();

        _upgradePanel.Setup(_metaSave, StartNextRun, sceneSelected: OnUpgradeSceneSelected);
    }

    void OnUpgradeSceneSelected(string sceneId)
    {
        if (exploreView != null)
            exploreView.ApplySceneBackground(sceneId);
    }

    void EnsureCheatManager()
    {
        if (GetComponent<CheatManager>() == null)
            gameObject.AddComponent<CheatManager>();
    }

    void EnsureTutorialManager()
    {
        if (tutorialManager == null)
            tutorialManager = FindObjectOfType<TutorialManager>(true);

        if (tutorialManager == null)
        {
            var go = new GameObject("TutorialManager");
            go.transform.SetParent(transform, false);
            go.AddComponent<TutorialView>();
            tutorialManager = go.AddComponent<TutorialManager>();
        }
    }

    Transform GetExploreParent()
    {
        return exploreRoot != null ? exploreRoot.transform : transform;
    }

    void ApplyExploreMode()
    {
        if (exploreRoot != null)
            exploreRoot.SetActive(true);
        if (upgradeRoot != null)
            upgradeRoot.SetActive(false);
    }

    public void EnterUpgradeMode()
    {
        IsPlaying = false;


        if (_spawn != null)
            _spawn.Stop();
        if (_specials != null)
            _specials.Stop();

        SettleRunGold();

        if (exploreRoot != null)
            exploreRoot.SetActive(false);
        if (upgradeRoot != null)
            upgradeRoot.SetActive(true);

        if (_upgradePanel != null)
            _upgradePanel.OnShown();
    }

    void SettleRunGold()
    {
        if (_runGoldSettled)
            return;

        _runGoldSettled = true;
        int runGold = Score;
        var save = MetaSaveService.Load();
        save.MetaGold += runGold;
        MetaSaveService.Save(save);
        _metaSave = save;
    }

    void StartNextRun()
    {
        // Continue from upgrade: reload for a clean board, but never return to TitleView.
        s_titleDone = true;
        SceneFlowService.ReloadActiveScene();
    }

    void BeginGame(Vector2Int start)
    {
        Transform parent = GetExploreParent();
        RuntimeManager.PlayOneShot("event:/SFX/UX/sx_ui_gameStart");

        var boardGo = new GameObject("Board");
        boardGo.transform.SetParent(parent, false);
        _board = boardGo.AddComponent<GridBoard>();
        _board.Init(mapData, _runtimeData);
        _board.SpawnStartMarkers();

        var playerGo = PrefabUtil.Instantiate("prefab/player", _board.EntityRoot, "Player");
        _player = playerGo.GetComponent<PlayerController>();
        if (_player == null)
            _player = playerGo.AddComponent<PlayerController>();
        _player.Setup(_board, _runtimeData, this, start);
        EnsureTutorialTarget(playerGo, "player");

        _spawn = gameObject.AddComponent<SpawnSystem>();
        _spawn.Init(_board, _runtimeData, _player, this, CurrentSceneId);

        _specials = gameObject.AddComponent<SceneSpecialSystem>();
        _specials.Init(_board, _runtimeData, this, _player,
            _sceneInfo != null ? _sceneInfo.special : null);
        _player.BindSpecials(_specials);

        if (exploreView != null)
        {
            exploreView.Setup(EndGame, CurrentSceneId);
            exploreView.SetScore(0, immediate: true);
            exploreView.SetFoodProgress(0, FoodTarget);
        }
        NotifyCarryChanged();

        CenterCamera();

        _timeLeft = _runtimeData.roundDuration;
        _timerStarted = false;
        IsPlaying = true;
        SceneCleared = false;
        BossPhaseActive = false;
        FoodProgress = 0;
        _boss = null;
        _spawn.SpawnInitial();
        if (exploreView != null)
            exploreView.SetTimer(_timeLeft);

        RegisterBoardTutorialTargets();
        if (exploreView != null)
            exploreView.RegisterTutorialHudTargets();

        if (tutorialManager != null)
            tutorialManager.TryShowTutorial("start");
    }

    static void EnsureTutorialTarget(GameObject go, string identifier)
    {
        if (go == null || string.IsNullOrEmpty(identifier))
            return;

        var target = go.GetComponent<TutorialGameobject>();
        if (target == null)
            target = go.AddComponent<TutorialGameobject>();
        target.SetIdentifier(identifier);
    }

    void RegisterBoardTutorialTargets()
    {
        if (_board == null)
            return;

        // Spaceship = start markers.
        for (int i = 0; i < _board.EntityRoot.childCount; i++)
        {
            var child = _board.EntityRoot.GetChild(i);
            if (child != null && child.name.StartsWith("Start"))
            {
                EnsureTutorialTarget(child.gameObject, "spaceship");
                break;
            }
        }

        // First ore / mine for higherSort highlights.
        var foods = _board.EntityRoot.GetComponentsInChildren<FoodItem>(true);
        if (foods != null && foods.Length > 0 && foods[0] != null)
            EnsureTutorialTarget(foods[0].gameObject, "ore");

        var enemies = _board.EntityRoot.GetComponentsInChildren<EnemyItem>(true);
        if (enemies != null && enemies.Length > 0 && enemies[0] != null)
            EnsureTutorialTarget(enemies[0].gameObject, "mine");
    }

    /// <summary>Starts round countdown and timed spawning on the player's first successful action.</summary>
    public void NotifyPlayerActed()
    {
        if (_timerStarted || !IsPlaying)
            return;

        _timerStarted = true;
        if (_spawn != null)
            _spawn.StartTimedSpawning();
        if (_specials != null)
            _specials.StartRunning();
    }

    public void ShowFullToast()
    {
        if (exploreView != null)
            exploreView.ShowToast("Full!");
    }

    public void ShowBossCaughtToast()
    {
        if (exploreView != null)
            exploreView.ShowToast("Caught! Return to hole!");
    }

    void CenterCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        float w = (mapData.Width - 1) * _runtimeData.cellSize;
        float h = (mapData.Height - 1) * _runtimeData.cellSize;
        cam.transform.position = new Vector3(w * 0.5f, h * 0.5f, -10f);
        float halfH = mapData.Height * _runtimeData.cellSize * 0.5f + 0.75f;
        float halfW = mapData.Width * _runtimeData.cellSize * 0.5f + 0.75f;
        float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
        cam.orthographicSize = Mathf.Max(halfH, halfW / aspect);
    }

    void Update()
    {
        if (!IsPlaying || !_timerStarted || IsStoryPlaying)
            return;

        _timeLeft -= Time.deltaTime;
        if (exploreView != null)
            exploreView.SetTimer(_timeLeft);
        if (_timeLeft <= 0f)
            EndGameByTimeout();
    }

    public void ApplyTimeDamage(float seconds)
    {
        if (!IsPlaying || IsStoryPlaying || seconds <= 0f)
            return;

        _timeLeft -= seconds;
        if (exploreView != null)
            exploreView.SetTimer(_timeLeft);

        if (_player != null)
            DamageNumber.Spawn(_player.transform.position, Mathf.Max(1, Mathf.RoundToInt(seconds)));

        if (_timeLeft <= 0f)
            EndGameByTimeout();
    }

    public bool IsLastMinuteActive =>
        IsPlaying
        && !IsStoryPlaying
        && _timerStarted
        && _runtimeData != null
        && _runtimeData.lastMinute
        && _timeLeft > 0f
        && _timeLeft < 5f;

    public FoodItem CreateLastMinuteFood()
    {
        if (_spawn == null)
            return null;
        return _spawn.CreateCarryOnlyFood();
    }

    void EndGameByTimeout()
    {
        // Mid-move into home past halfway still counts as arrived (deposit + finalSafe).
        if (_player != null)
            _player.TryCommitHomeArrivalForTimeout();
        TryApplyFinalSafeBonus();
        EndGame();
    }

    void TryApplyFinalSafeBonus()
    {
        if (_runtimeData == null || _runtimeData.finalSafePercent <= 0)
            return;
        if (_player == null || _board == null)
            return;
        if (!_board.Map.IsStart(_player.GridPos.x, _player.GridPos.y))
            return;

        int bonus = Mathf.RoundToInt(Score * (_runtimeData.finalSafePercent / 100f));
        if (bonus > 0)
            AddScore(bonus);
    }

    public void AddScore(int amount)
    {
        Score += amount;
        if (exploreView != null)
            exploreView.SetScore(Score);
    }

    /// <summary>Cheat: instantly deposit food into the hole during explore.</summary>
    public void CheatDepositFood(int count)
    {
        if (!IsPlaying || count <= 0 || _runtimeData == null)
            return;

        int score = count * (1 + Mathf.Max(0, _runtimeData.foodCollectAmount));
        AddScore(score);
        AddFoodProgress(count);
    }

    /// <summary>Each food deposited into the hole adds 1 progress toward scene.full.</summary>
    public void AddFoodProgress(int amount)
    {
        if (!IsPlaying || SceneCleared || amount <= 0)
            return;

        FoodProgress = Mathf.Min(FoodTarget, FoodProgress + amount);
        if (exploreView != null)
            exploreView.SetFoodProgress(FoodProgress, FoodTarget);

        if (FoodProgress < FoodTarget)
            return;

        // Progress bar full: trigger boss if configured, otherwise clear.
        if (_hasCollectFlyBoss)
        {
            if (!BossPhaseActive)
                BeginBossPhase();
            return;
        }

        CompleteSceneClear();
    }

    void BeginBossPhase()
    {
        BossPhaseActive = true;

        Vector2Int from = _player != null ? _player.GridPos : Vector2Int.zero;
        if (!BossCollectFly.TryPickSpawnCell(_board, from, _bossMinDistance, out var cell))
        {
            Debug.LogWarning("[GameManager] No cell for collectFly boss.");
            CompleteSceneClear();
            return;
        }

        var save = MetaSaveService.Load();
        bool firstBoss = save != null && !save.HasSeenBossStory;
        if (firstBoss && storyView != null)
        {
            PauseTimeForStory();
            PlayStory("story/boss", false, () => OnBossStoryComplete(cell));
            return;
        }

        SpawnCollectFlyBoss(cell);
    }

    void OnBossStoryComplete(Vector2Int cell)
    {
        MarkBossStorySeen();
        ResumeTimeAfterStory();
        if (storyView != null)
            storyView.HideImmediate();
        ApplyExploreMode();
        SpawnCollectFlyBoss(cell);
    }

    void SpawnCollectFlyBoss(Vector2Int cell)
    {
        if (exploreView != null)
            exploreView.ShowToast("Boss appeared!");

        var go = PrefabUtil.Instantiate("prefab/boss", _board.EntityRoot, "BossCollectFly");
        PrefabUtil.EnsureAnimPlayer(go);
        _boss = go.GetComponent<BossCollectFly>();
        if (_boss == null)
            _boss = go.AddComponent<BossCollectFly>();
        _boss.Setup(_board, _runtimeData, this, _player, cell, _bossHitsNeeded, _bossMinDistance);
    }

    void MarkBossStorySeen()
    {
        var save = MetaSaveService.Load();
        if (save == null || save.HasSeenBossStory)
            return;
        save.HasSeenBossStory = true;
        MetaSaveService.Save(save);
        _metaSave = save;
    }

    void PauseTimeForStory()
    {
        if (_storyPausedTime)
            return;
        _storyResumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        _storyPausedTime = true;
    }

    void ResumeTimeAfterStory()
    {
        if (!_storyPausedTime)
            return;
        Time.timeScale = _storyResumeTimeScale > 0f ? _storyResumeTimeScale : 1f;
        _storyPausedTime = false;
    }

    public bool TryTouchBoss(PlayerController player)
    {
        if (!BossPhaseActive || _boss == null || player == null)
            return false;
        if (_boss.IsCaught || _boss.IsFlying)
            return false;
        if (_boss.GridPos != player.GridPos)
            return false;

        return _boss.TryTouch(player);
    }

    public void NotifyBossDeposited()
    {
        if (!IsPlaying || SceneCleared)
            return;

        _boss = null;
        CompleteSceneClear();
    }

    void CompleteSceneClear()
    {
        if (SceneCleared)
            return;

        SceneCleared = true;
        var save = MetaSaveService.Load();
        MetaSaveService.ClearScene(save, CurrentSceneId);
        _metaSave = save;

        if (exploreView != null)
            exploreView.ShowToast("Cleared!");

        EndGame();
    }

    public void NotifyCarryChanged()
    {
        if (_player != null && exploreView != null)
            exploreView.SetCarry(_player.CarryCount, _runtimeData.holdItemCount);
    }

    public void EndGame()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;
        ResumeTimeAfterStory();
        if (_spawn != null)
            _spawn.Stop();
        if (_specials != null)
            _specials.Stop();

        if (TryPlayClearOrOverStory())
            return;

        ShowGameOverOrUpgrade();
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetGameState(0f);
    }

    bool TryPlayClearOrOverStory()
    {
        if (storyView == null)
            return false;

        var save = MetaSaveService.Load();
        if (save == null)
            return false;

        if (SceneCleared && !save.HasSeenClearStory)
        {
            PlayStory("story/clear", false, OnClearStoryComplete);
            return true;
        }

        if (!SceneCleared && !save.HasSeenOverStory)
        {
            PlayStory("story/over", false, OnOverStoryComplete);
            return true;
        }

        return false;
    }

    void OnClearStoryComplete()
    {
        MarkClearStorySeen();
        if (storyView != null)
            storyView.HideImmediate();
        ShowGameOverOrUpgrade();
    }

    void OnOverStoryComplete()
    {
        MarkOverStorySeen();
        if (storyView != null)
            storyView.HideImmediate();
        ShowGameOverOrUpgrade();
    }

    void MarkClearStorySeen()
    {
        var save = MetaSaveService.Load();
        if (save == null || save.HasSeenClearStory)
            return;
        save.HasSeenClearStory = true;
        MetaSaveService.Save(save);
        _metaSave = save;
    }

    void MarkOverStorySeen()
    {
        var save = MetaSaveService.Load();
        if (save == null || save.HasSeenOverStory)
            return;
        save.HasSeenOverStory = true;
        MetaSaveService.Save(save);
        _metaSave = save;
    }

    void ShowGameOverOrUpgrade()
    {
        if (gameOverView != null)
            gameOverView.Show(Score, SceneCleared);
        else
            EnterUpgradeMode();
    }
}
