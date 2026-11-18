#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Проверка форм площадок. Площадка — тело из плана и силуэта сбоку: в плане
/// углы срезаны, снизу полусторона только убывает (обрыв, уступы, шпиль). Все
/// меши обязаны быть вписаны в единичный куб: габариты, подбор места и
/// OverlapCheck считают коробку коллайдера, и менять её нельзя.
///
/// Проверяется три вещи, каждая своим способом, потому что каждая ломается
/// молча. Замкнутость и намотка: тело обязано быть без дыр и смотреть наружу —
/// объём со знаком это подтверждает, а «нормаль от центра» ловит отдельную
/// вывернутую грань. Форма: меш касается коробки в плане, иначе по краям
/// площадки стоять было бы не на чем. Вид: силуэт выводится из вида текстуры,
/// природное не превращается в доску, а рукотворное — в остров.
///
/// Второй слой — сквозной прогон настоящего уровня: у каждой площадки меш из
/// кэша PlatformMesh, кубовый коллайдер единичного размера, два подмеша и пара
/// материалов к ним, углы реально срезаны хотя бы у части площадок, а профили
/// и силуэты не вырождаются в один.
/// </summary>
public static class PlatformShapeCheck
{
    private static int _passed;
    private static int _failed;

    public static int Check()
    {
        _passed = 0;
        _failed = 0;

        CheckMeshes();
        CheckStyles();
        CheckPick();
        CheckLiveLevel();

        Debug.Log("[T] RESULT PlatformShape passed=" + _passed + " failed=" + _failed);
        return _failed;
    }

    private static void CheckMeshes()
    {
        for (int s = 0; s < PlatformMesh.StyleCount; s++)
        {
            for (int p = 0; p < PlatformMesh.ProfileCount; p++)
            {
                Mesh mesh = PlatformMesh.Get(p, s);
                string name = "mesh." + s + "." + p;

                Check(name + " exists", mesh != null && mesh.name == "Platform" + p + "_" + s,
                    mesh == null ? "null" : mesh.name);

                // Числа вершин и индексов: план 4 или 8 точек, на каждую полосу
                // по кваду на точку, плюс две крышки веером.
                int n = p == 0 ? 4 : 8;
                int bands = PlatformMesh.BandCount(s);
                int verts = 2 * (n + 1) + 4 * n * bands;
                int indices = 6 * n * (1 + bands);
                Check(name + " verts", mesh.vertexCount == verts,
                    "вершин " + mesh.vertexCount + " против " + verts);
                Check(name + " tris", mesh.triangles.Length == indices,
                    "индексов " + mesh.triangles.Length + " против " + indices);

                // Два подмеша: крышка и всё остальное.
                int topCount = mesh.subMeshCount > PlatformMesh.TopSubmesh
                    ? mesh.GetTriangles(PlatformMesh.TopSubmesh).Length : -1;
                int wallCount = mesh.subMeshCount > PlatformMesh.WallSubmesh
                    ? mesh.GetTriangles(PlatformMesh.WallSubmesh).Length : -1;
                Check(name + " submeshes",
                    mesh.subMeshCount == PlatformMesh.SubmeshCount &&
                    topCount == 3 * n && wallCount == indices - 3 * n,
                    "подмешей " + mesh.subMeshCount + ", крышка " + topCount + ", борт " + wallCount);

                // Всё в единичном кубе: меш лишь вписан в коробку коллайдера.
                bool inBox = true;
                foreach (Vector3 v in mesh.vertices)
                    if (Mathf.Abs(v.x) > 0.5f + 0.002f || Mathf.Abs(v.y) > 0.5f + 0.002f || Mathf.Abs(v.z) > 0.5f + 0.002f)
                        inBox = false;
                Check(name + " in box", inBox, "габариты " + mesh.bounds.extents.ToString("0.000"));

                // По осям меш достигает самой коробки: форма не сужается настолько,
                // чтобы «воздушные» углы коллайдера стали неправдоподобно большими.
                Vector3 ext = mesh.bounds.extents;
                Check(name + " fills collider",
                    ext.x > 0.499f && ext.z > 0.499f && ext.y > 0.499f,
                    "extents " + ext.ToString("0.000"));

                bool uvOk = true;
                foreach (Vector2 uv in mesh.uv)
                    if (uv.x < -0.01f || uv.x > 1.01f || uv.y < -0.01f || uv.y > 1.01f)
                        uvOk = false;
                Check(name + " uv", uvOk, "");

                bool unitNormals = true;
                foreach (Vector3 nrm in mesh.normals)
                    if (Mathf.Abs(nrm.sqrMagnitude - 1f) > 0.01f)
                        unitNormals = false;
                Check(name + " normals unit", unitNormals, "");

                // Ни одной вырожденной грани: у вырожденной нет площади, а значит
                // нет и нормали — такая грань выглядит дырой.
                Check(name + " no degenerate", NoDegenerate(mesh), "");

                // Замкнутое тело обязано иметь положительный объём со знаком:
                // это единственная проверка, которая ловит разворот всего меша.
                Check(name + " volume positive", SignedVolume(mesh) > 0.001f,
                    "объём " + SignedVolume(mesh).ToString("0.000"));

                // Грани смотрят наружу геометрически, независимо от буфера нормалей.
                Check(name + " outward", IsGeometricallyOutward(mesh), "");
            }
        }

        // Срез углов растёт от профиля к профилю: без этого меши одинаковы в плане.
        for (int s = 0; s < PlatformMesh.StyleCount; s++)
        {
            float prev = float.PositiveInfinity;
            bool monotone = true;
            for (int p = 0; p < PlatformMesh.ProfileCount; p++)
            {
                float corner = CornerSum(PlatformMesh.Get(p, s));
                if (corner >= prev - 0.001f) monotone = false;
                prev = corner;
            }
            Check("mesh.corner cut grows." + s, monotone, "срезы " + string.Join(" ", CutLine(s)));
        }

        // Плита нулевого профиля — ровно прежний куб: ничего не срезано, низ
        // плоский и во всю полусторону. Это стартовая «земля», по ней судят о
        // том, стоит ли игрок на видимом.
        Mesh slab = PlatformMesh.Get(0, (int)PlatformMesh.Style.Slab);
        Vector3 slabExt = slab.bounds.extents;
        bool slabBox = Mathf.Abs(slabExt.x - 0.5f) < 0.002f &&
                       Mathf.Abs(slabExt.y - 0.5f) < 0.002f &&
                       Mathf.Abs(slabExt.z - 0.5f) < 0.002f;
        foreach (Vector3 v in slab.vertices)
        {
            // Середины крышек стоят на оси и углами не являются.
            if (Mathf.Abs(v.x) < 0.001f && Mathf.Abs(v.z) < 0.001f) continue;
            if (Mathf.Abs(Mathf.Abs(v.x) - 0.5f) > 0.002f ||
                Mathf.Abs(Mathf.Abs(v.z) - 0.5f) > 0.002f) slabBox = false;
        }
        Check("mesh.0.0 is box", slabBox, "extents " + slabExt.ToString("0.000"));
    }

