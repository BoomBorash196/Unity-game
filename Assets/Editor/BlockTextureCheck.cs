#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Проверка внешних текстур блоков. Картинки пользователя (rock, cloud1, cloud2)
/// обязаны читаться из Resources/Textures как Read/Write, из них должны строиться
/// альбедо и нормаль одного размера, цвета шести видов обязаны остаться
/// различимыми (биомы читаются под ногами), а плитка — бесшовной, потому что
/// исходные края картинок не сходятся. Также проверяется, что материалы Surfaces
/// берут именно эти текстуры, а не процедурные.
/// </summary>
public static class BlockTextureCheck
{
    private static readonly Textures.Kind[] All =
    {
        Textures.Kind.Rock, Textures.Kind.Wood, Textures.Kind.Metal,
        Textures.Kind.Snow, Textures.Kind.Grass, Textures.Kind.Crate
    };

    private static int _passed;
    private static int _failed;

    public static int Check()
    {
        _passed = 0;
        _failed = 0;

        CheckFiles();
        CheckAlbedo();
        CheckNormals();
        CheckColours();
        CheckSeamlessness();
        CheckMaterials();
        CheckSideMaterials();

        Debug.Log("[T] RESULT BlockTexture passed=" + _passed + " failed=" + _failed);
        return _failed;
    }

    private static void CheckFiles()
    {
        string[] names = { "rock", "cloud1", "cloud2" };
        foreach (string name in names)
        {
            Texture2D tex = Resources.Load<Texture2D>("Textures/" + name);
            Check("file." + name, tex != null && tex.isReadable && tex.width > 0,
                tex == null ? "не найден" : (tex.isReadable ? tex.width + "x" + tex.height : "не Read/Write"));
        }

        // Все шесть видов вышли на внешние картинки, а не тихо скатились в
        // процедурные: иначе «замена всей палитры» выполняется наполовину.
        foreach (Textures.Kind kind in All)
            Check("ext." + kind, Textures.IsExternal(kind),
                Textures.IsExternal(kind) ? "да" : "внешняя не используется");
    }

    private static void CheckAlbedo()
    {
        foreach (Textures.Kind kind in All)
        {
            Texture2D tex = Textures.Albedo(kind);
            bool ok = tex != null && tex.name.StartsWith("Ext_") && !tex.name.StartsWith("ExtN_") &&
                      tex.width == Textures.ExternalSize && tex.height == Textures.ExternalSize &&
                      tex.wrapMode == TextureWrapMode.Repeat;
            Check("albedo." + kind, ok,
                tex == null ? "null" :
                !tex.name.StartsWith("Ext_") ? tex.name + " — выпали в процедурный" :
                (tex.width + "x" + tex.height + " wrap=" + tex.wrapMode));
        }
    }

    private static void CheckNormals()
    {
        foreach (Textures.Kind kind in All)
        {
            Texture2D tex = Textures.Normal(kind);
            bool ok = tex != null && tex.name.StartsWith("ExtN_") &&
                      tex.width == Textures.ExternalSize && tex.height == Textures.ExternalSize;
            Check("normal." + kind, ok,
                tex == null ? "null" : (tex.name + " " + tex.width + "x" + tex.height));

            // Средняя нормаль — строго вверх: карта не должна быть сдвинута.
            Vector2 avg = AvgNormalXY(tex);
            Check("normal." + kind + " up", ok && Mathf.Abs(avg.x) < 0.10f && Mathf.Abs(avg.y) < 0.10f,
                "средний наклон x/y " + avg.ToString("0.000"));
        }
    }

    private static void CheckColours()
    {
        Color[] mean = new Color[All.Length];
        for (int i = 0; i < All.Length; i++)
            mean[i] = MeanColour(Textures.Albedo(All[i]));

        // Различимость: любые два вида разнятся хотя бы по одному каналу. Это
        // главное свойство, ради которого цвет берётся из вида, а не из серых
        // картинок: биом под ногами обязан читаться и после замены.
        float worstPair = float.PositiveInfinity;
        for (int i = 0; i < All.Length; i++)
        {
            for (int j = i + 1; j < All.Length; j++)
            {
                float d = MaxChannel(Mathf.Abs(mean[i].r - mean[j].r),
                    Mathf.Abs(mean[i].g - mean[j].g),
                    Mathf.Abs(mean[i].b - mean[j].b));
                if (d < worstPair) worstPair = d;
            }
        }
        Check("colour.pairs distinct", worstPair > 0.04f,
            "ближайшая пара расходится на " + worstPair.ToString("0.000"));

        // Направленные проверки палитры. Пороги щадящие: ловят полный слив в
        // один серый, а не подправляют оттенки.
        int snow = (int)Textures.Kind.Snow;
        Check("colour.snow light", mean[snow].r > 0.55f, "средний r " + mean[snow].r.ToString("0.000"));

        int grass = (int)Textures.Kind.Grass;
        int rock = (int)Textures.Kind.Rock;
        Check("colour.grass greener", mean[grass].g - mean[grass].r > 0.05f,
            "g-r " + (mean[grass].g - mean[grass].r).ToString("0.000"));
        Check("colour.rock dark neutral", mean[rock].r < 0.58f && Mathf.Abs(mean[rock].r - mean[rock].g) < 0.10f,
            "средний " + mean[rock].r.ToString("0.000") + "," + mean[rock].g.ToString("0.000"));

        // Три вида из одной картинки cloud2 обязаны разойтись цветом, иначе
        // замена всей палитры тремя файлами не работает.
        int wood = (int)Textures.Kind.Wood;
        int crate = (int)Textures.Kind.Crate;
        float dWC = MaxChannel(Mathf.Abs(mean[wood].r - mean[crate].r),
            Mathf.Abs(mean[wood].g - mean[crate].g),
            Mathf.Abs(mean[wood].b - mean[crate].b));
        Check("colour.wood vs crate", dWC > 0.02f, "расходятся на " + dWC.ToString("0.000"));
    }

