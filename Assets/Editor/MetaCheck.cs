#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Автоматическая проверка мета-систем: батл-пасс, дейлики, ачивки.
/// Только редактор; в сборку не попадает.
///
/// Вся логика чистая (PlayerPrefs + каталог скинов), play mode не нужен.
/// Проверка подменяет системные часы (NowSource) и полностью затирает кучу
/// мета-ключей, поэтому в самом конце восстанавливает и часы, и всё сохранение:
/// мета-ключи, кошелёк, выбранный скин, владение пасс-скинами. Набор ключей,
/// которые тест трогает, ровно совпадает с набором, которые тест возвращает, —
/// ничего за пределами теста не теряется.
/// </summary>
public static class MetaCheck
{
    private static int _passed;
    private static int _failed;

    private static readonly string[] QuestIds =
    {
        "climb300", "climb900", "run120", "run500", "coins25", "coins75",
        "runs3", "jumps30", "stage2", "stage4"
    };

    private static readonly string[] AchIds =
    {
        "ach_height_100", "ach_height_500", "ach_heaven",
        "ach_stage_forest", "ach_stage_quarry", "ach_stage_clouds",
        "ach_finished", "ach_climb_1000", "ach_climb_10000",
        "ach_coins_500", "ach_coins_5000", "ach_runs_25"
    };

    private static readonly string[] PassSkinIds =
    {
        "pass_verdant", "pass_sunray", "pass_argenteus", "pass_aurora", "pass_regal"
    };

    private static Func<DateTime> _savedPassClock;
    private static Func<DateTime> _savedQuestClock;

    // Всё сохранение, которого касается тест: ключ -> значение до прогона.
    private static readonly Dictionary<string, string> Saved = new Dictionary<string, string>();
    private static readonly List<string> AllTouchedKeys = new List<string>();

    public static int Check()
    {
        _passed = 0;
        _failed = 0;

        SaveClocks();
        SnapshotAll();

        try
        {
            SeasonPassMath();
            SeasonPassClaims();
            SeasonPassRollover();
            QuestPool();
            QuestDeterminism();
            QuestProgressAndClaim();
            QuestRollover();
            AchievementsDefs();
            AchievementsEvaluate();
        }
        finally
        {
            WipeAll();
            RestoreAll();
            RestoreClocks();
        }

        Debug.Log("[T] RESULT MetaCheck passed=" + _passed + " failed=" + _failed);
        return _failed;
    }

    // ---------------- батл-пасс: математика ----------------

    private static void SeasonPassMath()
    {
        SetClock(new DateTime(2026, 10, 13));
        WipeAll();

        for (int n = 1; n < SeasonPass.LevelCount; n++)
        {
            Check("pass.need grows at " + n, SeasonPass.Need(n + 1) > SeasonPass.Need(n),
                SeasonPass.Need(n) + " -> " + SeasonPass.Need(n + 1));
        }
        Check("pass.cum(1)==60", SeasonPass.Cumulative(1) == 60, "вышло " + SeasonPass.Cumulative(1));
        Check("pass.cum(5)==500", SeasonPass.Cumulative(5) == 500, "вышло " + SeasonPass.Cumulative(5));
        Check("pass.cum(30)==10500", SeasonPass.Cumulative(30) == 10500, "вышло " + SeasonPass.Cumulative(30));

        SetXp(0);
        Check("pass.level 0 at xp0", SeasonPass.Level == 0, "уровень " + SeasonPass.Level);
        SetXp(SeasonPass.Cumulative(1) - 1);
        Check("pass.level 0 below level1", SeasonPass.Level == 0, "уровень " + SeasonPass.Level);
        SetXp(SeasonPass.Cumulative(1));
        Check("pass.level 1 at cum1", SeasonPass.Level == 1, "уровень " + SeasonPass.Level);
        SetXp(SeasonPass.Cumulative(5) - 1);
        Check("pass.level 4 below cum5", SeasonPass.Level == 4, "уровень " + SeasonPass.Level);
        SetXp(SeasonPass.Cumulative(5));
        Check("pass.level 5 at cum5", SeasonPass.Level == 5, "уровень " + SeasonPass.Level);
        SetXp(999999);
        Check("pass.level capped 40", SeasonPass.Level == SeasonPass.LevelCount, "уровень " + SeasonPass.Level);
        Check("pass.capped no next", SeasonPass.XpToNext == 0, "XP до следующего " + SeasonPass.XpToNext);

        SetXp(0);
        int before = GameProgress.GetCoins();
        SeasonPass.AddRunHeight(320);
        Check("pass.run adds xp", SeasonPass.Xp == 320, "XP " + SeasonPass.Xp);
        Check("pass.run no coins", GameProgress.GetCoins() == before, "монеты изменились");
    }