    /// <summary>Силуэты обязаны быть разными: иначе «разные виды» — это одно и
    /// то же тело, и подмена вида текстуры ничего не меняет на экране.</summary>
    private static void CheckStyles()
    {
        var seen = new Dictionary<int, int>();
        for (int s = 0; s < PlatformMesh.StyleCount; s++)
        {
            float volume = SignedVolume(PlatformMesh.Get(2, s));
            int key = Mathf.RoundToInt(volume * 1000f);
            if (seen.ContainsKey(key))
                Check("style." + s + " differs", false, "объём совпал с силуэтом " + seen[key]);
            else
                seen[key] = s;
        }
        Check("style.all differ", seen.Count == PlatformMesh.StyleCount, "разных объёмов " + seen.Count);

        // Обрыв у острова, террасы и шпиля меньше, чем у ровной плиты, и это
        // не украшение: низ, раздутый до габарита коробки, читался бы кубом.
        float slabVolume = SignedVolume(PlatformMesh.Get(2, (int)PlatformMesh.Style.Slab));
        foreach (int s in new[] { (int)PlatformMesh.Style.Island, (int)PlatformMesh.Style.Deep,
                                  (int)PlatformMesh.Style.Terrace, (int)PlatformMesh.Style.Spire })
            Check("style." + s + " narrower than slab",
                SignedVolume(PlatformMesh.Get(2, s)) < slabVolume - 0.01f, "");
    }

