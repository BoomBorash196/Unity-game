using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Простые иконки для нового интерфейса. Рисуются кодом (круг, прямоугольник,
/// многоугольник, звезда) и запекаются в текстуру со сглаживанием — так иконки
/// не зависят от шрифта: в Inter нет ни шестерёнки, ни корзины, а эмодзи в
/// WebGL не подставляются.
/// </summary>
public static class MenuIcons
{
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    /// <summary>Освобождает запечённые иконки после ухода из меню.</summary>
    public static void ClearCache()
    {
        foreach (Sprite sprite in _cache.Values)
        {
            if (sprite == null) continue;
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
        _cache.Clear();
    }

    public static Sprite Get(string id, Color color)
    {
        string key = id + "|" + Mathf.RoundToInt(color.r * 255f) + "," +
                     Mathf.RoundToInt(color.g * 255f) + "," + Mathf.RoundToInt(color.b * 255f) + "," +
                     Mathf.RoundToInt(color.a * 255f);
        Sprite cached;
        if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

        const int N = 96;
        const int SS = 3;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color32[] px = new Color32[N * N];
        int total = SS * SS;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < SS; sy++)
                {
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float u = (x + (sx + 0.5f) / SS) / N;
                        float v = 1f - (y + (sy + 0.5f) / SS) / N;
                        if (Inside(id, u, v)) hit++;
                    }
                }
                Color c = color;
                c.a = color.a * hit / total;
                px[y * N + x] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);

        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        _cache[key] = sprite;
        return sprite;
    }

    private static bool Inside(string id, float u, float v)
    {
        switch (id)
        {
            case "play":
                return Poly(u, v, new[] { 0.30f, 0.14f, 0.86f, 0.50f, 0.30f, 0.86f });

            case "shop":
                return Box(u, v, 0.14f, 0.42f, 0.86f, 0.68f) ||
                       Circle(u, v, 0.32f, 0.84f, 0.08f) ||
                       Circle(u, v, 0.68f, 0.84f, 0.08f) ||
                       Box(u, v, 0.06f, 0.22f, 0.28f, 0.30f);

            case "gift":
                return (Box(u, v, 0.18f, 0.46f, 0.82f, 0.88f) ||
                        Box(u, v, 0.12f, 0.34f, 0.88f, 0.48f) ||
                        Circle(u, v, 0.40f, 0.27f, 0.10f) ||
                        Circle(u, v, 0.60f, 0.27f, 0.10f)) &&
                       !Box(u, v, 0.44f, 0.34f, 0.56f, 0.88f);

            case "trophy":
                return (Poly(u, v, new[] { 0.30f, 0.14f, 0.70f, 0.14f, 0.63f, 0.54f, 0.37f, 0.54f }) ||
                        Box(u, v, 0.44f, 0.54f, 0.56f, 0.74f) ||
                        Box(u, v, 0.28f, 0.74f, 0.72f, 0.84f) ||
                        (Circle(u, v, 0.24f, 0.30f, 0.12f) && !Circle(u, v, 0.24f, 0.30f, 0.07f)) ||
                        (Circle(u, v, 0.76f, 0.30f, 0.12f) && !Circle(u, v, 0.76f, 0.30f, 0.07f)));

            case "gear":
            {
                if (Circle(u, v, 0.5f, 0.5f, 0.30f) && !Circle(u, v, 0.5f, 0.5f, 0.13f)) return true;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    float cx = 0.5f + Mathf.Cos(a) * 0.33f;
                    float cy = 0.5f + Mathf.Sin(a) * 0.33f;
                    if (RotBox(u, v, cx, cy, 0.10f, 0.07f, i * 45f)) return true;
                }
                return false;
            }

            case "star":
                return Star(u, v, 0.5f, 0.5f, 0.42f, 0.18f);

            case "person":
                return Circle(u, v, 0.5f, 0.26f, 0.14f) || Ellipse(u, v, 0.5f, 0.68f, 0.26f, 0.22f);

            case "coin":
                return (Circle(u, v, 0.5f, 0.5f, 0.36f) && !Circle(u, v, 0.5f, 0.5f, 0.27f)) ||
                       Box(u, v, 0.46f, 0.38f, 0.54f, 0.62f);

            case "check":
                return Poly(u, v, new[]
                {
                    0.12f, 0.52f, 0.25f, 0.39f, 0.42f, 0.57f,
                    0.75f, 0.20f, 0.88f, 0.33f, 0.42f, 0.81f
                });

            case "bolt":
                return Poly(u, v, new[]
                {
                    0.58f, 0.08f, 0.26f, 0.56f, 0.46f, 0.56f,
                    0.38f, 0.92f, 0.74f, 0.42f, 0.52f, 0.42f
                });

            case "flag":
                return Box(u, v, 0.28f, 0.10f, 0.37f, 0.90f) ||
                       Poly(u, v, new[] { 0.37f, 0.12f, 0.86f, 0.25f, 0.37f, 0.50f });

            case "pause":
                return Box(u, v, 0.28f, 0.16f, 0.43f, 0.84f) || Box(u, v, 0.57f, 0.16f, 0.72f, 0.84f);

            case "home":
                return Poly(u, v, new[]
                {
                    0.50f, 0.10f, 0.92f, 0.48f, 0.82f, 0.48f, 0.82f, 0.88f,
                    0.18f, 0.88f, 0.18f, 0.48f, 0.08f, 0.48f
                });

            case "arrow":
                return Poly(u, v, new[] { 0.50f, 0.14f, 0.84f, 0.56f, 0.62f, 0.56f, 0.62f, 0.88f, 0.38f, 0.88f, 0.38f, 0.56f, 0.16f, 0.56f });

            default:
                return Circle(u, v, 0.5f, 0.5f, 0.34f);
        }
    }

    private static bool Circle(float u, float v, float cx, float cy, float r)
    {
        float dx = u - cx, dy = v - cy;
        return dx * dx + dy * dy <= r * r;
    }

    private static bool Ellipse(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    private static bool Box(float u, float v, float x0, float y0, float x1, float y1)
    {
        return u >= x0 && u <= x1 && v >= y0 && v <= y1;
    }

    private static bool RotBox(float u, float v, float cx, float cy, float hw, float hh, float deg)
    {
        float r = -deg * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        float dx = u - cx, dy = v - cy;
        float x = dx * cs - dy * sn;
        float y = dx * sn + dy * cs;
        return Mathf.Abs(x) <= hw && Mathf.Abs(y) <= hh;
    }

    private static bool Poly(float u, float v, float[] p)
    {
        bool inside = false;
        int n = p.Length / 2;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = p[i * 2], yi = p[i * 2 + 1];
            float xj = p[j * 2], yj = p[j * 2 + 1];
            if (((yi > v) != (yj > v)) && (u < (xj - xi) * (v - yi) / (yj - yi) + xi))
                inside = !inside;
        }
        return inside;
    }

    private static bool Star(float u, float v, float cx, float cy, float outer, float inner)
    {
        float dx = u - cx, dy = v - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float ang = Mathf.Atan2(dy, dx) + Mathf.PI * 0.5f;
        float step = Mathf.PI / 5f;
        float a = ang % (2f * step);
        if (a < 0f) a += 2f * step;
        float t = Mathf.Abs(a - step) / step;
        float r = Mathf.Lerp(outer, inner, t);
        return dist <= r;
    }
}
