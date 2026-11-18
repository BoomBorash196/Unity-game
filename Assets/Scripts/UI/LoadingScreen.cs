using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран загрузки по макету Design/loading-mockup 1.svg: тот же ночной фон,
/// что и у меню, крупный знак подъёма, название игры и полоса прогресса.
///
/// Живёт на отдельном холсте с DontDestroyOnLoad: Bootstrap показывает его до
/// загрузки главного меню, а после — плавно растворяет уже поверх готового
/// меню. Пока экран виден, он перекрывает клики, поэтому случайное нажатие во
/// время загрузки ничего не запускает.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    private CanvasGroup _group;
    private Image _fill;
    private Text _status;
    private Image[] _pips;

    private float _targetProgress;
    private float _shownProgress;
    private float _hideAt = -1f;
    private bool _fadingOut;

    private const float FadeInTime = 0.25f;
    private const float FadeOutTime = 0.35f;

    public static LoadingScreen Show(string status)
    {
        if (Instance == null) Build();
        Instance.ResetState(status);
        return Instance;
    }

    public static void SetStatus(string status)
    {
        if (Instance != null) Instance.ApplyStatus(status);
    }

    public static void SetProgress(float progress)
    {
        if (Instance != null)
            Instance._targetProgress = Mathf.Clamp01(progress);
    }

    /// <summary>Начать растворение через delay секунд. Задержка нужна, чтобы
    /// новое меню успело отрисоваться под ещё непрозрачным экраном.</summary>
    public static void Hide(float delay)
    {
        if (Instance == null) return;
        Instance._hideAt = Time.realtimeSinceStartup + Mathf.Max(0f, delay);
    }

    private static void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas("Loading Canvas");
        canvas.sortingOrder = 5000;
        DontDestroyOnLoad(canvas.gameObject);

        LoadingScreen screen = canvas.gameObject.AddComponent<LoadingScreen>();
        screen.Construct(canvas);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Construct(Canvas canvas)
    {
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = true;

        // Фон тянется на весь холст, включая поля по бокам на широком окне.
        GameObject bgGo = new GameObject("Backdrop");
        RectTransform bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.SetParent(canvas.transform, false);
        UIFactory.Stretch(bgRt);
        Image bg = bgGo.AddComponent<Image>();
        bg.sprite = MenuArt.Background();
        bg.color = Color.white;
        bg.raycastTarget = true;

        GameObject dimGo = new GameObject("Dim");
        RectTransform dimRt = dimGo.AddComponent<RectTransform>();
        dimRt.SetParent(canvas.transform, false);
        UIFactory.Stretch(dimRt);
        Image dim = dimGo.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.04f, 0.08f, 0.35f);
        dim.raycastTarget = false;

        RectTransform root = UIFactory.DesignSpace(canvas.transform, "Loading Root");

        UiKit.Glow(root, new Color(0.24f, 0.55f, 1f, 0.55f), 710f, 150f, 1210f, 650f);

        RectTransform logo = UiKit.Surface(root, "Load Logo", 890f, 250f, 1030f, 390f,
            UiKit.C_AccentTop, UiKit.C_AccentBottom, 34);
        logo.GetComponent<Image>().raycastTarget = false;
        UiKit.Icon(logo, "arrow", UiKit.C_Text, 46f, 46f, 94f, 94f);

        UiKit.Label(root, "Паркур в рай", 72, UiKit.C_Text, 460f, 410f, 1460f, 500f,
            TextAnchor.MiddleCenter, true);
        UiKit.Label(root, "бесконечный подъём", 26, UiKit.C_Muted, 460f, 500f, 1460f, 540f,
            TextAnchor.MiddleCenter, false);

        _status = UiKit.Label(root, "", 22, UiKit.C_Muted, 460f, 600f, 1460f, 636f,
            TextAnchor.MiddleCenter, false);

        RectTransform bar = UiKit.Surface(root, "Load Bar", 660f, 650f, 1260f, 676f,
            UiKit.C_Scroll, UiKit.C_Scroll, 13, false);
        bar.GetComponent<Image>().raycastTarget = false;

        RectTransform fill = UiKit.Surface(bar, "Load Fill", 0f, 0f, 600f, 26f,
            UiKit.C_AccentTop, UiKit.C_AccentBottom, 13, false);
        _fill = fill.GetComponent<Image>();
        _fill.raycastTarget = false;
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fill.fillAmount = 0f;

        // Четыре пульсирующие точки — признак того, что игра жива, даже когда
        // полоса стоит на месте в ожидании ответа площадки.
        _pips = new Image[4];
        for (int i = 0; i < _pips.Length; i++)
        {
            float x = 906f + i * 32f;
            RectTransform pip = UiKit.Surface(root, "Load Pip " + i, x, 700f, x + 12f, 712f,
                UiKit.C_Soft, UiKit.C_Soft, 6, false);
            pip.GetComponent<Image>().raycastTarget = false;
            _pips[i] = pip.GetComponent<Image>();
        }
    }

    private void ResetState(string status)
    {
        gameObject.SetActive(true);
        _group.alpha = 0f;
        _fadingOut = false;
        _hideAt = -1f;
        _targetProgress = 0f;
        _shownProgress = 0f;
        if (_fill != null) _fill.fillAmount = 0f;
        ApplyStatus(status);
    }

    private void ApplyStatus(string status)
    {
        if (_status == null) return;
        string value = status ?? string.Empty;
        if (_status.text != value) _status.text = value;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (_fill != null)
        {
            _shownProgress = Mathf.MoveTowards(_shownProgress, _targetProgress, dt * 2.5f);
            _fill.fillAmount = _shownProgress;
        }

        if (_pips != null)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _pips.Length; i++)
            {
                float wave = Mathf.Sin(now * 3.2f - i * 0.9f) * 0.5f + 0.5f;
                float alpha = 0.2f + 0.8f * wave * wave;
                _pips[i].color = new Color(1f, 1f, 1f, alpha);
            }
        }

        if (!_fadingOut && _hideAt >= 0f && Time.realtimeSinceStartup >= _hideAt)
            _fadingOut = true;

        if (_fadingOut)
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, dt / FadeOutTime);
            if (_group.alpha <= 0.001f)
                Destroy(gameObject);
        }
        else if (_group.alpha < 1f)
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, dt / FadeInTime);
        }
    }
}
