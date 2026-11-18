using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Текстуры для платформ и деталей.
///
/// Два источника. По умолчанию всё рисуется кодом при первом обращении — ни
/// одного файла-картинки в проекте нет. Если пользователь положил внешнюю
/// картинку в Assets/Resources/Textures под именем из ExternalFiles, картинка
/// становится рельефом вида: код читает её яркость, сшивает края в бесшовную
/// плитку, растягивает контраст и из этой высоты делает и цвет (по палитре
/// вида), и карту нормалей. Поэтому рельеф совпадает с рисунком, а не расходится
/// с ним, и биомы остаются различимы по цвету, даже если картинки серые.
///
/// Картинки пришли серыми, поэтому цвет берётся не из них, а из палитры вида —
/// иначе трава, дерево и металл слились бы в один серый бетон. Внешние файлы
/// можно переименовывать и подменять: всё работает по имени из ExternalFiles.
/// </summary>
public static class Textures
{
    public const int Size = 128;
    public const int ExternalSize = 256;

    public enum Kind
    {
        Rock,
        Wood,
        Metal,
        Snow,
        Grass,
        Crate
    }

    /// <summary>
    /// Файл внешней текстуры для вида, без расширения и пути. Три картинки на
    /// шесть видов: тёмная на камень и металл, светлая гладкая на снег,
    /// контрастная на траву, дерево и ящик. Порядок — по индексам Kind.
    /// </summary>
    private static readonly string[] ExternalFiles =
    {
        "rock",   // Rock
        "cloud2", // Wood
        "rock",   // Metal
        "cloud1", // Snow
        "cloud2", // Grass
        "cloud2"  // Crate
    };

    private static readonly Dictionary<int, Texture2D> _albedoCache = new Dictionary<int, Texture2D>();
    private static readonly Dictionary<int, Texture2D> _normalCache = new Dictionary<int, Texture2D>();
    private static readonly Dictionary<int, float[]> _heightCache = new Dictionary<int, float[]>();
    private static readonly Dictionary<int, float[]> _externalHeight = new Dictionary<int, float[]>();
    private static Texture2D _cloud;

    private static readonly Texture2D[] _external = new Texture2D[6];
    private static readonly bool[] _externalTried = new bool[6];

    // ---------------- внешний рельеф ----------------

    /// <summary>Есть ли у вида внешняя картинка, которая реально используется.</summary>
    public static bool IsExternal(Kind kind)
    {
        return ExternalHeight(kind) != null;
    }

    /// <summary>
    /// Поле высот 0..1 из внешней картинки. null, если файла нет или он не
    /// читается. Считается один раз: яркость → сшивка краёв → растяжка контраста.
    /// </summary>
    public static float[] ExternalHeight(Kind kind)
    {
        int key = (int)kind;
        float[] cached;
        if (_externalHeight.TryGetValue(key, out cached) && cached != null) return cached;

        Texture2D src = LoadExternal(kind);
        if (src == null) return null;
        if (!src.isReadable)
        {
            Debug.LogWarning("Textures: " + src.name + " не помечена Read/Write — см. Assets/Editor/TextureImport.cs. Вид " + kind + " остаётся процедурным.");
            return null;
        }

        Color32[] px = src.GetPixels32();
        int w = src.width;
        int h = src.height;

        const int s = ExternalSize;
        float[] down = new float[s * s];
        for (int y = 0; y < s; y++)
        {
            float fy = Mathf.Clamp((y + 0.5f) / s * (h - 1), 0f, h - 1);
            for (int x = 0; x < s; x++)
            {
                float fx = Mathf.Clamp((x + 0.5f) / s * (w - 1), 0f, w - 1);
                down[y * s + x] = SampleLuma(px, w, h, fx, fy);
            }
        }

        // Пользовательские картинки не обязаны быть тайловыми: их края не
        // сходятся, и на плитке шов был бы виден. Сшивка краёв — смешивание
        // полосы у каждого
        // края с противоположным краем, сдвинутым на полпериода.
        float[] seamless = MakeSeamless(down, s);
        // Растяжка контраста по процентилям, а не по просторам min/max: одна
        // пиксельная выпадающая точка не должна выжигать весь рельеф.
        float[] stretched = ContrastStretch(seamless, s);

        _externalHeight[key] = stretched;
        return stretched;
    }

