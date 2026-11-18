using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Меши площадок. Раньше площадка была примитивом-кубом, и уровень читался
/// одинаковыми квадратами, как ни разводи их по размеру и повороту. Здесь куб
/// заменён телом со своим силуэтом сбоку: сверху это прямоугольник или
/// восьмиугольник со срезанными углами, снизу — обрыв, уступы или шпиль.
///
/// Меш единичный — от -0.5 до 0.5 по всем осям. Настоящий размер задаёт
/// localScale площадки, а коллайдер остаётся тем же кубом того же размера.
/// Поэтому вся арифметика подбора места (PlatformFit) и проверки наложений
/// остаются верными: меш вписан в коробку коллайдера и нигде её не превышает.
/// Углы площадки при этом «пустые» — по ним виден срез, а стоять всё равно
/// можно, потому что коллайдер там есть. Это безопасная сторона ошибки: игрок
/// никогда не проваливается сквозь то, что видит.
///
/// Вниз тело только сужается, никогда не расширяется. Это не только вопрос
/// вида: у стоящих друг над другом площадок зазор по высоте считается по
/// толще коллайдера, и раздуйся низ за её пределы — площадки начали бы
/// прорастать друг сквозь друга, а подбор места об этом не знал бы.
///
/// Два подмеша вместо одного: крышка (верх) и всё остальное (борт, уступы,
/// низ). Крышка — это то, по чему ходят, и она должна читаться отдельно от
/// обрыва: трава или снег сверху, порода снизу.
///
/// Профиль и силуэт выбираются хэшем от номера площадки, а не розыгрышем:
/// розыгрыш сдвинул бы последовательность _rng генератора и вместе с ней всю
/// трассу, а трасса заучивается игроком.
/// </summary>
public static class PlatformMesh
{
    /// <summary>Профилей плана: 0 — прямоугольник, дальше восьмиугольники с растущим срезом.</summary>
    public const int ProfileCount = 6;

    /// <summary>Силуэтов сбоку.</summary>
    public const int StyleCount = 5;

    /// <summary>Подмешей два: крышка и всё остальное.</summary>
    public const int SubmeshCount = 2;

    /// <summary>Номер подмеша с крышкой.</summary>
    public const int TopSubmesh = 0;

    /// <summary>Номер подмеша с бортом, уступами и низом.</summary>
    public const int WallSubmesh = 1;

    public enum Style
    {
        /// <summary>Ровная плита: доски, листы, ящики. Ровно прежняя призма.</summary>
        Slab = 0,

        /// <summary>Плавучий остров: бортик, наклонный обрыв, уступ и маленький низ.</summary>
        Island = 1,

        /// <summary>Он же, но обрыв глубже, а уступов два.</summary>
        Deep = 2,

        /// <summary>Терраса: площадка сходит двумя ступенями.</summary>
        Terrace = 3,

        /// <summary>Шпиль: плавный конус к узкому низу.</summary>
        Spire = 4
    }

    /// <summary>Доля полустороны, отрезаемая на каждом углу. Ноль — обычный
    /// прямоугольник; 0,46 — почти круглая в плане площадка.</summary>
    private static readonly float[] Cuts = { 0f, 0.14f, 0.22f, 0.30f, 0.38f, 0.46f };

    /// <summary>
    /// Точка силуэта: высота и доля полустороны плана на этой высоте.
    /// Кольца идут сверху вниз; равные высоты двух соседних колец означают
    /// уступ — горизонтальный выступ, на котором тело ступенькой сужается.
    /// </summary>
    private struct Ring
    {
        public readonly float Y;
        public readonly float Scale;

        public Ring(float y, float scale)
        {
            Y = y;
            Scale = scale;
        }
    }

