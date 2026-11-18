using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Общие строительные блоки для всех экранов меню.
///
/// Раньше каждый экран (магазин, пасс, задания, награды, рейтинг) рисовал
/// плоские прямоугольники своим набором зелёно-серых цветов, и на фоне нового
/// главного меню они выглядели как из другой игры. Здесь собрана одна палитра
/// и одни правила: тёмно-синие скруглённые карточки с градиентом, синий акцент
/// для действий, золото для наград, иконки MenuIcons и мягкая тень.
/// </summary>
public static class UiKit
{
    // ── палитра нового интерфейса ───────────────────────────────────────────
    public static readonly Color C_PanelTop = MenuArt.Hex("152A4E");
    public static readonly Color C_PanelBottom = MenuArt.Hex("0C1830");
    public static readonly Color C_Card = MenuArt.Hex("12203C");
    public static readonly Color C_CardDark = MenuArt.Hex("0E1C36");
    public static readonly Color C_Tile = MenuArt.Hex("16294F");
    public static readonly Color C_TileDark = MenuArt.Hex("101F3C");
    public static readonly Color C_Scroll = MenuArt.Hex("0A1730");
    public static readonly Color C_AccentTop = MenuArt.Hex("3D8BFF");
    public static readonly Color C_AccentBottom = MenuArt.Hex("2563EB");
    public static readonly Color C_Text = MenuArt.Hex("EAF2FF");
    public static readonly Color C_Muted = MenuArt.Hex("8FA6CC");
    public static readonly Color C_Dim = MenuArt.Hex("5B6E8E");
    public static readonly Color C_Soft = MenuArt.Hex("7FB3FF");
    public static readonly Color C_Gold = MenuArt.Hex("FFD86B");
    public static readonly Color C_GoldTop = MenuArt.Hex("FFE59A");
    public static readonly Color C_GoldBottom = MenuArt.Hex("F2C24E");
    public static readonly Color C_GoldDark = MenuArt.Hex("2A2308");
    public static readonly Color C_Green = MenuArt.Hex("3FBF7F");
    public static readonly Color C_GreenTop = MenuArt.Hex("1D4E39");
    public static readonly Color C_GreenBottom = MenuArt.Hex("123526");
    public static readonly Color C_BlueTop = MenuArt.Hex("17305A");
    public static readonly Color C_BlueBottom = MenuArt.Hex("11233F");
    public static readonly Color C_RowTop = MenuArt.Hex("172C52");
    public static readonly Color C_RowBottom = MenuArt.Hex("12203C");

    // ── текст ───────────────────────────────────────────────────────────────
    public static Text Label(Transform parent, string text, int size, Color color,
        float x0, float y0, float x1, float y1, TextAnchor align, bool bold)
    {
        Text t = UIFactory.Text(parent, text, size, color);
        if (bold) t.font = UIFactory.FontBold();
        t.alignment = align;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = size;
        UIFactory.SetRect(t.rectTransform, x0, y0, x1, y1);
        return t;
    }

    /// <summary>Текст с переносом строк — для описаний и подсказок.</summary>
    public static Text Wrap(Text t)
    {
        if (t != null) t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }

    // ── поверхности ─────────────────────────────────────────────────────────
    /// <summary>Скруглённая карточка с градиентом и мягкой тенью.</summary>
    public static RectTransform Surface(Transform parent, string name,
        float x0, float y0, float x1, float y1, Color top, Color bottom, int radius = 20, bool shadow = true)
    {
        RectTransform rt = UIFactory.Panel(parent, name, Color.white);
        UIFactory.SetRect(rt, x0, y0, x1, y1);
        ApplyRound(rt.GetComponent<Image>(), top, bottom, radius, shadow);
        return rt;
    }

