using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Игровой HUD: высота и рекорд слева, монеты и пасс справа, ступень по центру.
///
/// Раньше это была плоская полоса на весь верх экрана из старых серо-зелёных
/// прямоугольников — на фоне меню она выглядела как из другой игры. Теперь HUD
/// собран из тех же блоков UiKit, что и меню: тёмно-синие скруглённые карточки,
/// синий акцент, золото для наград, иконки MenuIcons. Никаких новых систем не
/// добавлено — только те же высота, рекорд, чекпоинт, монеты, пасс и подсказки.
/// </summary>
public class HUD : MonoBehaviour
{
    public static HUD Instance { get; private set; }

    private LevelManager _manager;
    private Text _heightText;
    private Text _bestText;
    private Text _stageText;
    private Text _checkpointText;
    private Text _coinText;
    private Text _coinPopText;
    private Text _hintText;
    private PauseOverlay _overlay;
    private float _hintTimer;
    private float _coinPopTimer;
    private Text _passText;
    private Text _toastText;
    private readonly List<ToastLine> _toasts = new List<ToastLine>();
    private int _lastHeight = -1;
    private int _lastCoins = -1;
    private int _lastCheckpoint = -1;
    private string _lastStageTitle;
    private bool _toastDirty;

    private class ToastLine
    {
        public string Text;
        public float Until;
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    public void Init(LevelManager manager)
    {
        _manager = manager;
    }

    private void Start()
    {
        UIFactory.EnsureEventSystem();

        Canvas canvas = UIFactory.CreateCanvas("HUD Canvas");

        // Та же сцена 1920x1080, что и в меню: HUD не расползается на широком
        // окне и не уезжает за нижний край, когда холст в единицах ниже 1080.
        RectTransform root = UIFactory.DesignSpace(canvas.transform, "HUD Root");

        BuildHeightCard(root);
        BuildStatsCard(root);
        BuildStagePill(root);
        BuildTopRight(root);
        BuildMessages(root);

        _overlay = PauseOverlay.Build(root, _manager);
        _overlay.Hide();

        MetaEvents.PassChanged += RefreshPass;
        MetaEvents.AchievementUnlocked += OnAchievementUnlocked;
        RefreshPass();
        RefreshBest();
    }

    // ── левая карточка с текущей высотой ─────────────────────────────────────
    private void BuildHeightCard(Transform root)
    {
        RectTransform card = UiKit.Surface(root, "Height Card", 50f, 40f, 380f, 250f,
            UiKit.C_BlueTop, UiKit.C_PanelBottom, 24);
        // CloudStyle включает raycastTarget у каждого спрайта. Украшения HUD
        // не должны перехватывать обзор камеры на тач-экране.
        card.GetComponent<Image>().raycastTarget = false;

        RectTransform tile = UiKit.Surface(card, "Height Icon", 28f, 28f, 108f, 108f,
            UiKit.C_AccentTop, UiKit.C_AccentBottom, 20, false);
        tile.GetComponent<Image>().raycastTarget = false;
        UiKit.Icon(tile, "arrow", UiKit.C_Text, 22f, 22f, 58f, 58f);

        UiKit.Label(card, "ВЫСОТА", 20, UiKit.C_Muted, 124f, 30f, 302f, 60f,
            TextAnchor.MiddleLeft, false);

        _heightText = UiKit.Label(card, "0 м", 56, UiKit.C_Text, 120f, 54f, 310f, 136f,
            TextAnchor.MiddleLeft, true);

        UiKit.Label(card, "текущий подъём", 18, UiKit.C_Dim, 124f, 140f, 320f, 170f,
            TextAnchor.MiddleLeft, false);
    }

    // ── карточка с монетами, рекордом и чекпоинтом ───────────────────────────
    private void BuildStatsCard(Transform root)
    {
        RectTransform card = UiKit.Surface(root, "Stats Card", 398f, 40f, 750f, 250f,
            UiKit.C_Card, UiKit.C_PanelBottom, 24);
        card.GetComponent<Image>().raycastTarget = false;

        _coinText = StatRow(card, 18f, "coin", UiKit.C_Gold, "Монеты", "0");

        _coinPopText = UiKit.Label(card, "", 24, UiKit.C_GoldTop, 140f, 18f, 226f, 74f,
            TextAnchor.MiddleRight, true);

        _bestText = StatRow(card, 80f, "trophy", UiKit.C_Soft, "Рекорд", "0 м");
        _checkpointText = StatRow(card, 142f, "flag", UiKit.C_Green, "Чекпоинт", "0");
    }