    // ---------------- батл-пасс: заявки ----------------

    private static void SeasonPassClaims()
    {
        SetClock(new DateTime(2026, 10, 13));
        WipeAll();

        Check("pass.claim free blocked at 0", !SeasonPass.CanClaimFree(1), "можно забрать на нуле");

        SetXp(SeasonPass.Cumulative(10));
        Check("pass.level 10", SeasonPass.Level == 10, "уровень " + SeasonPass.Level);
        Check("pass.free(10) claimable", SeasonPass.CanClaimFree(10), "нельзя забрать ур 10");
        Check("pass.free(11) no reward", !SeasonPass.CanClaimFree(11), "награда есть на 11");

        int before = GameProgress.GetCoins();
        string message;
        Check("pass.free(10) claim ok", SeasonPass.ClaimFree(10, out message), "отказ: " + message);
        Check("pass.free(10) pays no coins", GameProgress.GetCoins() - before == 0,
            "заплачено " + (GameProgress.GetCoins() - before));
        Check("pass.free(10) skin owned", GameProgress.IsSkinOwned("pass_verdant"), "скин не выдан");
        Check("pass.free(10) marked", SeasonPass.FreeClaimed(10), "отметка не записана");
        Check("pass.free(10) not claimable twice", !SeasonPass.CanClaimFree(10), "можно забрать повторно");
        Check("pass.free(10) second refused", !SeasonPass.ClaimFree(10, out message), "повтор выплатился");

        SetXp(SeasonPass.Cumulative(5));
        Check("pass.premium blocked without pass", !SeasonPass.CanClaimPremium(5), "премиум доступен без покупки");
        string prem;
        Check("pass.premium claim refused", !SeasonPass.ClaimPremium(5, out prem), "премиум выдался без флага");

        SeasonPass.SetPremium(true);
        Check("pass.premium flag on", SeasonPass.IsPremium, "флаг не установился");
        Check("pass.premium(5) claimable", SeasonPass.CanClaimPremium(5), "нельзя забрать премиум ур 5");
        Check("pass.premium(5) claim ok", SeasonPass.ClaimPremium(5, out prem), "отказ: " + prem);
        Check("pass.premium(5) skin owned", GameProgress.IsSkinOwned("pass_argenteus"), "премиум-скин не выдан");
        Check("pass.premium(5) marked", SeasonPass.PremiumClaimed(5), "премиум-отметка не записана");
        Check("pass.premium(5) not twice", !SeasonPass.CanClaimPremium(5), "можно забрать повторно");

        // Премиум-монеты на 8-м уровне платятся, когда уровень достигнут.
        SetXp(SeasonPass.Cumulative(8));
        before = GameProgress.GetCoins();
        Check("pass.premium(8) claimable", SeasonPass.CanClaimPremium(8), "нельзя забрать ур 8");
        Check("pass.premium(8) ok", SeasonPass.ClaimPremium(8, out prem), "отказ: " + prem);
        Check("pass.premium(8) pays 95", GameProgress.GetCoins() - before == 95,
            "заплачено " + (GameProgress.GetCoins() - before));
    }

