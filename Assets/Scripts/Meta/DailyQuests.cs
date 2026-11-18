using System;
using System.Collections.Generic;
using UnityEngine;

public enum DailyMetric
{
    ClimbSum,
    RunMax,
    CoinsPicked,
    Runs,
    Jumps,
    Stage
}

public struct DailyQuest
{
    public string Id;
    public string Title;
    public DailyMetric Metric;
    public int Target;
    public int Reward;
    public int Progress;
    public bool Claimed;
    public bool IsComplete { get { return Progress >= Target; } }
}

/// <summary>
/// Ежедневные задания: три штуки в день, выбор детерминированный — один и тот
/// же день даёт один и тот же набор. Прогресс каждого задания хранится под
/// ключом с датой, поэтому новый день автоматически начинает с нуля, а старые
/// ключи просто перестают читаться (сервера нет, перевода часов это не
/// защищает, как и ежедневную награду — осознанный размен).
///
/// Монеты считаются только поднятые на трассе (MetaSystems.RegisterCoinPicked),
/// а не выданные наградами — иначе зажатая ежедневка непрозрачно «съедала» бы
/// задание на сбор монет.
/// </summary>
public static class DailyQuests
{
    public const int DailyCount = 3;

    public static Func<DateTime> NowSource = delegate { return DateTime.Now; };

    private class Def
    {
        public string Id;
        public string Title;
        public DailyMetric Metric;
        public int Target;
        public int Reward;
    }

    private static readonly List<Def> _pool = BuildPool();
    private static readonly List<DailyQuest> _fresh = new List<DailyQuest>();

    private static List<Def> BuildPool()
    {
        return new List<Def>
        {
            new Def { Id = "climb300",  Title = "Подняться на 300 м за день",    Metric = DailyMetric.ClimbSum,    Target = 300,  Reward = 60 },
            new Def { Id = "climb900",  Title = "Подняться на 900 м за день",    Metric = DailyMetric.ClimbSum,    Target = 900,  Reward = 150 },
            new Def { Id = "run120",    Title = "Достичь 120 м за один забег",   Metric = DailyMetric.RunMax,      Target = 120,  Reward = 70 },
            new Def { Id = "run500",    Title = "Достичь 500 м за один забег",   Metric = DailyMetric.RunMax,      Target = 500,  Reward = 160 },
            new Def { Id = "coins25",   Title = "Собрать 25 монет на трассе",    Metric = DailyMetric.CoinsPicked, Target = 25,   Reward = 40 },
            new Def { Id = "coins75",   Title = "Собрать 75 монет на трассе",    Metric = DailyMetric.CoinsPicked, Target = 75,   Reward = 90 },
            new Def { Id = "runs3",     Title = "Завершить 3 забега",            Metric = DailyMetric.Runs,        Target = 3,    Reward = 40 },
            new Def { Id = "jumps30",   Title = "Сделать 30 прыжков",            Metric = DailyMetric.Jumps,       Target = 30,   Reward = 30 },
            new Def { Id = "stage2",    Title = "Добраться до Карьера",          Metric = DailyMetric.Stage,       Target = 2,    Reward = 100 },
            new Def { Id = "stage4",    Title = "Добраться до Облаков",          Metric = DailyMetric.Stage,       Target = 4,    Reward = 180 }
        };
    }

    public static int PoolCount { get { return _pool.Count; } }

    private static DateTime Now { get { return NowSource(); } }
    public static string DateKey { get { return Now.ToString("yyyy-MM-dd"); } }

    // ---------------- выбор заданий дня ----------------

    /// <summary>Три задания дня в стабильном порядке.</summary>
    public static List<DailyQuest> List()
    {
        List<Def> picked = Pick();
        var result = new List<DailyQuest>(picked.Count);
        for (int i = 0; i < picked.Count; i++)
            result.Add(Load(picked[i]));
        return result;
    }

    private static List<Def> Pick()
    {
        string key = DateKey;
        int seed = 17;
        for (int i = 0; i < key.Length; i++)
            seed = seed * 31 + key[i];
        if (seed < 0) seed = -seed;

        var picked = new List<Def>(DailyCount);
        var used = new bool[_pool.Count];
        for (int p = 0; p < DailyCount && _pool.Count > 0; p++)
        {
            int idx = (seed + p * 40503) % _pool.Count;
            int guard = 0;
            while (used[idx] && guard++ < _pool.Count)
                idx = (idx + 1) % _pool.Count;
            used[idx] = true;
            picked.Add(_pool[idx]);
        }
        return picked;
    }