    private static Text StatRow(Transform card, float y, string iconId, Color iconColor,
        string caption, string value)
    {
        UiKit.Icon(card, iconId, iconColor, 20f, y + 10f, 60f, y + 50f);
        UiKit.Label(card, caption, 20, UiKit.C_Muted, 72f, y, 220f, y + 56f,
            TextAnchor.MiddleLeft, false);
        return UiKit.Label(card, value, 25, UiKit.C_Text, 220f, y, 322f, y + 56f,
            TextAnchor.MiddleRight, true);
    }

    // ── название ступени по центру ──────────────────────────────────────────
    private void BuildStagePill(Transform root)
    {
        RectTransform pill = UiKit.Surface(root, "Stage Pill", 770f, 40f, 1180f, 112f,
            UiKit.C_CardDark, UiKit.C_PanelBottom, 22);
        pill.GetComponent<Image>().raycastTarget = false;

        UiKit.Icon(pill, "flag", UiKit.C_Soft, 18f, 20f, 54f, 56f);

        _stageText = UiKit.Label(pill, "", 24, UiKit.C_Text, 68f, 0f, 400f, 72f,
            TextAnchor.MiddleLeft, true);
        _stageText.resizeTextMinSize = 14;
    }

    // ── кнопки и чипы у правой кромки ───────────────────────────────────────
    private void BuildTopRight(Transform root)
    {
        Button pause = UiKit.SurfaceButton(root, "Pause Button", 0f, 0f, 150f, 64f,
            UiKit.C_BlueTop, UiKit.C_BlueBottom, OnPauseClicked);
        UIFactory.SetRectRight(pause.GetComponent<RectTransform>(), 50f, 40f, 150f, 64f);
        UiKit.Icon(pause.transform, "pause", UiKit.C_Text, 18f, 18f, 46f, 46f);
        UiKit.Label(pause.transform, "Пауза", 22, UiKit.C_Text, 54f, 0f, 142f, 64f,
            TextAnchor.MiddleLeft, true);

        Button respawn = UiKit.SurfaceButton(root, "Respawn Button", 0f, 0f, 190f, 64f,
            UiKit.C_Tile, UiKit.C_TileDark, () =>
            {
                if (_manager != null) _manager.RespawnPlayer();
            });
        UIFactory.SetRectRight(respawn.GetComponent<RectTransform>(), 216f, 40f, 190f, 64f);
        UiKit.Icon(respawn.transform, "flag", UiKit.C_Green, 18f, 18f, 46f, 46f);
        UiKit.Label(respawn.transform, "К точке", 22, UiKit.C_Text, 54f, 0f, 182f, 64f,
            TextAnchor.MiddleLeft, true);

        RectTransform pass = UiKit.Surface(root, "Pass Chip", 0f, 0f, 430f, 60f,
            UiKit.C_CardDark, UiKit.C_PanelBottom, 18, false);
        pass.GetComponent<Image>().raycastTarget = false;
        UIFactory.SetRectRight(pass, 50f, 120f, 430f, 60f);
        UiKit.Icon(pass, "star", UiKit.C_Gold, 16f, 14f, 48f, 46f);

        // Индекс батл-пасса виден во время забега; «+XP» после смерти приходит
        // тостом снизу, чтобы не спорить с этой строкой за место.
        _passText = UiKit.Label(pass, "", 20, UiKit.C_Text, 58f, 0f, 418f, 60f,
            TextAnchor.MiddleLeft, false);
        _passText.resizeTextMinSize = 14;
    }

    // ── подсказки и тосты внизу по центру ───────────────────────────────────
    private void BuildMessages(Transform root)
    {
        _toastText = UiKit.Label(root, "", 23, UiKit.C_Text, 560f, 620f, 1360f, 880f,
            TextAnchor.LowerCenter, false);
        UiKit.Wrap(_toastText);
        _toastText.verticalOverflow = VerticalWrapMode.Overflow;

        _hintText = UiKit.Label(root, "", 26, new Color(1f, 1f, 1f, 0.8f),
            460f, 880f, 1460f, 980f, TextAnchor.LowerCenter, false);
        UiKit.Wrap(_hintText);

        _hintText.text = SaveService.TouchPreferred()
            ? "Левая половина экрана — джойстик, правая — обзор"
            : "WASD — движение, ПРОБЕЛ — прыжок, ESC — пауза";
        _hintTimer = 6f;
    }

    private void OnDestroy()
    {
        MetaEvents.PassChanged -= RefreshPass;
        MetaEvents.AchievementUnlocked -= OnAchievementUnlocked;
    }

    /// <summary>Тост-очередь мета-систем: +XP, выполненные задания, ачивки.</summary>
    public void PushToast(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        _toasts.Add(new ToastLine { Text = line, Until = Time.unscaledTime + 3.5f });
        if (_toasts.Count > 4)
            _toasts.RemoveAt(0);
        _toastDirty = true;
    }

