using System.Collections.Generic;
using UnityEngine;

public enum AchievementKind
{
    BestHeight,
    StageReached,
    LifeClimb,
    LifeCoins,
    LifeRuns,
    Finished
}

public class AchievementDefinition
{
    public string Id;
    public string Title;
    public string Description;
    public AchievementKind Kind;
    public int Target;
    public int Reward;
}

/// <summary>
/// Ачивки на всю жизнь: разблокируются раз и навсегда, за каждую — монеты.
/// Условия считаются от уже существующих данных прогресса (рекорд высоты,
/// взятые ступени, финиш) и от пожизненных счётчиков MetaSystems.
///
/// Evaluate() вызывается из MetaSystems в точках жизни забега; повторные вызовы
/// ничего не дают — разблокированное помечено.
/// </summary>
public static class Achievements
{
    private const string KeyPrefix = "ach_done_";

    private static readonly List<AchievementDefinition> _all = Build();

    public static IList<AchievementDefinition> All { get { return _all; } }

    public static AchievementDefinition Get(string id)
    {
        for (int i = 0; i < _all.Count; i++)
        {
            if (_all[i].Id == id) return _all[i];
        }
        return null;
    }

    public static bool IsUnlocked(string id)
    {
        return PlayerPrefs.GetInt(KeyPrefix + id, 0) == 1;
    }

    public static int UnlockedCount()
    {
        int count = 0;
        for (int i = 0; i < _all.Count; i++)
        {
            if (IsUnlocked(_all[i].Id)) count++;
        }
        return count;
    }

    public static void Evaluate()
    {
        bool any = false;
        for (int i = 0; i < _all.Count; i++)
        {
            AchievementDefinition def = _all[i];
            if (IsUnlocked(def.Id)) continue;
            if (!Satisfied(def)) continue;

            PlayerPrefs.SetInt(KeyPrefix + def.Id, 1);
            PlayerPrefs.Save();
            SkinService.AddCoins(def.Reward);
            any = true;
            MetaEvents.RaiseAchievementUnlocked(def);
        }
        if (any) MetaEvents.RaiseAchievementsChanged();
    }

    private static bool Satisfied(AchievementDefinition def)
    {
        switch (def.Kind)
        {
            case AchievementKind.BestHeight:
                return GameProgress.GetBestHeight() >= def.Target;
            case AchievementKind.StageReached:
                return GameProgress.GetStageReached() >= def.Target;
            case AchievementKind.LifeClimb:
                return MetaSystems.LifeClimb >= def.Target;
            case AchievementKind.LifeCoins:
                return MetaSystems.LifeCoins >= def.Target;
            case AchievementKind.LifeRuns:
                return MetaSystems.LifeRuns >= def.Target;
            case AchievementKind.Finished:
                return GameProgress.IsFinished();
            default:
                return false;
        }
    }

    private static List<AchievementDefinition> Build()
    {
        return new List<AchievementDefinition>
        {
            new AchievementDefinition { Id = "ach_height_100",   Title = "Первые сто метров",  Description = "Поднимись на 100 м",          Kind = AchievementKind.BestHeight,   Target = 100,  Reward = 30 },
            new AchievementDefinition { Id = "ach_height_500",   Title = "Выше леса",           Description = "Поднимись на 500 м",          Kind = AchievementKind.BestHeight,   Target = 500,  Reward = 80 },
            new AchievementDefinition { Id = "ach_heaven",       Title = "Достигнуть Рая",      Description = "Поднимись на 1200 м",          Kind = AchievementKind.BestHeight,   Target = 1200, Reward = 200 },
            new AchievementDefinition { Id = "ach_stage_forest", Title = "Лес",                 Description = "Дойди до Леса",                Kind = AchievementKind.StageReached, Target = 1,    Reward = 50 },
            new AchievementDefinition { Id = "ach_stage_quarry", Title = "Карьер",              Description = "Дойди до Карьера",              Kind = AchievementKind.StageReached, Target = 2,    Reward = 80 },
            new AchievementDefinition { Id = "ach_stage_clouds", Title = "Облака",              Description = "Дойди до Облаков",              Kind = AchievementKind.StageReached, Target = 4,    Reward = 150 },
            new AchievementDefinition { Id = "ach_finished",     Title = "Прохождение",         Description = "Заверши подъём один раз",       Kind = AchievementKind.Finished,     Target = 1,    Reward = 250 },
            new AchievementDefinition { Id = "ach_climb_1000",   Title = "Вертикаль",           Description = "Набери 1000 м подъёма всего",   Kind = AchievementKind.LifeClimb,    Target = 1000, Reward = 100 },
            new AchievementDefinition { Id = "ach_climb_10000",  Title = "Длинный маршрут",     Description = "Набери 10 000 м подъёма всего", Kind = AchievementKind.LifeClimb,    Target = 10000, Reward = 400 },
            new AchievementDefinition { Id = "ach_coins_500",    Title = "Копилка",             Description = "Собери 500 монет на трассе",    Kind = AchievementKind.LifeCoins,    Target = 500,  Reward = 100 },
            new AchievementDefinition { Id = "ach_coins_5000",   Title = "Банкир",              Description = "Собери 5000 монет на трассе",   Kind = AchievementKind.LifeCoins,    Target = 5000, Reward = 500 },
            new AchievementDefinition { Id = "ach_runs_25",      Title = "Мастер забегов",      Description = "Заверши 25 забегов",            Kind = AchievementKind.LifeRuns,     Target = 25,   Reward = 150 }
        };
    }
}