    private static void CheckPick()
    {
        Check("pick.index0 is box", PlatformMesh.Pick(0, 0, 13337) == 0, "");

        Check("pick.square range", InRange(PlatformMesh.Pick(0, 5, 1), 2, 4), "");
        Check("pick.small range", InRange(PlatformMesh.Pick(1, 5, 1), 3, 5), "");
        Check("pick.wide range", InRange(PlatformMesh.Pick(2, 5, 1), 0, 2), "");
        Check("pick.long range", InRange(PlatformMesh.Pick(3, 5, 1), 0, 1), "");

        Check("pick.square varied", Variety(0) >= 2, "разных " + Variety(0));
        Check("pick.small varied", Variety(1) >= 2, "разных " + Variety(1));
        Check("pick.wide varied", Variety(2) >= 2, "разных " + Variety(2));
        Check("pick.long varied", Variety(3) >= 2, "разных " + Variety(3));

        // Один и тот же номер всегда даёт один и тот же профиль.
        Check("pick.deterministic", PlatformMesh.Pick(0, 17, 13337) == PlatformMesh.Pick(0, 17, 13337), "");

        // Силуэт тоже: он берётся хэшем только потому, что розыгрыш сдвинул бы
        // поток генератора и вместе с ним всю заученную трассу.
        Check("style.deterministic",
            PlatformMesh.PickStyle(Textures.Kind.Grass, 41, 13337) ==
            PlatformMesh.PickStyle(Textures.Kind.Grass, 41, 13337), "");

        // Рукотворное — только плита: у доски и листа нет обрыва.
        int[] manmade = { (int)Textures.Kind.Wood, (int)Textures.Kind.Metal, (int)Textures.Kind.Crate };
        foreach (int kind in manmade)
            Check("style.manmade slab." + (Textures.Kind)kind, StylesOf((Textures.Kind)kind).Count == 1 &&
                StylesOf((Textures.Kind)kind).Contains((int)PlatformMesh.Style.Slab), "");

        Check("style.grass soft", SubsetOf(StylesOf(Textures.Kind.Grass),
            (int)PlatformMesh.Style.Island, (int)PlatformMesh.Style.Deep), "");
        Check("style.snow soft", SubsetOf(StylesOf(Textures.Kind.Snow),
            (int)PlatformMesh.Style.Island, (int)PlatformMesh.Style.Deep, (int)PlatformMesh.Style.Terrace), "");
        Check("style.rock sharp", SubsetOf(StylesOf(Textures.Kind.Rock),
            (int)PlatformMesh.Style.Spire, (int)PlatformMesh.Style.Terrace), "");

        // Все пять силуэтов достижимы: иначе силуэт в таблице есть, а площадки
        // с ним не бывает.
        var all = new HashSet<int>();
        foreach (Textures.Kind kind in Kinds)
            foreach (int s in StylesOf(kind)) all.Add(s);
        Check("style.all reachable", all.Count == PlatformMesh.StyleCount, "достижимо " + all.Count);

        // Разнообразие внутри природных видов: одна форма на всю траву читалась
        // бы так же одинаково, как прежний куб.
        Check("style.grass varied", StylesOf(Textures.Kind.Grass).Count >= 2, "");
        Check("style.rock varied", StylesOf(Textures.Kind.Rock).Count >= 2, "");
    }

    private static void CheckLiveLevel()
    {
        GameObject host = new GameObject("[Test] LevelGenerator");
        LevelGenerator gen = host.AddComponent<LevelGenerator>();
        gen.BuildData();
        gen.ResetPath(0);
        const int want = 400;
        int guard = 0;
        while (gen.AliveMax < want && guard++ < 60)
            gen.GenerateUpTo(gen.AliveMax + 100);

        Transform root = gen.Data.Root.transform;
        int platforms = 0;
        int cut = 0;
        int inscribed = 0;
        int paired = 0;
        int twoSubmeshes = 0;
        int grassUnderRock = 0;
        var used = new HashSet<int>();
        var styles = new HashSet<int>();

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child.name.StartsWith("Platform ")) continue;

            platforms++;
            MeshFilter filter = child.GetComponent<MeshFilter>();
            BoxCollider box = child.GetComponent<BoxCollider>();
            MeshCollider meshCol = child.GetComponent<MeshCollider>();
            Renderer renderer = child.GetComponent<Renderer>();

            bool ours = filter != null && filter.sharedMesh != null && PlatformMesh.IsOurs(filter.sharedMesh);
            Check("live." + platforms + " mesh", ours,
                filter == null || filter.sharedMesh == null ? "нет меша" : filter.sharedMesh.name);

            // Коллайдер — тот же куб, что давал примитив: единичный локальный
            // размер при localScale = size даёт ровно прежнюю коробку для
            // OverlapCheck и расчётов PlatformFit.
            Check("live." + platforms + " box", box != null && meshCol == null && box.size == Vector3.one,
                box == null ? "нет BoxCollider" : ("size " + box.size + ", meshCol " + (meshCol != null)));

            // Префаб площадки в проекте не участвует: тело строится кодом, и
            // Renderer обязан получить оба подмеша и оба материала к ним. Один
            // материал на два подмеша оставил бы площадку без борта.
            bool two = ours && filter.sharedMesh.subMeshCount == PlatformMesh.SubmeshCount;
            if (two) twoSubmeshes++;
            bool mats = renderer != null && renderer.sharedMaterials.Length == PlatformMesh.SubmeshCount &&
                        renderer.sharedMaterials[0] != null && renderer.sharedMaterials[1] != null;
            if (two && mats) paired++;
            if (two && mats && renderer.sharedMaterials[0] != renderer.sharedMaterials[1] &&
                renderer.sharedMaterials[1].mainTexture == Textures.Albedo(Textures.Kind.Rock))
                grassUnderRock++;