    private void OnAchievementUnlocked(AchievementDefinition achievement)
    {
        if (achievement == null) return;
        PushToast("Ачивка: " + achievement.Title + " · +" + achievement.Reward + " монет");
    }

    private void RefreshPass()
    {
        if (_passText == null) return;

        int level = SeasonPass.Level;
        if (level >= SeasonPass.LevelCount)
            _passText.text = "Пасс: ур " + level + " — сезон пройден";
        else
            _passText.text = "Пасс: ур " + level + " · " + SeasonPass.XpIntoLevel + "/" +
                             SeasonPass.XpToNext + " XP";
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && _manager != null)
            _manager.Pause();

        UpdateToasts();

        if (_manager == null || _manager.Player == null) return;

        int height = Mathf.Max(0, Mathf.FloorToInt(_manager.LevelHeight));
        if (height != _lastHeight)
        {
            _lastHeight = height;
            if (_heightText != null) _heightText.text = height + " м";
        }

        if (_stageText != null)
        {
            string title = _manager.CurrentStage.Title;
            if (title != _lastStageTitle)
            {
                _lastStageTitle = title;
                _stageText.text = title;
            }
        }

        if (_hintTimer > 0f)
        {
            _hintTimer -= Time.unscaledDeltaTime;
            if (_hintTimer <= 0f)
                _hintText.text = "";
        }

        if (_coinPopTimer > 0f)
        {
            _coinPopTimer -= Time.unscaledDeltaTime;
            if (_coinPopTimer <= 0f)
                _coinPopText.text = "";
        }

        bool paused = GameManager.Instance != null && GameManager.Instance.State == GameState.Paused;
        if (_overlay != null)
        {
            if (paused && !_overlay.IsVisible) _overlay.Show();
            if (!paused && _overlay.IsVisible) _overlay.Hide();
        }
    }

    private void UpdateToasts()
    {
        if (_toastText == null) return;

        bool changed = _toastDirty;
        _toastDirty = false;
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime >= _toasts[i].Until)
            {
                _toasts.RemoveAt(i);
                changed = true;
            }
        }

        if (!changed) return;

        if (_toasts.Count == 0)
        {
            _toastText.text = "";
        }
        else
        {
            string joined = "";
            for (int i = 0; i < _toasts.Count; i++)
                joined += (i > 0 ? "\n" : "") + "• " + _toasts[i].Text;
            _toastText.text = joined;
        }
    }

    private void OnPauseClicked()
    {
        if (_manager != null)
            _manager.Pause();
    }

    private void RefreshBest()
    {
        if (_manager == null) return;
        int best = GameProgress.GetBestHeight();
        _bestText.text = best + " м";

        _lastCoins = GameProgress.GetCoins();
        _coinText.text = UiKit.Money(_lastCoins);

        _lastCheckpoint = _manager.ActiveCheckpointIndex;
        _checkpointText.text = _lastCheckpoint.ToString();
    }

    public void NotifyCheckpoint(int bestHeight)
    {
        if (_bestText != null)
            _bestText.text = bestHeight + " м";
        if (_checkpointText != null && _manager != null)
        {
            _lastCheckpoint = _manager.ActiveCheckpointIndex;
            _checkpointText.text = _lastCheckpoint.ToString();
        }
    }

    public void NotifyCoins(int amount)
    {
        if (amount <= 0) return;

        int coins = GameProgress.GetCoins();
        if (_coinText != null && coins != _lastCoins)
        {
            _lastCoins = coins;
            _coinText.text = UiKit.Money(coins);
        }
        if (_coinPopText != null)
        {
            _coinPopText.text = "+" + amount;
            _coinPopTimer = 0.9f;
        }
    }

    /// <summary>Объявление новой ступени. Пишем в подсказку, а не в отдельный
    /// экран: тот же канал уже занят подсказками управления, и игрок не
    /// привык его игнорировать.</summary>
    public void NotifyStage(string stageTitle, bool wentUp)
    {
        if (_hintText == null) return;

        _hintText.text = (wentUp ? "Новая ступень: " : "Спуск: ") + stageTitle;
        _hintText.color = wentUp ? new Color(1f, 0.92f, 0.7f, 1f) : new Color(0.75f, 0.8f, 0.9f, 1f);
        _hintTimer = 2.6f;
    }

    public void NotifyGoal()
    {
        if (_hintText == null) return;

        _hintText.text = "РАЙ ДОСТИГНУТ — подъём пройден. Выше можно продолжать.";
        _hintText.color = new Color(1f, 1f, 0.8f, 1f);
        _hintTimer = 5f;
    }
}
