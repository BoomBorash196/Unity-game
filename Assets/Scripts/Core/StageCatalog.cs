using System.Collections.Generic;

/// <summary>
/// Ступени подъёма от земли к небу. Шесть штук — ровно столько же, сколько
/// полос неба в Sky.Bands, и границы те же самые: -50, 130, 300, 460, 660, 950.
///
/// Совпадение не декоративное. Если биом меняется на 300 м, а небо — на 305 м,
/// подъём читается криво: игрок видит, как посветлело небо, а под ногами ещё
/// лес. Одна таблица высот на оба слоя исключает расхождение по построению.
///
/// Высоты в метрах над стартом, то есть y - StartY: сюда сдаётся тот же
/// LevelHeight, который игрок видит в HUD и который пишется в рекорд.
/// </summary>
public static class StageCatalog
{
    /// <summary>Высота, на которой подъём считается пройденным.</summary>
    public const float GoalHeight = 1200f;

    /// <summary>Мировая высота нулевой отметки. Стартовая площадка стоит на
    /// этом y, поэтому метры над стартом — это y минус это число. Держим здесь,
    /// а не константой в менеджере: по этой формуле считаются и ступени, и
    /// рекорд, и экран, и разъедутся они только если появятся две копии числа.</summary>
    public const float WorldOriginY = 1.0f;

    private static readonly List<StageDefinition> _stages = Build();

    public static int Count { get { return _stages.Count; } }

    public static IList<StageDefinition> All { get { return _stages; } }

    /// <summary>Номер ступени, которой принадлежит высота. За пределами
    /// последней остаётся последняя — подъём не имеет потолка, но и не
    /// вырождается: рай просто продолжается.</summary>
    public static int IndexAt(float levelHeight)
    {
        int index = 0;
        for (int i = 1; i < _stages.Count; i++)
        {
            if (levelHeight < _stages[i].StartHeight) break;
            index = i;
        }
        return index;
    }

    public static StageDefinition StageAt(float levelHeight)
    {
        return _stages[IndexAt(levelHeight)];
    }

    public static StageDefinition Stage(int index)
    {
        if (index < 0) index = 0;
        if (index >= _stages.Count) index = _stages.Count - 1;
        return _stages[index];
    }

