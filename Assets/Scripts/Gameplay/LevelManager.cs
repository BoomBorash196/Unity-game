using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    public static bool RestartFresh;

    public GameObject PlayerPrefab;

    public PlayerController Player { get; private set; }
    public LevelData Data { get; private set; }

    public int ActiveCheckpointIndex
    {
        get { return Data != null ? Data.ActiveCheckpointIndex : 0; }
    }

    public float LevelHeight
    {
        get { return Player != null ? Player.CurrentHeight - StageCatalog.WorldOriginY : 0f; }
    }

    /// <summary>Конфиг ступени, на которой игрок находится прямо сейчас.
    /// Раньше он был один на весь подъём; теперь ступени меняют и габариты,
    /// и дальность прыжка, и глубину, на которой считается смертью падения.</summary>
    private LevelConfig Cfg
    {
        get { return StageCatalog.StageAt(LevelHeight).Cfg; }
    }

    /// <summary>Ступень, на которой игрок находится сейчас. HUD берёт отсюда
    /// название, и на этом же значении строится разделительный экран.</summary>
    public StageDefinition CurrentStage
    {
        get { return _gen != null ? _gen.CurrentStage : StageCatalog.Stage(0); }
    }

    public int CurrentStageIndex
    {
        get { return _gen != null ? _gen.CurrentStageIndex : 0; }
    }

    private const int CheckpointCoins = 25;

    /// <summary>Награда за взятую ступень, платится один раз за ступень за
    /// всё время. Не за забег: иначе второй забег до неба был бы уже дороже,
    /// и нагромождение дешёвых монет снова съедало бы смысл шопа.</summary>
    private const int StageClearCoins = 120;

    private LevelGenerator _gen;
    private PlayerRespawn _respawn;
    private CameraFollow _camera;
    private HUD _hud;
    private int _estimateIndex;
    private int _lastKeepFrom = int.MinValue;
    private float _sessionMaxHeight;
    private int _lastStageIndex;
    private bool _finished;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            GameObject app = new GameObject("[App] GameManager");
            app.AddComponent<GameManager>();
        }
        if (GameManager.Instance != null) GameManager.Instance.State = GameState.Playing;
        Time.timeScale = 1f;
        InputService.EnsureExists();
        SceneSetup.EnsureLight();

        // Все биомы и силуэты заранее: иначе первый вид на новой ступени
        // генерирует текстуру и меш в игровом кадре.
        Textures.Prewarm();
        PlatformMesh.Prewarm();

        BuildLevel();
        BuildCounters();
    }

    private void BuildLevel()
    {
        int startCp = RestartFresh
            ? 0
            : (GameManager.Instance != null ? GameManager.Instance.RequestedStartCheckpoint : 0);
        RestartFresh = false;

        _gen = gameObject.AddComponent<LevelGenerator>();
        _gen.BuildData();
        Data = _gen.Data;

        int startPlatform = _gen.CheckpointPlatformIndex(startCp);
        _gen.ResetPath(Mathf.Max(0, startPlatform - _gen.Cfg.SupportBelow));
        _gen.GenerateUpTo(startPlatform + _gen.Cfg.StreamAhead);

        Vector3 spawn;
        if (Data.Checkpoints.TryGetValue(startCp, out Checkpoint spawnCp))
            spawn = spawnCp.RespawnPosition;
        else
            spawn = _gen.PositionOf(Mathf.Max(0, startPlatform)) + Vector3.up * (0.35f + Cfg.Thickness * 0.4f);

        GameObject playerGo = PlayerPrefab != null
            ? Instantiate(PlayerPrefab, spawn, Quaternion.identity)
            : BuildFallbackPlayer(spawn);
        playerGo.name = "Player";
        Player = playerGo.GetComponent<PlayerController>();

        SkinApplier.Apply(playerGo, SkinService.SelectedSkin);

        _respawn = playerGo.AddComponent<PlayerRespawn>();
        _respawn.Player = Player;
        _respawn.SetCheckpoint(spawn, false);
        _respawn.SetKillY(Player.CurrentHeight - Cfg.FallMargin);

        // Отслеживание мета-прогресса забега: высота отрезка, прыжки, концы
        // отрезков. Сидит на игроке, потому что все события здесь же.
        MetaRunTracker metaTracker = playerGo.AddComponent<MetaRunTracker>();
        metaTracker.Init(Player, _respawn);

        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 800f;
            camGo.AddComponent<AudioListener>();
        }
        _camera = cam.GetComponent<CameraFollow>();
        if (_camera == null)
            _camera = cam.gameObject.AddComponent<CameraFollow>();
        _camera.Target = Player.transform;
        _camera.ResetTo(35f);
        if (_respawn != null)
            _respawn.Camera = _camera;

        // Небо ставим после камеры: камера создаётся здесь же, и до этого момента
        // ClearFlags менять не на чем.
        Sky.Reset();
        SceneSetup.EnsureSky(cam, new Color(0.55f, 0.75f, 0.95f, 1f));

        Data.ActiveCheckpointIndex = startCp;
        foreach (var pair in Data.Checkpoints)
        {
            Checkpoint c = pair.Value;
            if (c.Index <= startCp)
                c.Activate();
            else if (c.Index == startCp + 1)
                c.SetCollectable(true);
            else
                c.SetCollectable(false);
        }

        _estimateIndex = startPlatform;
        _sessionMaxHeight = spawn.y;

        // Стартовая ступень фиксируется сразу: если игрок продолжает забег с
        // чекпоинта, первое же срабатывание не должно выскочить как «новый этап».
        _lastStageIndex = CurrentStageIndex;
        _finished = false;
    }

    private GameObject BuildFallbackPlayer(Vector3 spawn)
    {
        GameObject root = new GameObject("Player (fallback)");
        root.transform.position = spawn;
        CharacterController cc = root.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1f, 0f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = Vector3.one;
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
            Destroy(bodyCollider);
        body.GetComponent<Renderer>().material = Palette.Player();

        root.AddComponent<PlayerController>();
        return root;
    }

    private void BuildCounters()
    {
        GameObject hudGo = new GameObject("[UI] HUD");
        _hud = hudGo.AddComponent<HUD>();
        _hud.Init(this);

        hudGo.AddComponent<TouchControls>();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            return;
        if (Player == null || _gen == null)
            return;

        Vector3 playerPosition = Player.transform.position;
        LevelConfig cfg = Cfg;

        _gen.GenerateUpTo(Mathf.Max(_gen.AliveMax, _estimateIndex + cfg.StreamAhead));

        while (_estimateIndex < _gen.AliveMax &&
               playerPosition.y >= _gen.PositionOf(_estimateIndex + 1).y - 0.1f)
        {
            _estimateIndex++;
        }

        _gen.GenerateUpTo(_estimateIndex + cfg.StreamAhead);

        if (playerPosition.y > _sessionMaxHeight)
            _sessionMaxHeight = playerPosition.y;

        // Нижняя граница меняется только на чекпоинте или при смене ступени,
        // поэтому нечего перебирать платформы каждый кадр.
        int keepFrom = _gen.CheckpointPlatformIndex(ActiveCheckpointIndex) - cfg.SupportBelow;
        if (keepFrom != _lastKeepFrom)
        {
            _lastKeepFrom = keepFrom;
            _gen.ReleaseBelow(keepFrom);
        }

        // Небо, туман и облачные ярусы ведутся за игроком. Отдельно от Update
        // генератора, чтобы не смешивать поток уровня с оформлением кадра.
        Sky.Follow(playerPosition);

        // Следующий чекпоинт открывается в BuildLevel и ReachedCheckpoint,
        // отдельный вызов каждый кадр только повторно красил бы его материал.
        TrackStage();
        CheckGoal();
    }

    /// <summary>
    /// Следит за сменой ступени. Считаем по высоте игрока, а не по ступени
    /// генератора: генератор строит трассу на 20 площадок вперёд и к моменту
    /// прихода игрока уже может быть на ступень выше — и тогда этап объявился
    /// бы раньше, чем игрок до него дошёл.
    /// </summary>
    private void TrackStage()
    {
        int index = StageCatalog.IndexAt(LevelHeight);
        if (index == _lastStageIndex) return;

        bool wentUp = index > _lastStageIndex;
        _lastStageIndex = index;

        if (!wentUp) return;

        StageDefinition stage = StageCatalog.Stage(index);
        if (GameProgress.SetStageReached(index))
        {
            SkinService.AddCoins(StageClearCoins);
        }
        MetaSystems.RegisterStageReached(index);

        if (_hud != null) _hud.NotifyStage(stage.Title, wentUp);
    }

    /// <summary>Финиш подъёма. Отмечается один раз за забег: состояние
    /// ставится, но игра не останавливается — рай продолжается, просто
    /// прохождение уже засчитано.</summary>
    private void CheckGoal()
    {
        if (_finished) return;
        if (LevelHeight < StageCatalog.GoalHeight) return;

        _finished = true;
        GameProgress.SetFinished();
        MetaSystems.EvaluateNow();
        YandexCloudSave.RequestSave();
        SaveService.FlushAll();
        if (_hud != null) _hud.NotifyGoal();
    }

    public void ReachedCheckpoint(Checkpoint cp)
    {
        if (cp.Index <= ActiveCheckpointIndex)
            return;

        cp.Activate();
        Data.ActiveCheckpointIndex = cp.Index;

        if (Data.Checkpoints.TryGetValue(cp.Index + 1, out Checkpoint next))
            next.SetCollectable(true);

        int cpPlatform = _gen.CheckpointPlatformIndex(cp.Index);
        _respawn.SetCheckpoint(cp.RespawnPosition, true);
        _respawn.SetKillY(_gen.PositionOf(cpPlatform).y - Cfg.FallMargin);

        int levelHeight = Mathf.FloorToInt(_sessionMaxHeight - StageCatalog.WorldOriginY);
        int previousBest = GameProgress.GetBestHeight();
        float previousHeight = GameProgress.GetCheckpointHeight(cp.Index);
        int best = Mathf.Max(previousBest, levelHeight);
        GameProgress.SetBest(best, cp.Index);
        GameProgress.SetCheckpointHeight(cp.Index, levelHeight);

        if (previousHeight <= 0f)
            SkinService.AddCoins(CheckpointCoins);
        if (best > previousBest)
            SkinService.AddRecord(best - previousBest);

        // Рекорд обновился — на нём стоят ачивки высоты.
        MetaSystems.EvaluateNow();

        // Чекпоинт — точка сохранения: собираем все изменения кадра в один
        // локальный Save и один облачный снапшот.
        YandexCloudSave.RequestSave();
        SaveService.FlushAll();

        if (_hud != null)
            _hud.NotifyCheckpoint(best);
    }

    public void NotifyCoins(int amount)
    {
        if (_hud != null)
            _hud.NotifyCoins(amount);
    }

    public void RespawnPlayer()
    {
        if (_respawn != null)
            _respawn.Respawn();
    }

    public void Pause()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause();
    }

    public void ToMenu()
    {
        // Замыкаем текущий отрезок забега мета-системами, иначе выход в меню
        // молча терял бы набранную высоту и задания по «завершённым забегам».
        if (MetaRunTracker.Current != null)
            MetaRunTracker.Current.EndByLeaving();

        if (GameManager.Instance != null)
            GameManager.Instance.ToMainMenu();
        else
            SceneLoader.LoadMainMenu();
    }

    public void RestartLevel()
    {
        RestartFresh = true;
        SceneLoader.RestartGameplay();
    }
}