    /// <summary>
    /// Силуэты сбоку, сверху вниз. Общее правило: первое кольцо — крышка во всю
    /// полусторону (по ней ходят, и по ней же меш касается коробки коллайдера),
    /// дальше полусторона только убывает. Уступы стоят ниже середины: выступ,
    /// смотрящий вниз выше середины, читался бы как поднутрение, то есть как
    /// ошибка геометрии, а не как форма.
    /// </summary>
    private static readonly Ring[][] Silhouettes =
    {
        // Slab: стенки строго вертикальные, низ плоский.
        new[]
        {
            new Ring(0.50f, 1f), new Ring(-0.50f, 1f)
        },

        // Island: узкий бортик, обрыв, один уступ, плоское дно в треть ширины.
        new[]
        {
            new Ring(0.50f, 1f), new Ring(0.34f, 1f),
            new Ring(-0.04f, 0.74f), new Ring(-0.04f, 0.64f),
            new Ring(-0.50f, 0.34f)
        },

        // Deep: бортик повыше и два уступа — низ уходит почти в пятую часть.
        new[]
        {
            new Ring(0.50f, 1f), new Ring(0.38f, 1f),
            new Ring(-0.06f, 0.70f), new Ring(-0.06f, 0.58f),
            new Ring(-0.32f, 0.42f), new Ring(-0.32f, 0.30f),
            new Ring(-0.50f, 0.20f)
        },

        // Terrace: две явные ступени, между ними почти отвесные стенки.
        new[]
        {
            new Ring(0.50f, 1f), new Ring(0.30f, 1f),
            new Ring(-0.04f, 0.78f), new Ring(-0.04f, 0.66f),
            new Ring(-0.28f, 0.50f), new Ring(-0.28f, 0.40f),
            new Ring(-0.50f, 0.26f)
        },

        // Spire: один длинный скос к узкому плоскому низу.
        new[]
        {
            new Ring(0.50f, 1f), new Ring(0.36f, 0.94f),
            new Ring(-0.24f, 0.44f), new Ring(-0.50f, 0.16f)
        }
    };

    private static readonly Mesh[] Cache = new Mesh[ProfileCount * StyleCount];

    /// <summary>Сколько полос (стенок и уступов) у силуэта.</summary>
    public static int BandCount(int style)
    {
        return Silhouettes[ClampStyle(style)].Length - 1;
    }

    /// <summary>Меш профиля силуэта «плита». Оставлено ради площадки-подложки
    /// и проверок, которым нужен ровный низ.</summary>
    public static Mesh Get(int profile)
    {
        return Get(profile, (int)Style.Slab);
    }

    /// <summary>Меш пары «профиль плана + силуэт». Строится один раз и делится
    /// всеми площадками с той же парой.</summary>
    public static Mesh Get(int profile, int style)
    {
        if (profile < 0) profile = 0;
        if (profile >= ProfileCount) profile = ProfileCount - 1;
        style = ClampStyle(style);

        int index = style * ProfileCount + profile;
        Mesh mesh = Cache[index];
        if (mesh == null)
        {
            mesh = Build(profile, Cuts[profile], style);
            Cache[index] = mesh;
        }
        return mesh;
    }

    /// <summary>
    /// Строит все сочетания профиля и силуэта заранее. Их всего тридцать и они
    /// крошечные; зато при подъёме новая форма площадки больше не вызывает
    /// Build в игровом кадре.
    /// </summary>
    public static void Prewarm()
    {
        for (int style = 0; style < StyleCount; style++)
            for (int profile = 0; profile < ProfileCount; profile++)
                Get(profile, style);
    }

    /// <summary>Принадлежит ли меш кэшу PlatformMesh. Проверки по этому узнают
    /// площадку, не сверяя имена и не разбирая её содержимое.</summary>
    public static bool IsOurs(Mesh mesh)
    {
        return IndexOf(mesh) >= 0;
    }

    /// <summary>Профиль плана у меша из кэша, или -1, если меш чужой.</summary>
    public static int ProfileOf(Mesh mesh)
    {
        int index = IndexOf(mesh);
        return index < 0 ? -1 : index % ProfileCount;
    }

    /// <summary>Силуэт меша из кэша, или -1, если меш чужой.</summary>
    public static int StyleOf(Mesh mesh)
    {
        int index = IndexOf(mesh);
        return index < 0 ? -1 : index / ProfileCount;
    }

    private static int IndexOf(Mesh mesh)
    {
        if (mesh == null) return -1;
        for (int i = 0; i < Cache.Length; i++)
            if (Cache[i] == mesh) return i;
        return -1;
    }

