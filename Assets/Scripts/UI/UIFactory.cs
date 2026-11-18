using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIFactory
{
    // Встроенный в Unity 6 шрифт LegacyRuntime.ttf — урезанное латинское
    // подмножество (~49 КБ, без кириллицы). В редакторе буквы видны, потому что
    // Unity подставляет системный шрифт, а в WebGL подстановки нет: кириллица
    // просто не рисуется, остаются цифры и знаки. Поэтому свой шрифт из Resources.
    private const string RegularResource = "Fonts/Inter-Regular";
    private const string BoldResource = "Fonts/Inter-SemiBold";
    private static Font _font;
    private static Font _bold;

    public static Font Font()
    {
        if (_font != null) return _font;
        _font = Resources.Load<Font>(RegularResource);
        if (_font == null)
            _font = TryLoadFont("LegacyRuntime.ttf");
        if (_font == null)
            _font = TryLoadFont("Arial.ttf");
        WarnIfNoCyrillic(_font);
        return _font;
    }

    public static Font FontBold()
    {
        if (_bold != null) return _bold;
        _bold = Resources.Load<Font>(BoldResource);
        if (_bold == null)
            _bold = Font();
        return _bold;
    }

    // Страховка на будущее: молча невидимый текст — худший из багов,
    // он не виден ни в редакторе, ни в логах сборки.
    private static void WarnIfNoCyrillic(Font f)
    {
        if (f == null)
        {
            Debug.LogError("[UI] шрифт не найден — весь текст в сборке будет пустым");
            return;
        }
        if (!f.HasCharacter('А') || !f.HasCharacter('я') || !f.HasCharacter('Ё'))
            Debug.LogError("[UI] в шрифте '" + f.name + "' нет кириллицы — буквы пропадут в WebGL");
    }

    private static Font TryLoadFont(string resource)
    {
        try
        {
            Font f = Resources.GetBuiltinResource<Font>(resource);
            return f;
        }
        catch
        {
            return null;
        }
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    public static Canvas CreateCanvas(string name)
    {
        GameObject go = new GameObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Panel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    public static Text Text(Transform parent, string label, int size, Color color)
    {
        GameObject go = new GameObject("Text: " + label);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = Font();
        text.text = label;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public static Button Button(Transform parent, string label, int fontSize, Color background, Color textColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("Button: " + label);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = background;
        img.raycastTarget = true;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;

        Text text = Text(rt, label, fontSize, textColor);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        if (onClick != null)
            button.onClick.AddListener(onClick);
        return button;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Прямоугольник от левого верхнего угла родителя: xMin/yMin — левый
    /// верхний угол элемента, xMax/yMax — правый нижний.
    ///
    /// Привязка именно к верхнему краю, а не пересчёт из высоты родителя.
    /// Раньше здесь читалась parent.rect.height, чтобы перевести низ в верх, —
    /// и меню уезжало с экрана целиком. Панели строятся в Start(), сразу после
    /// создания холста, а холст получает настоящий размер только к отрисовке
    /// кадра: до этого rect пустой. Все позиции считались от нуля, и каждая
    /// кнопка оказывалась ниже нижнего края. Ошибок в логе нет — только пустой
    /// экран, поэтому ловится это глазами.
    ///
    /// Верхняя привязка от размера родителя не зависит вовсе.
    /// </summary>
    public static void SetRect(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(xMax - xMin, yMax - yMin);
        rt.anchoredPosition = new Vector2(xMin, -yMin);
    }

    /// <summary>
    /// Прямоугольник, привязанный к правому верхнему углу родителя: right и
    /// top — отступы от его правого и верхнего края.
    ///
    /// Нужен там, где элемент стоит у правой кромки экрана: холст в единицах
    /// не всегда шириной 1920 (при окне уже 16:9 он уже), и координата
    /// «прижато к 1890 из 1920» уводит кнопку за границу экрана.
    /// </summary>
    public static void SetRectRight(RectTransform rt, float right, float top, float width, float height)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(-right, -top);
    }

    /// <summary>Размер сцены макета, в которой расставлен весь интерфейс.</summary>
    public static readonly Vector2 DesignSize = new Vector2(1920f, 1080f);

    /// <summary>
    /// Контейнер макета: сцена фиксированного размера (1920x1080), которая
    /// вписывается в родителя целиком. Интерфейс расставляется в её
    /// координатах, поэтому выглядит одинаково при любом соотношении сторон и
    /// не уезжает за край на широком окне.
    ///
    /// Без неё одной верхней привязки мало: холст растягивается под окно, и на
    /// широком экране его высота в единицах меньше 1080 — нижние кнопки
    /// пропадали, хотя координаты верные.
    /// </summary>
    public static RectTransform DesignSpace(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = DesignSize;
        rt.anchoredPosition = Vector2.zero;

        DesignSpaceFitter fitter = go.AddComponent<DesignSpaceFitter>();
        fitter.Setup(rt, DesignSize);
        return rt;
    }

    public static ScrollRect CreateScrollView(Transform parent, Color bg)
    {
        GameObject go = new GameObject("Scroll View");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = bg;
        image.raycastTarget = true;
        ScrollRect sr = go.AddComponent<ScrollRect>();

        GameObject vgo = new GameObject("Viewport");
        RectTransform vp = vgo.AddComponent<RectTransform>();
        vp.SetParent(rt, false);
        vp.anchorMin = Vector2.zero;
        vp.anchorMax = Vector2.one;
        vp.offsetMin = Vector2.zero;
        vp.offsetMax = Vector2.zero;
        Image vm = vgo.AddComponent<Image>();
        vm.color = new Color(0.1f, 0.14f, 0.2f, 0.85f);
        vm.raycastTarget = true;
        vgo.AddComponent<RectMask2D>();

        GameObject cgo = new GameObject("Content");
        RectTransform content = cgo.AddComponent<RectTransform>();
        content.SetParent(vp, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        sr.viewport = vp;
        sr.content = content;
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 25f;
        return sr;
    }

    public static RectTransform Row(Transform content, float height, float gap, int index, float padX)
    {
        GameObject go = new GameObject("Row " + index);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(content, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        float top = index * (height + gap);
        rt.offsetMin = new Vector2(padX, -(top + height));
        rt.offsetMax = new Vector2(-padX, -top);
        return rt;
    }

    public static UnityEngine.UI.Slider Slider(Transform parent, float value, float min, float max, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject go = new GameObject("Slider");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image track = go.AddComponent<Image>();
        track.color = new Color(0.22f, 0.24f, 0.3f, 1f);
        track.raycastTarget = true;

        UnityEngine.UI.Slider slider = go.AddComponent<UnityEngine.UI.Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;

        RectTransform fillArea = NewChild(rt, "Fill Area");
        Image fill = NewChildImage(fillArea, "Fill", new Color(0.2f, 0.7f, 0.95f, 1f));
        fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        RectTransform handleArea = NewChild(rt, "Handle Slide Area");
        Image handle = NewChildImage(handleArea, "Handle", new Color(0.85f, 0.9f, 1f, 1f));
        RectTransform hr = handle.rectTransform;
        hr.anchorMin = new Vector2(0f, 0.5f);
        hr.anchorMax = new Vector2(0f, 0.5f);
        hr.pivot = new Vector2(0.5f, 0.5f);
        hr.sizeDelta = new Vector2(26f, 26f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = hr;
        slider.targetGraphic = handle;

        slider.value = value;
        if (onChanged != null)
            slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    private static RectTransform NewChild(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static Image NewChildImage(RectTransform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }
}