#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Автоматическая проверка ступеней подъёма. Живёт только в редакторе,
/// в сборку попасть не может.
///
/// Проверяет три вещи, которые ломают игру молча:
///
///  1. Досягаемость. Прыжок даёт потолок по высоте и по длине, и если ступень
///     выставит прыжки длиннее потолка, игрок упрётся не в сложность, а в
///     стену. Это самая дорогая ошибка из возможных: снаружи она не выглядит
///     как ошибка вообще.
///
///  2. Монотонность сложности. Площадки должны мельчать, дальность прыжка и
///     доля опасностей — расти. Иначе подъём то взлетает, то падает по
///     сложности, и это читается как «автор не знал, что делает».
///
///  3. Совпадение границ ступеней с полосами неба. Таблицы независимы, и
///     разъехаться они могут молча — особенно если править только одну.
///
/// Перед прогоном запоминает ключи, которых касается, и возвращает их:
/// проверка не должна затирать сохранение игрока.
/// </summary>
public static class StageCheck
{
    private static int _passed;
    private static int _failed;
    private static readonly Dictionary<string, string> Snapshot = new Dictionary<string, string>();

    public static int Check()
    {
        TakeSnapshot();
        try
        {
            CheckShape();
            CheckSkyAlignment();
            CheckReachability();
            CheckMonotonicDifficulty();
            CheckHazards();
            CheckCatalogLookup();
            CheckGeneratorAscends();
            CheckCheckpointMapping();
            CheckProgress();
        }
        finally
        {
            Restore();
        }

        Debug.Log("[T] RESULT passed=" + _passed + " failed=" + _failed);
        return _failed;
    }

    public static void Run()
    {
        EditorApplication.Exit(Check());
    }

    // ---------------- проверки ----------------

    /// <summary>Шесть ступеней, как шесть полос неба, и все разные.</summary>
    private static void CheckShape()
    {
        Check("count == 6", StageCatalog.Count == 6, "ступеней " + StageCatalog.Count);

        var ids = new HashSet<string>();
        var titles = new HashSet<string>();
        float previous = -1f;

        for (int i = 0; i < StageCatalog.All.Count; i++)
        {
            StageDefinition stage = StageCatalog.All[i];

            Check("id unique::" + stage.Id, ids.Add(stage.Id), "повтор");
            Check("title unique::" + stage.Id, titles.Add(stage.Title), "повтор: " + stage.Title);
            Check("title not empty::" + stage.Id, !string.IsNullOrEmpty(stage.Title), "пусто");
            Check("start rises::" + stage.Id, stage.StartHeight > previous,
                stage.StartHeight + " после " + previous);
            Check("cfg not null::" + stage.Id, stage.Cfg != null, "конфиг null");
            Check("biome named::" + stage.Id, !string.IsNullOrEmpty(stage.Biome.Title), "биом без названия");

            previous = stage.StartHeight;
        }

        Check("ground starts at 0", StageCatalog.All[0].StartHeight == 0f,
            "начало " + StageCatalog.All[0].StartHeight);

        float lastStart = StageCatalog.All[StageCatalog.Count - 1].StartHeight;
        Check("goal above last stage", StageCatalog.GoalHeight > lastStart,
            "финиш " + StageCatalog.GoalHeight + " при последней ступени " + lastStart);
    }

    /// <summary>Границы ступеней должны лежать на границах полос неба.</summary>
    private static void CheckSkyAlignment()
    {
        float[] bands = Sky.BandAltitudes;
        Check("sky has 6 bands", bands.Length == 6, "полос " + bands.Length);
        Check("sky bands == stages", bands.Length == StageCatalog.Count,
            "полос " + bands.Length + ", ступеней " + StageCatalog.Count);

        // Полосы: -50, 130, 300, 460, 660, 950. Первая полоса — горизонт,
        // ниже нуля, и ступени не соответствует: земля начинается на нуле.
        // Поэтому совпадать должны полосы со второй и ступени с первой.
        Check("first band is horizon", bands[0] < 0f, "полоса " + bands[0]);
        Check("ground starts at zero", Mathf.Abs(StageCatalog.All[0].StartHeight) < 0.01f,
            "начало " + StageCatalog.All[0].StartHeight);

        for (int i = 1; i < StageCatalog.Count; i++)
        {
            float start = StageCatalog.All[i].StartHeight;
            float band = bands[i];
            Check("stage " + i + " on sky band",
                Mathf.Abs(start - band) < 0.01f,
                "ступень " + start + ", полоса " + band);
        }
    }