            if (ours)
            {
                int profileOf = PlatformMesh.ProfileOf(filter.sharedMesh);
                int styleOf = PlatformMesh.StyleOf(filter.sharedMesh);

                if (profileOf > 0) cut++;
                if (profileOf >= 0) used.Add(profileOf);
                if (styleOf >= 0) styles.Add(styleOf);

                // Вписанность: меш единичный (±0.5 локально), и коллайдер —
                // тоже единичный куб в той же локальной системе; localScale
                // растягивает их одинаково. Сравнение идёт в локальных единицах,
                // сравнивать с localScale нельзя — это был бы двойной масштаб.
                Vector3 ext = filter.sharedMesh.bounds.extents;
                if (ext.x <= 0.5f + 0.002f && ext.y <= 0.5f + 0.002f && ext.z <= 0.5f + 0.002f)
                    inscribed++;
            }
        }

        Check("live.generated", platforms >= want - 20, "площадок " + platforms);
        Check("live.profiles used", used.Count >= 3, "профилей " + used.Count);
        Check("live.styles used", styles.Count >= 3, "силуэтов " + styles.Count);
        Check("live.corners cut", cut * 4 >= platforms, "со срезом " + cut + " из " + platforms);
        Check("live.inscribed", inscribed == platforms, "вписаны " + inscribed + " из " + platforms);
        Check("live.two submeshes", twoSubmeshes == platforms, "с двумя подмешами " + twoSubmeshes + " из " + platforms);
        Check("live.materials paired", paired == platforms, "с парой материалов " + paired + " из " + platforms);
        Check("live.grass under rock", grassUnderRock > 0, "травяных над камнем " + grassUnderRock);

        Object.DestroyImmediate(host);
    }

    // ---------------- помощники ----------------

    private static readonly Textures.Kind[] Kinds =
    {
        Textures.Kind.Rock, Textures.Kind.Wood, Textures.Kind.Metal,
        Textures.Kind.Snow, Textures.Kind.Grass, Textures.Kind.Crate
    };

    private static HashSet<int> StylesOf(Textures.Kind kind)
    {
        var seen = new HashSet<int>();
        for (int idx = 1; idx <= 300; idx++)
            seen.Add(PlatformMesh.PickStyle(kind, idx, 13337));
        return seen;
    }

    private static bool SubsetOf(HashSet<int> seen, params int[] allowed)
    {
        var ok = new HashSet<int>(allowed);
        foreach (int s in seen)
            if (!ok.Contains(s)) return false;
        return seen.Count > 0;
    }

    private static bool IsGeometricallyOutward(Mesh mesh)
    {
        Vector3[] v = mesh.vertices;
        int[] t = mesh.triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]];
            Vector3 b = v[t[i + 1]];
            Vector3 c = v[t[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            Vector3 centre = (a + b + c) / 3f;

            // Тело звёздное относительно центра, поэтому внешняя нормаль любой
            // грани смотрит в ту же сторону, что и радиус-вектор её середины.
            if (Vector3.Dot(n, centre) <= 0f) return false;
        }
        return true;
    }

    private static bool NoDegenerate(Mesh mesh)
    {
        Vector3[] v = mesh.vertices;
        int[] t = mesh.triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]];
            Vector3 b = v[t[i + 1]];
            Vector3 c = v[t[i + 2]];
            if (Vector3.Cross(b - a, c - a).magnitude < 1e-6f) return false;
        }
        return true;
    }

    /// <summary>Объём со знаком через сумму тетраэдров от начала координат.
    /// У замкнутого тела с верной намоткой он положителен при любом начале отсчёта.</summary>
    private static float SignedVolume(Mesh mesh)
    {
        Vector3[] v = mesh.vertices;
        int[] t = mesh.triangles;
        float sum = 0f;
        for (int i = 0; i < t.Length; i += 3)
            sum += Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]]));
        return sum / 6f;
    }

    private static float CornerSum(Mesh mesh)
    {
        float best = 0f;
        foreach (Vector3 v in mesh.vertices)
        {
            float s = Mathf.Abs(v.x) + Mathf.Abs(v.z);
            if (s > best) best = s;
        }
        return best;
    }

    private static string[] CutLine(int style)
    {
        string[] outLine = new string[PlatformMesh.ProfileCount];
        for (int p = 0; p < PlatformMesh.ProfileCount; p++)
            outLine[p] = CornerSum(PlatformMesh.Get(p, style)).ToString("0.00");
        return outLine;
    }

    private static bool InRange(int v, int lo, int hi)
    {
        return v >= lo && v <= hi;
    }

    private static int Variety(int shape)
    {
        var seen = new HashSet<int>();
        for (int idx = 1; idx <= 300; idx++)
            seen.Add(PlatformMesh.Pick(shape, idx, 13337));
        return seen.Count;
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