    private static DailyQuest Load(Def def)
    {
        return new DailyQuest
        {
            Id = def.Id,
            Title = def.Title,
            Metric = def.Metric,
            Target = def.Target,
            Reward = def.Reward,
            Progress = PlayerPrefs.GetInt(ProgressKey(def.Id), 0),
            Claimed = PlayerPrefs.GetInt(ClaimKey(def.Id), 0) == 1
        };
    }

    // ---------------- прогресс ----------------

    private static string ProgressKey(string id) { return "quest_p_" + DateKey + "_" + id; }
    private static string ClaimKey(string id) { return "quest_c_" + DateKey + "_" + id; }

    private static void Bump(Def def, int delta)
    {
        if (delta <= 0 || def.Metric == DailyMetric.RunMax) return;
        int current = PlayerPrefs.GetInt(ProgressKey(def.Id), 0);
        if (current >= def.Target) return;
        PlayerPrefs.SetInt(ProgressKey(def.Id), Mathf.Min(def.Target, current + delta));
        SaveService.MarkLocal();
    }

    private static void SetMax(Def def, int value)
    {
        int current = PlayerPrefs.GetInt(ProgressKey(def.Id), 0);
        if (value <= current) return;
        PlayerPrefs.SetInt(ProgressKey(def.Id), Mathf.Min(def.Target, value));
        SaveService.MarkLocal();
    }

    private static HashSet<string> CompletedIds()
    {
        List<DailyQuest> quests = List();
        var set = new HashSet<string>();
        for (int i = 0; i < quests.Count; i++)
        {
            if (quests[i].IsComplete) set.Add(quests[i].Id);
        }
        return set;
    }

    private static List<DailyQuest> NewlyCompleted(HashSet<string> before, HashSet<string> after)
    {
        _fresh.Clear();
        List<DailyQuest> quests = List();
        for (int i = 0; i < quests.Count; i++)
        {
            if (quests[i].IsComplete && !before.Contains(quests[i].Id))
                _fresh.Add(quests[i]);
        }
        return _fresh;
    }

    /// <summary>Отрезок забега закрыт (смерть/респавн/выход). Обновляет задания
    /// на высоту, рекорд отрезка и счётчик забегов. Возвращает только что
    /// выполненные задания — для тоста.</summary>
    public static List<DailyQuest> RegisterRun(int levelHeight)
    {
        HashSet<string> before = CompletedIds();

        if (levelHeight > 0)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                Def def = _pool[i];
                switch (def.Metric)
                {
                    case DailyMetric.ClimbSum: Bump(def, levelHeight); break;
                    case DailyMetric.RunMax: SetMax(def, levelHeight); break;
                    case DailyMetric.Runs: Bump(def, 1); break;
                }
            }
        }

        HashSet<string> after = CompletedIds();
        List<DailyQuest> fresh = NewlyCompleted(before, after);
        MetaEvents.RaiseQuestsChanged();
        return fresh;
    }

    public static void RegisterCoins(int amount)
    {
        if (amount <= 0) return;
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i].Metric == DailyMetric.CoinsPicked)
                Bump(_pool[i], amount);
        }
        MetaEvents.RaiseQuestsChanged();
    }

    public static void RegisterJump()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i].Metric == DailyMetric.Jumps)
                Bump(_pool[i], 1);
        }
        MetaEvents.RaiseQuestsChanged();
    }

    public static void RegisterStage(int stageIndex)
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i].Metric == DailyMetric.Stage)
                SetMax(_pool[i], stageIndex);
        }
        MetaEvents.RaiseQuestsChanged();
    }

    // ---------------- заявка ----------------

    public static bool Claim(int index, out string message)
    {
        message = "";
        List<DailyQuest> quests = List();
        if (index < 0 || index >= quests.Count)
        {
            message = "Задание не найдено";
            return false;
        }

        DailyQuest quest = quests[index];
        if (quest.Claimed)
        {
            message = "Уже получено";
            return false;
        }
        if (!quest.IsComplete)
        {
            message = "Ещё не выполнено";
            return false;
        }

        SkinService.AddCoins(quest.Reward);
        PlayerPrefs.SetInt(ClaimKey(quest.Id), 1);
        PlayerPrefs.Save();
        MetaEvents.RaiseQuestsChanged();
        message = "+" + quest.Reward + " монет";
        return true;
    }

    /// <summary>Сколько выполненных, но незаявленных заданий — для бейджа меню.</summary>
    public static int CompletedUnclaimed()
    {
        List<DailyQuest> quests = List();
        int count = 0;
        for (int i = 0; i < quests.Count; i++)
        {
            if (quests[i].IsComplete && !quests[i].Claimed) count++;
        }
        return count;
    }
}