    private static Texture2D LoadExternal(Kind kind)
    {
        int key = (int)kind;
        if (_externalTried[key]) return _external[key];
        _externalTried[key] = true;
        _external[key] = Resources.Load<Texture2D>("Textures/" + ExternalFiles[key]);
        if (_external[key] == null)
            Debug.LogWarning("Textures: нет файла Resources/Textures/" + ExternalFiles[key] +
                ".jpg — вид " + kind + " остаётся процедурным.");
        return _external[key];
    }

    /// <summary>Яркость пикселя. Картинки серые, поэтому яркость = среднее каналов.</summary>
    private static float Luma(Color32 c)
    {
        return (c.r + c.g + c.b) * (1f / 765f);
    }

    private static float SampleLuma(Color32[] px, int w, int h, float fx, float fy)
    {
        int x0 = Mathf.Clamp((int)fx, 0, w - 1);
        int y0 = Mathf.Clamp((int)fy, 0, h - 1);
        int x1 = Mathf.Min(x0 + 1, w - 1);
        int y1 = Mathf.Min(y0 + 1, h - 1);
        float tx = fx - x0;
        float ty = fy - y0;

        float l00 = Luma(px[y0 * w + x0]);
        float l10 = Luma(px[y0 * w + x1]);
        float l01 = Luma(px[y1 * w + x0]);
        float l11 = Luma(px[y1 * w + x1]);

        return Mathf.Lerp(Mathf.Lerp(l00, l10, tx), Mathf.Lerp(l01, l11, tx), ty);
    }

