using System;

/// <summary>
/// Биом ступени: веса шести процедурных текстур, чем покрыты площадки.
/// Небо и сложность задаются отдельно — здесь только вид камня под ногами.
/// Вес — вероятность, сумма не обязана быть 100.
/// </summary>
[Serializable]
public struct Biome
{
    public string Title;
    public int Grass;
    public int Wood;
    public int Rock;
    public int Metal;
    public int Snow;
    public int Crate;

    public int Total
    {
        get { return Grass + Wood + Rock + Metal + Snow + Crate; }
    }

    // Земля — трава и утоптанная земля. Заметно светлее и зеленее леса нарочно:
    // первый переход биома должен быть виден сразу, а не где-то на трехсотом
    // метре. Раньше Земля и Лес пользовались одним и тем же Forest, и первые
    // триста метров подъёма читались как одна сплошная зелёная полоса: смена
    // биома была прописана в коде, а на экране не происходила.
    public static Biome Ground = new Biome
    {
        Title = "земля", Grass = 74, Wood = 10, Rock = 8, Metal = 0, Snow = 0, Crate = 6
    };

    // Лес — дерево вместо травы. Различие с землёй держится не на оттенке
    // зелени, а на том, из чего сделаны площадки, поэтому зелени здесь
    // меньше, а досок заметно больше.
    //
    // Ноль снега нарочно: снег на низких площадках читается как «скользко»,
    // а скользкости в игре нет.
    public static Biome Forest = new Biome
    {
        Title = "лес", Grass = 30, Wood = 50, Rock = 12, Metal = 2, Snow = 0, Crate = 6
    };

    // Карьер — выработка: руда, ящики, рельсы.
    public static Biome Quarry = new Biome
    {
        Title = "карьер", Grass = 0, Wood = 8, Rock = 40, Metal = 42, Snow = 0, Crate = 10
    };

    // Мороз — промёрзшая порода. Идёт на скалы, и раньше там стоял отдельный
    // биом Cliff с голой породой: он больше нигде не встречался и был мёртвым
    // кодом, а мороз на камне выглядит заметно сильнее и заодно убирает
    // лишнее определение.
    public static Biome Frost = new Biome
    {
        Title = "мороз", Grass = 0, Wood = 4, Rock = 30, Metal = 10, Snow = 44, Crate = 12
    };

    // Облака — металл и снег вперемешку: площадки уже не из чего делать.
    public static Biome Clouds = new Biome
    {
        Title = "облака", Grass = 0, Wood = 0, Rock = 10, Metal = 48, Snow = 36, Crate = 6
    };

    // Рай — почти всё белое.
    public static Biome Heaven = new Biome
    {
        Title = "рай", Grass = 0, Wood = 0, Rock = 4, Metal = 22, Snow = 68, Crate = 6
    };
}

/// <summary>
/// Ступень подъёма: диапазон высот, своя сложность и свой биом.
///
/// Ступень — это не сцена и не файл. Сцена в проекте одна, и это осознанно:
/// всё остальное собирается кодом при загрузке. Поэтому ступень — просто
/// набор чисел, а «уровни» меняются подстановкой другого набора.
/// </summary>
[Serializable]
public class StageDefinition
{
    public string Id = "ground";
    public string Title = "Земля";

    /// <summary>С какой высоты ступень начинается, метры над стартом
    /// (то есть y минус StartY). Границы совпадают с полосами неба.</summary>
    public float StartHeight;

    public Biome Biome = Biome.Ground;
    public LevelConfig Cfg = new LevelConfig();
}
