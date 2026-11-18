using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Материалы поверхностей из процедурных текстур. Кэш по паре «вид текстуры + масштаб
/// тайлинга», поэтому платформы одного вида делят один материал и не плодят копии.
///
/// У площадки два материала: крышка — то, по чему ходят, и борт с низом — то,
/// чем площадка обрывается. Вид борта выводится из вида крышки, а не задаётся
/// отдельно: отдельная таблица рано или поздно разошлась бы с крышкой, и трава
/// повисла бы над листом железа.
/// </summary>
public static class Surfaces
{
    private static readonly Dictionary<int, Material> _cache = new Dictionary<int, Material>();
    private static readonly Dictionary<int, Material[]> _pairs = new Dictionary<int, Material[]>();

    /// <summary>Материал крышки площадки — верхней грани, по которой ходят.</summary>
    public static Material Platform(Textures.Kind kind)
    {
        return Platform(kind, 1f);
    }

    /// <summary>
    /// Вид борта и низа по виду крышки. Трава и снег лежат на породе, поэтому
    /// снизу у них камень: иначе обрыв площадки был бы того же цвета, что и
    /// луг на ней, и площадка читалась бы одним куском, а не островом.
    /// Остальное снизу то же, из чего сделано: у доски борт дощатый.
    /// </summary>
    public static Textures.Kind SideKind(Textures.Kind kind)
    {
        switch (kind)
        {
            case Textures.Kind.Grass: return Textures.Kind.Rock;
            case Textures.Kind.Snow: return Textures.Kind.Rock;
            default: return kind;
        }
    }

    /// <summary>
    /// Материал борта, уступов и низа. Отдельного кэша ему не нужно: материал
    /// однозначно задан парой «вид + тайлинг», и борт травяной площадки — это
    /// ровно тот же камень, что и каменная площадка.
    /// </summary>
    public static Material PlatformSide(Textures.Kind kind, float tiling)
    {
        return Platform(SideKind(kind), tiling);
    }

    /// <summary>
    /// Пара материалов площадки в порядке подмешей PlatformMesh: крышка, затем
    /// борт с низом. Массив кэшируется целиком: Renderer копирует его себе, но
    /// собирать его на каждую площадку пятисотметровой трассы незачем.
    /// </summary>
    public static Material[] PlatformPair(Textures.Kind kind, float tiling)
    {
        int key = Key(kind, tiling);

        Material[] cached;
        if (_pairs.TryGetValue(key, out cached) && cached != null &&
            cached[0] != null && cached[1] != null)
            return cached;

        Material[] pair = { Platform(kind, tiling), PlatformSide(kind, tiling) };
        _pairs[key] = pair;
        return pair;
    }

    private static int Key(Textures.Kind kind, float tiling)
    {
        tiling = QuantizeTiling(tiling);
        return (int)kind * 100 + Mathf.RoundToInt(tiling * 2f);
    }

    /// <summary>
    /// Тайлинг платформ меняется вместе с их размером. Дробные значения до
    /// десятых плодили десятки почти одинаковых материалов: каждый — свой
    /// SetPass. Шаг 0,5 сохраняет плотность текстуры на глаз и укладывает
    /// материал в несколько общих buckets.
    /// </summary>
    private static float QuantizeTiling(float tiling)
    {
        tiling = Mathf.Clamp(tiling, 0.5f, 8f);
        return Mathf.Round(tiling * 2f) * 0.5f;
    }

    /// <param name="tiling">Сколько раз текстура укладывается на метр поверхности.</param>
    public static Material Platform(Textures.Kind kind, float tiling)
    {
        int key = Key(kind, tiling);

        Material cached;
        if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

        tiling = QuantizeTiling(tiling);
        Texture2D albedo = Textures.Albedo(kind);
        Texture2D normal = Textures.Normal(kind);

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.name = "Surf_" + kind;
        mat.mainTexture = albedo;
        mat.SetTextureScale("_MainTex", new Vector2(tiling, tiling));

        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            // Металл рельефнее, снег почти плоский — иначе блеск ловит свет неверно.
            mat.SetFloat("_BumpScale", kind == Textures.Kind.Metal ? 1.4f : 0.8f);
        }

        mat.SetFloat("_Glossiness", Gloss(kind));
        mat.SetFloat("_Metallic", kind == Textures.Kind.Metal ? 0.75f : 0f);
        mat.SetFloat("_Smoothness", Gloss(kind));

        _cache[key] = mat;
        return mat;
    }

    private static float Gloss(Textures.Kind kind)
    {
        switch (kind)
        {
            case Textures.Kind.Metal: return 0.55f;
            case Textures.Kind.Snow: return 0.30f;
            case Textures.Kind.Rock: return 0.10f;
            case Textures.Kind.Wood: return 0.16f;
            case Textures.Kind.Crate: return 0.14f;
            default: return 0.06f;
        }
    }

    /// <summary>Материал шипов: тёмный металл с зелёным отсветом, чтобы шипы читались как опасность.</summary>
    public static Material Hazard()
    {
        Material cached;
        if (_cache.TryGetValue(-1, out cached) && cached != null) return cached;

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.name = "Hazard";
        mat.color = new Color(0.32f, 0.36f, 0.30f, 1f);
        mat.SetFloat("_Glossiness", 0.6f);
        mat.SetFloat("_Metallic", 0.6f);
        _cache[-1] = mat;
        return mat;
    }

    /// <summary>Полупрозрачный материал для облачного слоя и тумана.</summary>
    public static Material Cloud(Color tint, float alpha)
    {
        int key = -100 - (ColorKey(tint) & 0x3FF);
        Material cached;
        if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.name = "Cloud";
        mat.color = new Color(tint.r, tint.g, tint.b, alpha);
        ConfigureTransparent(mat);
        _cache[key] = mat;
        return mat;
    }

    public static void ConfigureTransparent(Material mat)
    {
        if (mat == null) return;
        // Порядок рендера: Standard-прозрачный рисуется после всего непрозрачного,
        // но material.renderQueue нужно поднять явно, иначе слой режет платформы.
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static int ColorKey(Color c)
    {
        Color32 c32 = c;
        return (c32.r << 24) | (c32.g << 16) | (c32.b << 8) | c32.a;
    }
}
