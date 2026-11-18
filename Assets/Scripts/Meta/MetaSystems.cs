using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Центральный вход мета-систем: пожизненные счётчики и точки, из которых
/// геймплей докладывает о событиях забега. Всё остальное (пасс, дейлики,
/// ачивки) подписывается сюда, а не на каждый компонент уровня.
/// </summary>
public static class MetaSystems
{
    public const string KeyLifeClimb = "meta_life_climb";
    public const string KeyLifeCoins = "meta_life_coins";
    public const string KeyLifeRuns = "meta_life_runs";
    public const string KeyLifeJumps = "meta_life_jumps";

    public static int LifeClimb { get { return PlayerPrefs.GetInt(KeyLifeClimb, 0); } }
    public static int LifeCoins { get { return PlayerPrefs.GetInt(KeyLifeCoins, 0); } }
    public static int LifeRuns { get { return PlayerPrefs.GetInt(KeyLifeRuns, 0); } }
    public static int LifeJumps { get { return PlayerPrefs.GetInt(KeyLifeJumps, 0); } }

    private static void AddLife(string key, int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(key, Mathf.Max(0, PlayerPrefs.GetInt(key, 0) + amount));
        SaveService.MarkLocal();
    }

    /// <summary>
    /// Отрезок забега закрыт (смерть/респавн/выход/рестарт). levelHeight — макс.
    /// высота отрезка в метрах над стартом (уже floor и max по нулю).
    /// Возвращает только что выполненные задания — для тоста в геймплее.
    /// </summary>
    public static List<DailyQuest> RegisterRunEnded(int levelHeight)
    {
        if (levelHeight > 0)
        {
            AddLife(KeyLifeClimb, levelHeight);
            AddLife(KeyLifeRuns, 1);
            SeasonPass.AddRunHeight(levelHeight);
        }

        List<DailyQuest> fresh = DailyQuests.RegisterRun(levelHeight);
        Achievements.Evaluate();
        return fresh;
    }

    public static void RegisterCoinPicked(int amount)
    {
        if (amount <= 0) return;
        AddLife(KeyLifeCoins, amount);
        DailyQuests.RegisterCoins(amount);
        Achievements.Evaluate();
    }

    public static void RegisterJump()
    {
        AddLife(KeyLifeJumps, 1);
        DailyQuests.RegisterJump();
    }

    /// <summary>Новая ступень взята (GameProgress.SetStageReached уже вызван).</summary>
    public static void RegisterStageReached(int stageIndex)
    {
        DailyQuests.RegisterStage(stageIndex);
        Achievements.Evaluate();
    }

    /// <summary>Принудительная проверка ачивок (после обновления рекорда/финиша).</summary>
    public static void EvaluateNow()
    {
        Achievements.Evaluate();
        MetaEvents.RaiseAchievementsChanged();
    }
}