    /// <summary>
    /// Профиль для площадки. Определяется формой, номером и сидом; номер 0
    /// (стартовая «земля») всегда прямоугольник. Диапазоны срезов подобраны под
    /// то, как площадка обязана читаться: доска — это доска, а маленькие
    /// «таблетки» — почти круглые.
    /// </summary>
    public static int Pick(int shape, int index, int seed)
    {
        if (index == 0) return 0;

        int lo, hi;
        switch (shape)
        {
            case 3: lo = 0; hi = 1; break; // доска: срез лишь чтобы грани не были клоном
            case 2: lo = 0; hi = 2; break; // широкая
            case 1: lo = 3; hi = 5; break; // маленькая: почти круглая
            default: lo = 2; hi = 4; break; // квадрат
        }

        int h = Hash(unchecked(index * 73856093 + seed * 19349663));
        return lo + h % (hi - lo + 1);
    }

    /// <summary>
    /// Силуэт для площадки: чем покрыта крышка, тем и подсказан обрыв. Природное
    /// (трава, снег, камень) стоит на островах, террасах и шпилях, рукотворное
    /// (доски, листы, ящики) — ровными плитами: у доски, сходящейся к низу
    /// конусом, пропадает силуэт доски, а её узнают по силуэту.
    ///
    /// Разнообразие внутри вида берётся тем же хэшем, что и профиль, и тоже без
    /// розыгрыша: иначе сдвинулся бы поток генератора и вместе с ним трасса.
    /// </summary>
    public static int PickStyle(Textures.Kind kind, int index, int seed)
    {
        int h = Hash(unchecked(index * 83492791 + seed * 265443576 + (int)kind * 1274126177));

        switch (kind)
        {
            case Textures.Kind.Grass:
                return (h & 1) == 0 ? (int)Style.Island : (int)Style.Deep;

            case Textures.Kind.Snow:
                if (h % 3 == 0) return (int)Style.Terrace;
                return (h & 1) == 0 ? (int)Style.Island : (int)Style.Deep;

            case Textures.Kind.Rock:
                return (h % 5) < 3 ? (int)Style.Spire : (int)Style.Terrace;

            // Доска, лист и ящик остаются плитами в любом биоме.
            default:
                return (int)Style.Slab;
        }
    }

    private static int ClampStyle(int style)
    {
        if (style < 0) return 0;
        if (style >= StyleCount) return StyleCount - 1;
        return style;
    }

    private static int Hash(int value)
    {
        unchecked
        {
            uint h = (uint)value;
            h ^= h >> 16;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return (int)(h & 0x7fffffffu);
        }
    }

    /// <summary>План площадки в единичном квадрате. Срез 0 — прямоугольник из
    /// четырёх точек, иначе восьмиугольник со срезанными углами.</summary>
    private static Vector2[] Plan(float cut)
    {
        const float h = 0.5f;
        float c = h * cut;

        if (cut <= 0f)
        {
            return new[]
            {
                new Vector2(h, -h), new Vector2(h, h), new Vector2(-h, h), new Vector2(-h, -h)
            };
        }

        return new[]
        {
            new Vector2(h - c, -h),
            new Vector2(h, -h + c),
            new Vector2(h, h - c),
            new Vector2(h - c, h),
            new Vector2(-h + c, h),
            new Vector2(-h, h - c),
            new Vector2(-h, -h + c),
            new Vector2(-h + c, -h)
        };
    }

