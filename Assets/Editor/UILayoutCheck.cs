#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Раскладка интерфейса: макет обязан помещаться в экран при любом
/// соотношении сторон и не зависеть от того, успел ли холст получить размер.
///
/// Обе поломки тут не выдуманные. Кнопки меню уезжали с экрана целиком,
/// потому что позиция считалась от высоты родителя, а у холста в Start() она
/// ещё пустая. И та же высота в единицах холста меньше 1080 на широком окне:
/// даже с верными координатами нижняя кнопка пропадала. Ни то, ни другое не
/// пишет ошибок в лог — только пустой или обрезанный экран, поэтому проверять
/// это надо кодом, а не глазами.
/// </summary>
public static class UILayoutCheck
{
    private static int _passed;
    private static readonly List<GameObject> _temp = new List<GameObject>();

    public static int Check()
    {
        _passed = 0;
        int failed = 0;

        try
        {
            failed += SetRectIgnoresParentSize();
            failed += SetRectRightPinsToEdge();
            failed += DesignSpaceFits();
        }
        finally
        {
            for (int i = 0; i < _temp.Count; i++)
                if (_temp[i] != null) Object.DestroyImmediate(_temp[i]);
            _temp.Clear();
        }

        Debug.Log("[T] RESULT UILayoutCheck passed=" + _passed + " failed=" + failed);
        return failed;
    }

    /// <summary>SetRect меряет от левого верхнего угла родителя и на его
    /// высоту не смотрит вовсе: одна и та же раскладка обязана получиться что
    /// на низком экране, что на высоком.</summary>
    private static int SetRectIgnoresParentSize()
    {
        RectTransform low = NewScreen("Screen 800x300", 800f, 300f);
        RectTransform high = NewScreen("Screen 800x1440", 800f, 1440f);

        RectTransform a = UIFactory.Panel(low, "A", Color.clear);
        RectTransform b = UIFactory.Panel(high, "B", Color.clear);
        UIFactory.SetRect(a, 60f, 400f, 620f, 468f);
        UIFactory.SetRect(b, 60f, 400f, 620f, 468f);

        int failed = 0;
        failed += Check("layout.позиция не зависит от высоты родителя",
            a.anchoredPosition == b.anchoredPosition,
            "низкий=" + a.anchoredPosition + " высокий=" + b.anchoredPosition);

        failed += Check("layout.верхний угол = (xMin, -yMin)",
            Mathf.Approximately(a.anchoredPosition.x, 60f) && Mathf.Approximately(a.anchoredPosition.y, -400f),
            "получено " + a.anchoredPosition);

        failed += Check("layout.якорь — верхний левый угол",
            a.anchorMin == new Vector2(0f, 1f) && a.anchorMax == new Vector2(0f, 1f) &&
            a.pivot == new Vector2(0f, 1f),
            "anchor=" + a.anchorMin + " pivot=" + a.pivot);

        failed += Check("layout.размер берётся из границ",
            Mathf.Approximately(a.rect.width, 560f) && Mathf.Approximately(a.rect.height, 68f),
            "получено " + a.rect.size);

        return failed;
    }

    /// <summary>SetRectRight держит элемент у правой кромки: на узком экране он
    /// остаётся видимым, а не уезжает за границу вместе с координатой из 1920.
    /// Кнопка паузы в HUD ровно так и пропадала.</summary>
    private static int SetRectRightPinsToEdge()
    {
        // Окно уже 16:9: холст в единицах 1746x1187, а не 1920x1080.
        const float screenWidth = 1746f;
        RectTransform screen = NewScreen("Screen 1746x1187", screenWidth, 1187f);
        RectTransform button = UIFactory.Panel(screen, "Pause", Color.clear);
        UIFactory.SetRectRight(button, 30f, 20f, 110f, 50f);

        float offset = -button.anchoredPosition.x;
        int failed = Check("layout.правый элемент не выходит за кромку",
            offset >= 0f && offset + button.rect.width <= screenWidth + 0.5f,
            "отступ справа " + offset + ", ширина " + button.rect.width + ", экран " + screenWidth);

        failed += Check("layout.правый верхний угол = (-right, -top)",
            Mathf.Approximately(button.anchorMin.x, 1f) && Mathf.Approximately(button.anchorMin.y, 1f) &&
            Mathf.Approximately(button.pivot.x, 1f) && Mathf.Approximately(button.pivot.y, 1f) &&
            Mathf.Approximately(button.anchoredPosition.x, -30f) &&
            Mathf.Approximately(button.anchoredPosition.y, -20f),
            "anchor=" + button.anchorMin + " pivot=" + button.pivot + " pos=" + button.anchoredPosition);

        return failed;
    }

    /// <summary>Сцена макета влезает в экран при любом соотношении сторон, а на
    /// 16:9 остаётся ровно в исходном масштабе — вид не меняется там, где он и
    /// так был верным.</summary>
    private static int DesignSpaceFits()
    {
        // От портрета до очень широкого окна: площадка допускает не больше
        // 2:1, но окно браузера и окно редактора могут быть любыми.
        float[] aspects = { 16f / 9f, 21f / 9f, 2f, 3f / 2f, 4f / 3f, 1f, 3f / 4f, 9f / 16f, 1f / 2f };

        int failed = 0;
        for (int i = 0; i < aspects.Length; i++)
        {
            float aspect = aspects[i];
            Vector2 screen = CanvasUnits(aspect);
            RectTransform parent = NewScreen("Screen " + aspect, screen.x, screen.y);
            RectTransform space = UIFactory.DesignSpace(parent, "Design");

            float scale = space.localScale.x;
            float width = space.rect.width * scale;
            float height = space.rect.height * scale;

            failed += Check("layout.сцена влезает в экран " + aspect.ToString("0.##") + ":1",
                scale > 0f && width <= screen.x + 0.5f && height <= screen.y + 0.5f,
                "сцена " + width.ToString("0") + "x" + height.ToString("0") +
                " в экране " + screen.x.ToString("0") + "x" + screen.y.ToString("0"));

            if (Mathf.Approximately(aspect, 16f / 9f))
                failed += Check("layout.на 16:9 масштаб 1:1", Mathf.Approximately(scale, 1f),
                    "масштаб " + scale);
        }
        return failed;
    }

    /// <summary>Размер холста в единицах при CanvasScaler с match=0.5: площадь
    /// единиц всегда равна эталонной 1920x1080, а соотношение сторон повторяет
    /// экран. Именно этот прямоугольник и видит сцена макета в рантайме.</summary>
    private static Vector2 CanvasUnits(float aspect)
    {
        float area = UIFactory.DesignSize.x * UIFactory.DesignSize.y;
        float height = Mathf.Sqrt(area / aspect);
        return new Vector2(height * aspect, height);
    }

    /// <summary>Экран заданного размера: обычный прямоугольник, у которого rect
    /// сразу равен заданным числам — без холста, отрисовки и кадров.</summary>
    private static RectTransform NewScreen(string name, float width, float height)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.sizeDelta = new Vector2(width, height);
        _temp.Add(go);
        return rt;
    }

    private static int Check(string name, bool ok, string detail)
    {
        if (ok)
        {
            _passed++;
            Debug.Log("[T] PASS " + name);
            return 0;
        }
        Debug.Log("[T] FAIL " + name + " :: " + detail);
        return 1;
    }
}
#endif