    /// <summary>
    /// Главная проверка. Дальность прыжка при наборе высоты dy считается по
    /// нисходящей ветви: игрок взлетает, переваливается и приземляется сверху.
    ///
    /// Проверяем худший случай ступени — одновременно максимальный набор
    /// высоты и максимальную дальность, потому что генератор тянет их
    /// независимо и такой союз случается регулярно.
    /// </summary>
    private static void CheckReachability()
    {
        for (int i = 0; i < StageCatalog.All.Count; i++)
        {
            StageDefinition stage = StageCatalog.All[i];
            LevelConfig c = stage.Cfg;
            string tag = "reach." + stage.Id;

            float reach = PlatformFit.ReachAt(c.MaxDy);

            Check(tag + " maxdy reachable", c.MaxDy <= PlayerPhysics.MaxVerticalJump,
                "MaxDy " + c.MaxDy + " при потолке " + PlayerPhysics.MaxVerticalJump);

            Check(tag + " maxdx reachable", c.MaxDx <= reach,
                "MaxDx " + c.MaxDx + " при досягаемости " + reach);

            // Запас меньше 10% — приземляться придётся впритык, и любой
            // промах по горизонтали станет падением.
            Check(tag + " has margin", reach - c.MaxDx >= reach * 0.10f,
                "запас " + (reach - c.MaxDx) + " из " + reach);

            // Потолок, который держит сам генератор при подборе места. MaxDx выше
            // него означал бы, что подбор упрётся в потолок и площадка встанет
            // туда, куда не допрыгнуть.
            float ceiling = PlatformFit.SafeReachAt(c.MaxDy);
            Check(tag + " under generator ceiling", c.MaxDx <= ceiling,
                "MaxDx " + c.MaxDx + " при потолке " + ceiling);

            Check(tag + " maxdy >= mindy", c.MaxDy >= c.MinDy,
                "MaxDy " + c.MaxDy + " < MinDy " + c.MinDy);
            Check(tag + " maxdx >= mindx", c.MaxDx >= c.MinDx,
                "MaxDx " + c.MaxDx + " < MinDx " + c.MinDx);
            Check(tag + " mindy >= 0", c.MinDy >= 0f, "MinDy " + c.MinDy);
            Check(tag + " maxdx > 0", c.MaxDx > 0f, "MaxDx " + c.MaxDx);
        }
    }

    /// <summary>Сложность обязана расти, а площадки — мельчать.</summary>
    private static void CheckMonotonicDifficulty()
    {
        for (int i = 1; i < StageCatalog.All.Count; i++)
        {
            LevelConfig prev = StageCatalog.All[i - 1].Cfg;
            LevelConfig cur = StageCatalog.All[i].Cfg;
            string tag = "mono." + StageCatalog.All[i].Id;

            Check(tag + " min size down", cur.MinSize <= prev.MinSize,
                cur.MinSize + " после " + prev.MinSize);
            Check(tag + " small size down", cur.SmallSize <= prev.SmallSize,
                cur.SmallSize + " после " + prev.SmallSize);
            Check(tag + " maxdx up", cur.MaxDx >= prev.MaxDx, cur.MaxDx + " после " + prev.MaxDx);
            Check(tag + " maxdy up", cur.MaxDy >= prev.MaxDy, cur.MaxDy + " после " + prev.MaxDy);
            Check(tag + " hazard up", cur.HazardChanceMax >= prev.HazardChanceMax,
                cur.HazardChanceMax + " после " + prev.HazardChanceMax);
            Check(tag + " small weight up", cur.SmallWeight >= prev.SmallWeight,
                cur.SmallWeight + " после " + prev.SmallWeight);

            int total = cur.SquareWeight + cur.SmallWeight + cur.WideWeight + cur.LongWeight;
            Check(tag + " shape weights positive", total > 0, "сумма весов " + total);
        }
    }

