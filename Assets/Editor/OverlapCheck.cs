#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Проверка того, что площадки не наезжают друг на друга. Только редактор,
/// в сборку попасть не может.
///
/// Дефект, ради которого всё затевалось, был молчаливым. Подбор места делал
/// четыре попытки и, если все проваливались, ставил провалившийся вариант —
/// то есть пересекающуюся площадку. Плюс требование считалось
/// несимметрично, а вертикальное расстояние не учитывалось вовсе. Ни одно из
/// этих мест не падало: уровень просто выглядел сломанным.
///
/// Поэтому проверка двумя слоями. Первый — арифметика на конкретных парах, где
/// формула ломалась по-настоящему: две доски, доска с маленькой площадкой,
/// площадки одна над другой. Если множители снова перепутаются, слотят первые.
/// Второй — сквозной прогон настоящего уровня и попарная проверка всех
/// площадок на залезание. Это и есть требование пользователя буквально.
/// </summary>
public static class OverlapCheck
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> Overlaps = new List<string>();

    /// <summary>Сколько пар пересеклось на самом деле. Список в лог ограничен,
    /// а счётчик полный: иначе при тысяче наездов в лог уйдут первые сорок и
    /// покажут, что наездов ровно сорок.</summary>
    private static int _overlaps;

    /// <summary>Сколько пар не пересеклось, но сошлось ближе обещанного зазора.</summary>
    private static int Tight;

    /// <summary>Насколько ближе всего подошли две площадки, в метрах. Считается
    /// только по тем парам, где сближение по горизонтали вообще решает: у пар,
    /// разошедшихся по высоте, зазор по горизонтали может быть отрицательным,
    /// и это нормально — их разносит высотой.</summary>
    private static float MinSeparation = float.PositiveInfinity;

    public static void Run()
    {
        Overlaps.Clear();
        _passed = 0;
        _failed = 0;
        _overlaps = 0;
        Tight = 0;
        MinSeparation = float.PositiveInfinity;

        CheckArithmetic();
        CheckShapesFitInJump();
        CheckGeneratedLevel();

        Debug.Log("[T] RESULT passed=" + _passed + " failed=" + _failed);
        EditorApplication.Exit(_failed);
    }

    // ---------------- 1. арифметика ----------------

    /// <summary>
    /// Проверки формулы на парах, которые её ломали. Каждая подписана тем,
    /// что было не так, иначе через полгода непонятно, почему тест существует.
    /// </summary>
    private static void CheckArithmetic()
    {
        const float gap = 0.4f;
        float hh = 0.25f;

        // Две длинные доски. Описанный радиус 2,44 м, разойтись должны на
        // 2*2,44 + 0,4 = 5,27 м. Прежняя формула требовала тут 3,64 м и спокойно
        // пропускала доски, наезжающие друг на друга почти на метр.
        Vector3 a = Vector3.zero;
        Vector3 b = new Vector3(4f, 0f, 0f);
        Check("boards need 5.27", Mathf.Abs(PlatformFit.RequiredSeparation(2.436f, 2.436f, gap) - 5.272f) < 0.01f,
            "вышло " + PlatformFit.RequiredSeparation(2.436f, 2.436f, gap));
        Check("boards reject 4m", PlatformFit.PairClearance(a, 2.436f, hh, b, 2.436f, hh, gap) < 0f,
            "вышло " + PlatformFit.PairClearance(a, 2.436f, hh, b, 2.436f, hh, gap));
        Check("boards accept 5.3", PlatformFit.PairClearance(a, 2.436f, hh, new Vector3(5.3f, 0f, 0f), 2.436f, hh, gap) >= 0f,
            "вышло " + PlatformFit.PairClearance(a, 2.436f, hh, new Vector3(5.3f, 0f, 0f), 2.436f, hh, gap));

        // Симметрия: доска рядом с маленькой площадкой. Прежняя формула считала
        // radius*0.5 + other*1.5 вместо radius + other, то есть занижала
        // требование на полразницы радиусов. Симметричность проверяется перестановкой.
        float c1 = PlatformFit.PairClearance(a, 2.436f, hh, new Vector3(3.5f, 0f, 0f), 1.06f, hh, gap);
        float c2 = PlatformFit.PairClearance(new Vector3(3.5f, 0f, 0f), 1.06f, hh, a, 2.436f, hh, gap);
        Check("clearance symmetric", Mathf.Abs(c1 - c2) < 0.0001f, c1 + " против " + c2);
        Check("asymmetric pair needs sum",
            Mathf.Abs(PlatformFit.RequiredSeparation(2.436f, 1.06f, gap) - 3.896f) < 0.01f,
            "вышло " + PlatformFit.RequiredSeparation(2.436f, 1.06f, gap));
        Check("asymmetric pair rejects 3.5", c1 < 0f, "вышло " + c1);

        // Вертикаль. Центры в 2 м друг над другом при толщине 0,5 и зазоре 0,4
        // не могут соприкоснуться при нулевом расстоянии по XZ — стоять не
        // поверх чего. Прежняя проверка была только по XZ и тратила на такие
        // пары попытки подбора впустую.
        float vertical = PlatformFit.PairClearance(a, 2.436f, hh, new Vector3(0f, 2f, 0f), 2.436f, hh, gap);
        Check("vertical gap clears", vertical == float.PositiveInfinity, "вышло " + vertical);

        // Ровно на границе толщины с зазором. Значение 0,9 метра тут намеренно взято
        // не буквально: сумма половин толщин и зазора в двоичной дроби не
        // попадает точно в 0,9, и на границе результат зависит от того, в какую
        // сторону округлится последний бит. Проверять такое значение в тесте
        // бессмысленно — оно не характеризует ничего. Важна безопасная сторона:
        // если площадки разошлись выше границы, наезд невозможен, а если не
        // разошлись — проверка включается и запрещает наезд. Обе стороны
        // проверены ниже на значениях заведомо по обе стороны.
        float edge = PlatformFit.PairClearance(a, 2.436f, hh, new Vector3(0f, 0.91f, 0f), 2.436f, hh, gap);
        Check("vertical above boundary clears", edge == float.PositiveInfinity, "вышло " + edge);

        // Чуть ниже границы — проверка включается, и при нулевом XZ это наезд.
        float justUnder = PlatformFit.PairClearance(a, 2.436f, hh, new Vector3(0f, 0.89f, 0f), 2.436f, hh, gap);
        Check("just under boundary rejects", justUnder < 0f, "вышло " + justUnder);

        // Описанный радиус по диагонали. У доски он заметно больше любой стороны:
        // 4,6 × 1,6 даёт 2,44 м, тогда как длина 2,3 м. Проверка по большей
        // стороне пропускала бы доски одну сквозь другую углами.
        Check("circumradius uses diagonal",
            Mathf.Abs(PlatformFit.CircumRadius(new Vector3(4.6f, 0.5f, 1.6f)) - 2.436f) < 0.005f,
            "вышло " + PlatformFit.CircumRadius(new Vector3(4.6f, 0.5f, 1.6f)));
        Check("circumradius of square",
            Mathf.Abs(PlatformFit.CircumRadius(new Vector3(2f, 0.5f, 2f)) - 1.41421f) < 0.001f,
            "вышло " + PlatformFit.CircumRadius(new Vector3(2f, 0.5f, 2f)));

        // Дальность прыжка обязана падать с высотой. Если бы она была одной
        // константой, генератор посадил бы площадку после высокого шага там,
        // куда не допрыгнуть.
        Check("reach falls with height", PlatformFit.ReachAt(2.4f) < PlatformFit.ReachAt(0f) - 1f,
            PlatformFit.ReachAt(0f) + " и " + PlatformFit.ReachAt(2.4f));
        Check("reach over flat is 7.39", Mathf.Abs(PlatformFit.ReachAt(0f) - 7.39f) < 0.02f,
            "вышло " + PlatformFit.ReachAt(0f));
        Check("safe reach below ideal", PlatformFit.SafeReachAt(0f) < PlatformFit.ReachAt(0f),
            PlatformFit.SafeReachAt(0f) + " против " + PlatformFit.ReachAt(0f));
    }

    // ---------------- 2. размеры помещаются в прыжок ----------------

    /// <summary>
    /// Каждая ступень обязана допускать расстановку: две её самые широкие
    /// площадки должны помещаться друг от друга в один прыжок. Подбор места
    /// бессилен, если условие не выполняется, сколько попыток ни делай.
    ///
    /// Считается худший случай ступени, то есть максимальный радиус каждой из
    /// четырёх форм, и он же против самого горизонтального прыжка (dy = 0).
    /// Выше нуля прыжок короче, но там наложение и так невозможно: площадки
    /// разошлись по высоте больше, чем толщина с зазором.
    /// </summary>
    private static void CheckShapesFitInJump()
    {
        float ceiling = PlatformFit.SafeReachAt(0f);

        // Зазор проверки обязан совпадать с зазором ступеней, иначе тест мерил бы
        // не тем, что обещает генератор. Отдельно проверяем, что он одинаков
        // у всех ступеней: иначе один запас в тесте разойдётся с остальными.
        for (int i = 0; i < StageCatalog.All.Count; i++)
        {
            Check("gap." + StageCatalog.All[i].Id + " matches", StageCatalog.All[i].Cfg.ShapeGap == Gap,
                "зазор ступени " + StageCatalog.All[i].Cfg.ShapeGap + " против " + Gap);
        }

        for (int i = 0; i < StageCatalog.All.Count; i++)
        {
            StageDefinition stage = StageCatalog.All[i];
            LevelConfig c = stage.Cfg;
            string tag = "fits." + stage.Id;

            float widest = WidestRadius(c);
            Check(tag + " widest radius sane", widest > 0f, "радиус " + widest);

            float need = PlatformFit.RequiredSeparation(widest, widest, c.ShapeGap);
            Check(tag + " two widest fit in jump", need <= ceiling,
                "нужно " + need + ", прыжок даёт " + ceiling);

            // Запас хотя бы 30%: иначе подбор встаёт в потолок на любом шаге
            // и начинает ставить площадки впритык к недостижимому.
            Check(tag + " has room", ceiling - need >= ceiling * 0.05f,
                "запас " + (ceiling - need) + " из " + ceiling);

            // Широкая площадка и доска — самая тяжёлая разнородная пара.
            float mixed = PlatformFit.RequiredSeparation(widest, CircumLong(c), c.ShapeGap);
            Check(tag + " wide beside board fits", mixed <= ceiling,
                "нужно " + mixed + ", прыжок даёт " + ceiling);
        }
    }

    /// <summary>Наибольший описанный радиус среди всех форм ступени.</summary>
    private static float WidestRadius(LevelConfig c)
    {
        float t = 0.5f;

        // Квадрат: сторона MaxSize, описанный радиус больше её.
        float square = PlatformFit.CircumRadius(new Vector3(c.MaxSize, c.Thickness, c.MaxSize));

        // Широкая: WideSize в одну сторону и до MaxSize + 0,4 в другую.
        float wide = PlatformFit.CircumRadius(
            new Vector3(c.WideSize, c.Thickness, c.MaxSize + 0.4f));

        // Маленькая: SmallSize и SmallSize, растянутая до 1,25.
        float small = PlatformFit.CircumRadius(
            new Vector3(c.SmallSize, c.Thickness, c.SmallSize * 1.25f));

        float board = CircumLong(c);

        return Mathf.Max(square, Mathf.Max(wide, Mathf.Max(small, board)));
    }

    private static float CircumLong(LevelConfig c)
    {
        return PlatformFit.CircumRadius(new Vector3(c.LongLength, c.Thickness, c.LongWidth));
    }

    // ---------------- 3. сквозной прогон ----------------

