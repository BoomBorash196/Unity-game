using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Весь класс закрыт этим условием: в обычный (не dev) билд ничего не попадёт,
// потому что в нём не будет даже метода автосоздания.
#if UNITY_EDITOR || DEVELOPMENT_BUILD

/// <summary>
/// Клавиши и экранные кнопки для тестов скинов и экономики. Цены в каталоге
/// не трогает — только выдаёт монеты/очки и принудительно надевает скин.
/// Класс собирается лишь в редакторе и в Development Build.
/// </summary>
public class DevCheats : MonoBehaviour
{
    private const int SmallCoins = 100;
    private const int BigCoins = 1000;

    // Логируем все нажатия, чтобы отличить «клавиши не доходят» от «чит сломан».
    private static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
    private static string _lastKey = "(ещё не нажато)";
    private static int _presses;

    private Text _status;
    private Text _animDebug;
    private float _refreshAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        new GameObject("[Dev] Cheats").AddComponent<DevCheats>();
    }

    private void Awake()
    {
        // Обязательно: объект создан до загрузки сцены, а Bootstrap сразу
        // грузит MainMenu. Без DontDestroyOnLoad читы умирали через секунду.
        DontDestroyOnLoad(gameObject);
        Log("Awake() — объект создан и сохранён между сценами");
        BuildOverlay();
    }

    private void Start()
    {
        Log("Start(). F1 +" + SmallCoins + " монет | F2 +" + BigCoins +
            " монет | F3 +100 очков | F5 след. скин | F6 Тёмная Ива | F7 Классик");
    }

    private void Update()
    {
        for (int i = 0; i < AllKeys.Length; i++)
        {
            if (!Input.GetKeyDown(AllKeys[i])) continue;

            _presses++;
            _lastKey = AllKeys[i].ToString();
            Log("нажата " + _lastKey + " (номер " + _presses + ")");
            Handle(_lastKey);
        }
    }

    private void LateUpdate()
    {
        // Статус нужен редно, пересобираем строку 4 раза в секунду.
        if (Time.unscaledTime < _refreshAt || _status == null) return;
        _refreshAt = Time.unscaledTime + 0.25f;

        UpdateAnimDebug();

        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        _status.text = "DEV CHEATS  нажатий: " + _presses +
                       "   клавиша: " + _lastKey +
                       "   монет: " + SkinService.Coins +
                       "   очков: " + SkinService.Points +
                       "   сцена: " + scene;
    }

    /// <summary>
    /// Показывает состояние аниматора игрока прямо на экране. Нужно, чтобы отличить
    /// «анимации нет» от «анимация есть, но её плохо видно»: видно и скорость,
    /// и какие кости или части нашлись, и счётчик кадров анимации.
    /// </summary>
    private void UpdateAnimDebug()
    {
        if (_animDebug == null) return;

        LevelManager manager = LevelManager.Instance;
        PlayerController player = manager != null ? manager.Player : null;
        if (player == null)
        {
            _animDebug.text = "АНИМАЦИЯ: игрока нет (не в игровой сцене)";
            return;
        }

        SkinAnimator anim = player.GetComponentInChildren<SkinAnimator>();
        if (anim == null)
        {
            _animDebug.text = "АНИМАЦИЯ: SkinAnimator НЕ НАЙДЕН на игроке — вот и причина";
            return;
        }

        _animDebug.text = anim.AnimationStatus(player);
    }

    private void OnDestroy()
    {
        Log("OnDestroy() — читы уничтожены");
    }

    private static void Handle(string key)
    {
        switch (key)
        {
            case "F1": SkinService.AddCoins(SmallCoins); Log("+" + SmallCoins + " монет"); break;
            case "F2": SkinService.AddCoins(BigCoins); Log("+" + BigCoins + " монет"); break;
            case "F3": SkinService.AddPoints(100); Log("+100 очков"); break;
            case "F5": CycleSkin(1); break;
            case "F6": Equip("dark_willow"); break;
            case "F7": Equip("classic"); break;
        }
    }

    private static void Equip(string id)
    {
        SkinDefinition skin = SkinCatalog.Get(id);
        if (skin == null)
        {
            Log("нет скина " + id);
            return;
        }

        Equip(skin);
    }

    private static void Equip(SkinDefinition skin)
    {
        // Select() откажется, если скин не открыт, поэтому открываем в обход цены.
        GameProgress.SetSkinOwned(skin.Id, true);
        SkinService.Select(skin.Id);

        // Модель надевается только при спавне игрока, а мы хотим сразу.
        LevelManager manager = LevelManager.Instance;
        bool applied = false;
        if (manager != null && manager.Player != null)
        {
            SkinApplier.Apply(manager.Player.gameObject, SkinService.SelectedSkin);
            applied = true;
        }

        Log("скин: " + skin.Title + " (" + skin.Id + "), модель переодета: " + applied);
    }

    /// <summary>Листает скины по кругу, принудительно открывая каждый.</summary>
    private static void CycleSkin(int step)
    {
        List<SkinDefinition> all = SkinCatalog.All;
        if (all.Count == 0) return;

        string current = GameProgress.GetSelectedSkin();
        int index = 0;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Id == current)
            {
                index = i;
                break;
            }
        }

        SkinDefinition next = all[(index + step + all.Count) % all.Count];
        if (next != null) Equip(next);
    }

    /// <summary>
    /// Свой канвас поверх всех игровых. OnGUI для этого не годится: IMGUI
    /// рисуется под Screen Space Overlay канвасами, и непрозрачная панель
    /// меню его просто закрашивала.
    /// </summary>
    private void BuildOverlay()
    {
        UIFactory.EnsureEventSystem();

        Canvas canvas = UIFactory.CreateCanvas("Dev Canvas");
        canvas.sortingOrder = short.MaxValue;
        canvas.transform.SetParent(transform, false);

        RectTransform root = UIFactory.Panel(canvas.transform, "Dev Root", new Color(0f, 0f, 0f, 0.72f));
        TopLeft(root, 8f, 8f, 1380f, 156f);

        _status = UIFactory.Text(root, "DEV CHEATS: загрузка...", 24, Color.yellow);
        TopLeft(_status.rectTransform, 12f, 6f, 1216f, 34f);

        _animDebug = UIFactory.Text(root, "АНИМАЦИЯ: ...", 22, new Color(0.6f, 0.95f, 0.7f, 1f));
        TopLeft(_animDebug.rectTransform, 12f, 120f, 1216f, 150f);

        string[] labels =
        {
            "+100 монет", "+1000 монет", "+100 очков",
            "След. скин (F5)", "Тёмная Ива (F6)", "Классик (F7)"
        };
        string[] keys = { "F1", "F2", "F3", "F5", "F6", "F7" };
        Color[] colors =
        {
            new Color(0.18f, 0.20f, 0.26f, 1f),
            new Color(0.18f, 0.20f, 0.26f, 1f),
            new Color(0.18f, 0.20f, 0.26f, 1f),
            new Color(0.16f, 0.36f, 0.30f, 1f),
            new Color(0.40f, 0.16f, 0.20f, 1f),
            new Color(0.30f, 0.30f, 0.38f, 1f)
        };

        const float width = 186f;
        const float height = 56f;
        const float y = 48f;

        for (int i = 0; i < labels.Length; i++)
        {
            // Локальная копия: переменную цикла for нельзя захватить в лямбде.
            int slot = i;
            Button button = UIFactory.Button(root, labels[slot], 20, colors[slot], Color.white,
                                              () => Handle(keys[slot]));
            TopLeft(button.image.rectTransform, slot * (width + 8f), y, width, height);
        }
    }

    private static void TopLeft(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void Log(string message)
    {
        Debug.Log("[DEV] " + message);
    }
}

#endif