    /// <summary>
    /// Опасности не должны начинаться раньше своей ступени, а биом обязан
    /// иметь ненулевые веса: иначе KindByWeights отдаст Rock по умолчанию и
    /// ступень станет одноцветной. Это молчаливый дефект — код не падает,
    /// просто вся местность выглядит одинаковой.
    /// </summary>
    private static void CheckHazards()
    {
        for (int i = 0; i < StageCatalog.All.Count; i++)
        {
            StageDefinition stage = StageCatalog.All[i];
            LevelConfig c = stage.Cfg;
            string tag = "haz." + stage.Id;

            Check(tag + " ramp positive", c.HazardRampHeight > 0f, "разгон " + c.HazardRampHeight);
            Check(tag + " chance <= max", c.HazardChance <= c.HazardChanceMax,
                c.HazardChance + " > " + c.HazardChanceMax);
            Check(tag + " chance in range", c.HazardChance >= 0f && c.HazardChanceMax <= 1f,
                c.HazardChance + ".." + c.HazardChanceMax);

            // Порог опасностей — высота абсолютная, но конфиг берётся из
            // текущей ступени, а высоты площадок не убывают. Поэтому порог
            // ниже начала ступени означает «опасно сразу». Безопасность этого
            // держит инвариант высот в CheckGeneratorAscends.
            //
            // Недопустим только один вариант: порог настолько высок, что
            // ступень останется без опасностей целиком — тогда ступень
            // вырождается в предыдущую, и подъём теряет смысл.
            bool disabled = c.HazardStartHeight >= LevelConfig.NoHazards;
            bool withinStage = c.HazardStartHeight < stage.StartHeight + c.HazardRampHeight;
            Check(tag + " start height sane", disabled || withinStage,
                "порог " + c.HazardStartHeight + " при начале ступени " + stage.StartHeight +
                " и разгоне " + c.HazardRampHeight);

            Check(tag + " biome has weight", stage.Biome.Total > 0, "сумма весов " + stage.Biome.Total);
        }

        // Первая ловушка должна появляться в начале подъёма, а не где-то на
        // десятом проценте. Стояло 145 метров из 1200 — игрок успевал забыть,
        // что опасности вообще бывают. Порог с запасом, но за превышение уже
        // не сойти.
        float goal = StageCatalog.GoalHeight;
        float firstHazard = LevelConfig.NoHazards;
        for (int i = 0; i < StageCatalog.All.Count; i++)
        {
            float h = StageCatalog.All[i].Cfg.HazardStartHeight;
            if (h < firstHazard) firstHazard = h;
        }
        Check("haz.appears early", firstHazard <= goal * 0.10f,
            "первая ловушка на " + firstHazard + " из " + goal);

        // При этом первые метры всё же должны быть чистыми: игрок ещё не понял
        // управление, и респавн за шип на третьей площадке — это не сложность,
        // а наказание за отсутствие инструкции.
        Check("haz.ground has clean floor", StageCatalog.All[0].Cfg.HazardStartHeight >= 45f,
            "ловушка с " + StageCatalog.All[0].Cfg.HazardStartHeight + " метра");

        // Земля осторожнее леса даже при включённых ловушках.
        Check("haz.ground gentler than forest",
            StageCatalog.All[0].Cfg.HazardChanceMax < StageCatalog.All[1].Cfg.HazardChanceMax,
            "земля " + StageCatalog.All[0].Cfg.HazardChanceMax +
            " против леса " + StageCatalog.All[1].Cfg.HazardChanceMax);

        // Каждая ступень обязана отличаться от предыдущей биомом. Проверка не
        // косметическая: Земля и Лес когда-то пользовались одним и тем же
        // Forest, и триста метров подъёма выглядели одинаково. Никакое другое
        // утверждение этого не ловило — ступень была на месте, биом был на
        // месте, а смены на экране не происходило.
        for (int i = 0; i < StageCatalog.All.Count; i++)
        {
            StageDefinition stage = StageCatalog.All[i];
            string tag = "biome." + stage.Id;

            Check(tag + " readable", DominantWeight(stage.Biome) >= 25,
                "преобладающий вид весит " + DominantWeight(stage.Biome) + " из " + stage.Biome.Total);

            if (i == 0) continue;

            StageDefinition prev = StageCatalog.All[i - 1];
            Check(tag + " differs from previous", stage.Biome.Title != prev.Biome.Title,
                "один биом с " + prev.Id + ": " + stage.Biome.Title);

            // Различаться должен не только ярлык, но и то, что реально видно:
            // преобладающий вид текстуры площадок.
            Textures.Kind mine = Dominant(stage.Biome);
            Textures.Kind theirs = Dominant(prev.Biome);
            Check(tag + " dominant kind differs", mine != theirs,
                "преобладает " + mine + " как и у " + prev.Id);
        }
    }