    // ---------------- батл-пасс: смена сезона ----------------

    private static void SeasonPassRollover()
    {
        SetClock(new DateTime(2026, 10, 13));
        WipeAll();

        SeasonPass.AddRunHeight(500);
        SeasonPass.SetPremium(true);
        Check("rollover season got xp", SeasonPass.Xp == 500, "XP " + SeasonPass.Xp);
        Check("rollover premium on", SeasonPass.IsPremium, "премиум не включён");

        SetClock(new DateTime(2026, 11, 3));
        Check("rollover xp reset", SeasonPass.Xp == 0, "XP " + SeasonPass.Xp);
        Check("rollover level reset", SeasonPass.Level == 0, "уровень " + SeasonPass.Level);
        Check("rollover premium reset", !SeasonPass.IsPremium, "премиум не сброшен");
    }

    // ---------------- дейлики: пул ----------------

    private static void QuestPool()
    {
        Check("quest.pool == 10", DailyQuests.PoolCount == 10, "пул " + DailyQuests.PoolCount);

        SetClock(new DateTime(2026, 10, 13));
        WipeAll();

        List<DailyQuest> quests = DailyQuests.List();
        Check("quest.daily == 3", quests.Count == DailyQuests.DailyCount, "выдано " + quests.Count);

        var ids = new HashSet<string>();
        for (int i = 0; i < quests.Count; i++)
        {
            DailyQuest q = quests[i];
            Check("quest." + q.Id + " title", q.Title.Length > 0, "пустой заголовок");
            Check("quest." + q.Id + " target", q.Target > 0, "цель " + q.Target);
            Check("quest." + q.Id + " reward", q.Reward > 0, "награда " + q.Reward);
            if (!ids.Add(q.Id))
                Check("quest.unique::" + q.Id, false, "повтор задания в дне");
        }
        Check("quest.ids unique in day", ids.Count == quests.Count, "повтор id");
    }

    /// <summary>Один день — один набор. Два прочтения обязаны совпасть.</summary>
    private static void QuestDeterminism()
    {
        SetClock(new DateTime(2026, 10, 13));
        WipeAll();

        List<DailyQuest> first = DailyQuests.List();
        List<DailyQuest> second = DailyQuests.List();
        Check("quest.determinism count", first.Count == second.Count, first.Count + " против " + second.Count);
        bool same = true;
        if (first.Count == second.Count)
        {
            for (int i = 0; i < first.Count; i++)
                same = same && first[i].Id == second[i].Id;
        }
        Check("quest.determinism ids", same, "наборы разошлись");
    }

    // ---------------- дейлики: прогресс и заявка ----------------

    private static void QuestProgressAndClaim()
    {
        SetClock(new DateTime(2026, 10, 13));
        WipeAll();

        // Три забега, монеты, прыжки, ступени — закрывает все типы метрик.
        DailyQuests.RegisterRun(900);
        DailyQuests.RegisterRun(600);
        DailyQuests.RegisterRun(700);
        DailyQuests.RegisterCoins(80);
        for (int i = 0; i < 30; i++) DailyQuests.RegisterJump();
        DailyQuests.RegisterStage(4);

        List<DailyQuest> quests = DailyQuests.List();
        Check("quest.all complete", quests.Count == 3 &&
            quests[0].IsComplete && quests[1].IsComplete && quests[2].IsComplete,
            "не все выполнены");

        int before = GameProgress.GetCoins();
        string message;
        for (int i = 0; i < quests.Count; i++)
        {
            Check("quest.claim " + i, DailyQuests.Claim(i, out message), "отказ: " + message);
        }
        int earned = GameProgress.GetCoins() - before;
        int expected = quests[0].Reward + quests[1].Reward + quests[2].Reward;
        Check("quest.claim pays sum", earned == expected, "получено " + earned + ", ждали " + expected);

        Check("quest.claim second refused", !DailyQuests.Claim(0, out message), "повторная заявка прошла");
        Check("quest.completed unclaimed now 0", DailyQuests.CompletedUnclaimed() == 0,
            "осталось " + DailyQuests.CompletedUnclaimed());
    }

