using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Главный экран по макету Figma: иллюстрированный фон, карточка скина слева,
/// столбец крупных кнопок по центру и три карточки статистики справа.
///
/// Все координаты взяты из макета (холст 844x390) и переводятся в сцену
/// 1920x1080 равномерным масштабом 2,275. Макет шире 16:9, поэтому по высоте
/// остаётся по 96 пикселей сверху и снизу — интерфейс стоит по центру.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    // ── палитра макета ──────────────────────────────────────────────────────
    private static readonly Color C_Card = MenuArt.Hex("12203C");
    private static readonly Color C_CardDark = MenuArt.Hex("0E1C36");
    private static readonly Color C_Tile = MenuArt.Hex("16294F");
    private static readonly Color C_AccentTop = MenuArt.Hex("3D8BFF");
    private static readonly Color C_AccentBottom = MenuArt.Hex("2563EB");
    private static readonly Color C_Text = MenuArt.Hex("EAF2FF");
    private static readonly Color C_Muted = MenuArt.Hex("8FA6CC");
    private static readonly Color C_Soft = MenuArt.Hex("7FB3FF");
    private static readonly Color C_Gold = MenuArt.Hex("FFD86B");
    private static readonly Color C_GoldDark = MenuArt.Hex("2A2308");
    private static readonly Color C_Green = MenuArt.Hex("3FBF7F");

    // ── масштаб макета 844x390 → сцена 1920x1080 ────────────────────────────
    private const float S = 2.275f;
    private const float OY = 96f;

    private static float X(float d) { return d * S; }
    private static float Y(float d) { return d * S + OY; }

    private RectTransform _root;
    private GameObject _settingsPanel;
    private GameObject _checkpointsPanel;

    private Text _coinsValue;
    private Text _pointsValue;
    private Text _checkpointsValue;
    private Text _skinName;
    private Text _bestHeight;
    private Text _dailyLabel;
    private Text _passLabel;
    private Text _questLabel;
    private Text _leaderboardLabel;

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            GameObject app = new GameObject("[App] GameManager");
            app.AddComponent<GameManager>();
        }

        SceneSetup.EnsureLight();
        SceneSetup.EnsureBackgroundCamera(new Color(0.06f, 0.08f, 0.13f, 1f));

        UIFactory.EnsureEventSystem();

        Canvas canvas = UIFactory.CreateCanvas("MainMenu Canvas");

        // Иллюстрированный фон макета: градиент, луна, облака, горы.
        GameObject bgGo = new GameObject("Background");
        RectTransform bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.SetParent(canvas.transform, false);
        UIFactory.Stretch(bgRt);
        Image bg = bgGo.AddComponent<Image>();
        bg.sprite = MenuArt.Background();
        bg.color = Color.white;
        bg.raycastTarget = false;

        RectTransform root = UIFactory.DesignSpace(canvas.transform, "Root");
        _root = root;

        // ── заголовок ───────────────────────────────────────────────────────
        Label(root, "Паркур в рай", 58, C_Text, X(65), 74f, X(430), 158f, TextAnchor.MiddleLeft, true);
        Label(root, "бесконечный подъём", 22, C_Muted, X(65) + 2f, 154f, X(430), 190f,
            TextAnchor.MiddleLeft, false);

        BuildSkinCard(root);
        BuildMenuColumn(root);
        BuildStatColumn(root);
        BuildQuickActions(root);

        RefreshStats();
        RefreshMetaButtons();
    }

    // ── левая карточка со скином ────────────────────────────────────────────
    private void BuildSkinCard(Transform root)
    {
        RectTransform card = Surface(root, "Skin Card", X(65), Y(60), X(275), Y(310), C_Card, C_Card);

        // Свечение за персонажем.
        Glow(card, new Color(0.24f, 0.55f, 1f, 0.55f), 30f, 70f, 448f, 450f);

        Label(card, "ТЕКУЩИЙ СКИН", 20, C_Muted, 30f, 24f, 448f, 54f, TextAnchor.MiddleLeft, false);
        _skinName = Label(card, "—", 32, C_Text, 30f, 54f, 448f, 100f, TextAnchor.MiddleLeft, true);

        Icon(card, "person", C_Soft, 89f, 112f, 389f, 412f);

        Label(card, "ЛУЧШИЙ РЕЗУЛЬТАТ", 20, C_Muted, 30f, 404f, 448f, 434f, TextAnchor.MiddleLeft, false);
        _bestHeight = Label(card, "0 м", 44, C_Text, 30f, 432f, 448f, 490f, TextAnchor.MiddleLeft, true);

        // Полоска прогресса до следующего десятка метров.
        RectTransform barBg = Surface(card, "Progress", 30f, 506f, 448f, 528f,
            MenuArt.Hex("16305C"), MenuArt.Hex("16305C"));
        RectTransform barFill = Surface(barBg, "Fill", 0f, 0f, 300f, 22f, C_AccentTop, C_AccentBottom);
        barFill.name = "Progress Fill";

        Button skins = SurfaceButton(root, "Skins", X(90), Y(296), X(250), Y(346),
            MenuArt.Hex("16305C"), C_Card, ShowSkins);
        Icon(skins.GetComponent<RectTransform>(), "person", C_Soft, 40f, 33f, 84f, 77f);
        Label(skins.transform, "Скины", 30, C_Text, 100f, 0f, 340f, 114f, TextAnchor.MiddleLeft, true);
    }

    // ── центральный столбец ─────────────────────────────────────────────────
    private void BuildMenuColumn(Transform root)
    {
        MenuButton(root, X(320), Y(50), X(560), Y(100), "Играть", "play",
            C_AccentTop, C_AccentBottom, Play, true);

        MenuButton(root, X(320), Y(110), X(560), Y(160), "Магазин", "shop",
            C_Card, C_Card, ShowSkinsAfterPlay);

        _dailyLabel = MenuButton(root, X(320), Y(170), X(560), Y(220), "Ежедневные награды", "gift",
            C_Card, C_Card, OpenDaily).GetComponentInChildren<Text>();

        MenuButton(root, X(320), Y(230), X(560), Y(280), "Достижения", "trophy",
            C_Card, C_Card, OpenAchievements);

        MenuButton(root, X(320), Y(290), X(560), Y(340), "Настройки", "gear",
            C_Card, C_Card, ShowSettings);
    }

    // ── правая колонка со статистикой ───────────────────────────────────────
    private void BuildStatColumn(Transform root)
    {
        _coinsValue = StatCard(root, X(588), Y(90), X(804), Y(154), "coin", C_GoldDark, C_Gold,
            "МОНЕТЫ", "0", null);

        _pointsValue = StatCard(root, X(588), Y(164), X(804), Y(228), "star", C_Tile, C_Soft,
            "ОЧКИ", "0", null);

        _checkpointsValue = StatCard(root, X(588), Y(238), X(804), Y(302), "flag", C_Tile, C_Green,
            "ЧЕКПОИНТЫ", "0 пройдено", ShowCheckpoints);
    }

    // ── три круглые кнопки в правом верхнем углу ────────────────────────────
    private void BuildQuickActions(Transform root)
    {
        _leaderboardLabel = RoundButton(root, 40f, 40f, 96f, "arrow", C_Card, C_Soft, "Рейтинг",
            OpenLeaderboard);
        _questLabel = RoundButton(root, 152f, 40f, 96f, "check", C_Card, C_Green, "Задания",
            OpenQuests);
        _passLabel = RoundButton(root, 264f, 40f, 96f, "star", C_Card, C_Gold, "Пасс", OpenPass);
    }

    // ── действия ────────────────────────────────────────────────────────────
    private void Play()
    {
        if (GameManager.Instance == null) return;
        int bestCp = GameProgress.GetBestCheckpoint();
        if (bestCp > 0) GameManager.Instance.StartFromCheckpoint(bestCp);
        else GameManager.Instance.StartNewGame();
    }

    private void ShowSkinsAfterPlay()
    {
        ShowSkins();
    }

    private void ShowSkins()
    {
        if (_root == null) return;
        SkinsUI.Open(_root, RefreshStats);
    }

    private void OpenDaily()
    {
        if (_root == null) return;
        DailyRewardUI.Open(_root, RefreshStats);
    }

    private void OpenAchievements()
    {
        if (_root == null) return;
        AchievementsUI.Open(_root, RefreshAll);
    }

    private void OpenPass()
    {
        if (_root == null) return;
        PassUI.Open(_root, RefreshAll);
    }

    private void OpenQuests()
    {
        if (_root == null) return;
        DailyQuestsUI.Open(_root, RefreshAll);
    }

    private void OpenLeaderboard()
    {
        if (_root == null) return;
        LeaderboardUI.Open(_root, RefreshAll);
    }

    private void RefreshAll()
    {
        RefreshMetaButtons();
        RefreshStats();
    }

    // ── обновление данных ───────────────────────────────────────────────────
    private void RefreshStats()
    {
        if (_coinsValue != null)
            _coinsValue.text = FormatNumber(GameProgress.GetCoins());

        if (_pointsValue != null)
            _pointsValue.text = FormatNumber(GameProgress.GetPoints());

        if (_checkpointsValue != null)
            _checkpointsValue.text = GameProgress.GetBestCheckpoint() + " пройдено";

        if (_skinName != null)
        {
            SkinDefinition skin = SkinService.SelectedSkin;
            _skinName.text = skin != null ? skin.Title : "—";
        }

        if (_bestHeight != null)
        {
            int best = GameProgress.GetBestHeight();
            _bestHeight.text = best + " м";

            RectTransform card = _bestHeight.transform.parent as RectTransform;
            Transform fill = card != null ? card.Find("Progress/Progress Fill") : null;
            if (fill != null)
            {
                float ratio = Mathf.Clamp01((best % 50) / 50f);
                RectTransform frt = fill.GetComponent<RectTransform>();
                frt.sizeDelta = new Vector2(418f * ratio, 22f);
            }
        }

        RefreshDailyLabel();
    }

    private void RefreshDailyLabel()
    {
        if (_dailyLabel == null) return;
        _dailyLabel.text = DailyReward.CanClaim ? "Ежедневные награды  ●" : "Ежедневные награды";
    }

    private void RefreshMetaButtons()
    {
        if (_passLabel != null)
            _passLabel.text = "Пасс " + SeasonPass.Level;

        if (_questLabel != null)
        {
            int ready = DailyQuests.CompletedUnclaimed();
            _questLabel.text = ready > 0 ? "Задания ·" + ready : "Задания";
        }

        if (_leaderboardLabel != null)
            _leaderboardLabel.text = "Рейтинг";

        // Яндекс SDK: показать подготовленный межстраничный баннер.
        if (YandexGames.Instance != null) YandexGames.Instance.ShowInterstitialIfNeeded();
        if (YandexGames.Ready && YandexGames.Instance != null) YandexGames.Instance.RequestLoadingReady();
    }

    private static string FormatNumber(int v)
    {
        return v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
    }

    // ── окно настроек ───────────────────────────────────────────────────────
    private void ShowSettings()
    {
        if (_settingsPanel != null || _root == null) return;

        GameObject go = new GameObject("Settings Overlay");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(_root, false);
        UIFactory.Stretch(rt);

        Image dim = go.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.04f, 0.08f, 0.78f);
        dim.raycastTarget = true;

        RectTransform panel = UIFactory.Panel(go.transform, "Settings Panel", C_Card);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(860f, 830f);
        panel.anchoredPosition = Vector2.zero;
        ApplyRound(panel.GetComponent<Image>(), C_Card, C_Card);

        Label(panel, "НАСТРОЙКИ", 44, C_Text, 0f, 28f, 860f, 100f, TextAnchor.MiddleCenter, true);
        Label(panel, "Чувствительность обзора: мышь — база ×7, тач — база ×1", 20, C_Muted,
            70f, 108f, 790f, 148f, TextAnchor.MiddleLeft, false);

        Slider mouseSlider = BuildSettingsRow(panel, "Мышь — обзор", () => GameSettings.MouseLookSensitivity,
            v => { GameSettings.MouseLookSensitivity = v; },
            GameSettings.MouseMin, GameSettings.MouseMax, GameSettings.MouseStep, 190f);

        Slider touchSlider = BuildSettingsRow(panel, "Тач — обзор (драг экрана)", () => GameSettings.TouchLookSensitivity,
            v => { GameSettings.TouchLookSensitivity = v; },
            GameSettings.TouchMin, GameSettings.TouchMax, GameSettings.TouchStep, 310f);

        Button resetSettings = SurfaceButton(panel, "Reset Settings", 70f, 420f, 790f, 494f,
            C_Tile, MenuArt.Hex("12203C"), () =>
            {
                GameSettings.ResetAll();
                mouseSlider.value = GameSettings.MouseLookSensitivity;
                touchSlider.value = GameSettings.TouchLookSensitivity;
            });
        Label(resetSettings.transform, "Сбросить настройки", 26, C_Text, 0f, 0f, 720f, 74f,
            TextAnchor.MiddleCenter, true);

        Button adBtn = SurfaceButton(panel, "Rewarded Ad", 70f, 508f, 790f, 582f,
            MenuArt.Hex("FFE59A"), MenuArt.Hex("F2C24E"), ShowRewardedAd);
        Icon(adBtn.GetComponent<RectTransform>(), "gift", MenuArt.Hex("2A2308"), 28f, 19f, 64f, 55f);
        Label(adBtn.transform, "Реклама +100 монет", 26, MenuArt.Hex("2A2308"), 80f, 0f, 720f, 74f,
            TextAnchor.MiddleLeft, true);

        Button resetProgress = SurfaceButton(panel, "Reset Progress", 70f, 596f, 790f, 670f,
            MenuArt.Hex("3A1F2A"), MenuArt.Hex("2A1620"), ResetProgress);
        Label(resetProgress.transform, "Сбросить прогресс", 26, MenuArt.Hex("FFB4C0"), 0f, 0f, 720f, 74f,
            TextAnchor.MiddleCenter, true);

        Button back = SurfaceButton(panel, "Back", 70f, 712f, 790f, 790f,
            MenuArt.Hex("2A3A5C"), MenuArt.Hex("1E2C48"), CloseSettings);
        Label(back.transform, "Назад", 28, C_Text, 0f, 0f, 720f, 78f, TextAnchor.MiddleCenter, true);

        _settingsPanel = go;
    }

    private Slider BuildSettingsRow(Transform parent, string label, Func<float> get, Action<float> set,
        float min, float max, float step, float y0)
    {
        Label(parent, label, 22, C_Muted, 70f, y0, 790f, y0 + 40f, TextAnchor.MiddleLeft, false);

        Text value = Label(parent, "1,0x", 26, C_Text, 145f, y0 + 50f, 255f, y0 + 120f,
            TextAnchor.MiddleCenter, true);

        Button minus = SurfaceButton(parent, "Minus", 70f, y0 + 55f, 130f, y0 + 115f,
            MenuArt.Hex("26365A"), MenuArt.Hex("1B2846"), () =>
            {
                set(Mathf.Max(min, get() - step));
                value.text = FormatSens(get());
            });
        Label(minus.transform, "−", 34, C_Text, 0f, 0f, 60f, 60f, TextAnchor.MiddleCenter, true);

        Slider slider = UIFactory.Slider(parent, get(), min, max, v =>
        {
            set(v);
            value.text = FormatSens(v);
        });
        UIFactory.SetRect(slider.GetComponent<RectTransform>(), 265f, y0 + 60f, 640f, y0 + 110f);

        Button plus = SurfaceButton(parent, "Plus", 655f, y0 + 55f, 715f, y0 + 115f,
            MenuArt.Hex("26365A"), MenuArt.Hex("1B2846"), () =>
            {
                set(Mathf.Min(max, get() + step));
                value.text = FormatSens(get());
            });
        Label(plus.transform, "+", 34, C_Text, 0f, 0f, 60f, 60f, TextAnchor.MiddleCenter, true);

        value.text = FormatSens(get());
        return slider;
    }

    private void ShowRewardedAd()
    {
        if (YandexAds.CanShowRewarded)
        {
            YandexAds.ShowRewarded();
            YandexAds.RewardedCompleted += OnRewarded;
            YandexAds.RewardedClosed += UnhookRewarded;
        }
        else
        {
            SkinService.AddCoins(100);
            RefreshStats();
        }
    }

    private void ResetProgress()
    {
        GameProgress.ClearAll();
        YandexCloudSave.Save();
        SceneLoader.LoadMainMenu();
    }

    // ── окно чекпоинтов ─────────────────────────────────────────────────────
    private void ShowCheckpoints()
    {
        if (_checkpointsPanel != null || _root == null) return;

        GameObject go = new GameObject("Checkpoints Overlay");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(_root, false);
        UIFactory.Stretch(rt);

        Image dim = go.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.04f, 0.08f, 0.78f);
        dim.raycastTarget = true;

        RectTransform panel = UIFactory.Panel(go.transform, "Checkpoints Panel", C_Card);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(1180f, 880f);
        panel.anchoredPosition = Vector2.zero;
        ApplyRound(panel.GetComponent<Image>(), C_Card, C_Card);

        Label(panel, "ЧЕКПОИНТЫ", 44, C_Text, 0f, 28f, 1180f, 100f, TextAnchor.MiddleCenter, true);
        Label(panel, "Начни с пройденной точки или с самого начала", 20, C_Muted,
            0f, 98f, 1180f, 132f, TextAnchor.MiddleCenter, false);

        ScrollRect scroll = UIFactory.CreateScrollView(panel, new Color(0.07f, 0.11f, 0.19f, 0.9f));
        UIFactory.SetRect(scroll.GetComponent<RectTransform>(), 60f, 150f, 1120f, 760f);
        ApplyRound(scroll.GetComponent<Image>(), MenuArt.Hex("12203C"), MenuArt.Hex("12203C"));

        RectTransform content = scroll.content;
        const float rowH = 66f;
        const float gap = 12f;
        const float rowWidth = 1030f;
        int row = 0;

        float y0 = row * (rowH + gap);
        Button start = SurfaceButton(content, "Start", 14f, y0, 14f + rowWidth, y0 + rowH,
            C_AccentTop, C_AccentBottom, () => { CloseCheckpoints(); Play(); });
        Label(start.transform, "Играть с начала", 26, C_Text, 24f, 0f, rowWidth - 30f, rowH,
            TextAnchor.MiddleLeft, true);
        row++;

        int bestCp = GameProgress.GetBestCheckpoint();
        int limit = Mathf.Min(bestCp, 200);
        for (int cp = 1; cp <= limit; cp++)
        {
            int checkpoint = cp;
            float height = GameProgress.GetCheckpointHeight(checkpoint);
            float y = row * (rowH + gap);
            Button b = SurfaceButton(content, "Checkpoint " + checkpoint, 14f, y, 14f + rowWidth, y + rowH,
                MenuArt.Hex("1A2A4A"), MenuArt.Hex("152340"), () =>
                {
                    CloseCheckpoints();
                    if (GameManager.Instance != null)
                        GameManager.Instance.StartFromCheckpoint(checkpoint);
                });
            Label(b.transform, "Точка " + checkpoint + " — " + Mathf.FloorToInt(height) + " м", 26,
                C_Text, 24f, 0f, rowWidth - 30f, rowH, TextAnchor.MiddleLeft, false);
            row++;
        }

        if (bestCp == 0)
        {
            float y = row * (rowH + gap);
            Label(content, "Пройденных чекпоинтов пока нет — заберись до первого в игре.", 22, C_Muted,
                24f, y, 24f + rowWidth, y + rowH, TextAnchor.MiddleLeft, false);
            row++;
        }
        else if (bestCp > limit)
        {
            float y = row * (rowH + gap);
            Label(content, "... показаны последние " + limit + " чекпоинтов", 20, C_Muted,
                24f, y, 24f + rowWidth, y + rowH, TextAnchor.MiddleLeft, false);
            row++;
        }

        content.sizeDelta = new Vector2(0f, row * (rowH + gap) + 10f);

        Button back = SurfaceButton(panel, "Back", 400f, 780f, 780f, 852f,
            MenuArt.Hex("2A3A5C"), MenuArt.Hex("1E2C48"), CloseCheckpoints);
        Label(back.transform, "Назад", 28, C_Text, 0f, 0f, 380f, 72f, TextAnchor.MiddleCenter, true);

        _checkpointsPanel = go;
    }

    private void CloseCheckpoints()
    {
        if (_checkpointsPanel != null)
        {
            Destroy(_checkpointsPanel);
            _checkpointsPanel = null;
        }
    }

    private void CloseSettings()
    {
        if (_settingsPanel != null)
        {
            Destroy(_settingsPanel);
            _settingsPanel = null;
        }
    }

    private void OnRewarded()
    {
        YandexAds.RewardedCompleted -= OnRewarded;
        YandexAds.RewardedClosed -= UnhookRewarded;
        SkinService.AddCoins(100);
        RefreshStats();
    }

    private void UnhookRewarded()
    {
        YandexAds.RewardedCompleted -= OnRewarded;
        YandexAds.RewardedClosed -= UnhookRewarded;
    }

    // ── строительные блоки ──────────────────────────────────────────────────
    private static Text Label(Transform parent, string text, int size, Color color,
        float x0, float y0, float x1, float y1, TextAnchor align, bool bold)
    {
        Text t = UIFactory.Text(parent, text, size, color);
        if (bold) t.font = UIFactory.FontBold();
        t.alignment = align;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 12;
        t.resizeTextMaxSize = size;
        UIFactory.SetRect(t.rectTransform, x0, y0, x1, y1);
        return t;
    }

    private static RectTransform Surface(Transform parent, string name,
        float x0, float y0, float x1, float y1, Color top, Color bottom)
    {
        RectTransform rt = UIFactory.Panel(parent, name, Color.white);
        UIFactory.SetRect(rt, x0, y0, x1, y1);
        ApplyRound(rt.GetComponent<Image>(), top, bottom);
        return rt;
    }

    private static void ApplyRound(Image img, Color top, Color bottom, bool shadow = true)
    {
        if (img == null) return;
        CloudStyle.Apply(img, top, bottom);
        img.raycastTarget = false;
        if (!shadow) return;
        Shadow sh = img.gameObject.GetComponent<Shadow>();
        if (sh == null) sh = img.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.32f);
        sh.effectDistance = new Vector2(0f, -5f);
        sh.useGraphicAlpha = false;
    }

    private static Button SurfaceButton(Transform parent, string name,
        float x0, float y0, float x1, float y1, Color top, Color bottom, UnityAction onClick)
    {
        RectTransform rt = Surface(parent, name, x0, y0, x1, y1, top, bottom);
        Image img = rt.GetComponent<Image>();
        img.raycastTarget = true;

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = Color.white;
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.82f, 0.86f, 0.94f, 1f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }

    private static Image Icon(Transform parent, string id, Color color,
        float x0, float y0, float x1, float y1)
    {
        GameObject go = new GameObject("Icon " + id);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = MenuIcons.Get(id, color);
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;
        UIFactory.SetRect(rt, x0, y0, x1, y1);
        return img;
    }

    private static Image Glow(Transform parent, Color color, float x0, float y0, float x1, float y1)
    {
        GameObject go = new GameObject("Glow");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = MenuArt.Glow(256, color);
        img.color = Color.white;
        img.raycastTarget = false;
        UIFactory.SetRect(rt, x0, y0, x1, y1);
        return img;
    }

    private Button MenuButton(Transform root, float x0, float y0, float x1, float y1,
        string label, string iconId, Color top, Color bottom, UnityAction onClick, bool enterHint = false)
    {
        Button b = SurfaceButton(root, "Button " + label, x0, y0, x1, y1, top, bottom, onClick);
        RectTransform rt = b.GetComponent<RectTransform>();
        float w = x1 - x0;
        float h = y1 - y0;

        Icon(rt, iconId, new Color(1f, 1f, 1f, 0.96f), 52f, (h - 46f) * 0.5f, 98f, (h - 46f) * 0.5f + 46f);

        float textRight = enterHint ? w - 150f : w - 30f;
        Text t = Label(rt, label, 36, C_Text, 132f, 0f, textRight, h, TextAnchor.MiddleLeft, true);
        t.resizeTextMaxSize = 36;

        if (enterHint)
        {
            RectTransform chip = Surface(rt, "Enter Hint", w - 140f, (h - 46f) * 0.5f,
                w - 28f, (h - 46f) * 0.5f + 46f, new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0.35f));
            Label(chip, "Enter", 18, C_Muted, 0f, 0f, 112f, 46f, TextAnchor.MiddleCenter, false);
        }

        return b;
    }

    private Text StatCard(Transform root, float x0, float y0, float x1, float y1,
        string iconId, Color tileColor, Color iconColor, string caption, string value, UnityAction onClick)
    {
        Button b = SurfaceButton(root, "Stat " + caption, x0, y0, x1, y1, C_CardDark, C_CardDark, onClick);
        RectTransform rt = b.GetComponent<RectTransform>();
        float w = x1 - x0;
        float h = y1 - y0;

        RectTransform tile = Surface(rt, "Tile", 34f, (h - 92f) * 0.5f, 126f, (h + 92f) * 0.5f,
            tileColor, Color.Lerp(tileColor, Color.black, 0.25f));
        Icon(tile, iconId, iconColor, 23f, 23f, 69f, 69f);

        Label(rt, caption, 20, C_Muted, 148f, 22f, w - 24f, 58f, TextAnchor.MiddleLeft, false);
        Text v = Label(rt, value, 32, C_Text, 148f, 56f, w - 24f, 116f, TextAnchor.MiddleLeft, true);
        return v;
    }

    private Text RoundButton(Transform root, float right, float top, float size,
        string iconId, Color tile, Color iconColor, string caption, UnityAction onClick)
    {
        GameObject go = new GameObject("Round " + caption);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(root, false);
        UIFactory.SetRectRight(rt, right, top, size, size);

        Image img = go.AddComponent<Image>();
        img.sprite = CloudStyle.Button(Mathf.RoundToInt(size), Mathf.RoundToInt(size), tile, tile,
            Mathf.RoundToInt(size * 0.5f));
        img.type = Image.Type.Simple;
        img.color = Color.white;
        img.raycastTarget = true;

        Button b = go.AddComponent<Button>();
        b.targetGraphic = img;
        if (onClick != null) b.onClick.AddListener(onClick);

        Icon(rt, iconId, iconColor, size * 0.28f, size * 0.28f, size * 0.72f, size * 0.72f);

        Text cap = Label(rt, caption, 18, C_Muted, -20f, size + 4f, size + 20f, size + 30f,
            TextAnchor.MiddleCenter, false);
        return cap;
    }

    private static string FormatSens(float v)
    {
        int tenths = Mathf.RoundToInt(v * 10f);
        return (tenths / 10).ToString() + "," + Mathf.Abs(tenths % 10).ToString() + "x";
    }
}