    /// <summary>Вес преобладающего вида текстуры в биоме. Ниже четверти биом не
    /// читается: площадки выглядят как каша, и смены местности не видно.</summary>
    private static int DominantWeight(Biome b)
    {
        int max = b.Grass;
        if (b.Wood > max) max = b.Wood;
        if (b.Rock > max) max = b.Rock;
        if (b.Metal > max) max = b.Metal;
        if (b.Snow > max) max = b.Snow;
        if (b.Crate > max) max = b.Crate;
        return max;
    }

    /// <summary>Какая текстура преобладает в биоме. Тот же порядок полей, что в
    /// KindByWeights, иначе проверка смотрела бы не туда.</summary>
    private static Textures.Kind Dominant(Biome b)
    {
        int max = b.Grass;
        Textures.Kind kind = Textures.Kind.Grass;

        if (b.Wood > max) { max = b.Wood; kind = Textures.Kind.Wood; }
        if (b.Rock > max) { max = b.Rock; kind = Textures.Kind.Rock; }
        if (b.Metal > max) { max = b.Metal; kind = Textures.Kind.Metal; }
        if (b.Snow > max) { max = b.Snow; kind = Textures.Kind.Snow; }
        if (b.Crate > max) { kind = Textures.Kind.Crate; }
        return kind;
    }

    /// <summary>Поиск ступени по высоте: граница включается в верхнюю ступень.</summary>
    private static void CheckCatalogLookup()
    {
        for (int i = 0; i < StageCatalog.All.Count; i++)
        {
            float start = StageCatalog.All[i].StartHeight;
            Check("lookup at " + start, StageCatalog.IndexAt(start) == i,
                "вернуло " + StageCatalog.IndexAt(start));

            if (i + 1 < StageCatalog.Count)
            {
                float justBelow = StageCatalog.All[i + 1].StartHeight - 0.01f;
                Check("lookup below " + start, StageCatalog.IndexAt(justBelow) == i,
                    "вернуло " + StageCatalog.IndexAt(justBelow));
            }
        }

        Check("lookup negative clamps", StageCatalog.IndexAt(-500f) == 0,
            "вернуло " + StageCatalog.IndexAt(-500f));

        int last = StageCatalog.Count - 1;
        Check("lookup above top stays last",
            StageCatalog.IndexAt(StageCatalog.All[last].StartHeight + 99999f) == last,
            "вернуло " + StageCatalog.IndexAt(StageCatalog.All[last].StartHeight + 99999f));

        Check("stage clamps high", StageCatalog.Stage(999).Id == StageCatalog.All[last].Id,
            "вернуло " + StageCatalog.Stage(999).Id);
        Check("stage clamps low", StageCatalog.Stage(-5).Id == StageCatalog.All[0].Id,
            "вернуло " + StageCatalog.Stage(-5).Id);
    }

    /// <summary>
    /// Генератор обязан подниматься по ступеням снизу вверх, ровно по одной
    /// ступени за раз. Прыгать через ступень нельзя — тогда игрок пролетит
    /// мимо целого этапа, а ступень начнёт мигать на границе.
    /// </summary>
    private static void CheckGeneratorAscends()
    {
        LevelGenerator gen = NewGenerator();
        gen.ResetPath(0);

        // Строим, пока не перевалим за финиш с запасом.
        const int hardCap = 6000;
        int guard = 0;
        while (gen.AliveMax < hardCap && guard++ < 400)
        {
            gen.GenerateUpTo(gen.AliveMax + 250);
            if (TopHeight(gen) >= StageCatalog.GoalHeight + 50f) break;
        }

        int top = gen.AliveMax;
        Check("generated enough", top > 0, "верхний индекс " + top);
        Check("reached goal height", TopHeight(gen) >= StageCatalog.GoalHeight,
            "высота верха " + TopHeight(gen));

        int previousIndex = 0;
        int stages = 1;
        int steps = 0;
        bool orderOk = true;
        string orderDetail = "";

        for (int i = 0; i <= top; i++)
        {
            int index = StageCatalog.IndexAt(gen.PositionOf(i).y - StageCatalog.WorldOriginY);
            if (index == previousIndex) continue;

            steps++;
            if (index != previousIndex + 1)
            {
                orderOk = false;
                orderDetail = "прыжок со " + previousIndex + " на " + index + " у платформы " + i;
                break;
            }
            previousIndex = index;
            stages++;
        }

        Check("stages ascend by one", orderOk, orderDetail);
        Check("visited every stage", stages == StageCatalog.Count,
            "пройдено " + stages + " из " + StageCatalog.Count + ", переходов " + steps);
        Check("ends on last stage", previousIndex == StageCatalog.Count - 1,
            "остановился на " + previousIndex);

        CheckHeightsNeverFall(gen, top);
        CheckHazardsAppearEarly(gen);
    }