    private static void CheckSeamlessness()
    {
        const int s = Textures.ExternalSize;
        foreach (Textures.Kind kind in All)
        {
            float[] h = Textures.ExternalHeight(kind);
            if (h == null) continue; // упало выше, здесь не дублируем

            float seamX = 0f;
            for (int y = 0; y < s; y++)
                seamX += Mathf.Abs(h[y * s] - h[y * s + s - 1]);
            seamX /= s;

            float seamY = 0f;
            for (int x = 0; x < s; x++)
                seamY += Mathf.Abs(h[x] - h[(s - 1) * s + x]);
            seamY /= s;

            // Локальный разброс между соседями — честный масштаб для «виден ли шов».
            float neighbour = 0f;
            int n = 0;
            for (int y = 0; y < s; y += 2)
            {
                for (int x = 0; x < s - 1; x += 2)
                {
                    neighbour += Mathf.Abs(h[y * s + x] - h[y * s + x + 1]);
                    n++;
                }
            }
            neighbour /= Mathf.Max(1, n);

            bool ok = seamX <= 2f * neighbour && seamY <= 2f * neighbour;
            Check("seamless." + kind, ok,
                "шов x " + (seamX * 255f).ToString("0.0") + ", y " + (seamY * 255f).ToString("0.0") +
                ", соседи " + (neighbour * 255f).ToString("0.0"));
        }
    }

    private static void CheckMaterials()
    {
        foreach (Textures.Kind kind in All)
        {
            Material mat = Surfaces.Platform(kind);
            bool ok = mat != null && mat.mainTexture == Textures.Albedo(kind) &&
                      mat.GetTexture("_BumpMap") == Textures.Normal(kind);
            Check("material." + kind, ok,
                mat == null ? "нет материала" :
                (mat.mainTexture == null ? "нет альбедо" : "альбедо не то или нет bump"));
        }
    }

    /// <summary>
    /// Материалы борта. У площадки два материала, и второй — борт с низом —
    /// берётся не из отдельной таблицы, а выводится из вида крышки. Здесь и
    /// проверяется, что вывод не разошёлся с картинками: трава и снег обрываются
    /// камнем, остальное — тем же, из чего сделано, и борт не заводит себе
    /// второй, ни на что не похожий материал.
    /// </summary>
    private static void CheckSideMaterials()
    {
        foreach (Textures.Kind kind in All)
        {
            Textures.Kind side = Surfaces.SideKind(kind);
            bool under = kind == Textures.Kind.Grass || kind == Textures.Kind.Snow;
            Check("side kind." + kind, side == (under ? Textures.Kind.Rock : kind), "борт " + side);

            // Борт — это ровно тот же материал, что и площадка того вида, в
            // который он выведен: отдельной копии у кэша нет.
            Check("side material." + kind,
                Surfaces.PlatformSide(kind, 1f) == Surfaces.Platform(side, 1f), "не тот материал");
        }

        foreach (Textures.Kind kind in All)
        {
            Material[] pair = Surfaces.PlatformPair(kind, 2f);
            bool ok = pair != null && pair.Length == 2 && pair[0] != null && pair[1] != null &&
                      pair[0] == Surfaces.Platform(kind, 2f) && pair[1] == Surfaces.PlatformSide(kind, 2f);
            Check("pair." + kind, ok, pair == null ? "нет пары" : ("пара " + pair.Length));

            // Текстура борта — текстура выведенного вида, а не крышки: именно
            // это и видно на экране снизу площадки.
            Check("pair texture." + kind,
                ok && pair[1].mainTexture == Textures.Albedo(Surfaces.SideKind(kind)), "");

            // Пара кэшируется целиком: на трассе в пятьсот площадок новый
            // массив на каждую — это сотни массивов в мусоре.
            Check("pair cached." + kind, Surfaces.PlatformPair(kind, 2f) == pair, "");
        }
    }

    // ---------------- измерения ----------------

    private static Color MeanColour(Texture2D tex)
    {
        if (tex == null) return Color.clear;
        Color32[] px = tex.GetPixels32();
        long r = 0, g = 0, b = 0;
        for (int i = 0; i < px.Length; i++)
        {
            r += px[i].r;
            g += px[i].g;
            b += px[i].b;
        }
        return new Color(r / (float)px.Length / 255f,
            g / (float)px.Length / 255f,
            b / (float)px.Length / 255f, 1f);
    }

    private static Vector2 AvgNormalXY(Texture2D tex)
    {
        if (tex == null) return Vector2.zero;
        Color32[] px = tex.GetPixels32();
        float sx = 0f, sy = 0f;
        for (int i = 0; i < px.Length; i++)
        {
            sx += px[i].r * (1f / 255f) - 0.5f;
            sy += px[i].g * (1f / 255f) - 0.5f;
        }
        return new Vector2(sx / px.Length, sy / px.Length);
    }

    private static float MaxChannel(float a, float b, float c)
    {
        return Mathf.Max(a, Mathf.Max(b, c));
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok)
        {
            _passed++;
            Debug.Log("[T] PASS " + name);
        }
        else
        {
            _failed++;
            Debug.Log("[T] FAIL " + name + " :: " + detail);
        }
    }
}
#endif