    // ---------------- дейлики: новый день ----------------

    private static void QuestRollover()
    {
        SetClock(new DateTime(2026, 10, 13));
        WipeAll();

        DailyQuests.RegisterRun(900);
        DailyQuests.RegisterCoins(80);
        DailyQuests.RegisterStage(4);
        Check("quest.day1 has ready", DailyQuests.CompletedUnclaimed() > 0,
            "не выполнено на первый день: " + DailyQuests.CompletedUnclaimed());

        SetClock(new DateTime(2026, 10, 14));
        List<DailyQuest> next = DailyQuests.List();
        Check("quest.day2 count", next.Count == DailyQuests.DailyCount, "выдано " + next.Count);
        bool allFresh = true;
        for (int i = 0; i < next.Count; i++)
            allFresh = allFresh && next[i].Progress == 0 && !next[i].Claimed;
        Check("quest.day2 fresh", allFresh, "прогресс перенёсся на новый день");
        Check("quest.day2 nothing ready", DailyQuests.CompletedUnclaimed() == 0,
            "готово " + DailyQuests.CompletedUnclaimed());
    }

    // ---------------- ачивки: каталог ----------------

    private static void AchievementsDefs()
    {
        IList<AchievementDefinition> all = Achievements.All;
        Check("ach.count == 12", all.Count == 12, "ачивок " + all.Count);

        var ids = new HashSet<string>();
        for (int i = 0; i < all.Count; i++)
        {
            AchievementDefinition def = all[i];
            Check("ach." + def.Id + " title", def.Title.Length > 0, "пустой заголовок");
            Check("ach." + def.Id + " desc", def.Description.Length > 0, "пустое описание");
            Check("ach." + def.Id + " reward", def.Reward > 0, "награда " + def.Reward);
            Check("ach." + def.Id + " target", def.Target > 0, "цель " + def.Target);
            if (!ids.Add(def.Id))
                Check("ach.unique::" + def.Id, false, "повтор id");
        }
        Check("ach.ids unique", ids.Count == all.Count, "повтор id");
    }

    // ---------------- ачивки: разблокировка ----------------

    private static void AchievementsEvaluate()
    {
        WipeAll();

        GameProgress.SetBest(100, 5);
        Achievements.Evaluate();
        Check("ach.height100 unlocked", Achievements.IsUnlocked("ach_height_100"), "не разблокирована");
        Check("ach.height100 pays 30", GameProgress.GetCoins() == 30, "монет " + GameProgress.GetCoins());

        int afterFirst = GameProgress.GetCoins();
        Achievements.Evaluate();
        Check("ach.no double pay", GameProgress.GetCoins() == afterFirst, "монеты удвоились");

        GameProgress.SetBest(1200, 30);
        GameProgress.SetStageReached(4);
        GameProgress.SetFinished();
        PlayerPrefs.SetInt(MetaSystems.KeyLifeClimb, 10000);
        PlayerPrefs.SetInt(MetaSystems.KeyLifeCoins, 5000);
        PlayerPrefs.SetInt(MetaSystems.KeyLifeRuns, 30);
        PlayerPrefs.Save();

        Achievements.Evaluate();
        Check("ach.all unlocked", Achievements.UnlockedCount() == Achievements.All.Count,
            "раскрыто " + Achievements.UnlockedCount() + " из " + Achievements.All.Count);

        int expectedCoins = 0;
        for (int i = 0; i < Achievements.All.Count; i++)
            expectedCoins += Achievements.All[i].Reward;
        Check("ach.pays total exactly", GameProgress.GetCoins() == expectedCoins,
            "монет " + GameProgress.GetCoins() + ", ждали " + expectedCoins);
    }

    // ---------------- утилиты ----------------

