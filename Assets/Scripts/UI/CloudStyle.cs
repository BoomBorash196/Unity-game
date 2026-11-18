using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Облачный стиль кнопок: скруглённый прямоугольник с вертикальным градиентом
/// сверху вниз и мягким бликом у верхней кромки. Спрайты генерируются один раз
/// на уникальный набор «размер + цвета» и кэшируются, чтобы не пересобирать
/// текстуру заново на каждый кадр.
///
/// Форма считается по знаковой дистанции (SDF) прямоугольника со скруглением —
/// это даёт ровные углы и аккуратный антиалиас без ручных веток по четвертям.
/// </summary>
public static class CloudStyle
{
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    /// <summary>Сколько уникальных спрайтов успело накопиться. Сцены меню и
    /// игры используют разные размеры, поэтому кэш чистят при переходе между
    /// сценами: старые текстуры уже никто не рисует, а память они держат.</summary>
    public static int CachedCount { get { return _cache.Count; } }

    /// <summary>Уничтожает закэшированные спрайты и их текстуры. Вызывать
    /// только когда старый интерфейс уже не нужен — перед загрузкой сцены.</summary>
    public static void ClearCache()
    {
        foreach (Sprite sprite in _cache.Values)
            DestroyCached(sprite);
        _cache.Clear();
    }

    private static void DestroyCached(Sprite sprite)
    {
        if (sprite == null) return;
        Texture2D texture = sprite.texture;

        if (Application.isPlaying)
        {
            Object.Destroy(sprite);
            if (texture != null) Object.Destroy(texture);
        }
        else
        {
            Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }

    /// <summary>Спрайт для кнопки width x height: градиент top → bottom, углы
    /// скруглены на radius (в пикселях текстуры).</summary>
    public static Sprite Button(int width, int height, Color top, Color bottom, int radius)
    {
        width = Mathf.Clamp(width, 8, 1024);
        height = Mathf.Clamp(height, 8, 512);
        int maxRadius = Mathf.Min(width, height) / 2;
        radius = Mathf.Clamp(radius, 0, maxRadius);

        string key = width + "x" + height + "r" + radius + "_" +
                     ColorKey(top) + "_" + ColorKey(bottom);
        Sprite sprite;
        if (_cache.TryGetValue(key, out sprite) && sprite != null)
            return sprite;

        sprite = Build(width, height, radius, top, bottom);
        _cache[key] = sprite;
        return sprite;
    }

    /// <summary>Применяет облачный стиль к Image: подменяет спрайт на
    /// градиентно-скруглённый нужного размера и возвращает изображение в Simple.</summary>
    public static void Apply(UnityEngine.UI.Image image, Color top, Color bottom)
    {
        if (image == null) return;
        RectTransform rt = image.rectTransform;
        int w = Mathf.RoundToInt(Mathf.Abs(rt != null ? rt.rect.width : 200f));
        int h = Mathf.RoundToInt(Mathf.Abs(rt != null ? rt.rect.height : 60f));
        int radius = Mathf.Clamp(h / 2, 6, 40);
        image.sprite = Button(w, h, top, bottom, radius);
        image.type = UnityEngine.UI.Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;
    }

    private static string ColorKey(Color c)
    {
        return Mathf.RoundToInt(c.r * 255f) + "," +
               Mathf.RoundToInt(c.g * 255f) + "," +
               Mathf.RoundToInt(c.b * 255f);
    }

    private static Sprite Build(int width, int height, int radius, Color top, Color bottom)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        // Полуоси прямоугольника (без скругления) и радиус — для SDF.
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;
        float r = radius;
        const float edge = 1.5f; // ширина мягкой кромки антиалиаса

        // Color32, а не Color: временный массив для текстуры 1024x512 иначе
        // занимает 8 МБ и остаётся в куче до сборки мусора. Формат текстуры всё
        // равно RGBA32, так что на итоговую картинку это не влияет.
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            // 0 внизу, 1 вверху (в Texture2D строка 0 — это низ)
            float v = height > 1 ? (y + 0.5f) / height : 1f;

            // Градиент: сверху top, снизу bottom.
            Color grad = Color.Lerp(bottom, top, v);

            // Воздушный блик у верхней кромки: только в верхних 45% высоты.
            float highlight = Mathf.Clamp01((v - 0.55f) / 0.45f);
            highlight = highlight * highlight * (3f - 2f * highlight); // smoothstep
            Color tint = Color.Lerp(grad, Color.Lerp(grad, Color.white, 0.22f), highlight);

            for (int x = 0; x < width; x++)
            {
                // SDF: центр текстуры — начало координат.
                float px = (x + 0.5f) - halfW;
                float py = (y + 0.5f) - halfH;

                float dist = SignedRoundBox(px, py, halfW, halfH, r);

                // Внутри (dist < 0) — непрозрачно; кромка сглаживается по edge.
                float alpha = Mathf.Clamp01(0.5f - dist / edge);

                Color c = tint;
                c.a = alpha;
                pixels[y * width + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    /// <summary>Знаковая дистанция до прямоугольника (hx, hy) со скруглением r.
    /// Отрицательно внутри, ноль на границе, положительно снаружи.</summary>
    private static float SignedRoundBox(float px, float py, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(px) - hx + r;
        float qy = Mathf.Abs(py) - hy + r;
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                   Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - r;
    }
}