    /// <summary>
    /// Сшивка краёв в бесшовную плитку. Каждый пиксель в полосе у края смешивается
    /// с пикселем со сдвигом полпериода; внутри края смесь плавно уходит в
    /// оригинал. Стык при этом съезжает на середину картинки, где пиксели
    /// соседние — шва не остаётся.
    /// </summary>
    private static float[] MakeSeamless(float[] src, int s)
    {
        float[] dst = new float[s * s];
        int m = Mathf.Max(16, s / 6);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float ex = Mathf.Clamp01(Mathf.Min(x, s - 1 - x) / (float)m);
                float ey = Mathf.Clamp01(Mathf.Min(y, s - 1 - y) / (float)m);
                float e = Mathf.Min(ex, ey);
                float f = e * e * (3f - 2f * e);
                int ox = (x + s / 2) % s;
                int oy = (y + s / 2) % s;
                dst[y * s + x] = Mathf.Lerp(src[oy * s + ox], src[y * s + x], f);
            }
        }
        return dst;
    }

    /// <summary>Растяжка контраста: 2-й и 98-й процентиль ложатся на 0 и 1.</summary>
    private static float[] ContrastStretch(float[] a, int s)
    {
        float[] sorted = (float[])a.Clone();
        System.Array.Sort(sorted);
        int count = s * s;
        int lo = Mathf.Max(0, Mathf.RoundToInt(count * 0.02f) - 1);
        int hi = Mathf.Min(count - 1, Mathf.RoundToInt(count * 0.98f));
        float range = Mathf.Max(0.05f, sorted[hi] - sorted[lo]);

        float[] dst = new float[count];
        for (int i = 0; i < count; i++)
            dst[i] = Mathf.Clamp01((a[i] - sorted[lo]) / range);
        return dst;
    }

    // ---------------- альбедо и нормаль ----------------

    public static Texture2D Albedo(Kind kind)
    {
        int key = (int)kind;
        Texture2D cached;
        if (_albedoCache.TryGetValue(key, out cached) && cached != null) return cached;

        float[] height = ExternalHeight(kind);
        bool external = height != null;
        if (height == null) height = HeightField(kind);
        int size = external ? ExternalSize : Size;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.name = (external ? "Ext_" : "Gen_") + kind;

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float u = (float)x / size;
                float v = (float)y / size;
                pixels[i] = external ? ShadeExternal(kind, height[i]) : Shade(kind, u, v, height[i]);
            }
        }

        tex.SetPixels32(pixels);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.Apply(true);
        _albedoCache[key] = tex;
        return tex;
    }

    /// <summary>
    /// Карта нормалей из той же высоты, что и альбедо; период размера учитывается
    /// заворотом, поэтому внешняя плитка высотой ExternalSize ничем не отличается
    /// от процедурной высотой Size.
    /// </summary>
    public static Texture2D Normal(Kind kind)
    {
        int key = (int)kind;
        Texture2D cached;
        if (_normalCache.TryGetValue(key, out cached) && cached != null) return cached;

        float[] height = ExternalHeight(kind);
        bool external = height != null;
        if (height == null) height = HeightField(kind);
        int size = external ? ExternalSize : Size;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.name = (external ? "ExtN_" : "GenN_") + kind;

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Разности по четырём соседям с заворотом края — градиент высот.
                float l = height[y * size + Wrap(x - 1, size)];
                float r = height[y * size + Wrap(x + 1, size)];
                float d = height[Wrap(y - 1, size) * size + x];
                float up = height[Wrap(y + 1, size) * size + x];

                float nx = (l - r) * 6f;
                float ny = (d - up) * 6f;
                Vector3 n = new Vector3(nx, ny, 1f).normalized;

                pixels[y * size + x] = new Color(
                    n.x * 0.5f + 0.5f,
                    n.y * 0.5f + 0.5f,
                    n.z * 0.5f + 0.5f,
                    1f);
            }
        }

        tex.SetPixels32(pixels);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.Apply(true);
        _normalCache[key] = tex;
        return tex;
    }

    /// <summary>Поле высот 0..1. Считается один раз на вид и переиспользуется обоими каналами.</summary>
    public static float[] HeightField(Kind kind)
    {
        int key = (int)kind;
        float[] cached;
        if (_heightCache.TryGetValue(key, out cached) && cached != null) return cached;

        cached = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                cached[y * Size + x] = Height(kind, (float)x / Size, (float)y / Size);
            }
        }

        _heightCache[key] = cached;
        return cached;
    }

    /// <summary>
    /// Заранее строит альбедо и нормали всех биомов. Иначе первая площадка
    /// нового вида на высоте 300, 460 или 660 м генерирует текстуру прямо в
    /// кадре и даёт заметный рывок. На старте забега эти же текстуры всё равно
    /// нужны, просто раньше и одним куском.
    /// </summary>
    public static void Prewarm()
    {
        for (int i = 0; i <= (int)Kind.Crate; i++)
        {
            Kind kind = (Kind)i;
            Albedo(kind);
            Normal(kind);
        }
    }

    /// <summary>
    /// Полупрозрачные облака с мягкими краями. Отдельный метод, а не ещё один Kind:
    /// здесь важна альфа и мягкий градиент, а не рельеф.
    /// </summary>
    public static Texture2D Cloud()
    {
        if (_cloud != null) return _cloud;

        const int res = 256;
        Color32[] pixels = new Color32[res * res];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float u = (float)x / res;
                float v = (float)y / res;

                // Две выборки шума: крупная даёт сами облака, мелкая рвёт края.
                float big = Fbm(u, v, 4, 401);
                float small = Fbm(u * 2f, v * 2f, 3, 409);
                float mask = big * 0.78f + small * 0.22f;

                // Порог сглажен, чтобы не было резких дыр.
                float alpha = Mathf.SmoothStep(0.42f, 0.72f, mask);
                float shade = Mathf.SmoothStep(0.5f, 0.95f, mask);

                pixels[y * res + x] = new Color(
                    Mathf.Lerp(0.72f, 1f, shade),
                    Mathf.Lerp(0.75f, 1f, shade),
                    Mathf.Lerp(0.82f, 1f, shade),
                    alpha);
            }
        }

        _cloud = new Texture2D(res, res, TextureFormat.RGBA32, true);
        _cloud.name = "Gen_Cloud";
        _cloud.wrapMode = TextureWrapMode.Repeat;
        _cloud.filterMode = FilterMode.Trilinear;
        _cloud.anisoLevel = 4;
        _cloud.SetPixels32(pixels);
        _cloud.Apply(true);
        return _cloud;
    }

    // ---------------- рельеф ----------------

    private static float Height(Kind kind, float u, float v)
    {
        switch (kind)
        {
            case Kind.Rock:
                // Обычный шум даёт «попкорн», поэтому часть октав берём гребневыми:
                // получаются осыпи и сколы, а не бугорки.
                return Mathf.Clamp01(Fbm(u, v, 4, 11) * 0.7f + Ridged(u, v, 3, 7) * 0.5f);

            case Kind.Wood:
            {
                // Четыре доски поперёк текстуры, зерно вытянуто вдоль.
                float plank = u * 4f;
                float seam = Mathf.Abs(Frac(plank) - 0.5f) * 2f;
                float grain = Fbm(plank * 0.9f + 3.7f, v * 0.14f, 3, 23);
                return Mathf.Clamp01(0.72f - seam * 0.6f + (grain - 0.5f) * 0.34f);
            }

            case Kind.Metal:
            {
                // Рельефный лист «в елочку» плюс ряд заклёпок по краю.
                float d = Frac((u + v) * 6f);
                float tread = Mathf.Abs(d - 0.5f) * 2f;
                float d2 = Frac((u - v) * 6f + 0.5f);
                float tread2 = Mathf.Abs(d2 - 0.5f) * 2f;
                float bumps = Mathf.Max(tread, tread2);
                float rivet = RivetRow(u, v);
                return Mathf.Clamp01(0.3f + bumps * 0.45f + rivet * 0.5f + Fbm(u, v, 2, 41) * 0.1f);
            }

            case Kind.Snow:
            {
                // Мягкие надувы плюс редкие гребни ветра.
                float baseNoise = Fbm(u, v, 4, 61);
                float drift = Mathf.Sin((u * 2f + Fbm(u, v, 2, 77) * 1.6f) * Mathf.PI) * 0.5f + 0.5f;
                return Mathf.Clamp01(baseNoise * 0.65f + drift * 0.35f);
            }

            case Kind.Grass:
            {
                // Мелкая плотная шероховатость плюс редкие проплешины.
                float fine = Fbm(u, v, 4, 91);
                float patch = Mathf.SmoothStep(0f, 1f, Mathf.Abs(fine - 0.5f) * 2f);
                return Mathf.Clamp01(fine * 0.75f + patch * 0.25f);
            }

            case Kind.Crate:
            {
                // Доски внутри и тёмная рамка по периметру ящика.
                float inner = Height(Kind.Wood, u, v);
                float border = Border(u, v, 0.09f);
                return Mathf.Clamp01(Mathf.Lerp(inner, 0.15f, border));
            }
        }

        return 0.5f;
    }

    /// <summary>Заклёпки по краям листа — четыре ряда, повторяются бесшовно.</summary>
    private static float RivetRow(float u, float v)
    {
        float band = Mathf.Min(Mathf.Min(v, 1f - v), Mathf.Min(u, 1f - u));
        float row = band < 0.13f ? 1f : 0f;
        if (row == 0f) return 0f;

        float cellU = Frac(u * 8f) - 0.5f;
        float cellV = Frac(v * 8f) - 0.5f;
        float d = Mathf.Sqrt(cellU * cellU + cellV * cellV);
        return Mathf.SmoothStep(0.22f, 0.06f, d);
    }

    /// <summary>1 у края текстуры, 0 в середине. Рамка для ящиков.</summary>
    private static float Border(float u, float v, float thickness)
    {
        float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
        return Mathf.Clamp01(1f - d / thickness);
    }

    // ---------------- цвет ----------------

    /// <summary>
    /// Цвет внешней текстуры: палитра вида, рельеф от картинки. Отдельный метод
    /// вместо переиспользования Shade, потому что Shade добавляет к рельефу свои
    /// узоры (заклёпки, доски, рамки), а у внешней картинки узор свой.
    /// </summary>
    private static Color ShadeExternal(Kind kind, float h)
    {
        Color dark, light;
        switch (kind)
        {
            case Kind.Wood:
                dark = new Color(0.34f, 0.22f, 0.13f, 1f);
                light = new Color(0.66f, 0.48f, 0.29f, 1f);
                break;
            case Kind.Metal:
                dark = new Color(0.24f, 0.27f, 0.32f, 1f);
                light = new Color(0.72f, 0.77f, 0.84f, 1f);
                break;
            case Kind.Snow:
                dark = new Color(0.74f, 0.81f, 0.92f, 1f);
                light = Color.white;
                break;
            case Kind.Grass:
                dark = new Color(0.20f, 0.40f, 0.17f, 1f);
                light = new Color(0.47f, 0.68f, 0.26f, 1f);
                break;
            case Kind.Crate:
                dark = new Color(0.45f, 0.31f, 0.18f, 1f);
                light = new Color(0.72f, 0.55f, 0.34f, 1f);
                break;
            default:
                dark = new Color(0.28f, 0.27f, 0.30f, 1f);
                light = new Color(0.64f, 0.62f, 0.59f, 1f);
                break;
        }
        return Color.Lerp(dark, light, ExternalLight(kind, h));
    }

    /// <summary>Доля света по виду. Дерево и снег светлеют чуть раньше: у них
    /// верхний тон зажат в тёмном диапазоне, без множителя верх платформы
    /// почти неотличим от низа.</summary>
    private static float ExternalLight(Kind kind, float h)
    {
        switch (kind)
        {
            case Kind.Wood: return Mathf.Clamp01(h * 1.15f);
            case Kind.Snow: return Mathf.Clamp01(h * 1.1f);
            default: return Mathf.Clamp01(h);
        }
    }

    private static Color Shade(Kind kind, float u, float v, float h)
    {
        switch (kind)
        {
            case Kind.Rock:
            {
                Color dark = new Color(0.28f, 0.27f, 0.30f, 1f);
                Color light = new Color(0.64f, 0.62f, 0.59f, 1f);
                Color c = Color.Lerp(dark, light, h);
                return Tint(c, Fbm(u, v, 3, 131) * 0.14f - 0.07f);
            }

            case Kind.Wood:
            {
                int plank = Mathf.FloorToInt(u * 4f);
                // Каждая доска чуть своего оттенка, иначе полосы сливаются.
                float shift = Hash(plank, 5, 211) * 0.16f - 0.08f;
                Color dark = new Color(0.34f, 0.22f, 0.13f, 1f);
                Color light = new Color(0.66f, 0.48f, 0.29f, 1f);
                Color c = Color.Lerp(dark, light, Mathf.Clamp01(h * 1.15f));
                return Tint(c, shift);
            }

            case Kind.Metal:
            {
                Color dark = new Color(0.24f, 0.27f, 0.32f, 1f);
                Color light = new Color(0.72f, 0.77f, 0.84f, 1f);
                Color c = Color.Lerp(dark, light, Mathf.Clamp01(h));
                // Лёгкий ржавый налёт в низинах, чтобы металл не выглядел пластиком.
                float rust = Mathf.Clamp01(0.5f - h) * Mathf.Max(0f, Fbm(u, v, 3, 151) - 0.45f) * 1.6f;
                c = Color.Lerp(c, new Color(0.45f, 0.26f, 0.14f, 1f), rust);
                return c;
            }

            case Kind.Snow:
            {
                Color c = Color.Lerp(new Color(0.74f, 0.81f, 0.92f, 1f), Color.white,
                                    Mathf.Clamp01(h * 1.1f));
                return Tint(c, Fbm(u, v, 3, 171) * 0.05f - 0.025f);
            }

            case Kind.Grass:
            {
                Color c = Color.Lerp(new Color(0.20f, 0.40f, 0.17f, 1f), new Color(0.47f, 0.68f, 0.26f, 1f),
                                    Mathf.Clamp01(h));
                return Tint(c, Fbm(u, v, 2, 191) * 0.08f - 0.04f);
            }

            case Kind.Crate:
            {
                Color c = Height(Kind.Wood, u, v) < 0.2f
                    ? new Color(0.22f, 0.15f, 0.09f, 1f)
                    : Color.Lerp(new Color(0.45f, 0.31f, 0.18f, 1f), new Color(0.72f, 0.55f, 0.34f, 1f), h);
                return c;
            }
        }

        return Color.white;
    }

    private static Color Tint(Color c, float amount)
    {
        return new Color(
            Mathf.Clamp01(c.r + amount),
            Mathf.Clamp01(c.g + amount),
            Mathf.Clamp01(c.b + amount),
            1f);
    }

    // ---------------- шум ----------------

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }

    /// <summary>
    /// Значение шума с целочисленным периодом: решётка заворачивается, поэтому
    /// текстура повторяется без стыка. Период обязан быть целым.
    /// </summary>
    private static float Value(float x, float y, int period, int seed)
    {
        int xi = Mathf.FloorToInt(x);
        int yi = Mathf.FloorToInt(y);
        float xf = x - xi;
        float yf = y - yi;
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);

        int x0 = Mod(xi, period);
        int x1 = Mod(xi + 1, period);
        int y0 = Mod(yi, period);
        int y1 = Mod(yi + 1, period);

        float a = Hash(x0, y0, seed);
        float b = Hash(x1, y0, seed);
        float c = Hash(x0, y1, seed);
        float d = Hash(x1, y1, seed);

        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    private static float Fbm(float x, float y, int octaves, int seed)
    {
        float sum = 0f;
        float amp = 0.5f;
        float norm = 0f;
        int period = 4;

        for (int i = 0; i < octaves; i++)
        {
            sum += Value(x * period, y * period, period, seed + i * 17) * amp;
            norm += amp;
            amp *= 0.5f;
            period *= 2;
        }

        return norm > 0f ? sum / norm : 0.5f;
    }

    private static float Ridged(float x, float y, int octaves, int seed)
    {
        float sum = 0f;
        float amp = 0.5f;
        float norm = 0f;
        int period = 4;

        for (int i = 0; i < octaves; i++)
        {
            float n = Value(x * period, y * period, period, seed + i * 31);
            n = 1f - Mathf.Abs(n * 2f - 1f);
            sum += n * n * amp;
            norm += amp;
            amp *= 0.5f;
            period *= 2;
        }

        return norm > 0f ? sum / norm : 0.5f;
    }

    private static int Wrap(int v)
    {
        return Wrap(v, Size);
    }

    private static int Wrap(int v, int size)
    {
        v %= size;
        if (v < 0) v += size;
        return v;
    }

    /// <summary>Приведение к диапазону [0, mod). Нужно решётке шума, у которой свой период.</summary>
    private static int Mod(int v, int mod)
    {
        v %= mod;
        if (v < 0) v += mod;
        return v;
    }

    private static float Frac(float v)
    {
        return v - Mathf.Floor(v);
    }
}