    private static void SetClock(DateTime time)
    {
        SeasonPass.NowSource = delegate { return time; };
        DailyQuests.NowSource = delegate { return time; };
    }

    private static void SaveClocks()
    {
        _savedPassClock = SeasonPass.NowSource;
        _savedQuestClock = DailyQuests.NowSource;
    }

    private static void RestoreClocks()
    {
        SeasonPass.NowSource = _savedPassClock != null ? _savedPassClock : delegate { return DateTime.Now; };
        DailyQuests.NowSource = _savedQuestClock != null ? _savedQuestClock : delegate { return DateTime.Now; };
    }

    private static void SetXp(int value)
    {
        PlayerPrefs.SetInt("pass_xp_" + SeasonPass.SeasonId, value);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Каждый ключ, который тест вообще способен удалить. Снапшот и затирание
    /// оперируют ровно этим списком, поэтому восстановление всегда точное.
    /// </summary>
    private static List<string> TouchedKeys()
    {
        AllTouchedKeys.Clear();

        AllTouchedKeys.Add("pass_season");
        AllTouchedKeys.Add(GameProgress.KeyCoins);
        AllTouchedKeys.Add(GameProgress.KeyPoints);
        AllTouchedKeys.Add(GameProgress.KeySelectedSkin);
        AllTouchedKeys.Add(GameProgress.KeyBestHeight);
        AllTouchedKeys.Add(GameProgress.KeyBestCheckpoint);
        AllTouchedKeys.Add(GameProgress.KeyStageReached);
        AllTouchedKeys.Add(GameProgress.KeyFinished);
        AllTouchedKeys.Add(MetaSystems.KeyLifeClimb);
        AllTouchedKeys.Add(MetaSystems.KeyLifeCoins);
        AllTouchedKeys.Add(MetaSystems.KeyLifeRuns);
        AllTouchedKeys.Add(MetaSystems.KeyLifeJumps);

        for (int i = 0; i < PassSkinIds.Length; i++)
            AllTouchedKeys.Add(GameProgress.KeySkinOwnedPrefix + PassSkinIds[i]);
        for (int i = 0; i < AchIds.Length; i++)
            AllTouchedKeys.Add("ach_done_" + AchIds[i]);

        for (int d = 0; d < 2; d++)
        {
            string date = d == 0 ? "2026-10-13" : "2026-10-14";
            for (int i = 0; i < QuestIds.Length; i++)
            {
                AllTouchedKeys.Add("quest_p_" + date + "_" + QuestIds[i]);
                AllTouchedKeys.Add("quest_c_" + date + "_" + QuestIds[i]);
            }
        }

        for (int y = 2026; y <= 2027; y++)
        {
            for (int m = 1; m <= 12; m++)
            {
                string s = y + "-" + m.ToString("00");
                AllTouchedKeys.Add("pass_xp_" + s);
                AllTouchedKeys.Add("pass_premium_" + s);
                for (int n = 1; n <= SeasonPass.LevelCount; n++)
                {
                    AllTouchedKeys.Add("pass_free_" + s + "_" + n);
                    AllTouchedKeys.Add("pass_premium_claim_" + s + "_" + n);
                }
            }
        }

        return AllTouchedKeys;
    }

    /// <summary>Запоминаем всё, чего коснёмся, со значениями до прогона.</summary>
    private static void SnapshotAll()
    {
        Saved.Clear();
        List<string> keys = TouchedKeys();
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            Saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, "") : null;
        }
    }

    private static void WipeAll()
    {
        List<string> keys = TouchedKeys();
        for (int i = 0; i < keys.Count; i++)
            PlayerPrefs.DeleteKey(keys[i]);
        PlayerPrefs.Save();
    }

    private static void RestoreAll()
    {
        List<string> keys = TouchedKeys();
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            string value;
            if (Saved.TryGetValue(key, out value) && value != null)
                PlayerPrefs.SetString(key, value);
            else
                PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
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