    public static void ApplyRound(Image img, Color top, Color bottom, int radius = -1, bool shadow = true)
    {
        if (img == null) return;
        RectTransform rt = img.rectTransform;
        int w = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rt.rect.width)), 8, 1024);
        int h = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rt.rect.height)), 8, 512);
        if (radius < 0) radius = h / 2;
        img.sprite = CloudStyle.Button(w, h, top, bottom, radius);
        img.type = Image.Type.Simple;
        img.color = Color.white;
        if (!shadow) return;
        Shadow sh = img.gameObject.GetComponent<Shadow>();
        if (sh == null) sh = img.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.32f);
        sh.effectDistance = new Vector2(0f, -5f);
        sh.useGraphicAlpha = false;
    }

    public static Button SurfaceButton(Transform parent, string name,
        float x0, float y0, float x1, float y1, Color top, Color bottom,
        UnityAction onClick, int radius = -1)
    {
        RectTransform rt = Surface(parent, name, x0, y0, x1, y1, top, bottom, radius);
        Image img = rt.GetComponent<Image>();
        img.raycastTarget = true;

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = Color.white;
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.82f, 0.86f, 0.94f, 1f);
        cb.disabledColor = new Color(0.62f, 0.66f, 0.74f, 1f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }

    /// <summary>Кнопка-пилюля с подписью: используется для «Забрать», «Назад».</summary>
    public static Button Pill(Transform parent, string label, int fontSize,
        float x0, float y0, float x1, float y1, Color top, Color bottom,
        Color textColor, UnityAction onClick)
    {
        Button b = SurfaceButton(parent, "Btn " + label, x0, y0, x1, y1, top, bottom, onClick);
        Label(b.transform, label, fontSize, textColor, 0f, 0f, x1 - x0, y1 - y0,
            TextAnchor.MiddleCenter, true);
        return b;
    }

    public static Image Icon(Transform parent, string id, Color color,
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

    public static Image Glow(Transform parent, Color color, float x0, float y0, float x1, float y1)
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

    // ── каркас экрана ───────────────────────────────────────────────────────
    /// <summary>Затемнение на весь экран; возвращает корень окна.</summary>
    public static RectTransform Overlay(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        UIFactory.Stretch(rt);

        Image dim = go.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.04f, 0.08f, 0.80f);
        dim.raycastTarget = true;
        return rt;
    }

    /// <summary>Центральная карточка окна с градиентом и скруглением.</summary>
    public static RectTransform Modal(RectTransform root, string name, float width, float height)
    {
        RectTransform panel = UIFactory.Panel(root, name, Color.white);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(width, height);
        panel.anchoredPosition = Vector2.zero;
        ApplyRound(panel.GetComponent<Image>(), C_PanelTop, C_PanelBottom, 26);
        return panel;
    }

    /// <summary>Шапка экрана: иконка в плитке, заголовок и подпись.</summary>
    public static Text Header(Transform panel, string title, string subtitle,
        string iconId, Color iconColor, Color tileTop, Color tileBottom, float right = 1000f)
    {
        RectTransform tile = Surface(panel, "Header Icon", 40f, 30f, 122f, 112f,
            tileTop, tileBottom, 22);
        Icon(tile, iconId, iconColor, 17f, 17f, 65f, 65f);

        Label(panel, title, 42, C_Text, 146f, 24f, right, 84f, TextAnchor.MiddleLeft, true);
        return Label(panel, subtitle, 21, C_Muted, 148f, 82f, right, 118f,
            TextAnchor.MiddleLeft, false);
    }

    public static Button Back(Transform panel, string label,
        float x0, float y0, float x1, float y1, UnityAction onClick)
    {
        return Pill(panel, label, 25, x0, y0, x1, y1,
            MenuArt.Hex("26365A"), MenuArt.Hex("1B2846"), C_Text, onClick);
    }

    /// <summary>Прокручиваемая область в стиле карточки: скруглённый тёмный фон.</summary>
    public static ScrollRect Scroll(Transform parent, float x0, float y0, float x1, float y1)
    {
        ScrollRect sr = UIFactory.CreateScrollView(parent, new Color(0f, 0f, 0f, 0f));
        RectTransform rt = sr.GetComponent<RectTransform>();
        UIFactory.SetRect(rt, x0, y0, x1, y1);
        ApplyRound(sr.GetComponent<Image>(), C_Scroll, C_Scroll, 20, false);
        sr.GetComponent<Image>().raycastTarget = true;

        // Подложка вьюпорта была квадратной и срезала скругления фона.
        Image vp = sr.viewport.GetComponent<Image>();
        if (vp != null) vp.color = new Color(0f, 0f, 0f, 0f);
        return sr;
    }

    /// <summary>Закрашивает фон строки, созданной через UIFactory.Row.</summary>
    public static Image RowBackground(RectTransform row, Color top, Color bottom)
    {
        Image img = row.gameObject.GetComponent<Image>();
        if (img == null) img = row.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        ApplyRound(img, top, bottom, 18, false);
        return img;
    }

    // ── мелочи ──────────────────────────────────────────────────────────────
    /// <summary>Золотой чип с балансом монет в правом верхнем углу окна.</summary>
    public static Text CoinChip(Transform panel, float right, float top, float width, float height, int coins)
    {
        RectTransform chip = UIFactory.Panel(panel, "Coin Chip", Color.white);
        UIFactory.SetRectRight(chip, right, top, width, height);
        ApplyRound(chip.GetComponent<Image>(), C_GoldDark, MenuArt.Hex("1B1706"), 18, false);
        Icon(chip, "coin", C_Gold, 12f, (height - 34f) * 0.5f, 46f, (height + 34f) * 0.5f);
        return Label(chip, Money(coins), 24, C_Gold, 52f, 0f, width - 10f, height,
            TextAnchor.MiddleLeft, true);
    }

    public static string Money(int value)
    {
        return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
    }
}