    /// <summary>
    /// Ступени снизу вверх. Размеры площадок, дальность прыжка и доля опасностей
    /// растут монотонно, иначе подъём скачет по сложности туда-обратно.
    ///
    /// Потолок прыжка — 2,98 м вверх и 7,39 м в длину по пологой траектории.
    /// Значения ниже держатся внутри этих пределов с запасом: MaxDy не больше
    /// 2,4, а размеры площадок подобраны так, чтобы две соседние всегда
    /// помещались в один прыжок.
    ///
    /// Про последнее подробнее, потому что это неочевидно. В плоском отрезке
    /// соседние площадки стоят на одной высоте, и наложение возможно в
    /// горизонтали. Прыжок даёт 6,95 м с запасом, значит сумма двух описанных
    /// радиусов с зазором должна быть меньше 6,95 м. Широкая площадка — самая
    /// большая по радиусу, и на Земле её 5,4 × 4,0 м давали радиус 3,36 м: две
    /// такие требовали 7,12 м друг от друга и не помещались в прыжок ни при
    /// каком расположении. Подбор места тут бессилен, поэтому размер и есть
    /// настоящее решение, а не украшение. Проверка этого условия — в OverlapCheck.
    /// </summary>
    private static List<StageDefinition> Build()
    {
        return new List<StageDefinition>
        {
            // Земля. Разгон: площадки большие, прыжки короткие. Первые 55 метров
            // ловушек нет вовсе, чтобы игрок успел разобраться с управлением без
            // респавнов, дальше они включаются и к вершине ступени доходят до
            // 16 процентов площадок. Раньше опасностей не было на всей ступени,
            // и первая ловушка появлялась только на 145 метрах — на двенадцатом
            // проценте подъёма из километра с лишним.
            new StageDefinition
            {
                Id = "ground", Title = "Земля", StartHeight = 0f, Biome = Biome.Ground,
                Cfg = new LevelConfig
                {
                    MinSize = 2.6f, MaxSize = 3.6f,
                    SmallSize = 2.2f, WideSize = 4.6f,
                    MinDy = 0.9f, MaxDy = 1.8f,
                    MinDx = 2.9f, MaxDx = 3.6f,
                    SquareWeight = 52, SmallWeight = 6, WideWeight = 36, LongWeight = 6,
                    HazardStartHeight = 55f, HazardRampHeight = 75f, MinHazardPlatform = 2.4f,
                    HazardChance = 0.06f, HazardChanceMax = 0.16f
                }
            },

            // Лес. Опасности идут с самого начала ступени и разгоняются до 28 процентов.
            // Досок больше, чем на земле, — переход с травы на дерево должен
            // читаться и по площадкам, а не только по их цвету.
            new StageDefinition
            {
                Id = "forest", Title = "Лес", StartHeight = 130f, Biome = Biome.Forest,
                Cfg = new LevelConfig
                {
                    MinSize = 2.2f, MaxSize = 3.3f,
                    SmallSize = 2.0f, WideSize = 4.6f,
                    MinDy = 1.0f, MaxDy = 2.0f,
                    MinDx = 2.9f, MaxDx = 3.8f,
                    SquareWeight = 44, SmallWeight = 14, WideWeight = 26, LongWeight = 16,
                    HazardStartHeight = 130f, HazardRampHeight = 170f, MinHazardPlatform = 2.2f,
                    HazardChance = 0.16f, HazardChanceMax = 0.28f
                }
            },

            // Карьер. Уже настоящая сложность: площадки мельче, прыжки дальше,
            // опасности идут с самого начала ступени.
            new StageDefinition
            {
                Id = "quarry", Title = "Карьер", StartHeight = 300f, Biome = Biome.Quarry,
                Cfg = new LevelConfig
                {
                    MinSize = 1.9f, MaxSize = 3.0f,
                    SmallSize = 1.8f, WideSize = 4.4f,
                    MinDy = 1.0f, MaxDy = 2.1f,
                    MinDx = 3.0f, MaxDx = 4.0f,
                    SquareWeight = 38, SmallWeight = 22, WideWeight = 22, LongWeight = 18,
                    HazardStartHeight = 300f, HazardRampHeight = 160f, MinHazardPlatform = 2.1f,
                    HazardChance = 0.26f, HazardChanceMax = 0.36f
                }
            },

            // Скалы. Досок заметно больше: длинная площадка прощает промах по
            // горизонтали, и это единственный способ сделать дальний прыжок
            // честным, а не требованием точного прицела. Биом — промёрзшая
            // порода, а не голый камень: на промёрзшем переход читается сразу.
            new StageDefinition
            {
                Id = "cliff", Title = "Скалы", StartHeight = 460f, Biome = Biome.Frost,
                Cfg = new LevelConfig
                {
                    MinSize = 1.8f, MaxSize = 2.8f,
                    SmallSize = 1.7f, WideSize = 4.2f,
                    MinDy = 1.1f, MaxDy = 2.2f,
                    MinDx = 3.0f, MaxDx = 4.2f,
                    SquareWeight = 30, SmallWeight = 28, WideWeight = 18, LongWeight = 24,
                    HazardStartHeight = 460f, HazardRampHeight = 200f, MinHazardPlatform = 2.0f,
                    HazardChance = 0.30f, HazardChanceMax = 0.42f
                }
            },

            // Облака. Мелкие площадки над облачными ярусами: игрок уже выше
            // почти всех облаков, и само место читается как «край».
            new StageDefinition
            {
                Id = "clouds", Title = "Облака", StartHeight = 660f, Biome = Biome.Clouds,
                Cfg = new LevelConfig
                {
                    MinSize = 1.8f, MaxSize = 2.7f,
                    SmallSize = 1.5f, WideSize = 4.0f,
                    MinDy = 1.1f, MaxDy = 2.3f,
                    MinDx = 3.1f, MaxDx = 4.4f,
                    SquareWeight = 26, SmallWeight = 36, WideWeight = 14, LongWeight = 24,
                    HazardStartHeight = 660f, HazardRampHeight = 290f, MinHazardPlatform = 1.9f,
                    HazardChance = 0.34f, HazardChanceMax = 0.46f
                }
            },

            // Рай. Предел. Мелкие площадки, самые дальние прыжки и больше
            // половины площадок с опасностью.
            new StageDefinition
            {
                Id = "heaven", Title = "Рай", StartHeight = 950f, Biome = Biome.Heaven,
                Cfg = new LevelConfig
                {
                    MinSize = 1.8f, MaxSize = 2.6f,
                    SmallSize = 1.4f, WideSize = 3.9f,
                    MinDy = 1.1f, MaxDy = 2.4f,
                    MinDx = 3.1f, MaxDx = 4.6f,
                    SquareWeight = 22, SmallWeight = 42, WideWeight = 10, LongWeight = 26,
                    HazardStartHeight = 950f, HazardRampHeight = 250f, MinHazardPlatform = 1.8f,
                    HazardChance = 0.38f, HazardChanceMax = 0.52f
                }
            }
        };
    }
}