    /// <summary>
    /// Тело из плана и силуэта: крышка, полосы между кольцами, низ. Вершины у
    /// каждой грани свои, а не общие: так нормали остаются плоскими и площадка
    /// выглядит гранёной, а не оплывшей. UV крышки планарные в диапазоне [0,1]
    /// и по плотности совпадают с кубом; UV полос сквозные по высоте, поэтому
    /// текстура течёт по обрыву и уступам без разрывов. Масштаб тайлинга
    /// досчитывает материал.
    /// </summary>
    private static Mesh Build(int profile, float cut, int style)
    {
        Ring[] rings = Silhouettes[style];
        Vector2[] plan = Plan(cut);
        int n = plan.Length;
        int bands = rings.Length - 1;

        var verts = new List<Vector3>(2 * (n + 1) + 4 * n * bands);
        var uvs = new List<Vector2>(2 * (n + 1) + 4 * n * bands);
        var top = new List<int>(3 * n);
        var rest = new List<int>(6 * n * bands + 3 * n);

        AddCap(verts, uvs, top, plan, rings[0].Y, rings[0].Scale, true);
        for (int i = 0; i < bands; i++)
            AddBand(verts, uvs, rest, plan, rings[i], rings[i + 1]);
        AddCap(verts, uvs, rest, plan, rings[bands].Y, rings[bands].Scale, false);

        Mesh mesh = new Mesh();
        mesh.name = "Platform" + profile + "_" + style;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = SubmeshCount;
        mesh.SetTriangles(top, TopSubmesh);
        mesh.SetTriangles(rest, WallSubmesh);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Страховка от изнаночной намотки: у замкнутого тела изнаночная
        // геометрия единственна, и весь меш разворачивается разом. Если намотка
        // верна, этот проход ничего не делает — тело проходит IsOutward целиком.
        if (!IsOutward(mesh))
        {
            top.Reverse();
            rest.Reverse();
            mesh.SetTriangles(top, TopSubmesh);
            mesh.SetTriangles(rest, WallSubmesh);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
        return mesh;
    }

    private static void AddCap(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
        Vector2[] plan, float y, float scale, bool top)
    {
        int n = plan.Length;
        int centre = verts.Count;
        verts.Add(new Vector3(0f, y, 0f));
        uvs.Add(new Vector2(0.5f, 0.5f));

        int ring = verts.Count;
        for (int i = 0; i < n; i++)
        {
            float x = plan[i].x * scale;
            float z = plan[i].y * scale;
            verts.Add(new Vector3(x, y, z));
            uvs.Add(new Vector2(x + 0.5f, z + 0.5f));
        }

        for (int i = 0; i < n; i++)
        {
            int a = ring + i;
            int b = ring + (i + 1) % n;
            if (top) { tris.Add(centre); tris.Add(b); tris.Add(a); }
            else { tris.Add(centre); tris.Add(a); tris.Add(b); }
        }
    }

    /// <summary>
    /// Полоса между двумя кольцами. Высоты разные — наклонная стенка, одинаковые —
    /// горизонтальный уступ. Верхнее кольцо приходит первым: обход вершин и
    /// намотка те же, что были у вертикальной стенки призмы, поэтому нормаль
    /// смотрит наружу и вниз — то есть на обрыв снизу, а не внутрь тела.
    /// </summary>
    private static void AddBand(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
        Vector2[] plan, Ring upper, Ring lower)
    {
        int n = plan.Length;
        float vUpper = upper.Y + 0.5f;
        float vLower = lower.Y + 0.5f;

        for (int i = 0; i < n; i++)
        {
            Vector2 a = plan[i];
            Vector2 b = plan[(i + 1) % n];

            int p0 = verts.Count;
            verts.Add(new Vector3(a.x * lower.Scale, lower.Y, a.y * lower.Scale));
            verts.Add(new Vector3(b.x * lower.Scale, lower.Y, b.y * lower.Scale));
            verts.Add(new Vector3(b.x * upper.Scale, upper.Y, b.y * upper.Scale));
            verts.Add(new Vector3(a.x * upper.Scale, upper.Y, a.y * upper.Scale));

            uvs.Add(new Vector2(0f, vLower));
            uvs.Add(new Vector2(1f, vLower));
            uvs.Add(new Vector2(1f, vUpper));
            uvs.Add(new Vector2(0f, vUpper));

            AddTri(tris, p0 + 0, p0 + 3, p0 + 2);
            AddTri(tris, p0 + 0, p0 + 2, p0 + 1);
        }
    }

    private static void AddTri(List<int> tris, int a, int b, int c)
    {
        tris.Add(a);
        tris.Add(b);
        tris.Add(c);
    }

    /// <summary>
    /// Все ли грани смотрят наружу. Тело площадки устроено так, что полусторона
    /// плана не растёт книзу, поэтому оно звёздное относительно своего центра:
    /// каждый кусок границы виден из центра, и внешняя нормаль любого треугольника
    /// составляет с направлением на его середину положительное произведение.
    ///
    /// Прежняя проверка — «крышка по знаку y, стенка по горизонтали» — годилась
    /// только для выпуклого тела: уступ и наклонный низ она принимала за ошибку
    /// намотки и разворачивала правильный меш наизнанку.
    /// </summary>
    public static bool IsOutward(Mesh mesh)
    {
        Vector3[] v = mesh.vertices;
        int[] t = mesh.triangles;
        if (t.Length == 0) return false;

        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]];
            Vector3 b = v[t[i + 1]];
            Vector3 c = v[t[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            Vector3 centre = (a + b + c) / 3f;
            if (Vector3.Dot(n, centre) <= 0f) return false;
        }
        return true;
    }
}