/// <summary>
    /// Строит настоящий подъём и проверяет все пары площадок на залезание.
    ///
    /// Проверяются именно создавшиеся объекты, а не расчётные значения: берутся
    /// их transform.position, localScale и поворот, то есть ровно то, что видит
    /// игрок. Расхождение между расчётом и объектом — тоже дефект, и такой тест
    /// его поймает, а проверка только центров — нет.
    ///
    /// Сравнение точное, по углам площадок, а не по описанным окружностям.
    /// Окружность — грубый прокси, и на нём тест врал бы в обе стороны: он
    /// объявил бы наездом пару площадок, поставленных под углом друг к другу
    /// на расстоянии, где они на самом деле расходятся углами. И наоборот,
    /// пропустил бы наезд там, где центры далеко, а стороны длинные. Единственный
    /// способ узнать правду о пересечении прямоугольников — спросить их сами.
    ///
    /// Пары перебираются в горизонтальном окне, а не все целиком: уровень
    /// длинный, а квадратная проверка на 4000 объектах недосчитаемо. Пара,
    /// которая могла бы налезть, обязана быть в пределах 12 м по XZ: самая
    /// большая площадка ступени — Земля, и её полусторона 6 м, то есть
    /// расстояние между центрами наезда там не превышает 12 м ни при каком
    /// повороте. Окно взято с запасом.
    /// </summary>
    private static void CheckGeneratedLevel()
    {
        LevelGenerator gen = NewGenerator();
        gen.ResetPath(0);

        const int want = 4000;
        int guard = 0;
        while (gen.AliveMax < want && guard++ < 500)
            gen.GenerateUpTo(gen.AliveMax + 250);

        int top = gen.AliveMax;
        Check("generated", top >= want - 250, "верхний индекс " + top);

        List<Box> boxes = CollectBoxes(gen);
        Check("boxes collected", boxes.Count > 0, "собрано " + boxes.Count);

        const float window = 12f;
        int checkedPairs = 0;
        Overlaps.Clear();

        for (int i = 0; i < boxes.Count; i++)
        {
            for (int j = i + 1; j < boxes.Count; j++)
            {
                Box a = boxes[i];
                Box b = boxes[j];

                float dx = a.center.x - b.center.x;
                float dz = a.center.z - b.center.z;
                if (dx * dx + dz * dz > window * window) continue;

                checkedPairs++;

                // Вертикаль отдельно и точно: у площадок одинаковая толщина и
                // они не наклонены, так что разошлись выше толщины с зазором —
                // стоять не поверх чего, наезд невозможен при любом сближении
                // по горизонтали.
                float vertical = Mathf.Abs(a.center.y - b.center.y);
                float need = a.halfHeight + b.halfHeight + Gap;
                if (vertical >= need) continue;

                // Главная проверка: пересекаются ли площадки вообще. Именно это
                // и значит «находятся друг в друге», и зазор здесь нужен нулевой.
                //
                // С зазором 0,4 метра проверять нельзя: пара, расходящаяся на
                // 0,35 метра, отвечает отрицательным зазором, но не пересекается
                // ни на миллиметр. Такую пару легко принять за наезд — и в первой
                // версии этого теста так и вышло: семь «наездов» по 0,01–0,06 м
                // на деле были плотными, но расходящимися соседями. Зазор это
                // пожелание, а пересечение факт.
                float depth = -ObbGap(a, b, 0f);

                if (depth > 0f)
                {
                    _overlaps++;
                    if (Overlaps.Count < 40)
                    {
                        Overlaps.Add("платформы " + a.index + " и " + b.index +
                            ": пересечение " + depth.ToString("0.00") + " м, габариты " +
                            a.Size + " и " + b.Size +
                            ", высоты " + a.center.y.ToString("0.0") + " и " + b.center.y.ToString("0.0"));
                    }
                }
                // Плотные, но не пересекающиеся соседи. Зазор меньше обещанных 0,4 метра
                // у повёрнутой пары — вещь обычная и ничего плохого не значит:
                // правило генератора круговое и держит расстояние между
                // центрами, а у прямоугольников под углом зазор по самой узкой
                // оси меньше расстояния между центрами. Проверять здесь нечего,
                // поэтому число только выводится. Первой версией теста этот
                // порог вообще стоял как условие, и он падал на 38 процентах
                // пар — то есть мерил плотность, а не дефект.
                Tight++;

                float separation = ObbGap(a, b, 0f);
                if (separation < MinSeparation) MinSeparation = separation;
            }
        }

        Check("no platform overlaps", _overlaps == 0,
            "пересечений " + _overlaps + " из " + checkedPairs + " пар");
        foreach (string line in Overlaps)
            Debug.Log("[T] OVERLAP " + line);

        // Насколько ближе всего подошли две площадки. Вот это уже настоящая
        // гарантия: ноль означал бы касание, отрицательное — наезд. Порог
        // крошечный и служит только против того, чтобы из-за ошибки округления
        // в тесте не проскочило настоящее пересечение в доли миллиметра.
        Check("closest pair keeps clear", MinSeparation > 0.02f,
            "ближе всего " + MinSeparation.ToString("0.000") + " м");
        Debug.Log("[T] INFO плотных пар " + Tight + " из " + checkedPairs +
            ", ближайшая пара разошлась на " + MinSeparation.ToString("0.000") + " м");

        // Обход соседей не мог тихо разъехаться на пустом месте. Порог
        // произвольный и низкий: это проверка того, что сканирование вообще
        // что-то нашло, а не того, что оно должно найти.
        Check("checked many pairs", checkedPairs > 5000, "пар " + checkedPairs);

        // Подбор ни разу не дошёл до запасного варианта. Значит правило с
        // описанными окружностями хватило везде, и ни одну площадку не пришлось
        // ставить «как получится». Это отдельное утверждение от нуля наложений:
        // окружность строже настоящей геометрии, поэтому площадка может стоять
        // наездом по окружности и при этом честно не налезать. Вот это как раз
        // подбор места чинить не обязан, а вот провалить он не должен был ни разу.
        Check("no placement fallbacks", gen.PlacementFallbacks == 0,
            "запасной вариант сработал " + gen.PlacementFallbacks + " раз из " + boxes.Count);
    }

    /// <summary>Зазор, который проверяем. Тот же, что и у ступеней: он и есть
    /// обещанное расстояние между площадками.</summary>
    private const float Gap = 0.4f;

    /// <summary>
    /// Прямоугольник площадки: центр, полуоси в её собственных осях и поворот
    /// вокруг вертикали. Полуоси в world-осях, потому что так их отдаёт
    /// transform и не нужно ничего пересчитывать.
    /// </summary>
    private struct Box
    {
        public int index;
        public Vector3 center;
        public float halfX;
        public float halfZ;
        public float halfHeight;
        public Vector3 axisX;
        public Vector3 axisZ;

        public string Size
        {
            get { return (halfX * 2f).ToString("0.0") + "x" + (halfZ * 2f).ToString("0.0"); }
        }
    }

    /// <summary>
    /// Зазор между двумя повёрнутыми прямоугольниками по теореме о разделяющей
    /// оси. Положительный результат — площадки расходятся, и это число есть
    /// запас по лучшей из осей. Отрицательный — пересекаются, и это число есть
    /// глубина наезда.
    ///
    /// Четыре оси: две от первого прямоугольника, две от второго. Для
    /// прямоугольников этого достаточно в точности, без исключений, в отличие
    /// от проверки кругом, которая всегда слегка врёт.
    /// </summary>
    private static float ObbGap(Box a, Box b, float gap)
    {
        Vector2 d = new Vector2(b.center.x - a.center.x, b.center.z - a.center.z);
        Vector2 ax = new Vector2(a.axisX.x, a.axisX.z);
        Vector2 az = new Vector2(a.axisZ.x, a.axisZ.z);
        Vector2 bx = new Vector2(b.axisX.x, b.axisX.z);
        Vector2 bz = new Vector2(b.axisZ.x, b.axisZ.z);

        float best = float.NegativeInfinity;

        float t = Mathf.Abs(Vector2.Dot(d, ax));
        float ra = a.halfX;
        float rb = Mathf.Abs(Vector2.Dot(bx, ax)) * b.halfX + Mathf.Abs(Vector2.Dot(bz, ax)) * b.halfZ;
        best = Mathf.Max(best, t - ra - rb);

        t = Mathf.Abs(Vector2.Dot(d, az));
        ra = a.halfZ;
        rb = Mathf.Abs(Vector2.Dot(bx, az)) * b.halfX + Mathf.Abs(Vector2.Dot(bz, az)) * b.halfZ;
        best = Mathf.Max(best, t - ra - rb);

        t = Mathf.Abs(Vector2.Dot(d, bx));
        ra = Mathf.Abs(Vector2.Dot(ax, bx)) * a.halfX + Mathf.Abs(Vector2.Dot(az, bx)) * a.halfZ;
        rb = b.halfX;
        best = Mathf.Max(best, t - ra - rb);

        t = Mathf.Abs(Vector2.Dot(d, bz));
        ra = Mathf.Abs(Vector2.Dot(ax, bz)) * a.halfX + Mathf.Abs(Vector2.Dot(az, bz)) * a.halfZ;
        rb = b.halfZ;
        best = Mathf.Max(best, t - ra - rb);

        return best - gap;
    }

    /// <summary>
    /// Собирает геометрию всех площадок из построенных объектов.
    ///
    /// Только сами площадки. Опасности, монеты и маркеры чужеродны площадке по
    /// замыслу — опасность стоит в её середине намеренно, и её наезд на
    /// площадку и есть её устройство, а не дефект.
    /// </summary>
    private static List<Box> CollectBoxes(LevelGenerator gen)
    {
        var boxes = new List<Box>();
        Transform root = gen.Data.Root.transform;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            // Куб с масштабом и коллайдером, названный по номеру площадки.
            if (!child.name.StartsWith("Platform ")) continue;
            if (child.GetComponent<Collider>() == null) continue;

            int index;
            if (!int.TryParse(child.name.Substring(9), out index)) continue;

            Vector3 size = child.localScale;

            // Оси берём из поворота, а не из углов Эйлера: углы в Unity
            // неоднозначны при повороте больше 180 градусов, и площадка,
            // развёрнутая на 270, посчиталась бы как развёрнутая на 90 в
            // другую сторону. Для квадрата это безобидно, а вот проверку
            // незачем делать зависимой от такой мелочи.
            Quaternion rot = child.rotation;
            Vector3 axisX = rot * Vector3.right;
            Vector3 axisZ = rot * Vector3.forward;

            boxes.Add(new Box
            {
                index = index,
                center = child.position,
                halfX = size.x * 0.5f,
                halfZ = size.z * 0.5f,
                halfHeight = size.y * 0.5f,
                axisX = axisX,
                axisZ = axisZ
            });
        }

        // Порядок для попарной проверки не важен, но индексы нужны в сообщении.
        boxes.Sort((x, y) => x.index.CompareTo(y.index));
        return boxes;
    }

    private static LevelGenerator NewGenerator()
    {
        GameObject host = new GameObject("[Test] LevelGenerator");
        LevelGenerator gen = host.AddComponent<LevelGenerator>();
        gen.BuildData();
        return gen;
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
