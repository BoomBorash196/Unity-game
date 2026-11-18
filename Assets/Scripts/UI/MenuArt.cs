using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Фон меню по макету: вертикальный градиент из тёмно-синего в почти чёрный,
/// луна, полоса облаков у правого края и два слоя гор внизу. Всё считается в
/// координатах макета 844x390 и запекается в одну текстуру — так на экране
/// получается ровно та картинка, что нарисована в Figma, без спрайтов-исходников.
/// </summary>
public static class MenuArt
{
    private static Sprite _background;
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    /// <summary>Освобождает фон и свечения после ухода из меню. Спрайты
    /// создаются с HideAndDontSave, поэтому сборщик мусора их не заберёт сам.</summary>
    public static void ClearCache()
    {
        DestroyCached(_background);
        _background = null;

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

    /// <summary>Фон на весь экран (макет 844x390).</summary>
    public static Sprite Background()
    {
        if (_background != null) return _background;

        const int W = 960;
        const int H = 540;

        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color top = Hex("1B3A6E");
        Color bottom = Hex("0A1428");
        Color moon = Hex("EAF2FF");
        Color far = Hex("0F2447");
        Color near = Hex("0B1B36");

        float[] farPoly =
        {
            0f, 240f, 120f, 170f, 260f, 240f, 400f, 160f,
            560f, 245f, 700f, 185f, 844f, 240f, 844f, 390f, 0f, 390f
        };
        float[] nearPoly =
        {
            0f, 308f, 150f, 276f, 320f, 308f, 480f, 268f,
            640f, 308f, 800f, 278f, 844f, 308f, 844f, 390f, 0f, 390f
        };

        Color32[] px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float dy = 390f - ((y + 0.5f) / H) * 390f;
            for (int x = 0; x < W; x++)
            {
                float dx = ((x + 0.5f) / W) * 844f;

                Color c = Color.Lerp(top, bottom, Mathf.Clamp01(dy / 390f));

                float mx = dx - 690f;
                float my = dy - 75f;
                if (mx * mx + my * my < 58f * 58f) c = moon;

                // Облака у правого края: три круга, срезанных границей кадра.
                for (int i = 0; i < 3; i++)
                {
                    float qx = dx - 812f;
                    float qy = dy - (94f + i * 74f);
                    if (qx * qx + qy * qy < 48f * 48f)
                        c = Color.Lerp(c, moon, 0.92f);
                }

                if (InPoly(dx, dy, farPoly)) c = far;
                if (InPoly(dx, dy, nearPoly)) c = near;

                px[y * W + x] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);

        _background = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect);
        _background.hideFlags = HideFlags.HideAndDontSave;
        return _background;
    }

    /// <summary>Мягкое круглое свечение (radial falloff).</summary>
    public static Sprite Glow(int size, Color color)
    {
        size = Mathf.Clamp(size, 8, 512);
        string key = "glow" + size + "|" + Key(color);
        Sprite cached;
        if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        float half = size * 0.5f;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) - half;
                float dy = (y + 0.5f) - half;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / half);
                float a = 1f - d;
                a = a * a * (3f - 2f * a); // smoothstep
                Color c = color;
                c.a = color.a * a;
                px[y * size + x] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);

        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        _cache[key] = sprite;
        return sprite;
    }

    public static Color Hex(string hex)
    {
        Color c;
        if (ColorUtility.TryParseHtmlString("#" + hex, out c)) return c;
        return Color.magenta;
    }

    private static string Key(Color c)
    {
        return Mathf.RoundToInt(c.r * 255f) + "," + Mathf.RoundToInt(c.g * 255f) + "," +
               Mathf.RoundToInt(c.b * 255f) + "," + Mathf.RoundToInt(c.a * 255f);
    }

    private static bool InPoly(float x, float y, float[] p)
    {
        bool inside = false;
        int n = p.Length / 2;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = p[i * 2], yi = p[i * 2 + 1];
            float xj = p[j * 2], yj = p[j * 2 + 1];
            if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi))
                inside = !inside;
        }
        return inside;
    }
}