    /// <summary>
    /// Где встала первая ловушка в настоящем подъёме.
    ///
    /// Проверять тут конфиг было бы нечестно: он ничего не говорит о том,
    /// сколько ловушек дошло до уровня и на какой высоте. Шанс на ступени
    /// мог быть любым, а на деле выпадал ноль за всю ступень. Поэтому берутся
    /// построенные объекты, а не числа из каталога.
    /// </summary>
    private static void CheckHazardsAppearEarly(LevelGenerator gen)
    {
        Transform root = gen.Data.Root.transform;
        int hazards = 0;
        float lowest = float.PositiveInfinity;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child.name.StartsWith("Hazard ")) continue;

            hazards++;
            float h = child.position.y - StageCatalog.WorldOriginY;
            if (h < lowest) lowest = h;
        }

        Check("hazards built at all", hazards > 50, "ловушек " + hazards);
        if (hazards == 0) return;

        // Первая ловушка должна быть в первых процентах подъёма. Порог тот же,
        // что и у проверки конфига, но применяется к уровню: расхождение
        // между «выставлено 55» и «встало на 400» — это ровно тот случай, когда
        // конфиг врёт, а игрок об этом не узнает.
        Check("first hazard near ground", lowest <= StageCatalog.GoalHeight * 0.10f,
            "первая ловушка на " + lowest.ToString("0") + " м из " + StageCatalog.GoalHeight);

        Check("first hazard is not the very first platform", lowest > 20f,
            "первая ловушка на " + lowest.ToString("0") + " м");

        // Точное число, а не только «меньше порога»: по нему видно, насколько
        // рано ловушки стали появляться, и откат к старому поведению с 145
        // метрами сразу был бы заметен в логе.
        Debug.Log("[T] INFO ловушек " + hazards + ", первая на " +
            lowest.ToString("0") + " м, это " +
            Mathf.RoundToInt(lowest / StageCatalog.GoalHeight * 100f) + "% подъёма");

        // Разбивка по ступеням. Общее число мало о чём говорит: можно набить
        // сотню ловушек на одной ступени и ни одной на остальных, и сумма от
        // этого не изменится. Разбивка показывает, что разгон работает и что
        // ловушки не высыпаются молча на какой-то одной ступени.
        for (int i = 0; i < StageCatalog.All.Count; i++)
        {
            StageDefinition stage = StageCatalog.All[i];
            int inStage = 0;
            int platforms = 0;
            int eligible = 0;
            for (int k = 0; k < root.childCount; k++)
            {
                Transform child = root.GetChild(k);
                float h = child.position.y - StageCatalog.WorldOriginY;
                bool here = StageCatalog.IndexAt(h) == i;

                if (child.name.StartsWith("Hazard "))
                {
                    if (here) inStage++;
                    continue;
                }
                if (!child.name.StartsWith("Platform ")) continue;
                if (!here) continue;

                platforms++;

                // Площадка проходит отсев MinHazardPlatform по меньшей стороне.
                // Опасность нельзя поставить на узкую площадку, и чем мельче
                // ступень, тем больше площадок выпадает из этого отсева.
                Vector3 size = child.localScale;
                if (Mathf.Min(size.x, size.z) >= stage.Cfg.MinHazardPlatform) eligible++;
            }

            Debug.Log("[T] INFO ступень " + stage.Title + ": площадок " + platforms +
                ", под опасность годных " + eligible + ", ловушек " + inStage);
        }
    }

    /// <summary>
    /// Высоты площадок не должны убывать. Это не косметика: на этом стоит
    /// вся схема ступеней. Конфиг ступени выбирается по высоте предыдущей
    /// площадки, поэтому «нисходящая» трасса дала бы площадку, построенную
    /// под конфигом нижней ступени, но стоящую на высоте верхней. Опасности
    /// с порогом «с низа своей ступени» наехали бы на землю.
    /// </summary>
    private static void CheckHeightsNeverFall(LevelGenerator gen, int top)
    {
        float drops = 0f;
        float worst = 0f;
        for (int i = 1; i <= top; i++)
        {
            float fall = gen.PositionOf(i - 1).y - gen.PositionOf(i).y;
            if (fall <= 0f) continue;
            drops++;
            if (fall > worst) worst = fall;
        }

        Check("heights never fall", drops == 0f, "просадок " + drops + ", худшая " + worst);
    }

    /// <summary>
    /// CheckpointPlatformIndex обязан отвечать одинаково до и после того, как
    /// трасса построена. Это ровно то, что ломается, когда шаг чекпоинтов
    /// становится зависимым от высоты: функция начинает отвечать по-разному
    /// в зависимости от того, спрашивают её снизу или сверху, и ReleaseBelow
    /// уничтожает не тот кусок трассы.
    /// </summary>
    private static void CheckCheckpointMapping()
    {
        LevelGenerator gen = NewGenerator();

        int before = gen.CheckpointPlatformIndex(3);
        int two = gen.CheckpointPlatformIndex(2);
        int five = gen.CheckpointPlatformIndex(5);

        gen.ResetPath(0);
        gen.GenerateUpTo(400);

        Check("cp mapping stable", before == gen.CheckpointPlatformIndex(3),
            before + " против " + gen.CheckpointPlatformIndex(3));

        gen.ResetPath(400);
        gen.GenerateUpTo(1600);

        Check("cp mapping stable after reset", before == gen.CheckpointPlatformIndex(3),
            before + " против " + gen.CheckpointPlatformIndex(3));
        Check("cp mapping stable for 2", two == gen.CheckpointPlatformIndex(2),
            two + " против " + gen.CheckpointPlatformIndex(2));
        Check("cp mapping stable for 5", five == gen.CheckpointPlatformIndex(5),
            five + " против " + gen.CheckpointPlatformIndex(5));
        Check("cp monotonic", gen.CheckpointPlatformIndex(5) > gen.CheckpointPlatformIndex(2),
            "чекпоинты не растут");
        Check("cp zero is zero", gen.CheckpointPlatformIndex(0) == 0,
            "вернуло " + gen.CheckpointPlatformIndex(0));
    }

    /// <summary>Прогресс по ступеням растёт только вверх и платится один раз.</summary>
    private static void CheckProgress()
    {
        PlayerPrefs.DeleteKey(GameProgress.KeyStageReached);
        PlayerPrefs.DeleteKey(GameProgress.KeyFinished);

        Check("stage starts at 0", GameProgress.GetStageReached() == 0,
            "начало " + GameProgress.GetStageReached());

        Check("stage 1 is new", GameProgress.SetStageReached(1), "отказал первый раз");
        Check("stage 1 saved", GameProgress.GetStageReached() == 1, "сохранено " + GameProgress.GetStageReached());
        Check("stage 1 not paid twice", !GameProgress.SetStageReached(1), "выдалось повторно");
        Check("stage back refused", !GameProgress.SetStageReached(0), "принял откат назад");
        Check("stage stays 1", GameProgress.GetStageReached() == 1, "стало " + GameProgress.GetStageReached());
        Check("stage 2 is new", GameProgress.SetStageReached(2), "отказал переход");
        Check("stage 2 saved", GameProgress.GetStageReached() == 2, "сохранено " + GameProgress.GetStageReached());

        Check("not finished yet", !GameProgress.IsFinished(), "сразу finished");
        GameProgress.SetFinished();
        Check("finished saved", GameProgress.IsFinished(), "не сохранился");

        // Финиш не должен откатывать прогресс ступеней.
        Check("stage survives finish", GameProgress.GetStageReached() == 2,
            "стало " + GameProgress.GetStageReached());
    }

    // ---------------- утилиты ----------------

    private static float TopHeight(LevelGenerator gen)
    {
        return gen.PositionOf(gen.AliveMax).y - StageCatalog.WorldOriginY;
    }

    /// <summary>Хост намеренно остаётся живым: вернуть компонент, который
    /// уничтожен, нельзя, а методы мы вызываем именно на нём.</summary>
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

    private static void TakeSnapshot()
    {
        Snapshot[GameProgress.KeyStageReached] =
            PlayerPrefs.GetInt(GameProgress.KeyStageReached, -1).ToString();
        Snapshot[GameProgress.KeyFinished] =
            PlayerPrefs.GetInt(GameProgress.KeyFinished, -1).ToString();
    }

    private static void Restore()
    {
        foreach (var pair in Snapshot)
        {
            int value;
            if (!int.TryParse(pair.Value, out value) || value < 0)
            {
                PlayerPrefs.DeleteKey(pair.Key);
                continue;
            }
            PlayerPrefs.SetInt(pair.Key, value);
        }
        PlayerPrefs.Save();
    }
}
#endif
