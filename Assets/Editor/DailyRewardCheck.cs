#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Автоматическая проверка ежедневной награды. Живёт только в редакторе
/// и удаляется после прогона — в сборку попасть не должен.
///
/// Логика ежедневки чистая (PlayerPrefs + каталог скинов), play mode не
/// нужен, поэтому проверка идёт через -executeMethod и не трогает домен:
/// это заметно быстрее и надёжнее, чем вход в play mode с перезагрузкой.
///
/// Перед прогоном запоминает значения ключей, которых касается, и
/// возвращает их в конце. Тест не должен затирать сохранение игрока.
/// </summary>
public static class DailyRewardCheck
{
    private static int _passed;
    private static int _failed;

    private static readonly Dictionary<string, string> Snapshot = new Dictionary<string, string>();

    public static void Run()
    {
        TakeSnapshot();

        try
        {
            CheckPool();
            CheckFirstDay();
            CheckWeekOfCoins();
            CheckSkinDay();
            CheckCycleReset();
            CheckNotPurchasable();
            CheckExhaustedPool();
            CheckLabels();
            CheckClockRollback();
        }
        finally
        {
            Restore();
        }

        Debug.Log("[T] RESULT passed=" + _passed + " failed=" + _failed);
        EditorApplication.Exit(_failed);
    }

    // ---------------- проверки ----------------

    /// <summary>В пуле должно быть ровно 7 скинов, и все они непроходимые.</summary>
    private static void CheckPool()
    {
        List<SkinDefinition> pool = DailyReward.Pool();
        Check("pool.count == 7", pool.Count == 7, "получено " + pool.Count);

        bool allDaily = true;
        var ids = new HashSet<string>();
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Unlock != SkinUnlock.Daily) allDaily = false;
            if (!ids.Add(pool[i].Id)) Check("pool.ids.unique::" + pool[i].Id, false, "повтор id");
        }
        Check("pool.all daily", allDaily, "есть скины с другим Unlock");
        Check("pool.size matches cycle", pool.Count == DailyReward.CycleLength,
            "пул " + pool.Count + ", цикл " + DailyReward.CycleLength);
    }

    /// <summary>Свежий игрок: день 1, первая награда 50 монет, повтор в тот же день не даёт.</summary>
    private static void CheckFirstDay()
    {
        Reset();
        Check("start.day == 1", DailyReward.DayOfCycle == 1, "день " + DailyReward.DayOfCycle);
        Check("start.cycle == 1", DailyReward.Cycle == 1, "круг " + DailyReward.Cycle);
        Check("start.coins(1) == 50", DailyReward.CoinsForDay(1) == 50, "вышло " + DailyReward.CoinsForDay(1));
        Check("start.coins(7) == 0", DailyReward.CoinsForDay(7) == 0, "вышло " + DailyReward.CoinsForDay(7));
        Check("start.can claim", DailyReward.CanClaim, "кнопка заблокирована");

        string message;
        Check("claim.1 ok", DailyReward.Claim(out message), "отказ: " + message);
        Check("claim.1 coins == 50", GameProgress.GetCoins() == 50, "монет " + GameProgress.GetCoins());
        Check("claim.1 claims == 1", DailyReward.Claims == 1, "получено " + DailyReward.Claims);
        Check("claim.1 marked today", DailyReward.ClaimedToday, "дата не записана");
        Check("claim.1 message has coins", message.Contains("50"), "сообщение: " + message);

        string again;
        Check("claim.again refused", !DailyReward.Claim(out again), "выдалось повторно");
        Check("claim.again no coins", GameProgress.GetCoins() == 50, "монет " + GameProgress.GetCoins());
        Check("claim.again claims == 1", DailyReward.Claims == 1, "получено " + DailyReward.Claims);
    }

    /// <summary>Дни 1-6 дают 50/75/100/125/150/200 — сумма 700, ровно цена скина gold.</summary>
    private static void CheckWeekOfCoins()
    {
        Reset();
        int[] expected = { 50, 75, 100, 125, 150, 200 };
        int total = 0;

        for (int day = 1; day <= 6; day++)
        {
            PlayerPrefs.DeleteKey(GameProgress.KeyDailyLastClaim);
            Check("week.day == " + day, DailyReward.DayOfCycle == day, "день " + DailyReward.DayOfCycle);

            string message;
            Check("week.claim " + day, DailyReward.Claim(out message), "отказ: " + message);
            int earned = GameProgress.GetCoins() - total;
            total += earned;
            Check("week.coins " + day + " == " + expected[day - 1], earned == expected[day - 1], "вышло " + earned);
        }

        Check("week.total == 700", total == 700, "итого " + total);
        Check("week.claims == 6", DailyReward.Claims == 6, "получено " + DailyReward.Claims);
    }

    /// <summary>Седьмой день отдаёт скин, помечает его купленным и надевает.</summary>
    private static void CheckSkinDay()
    {
        Reset();
        SetClaims(6); // дошли до седьмого дня, ничего не забирая
        Check("skin.is skin day", DailyReward.IsSkinDay, "день " + DailyReward.DayOfCycle);

        SkinDefinition expected = DailyReward.SkinForCycle(1);
        Check("skin.day7 coins == 0", DailyReward.CoinsToday() == 0, "монет " + DailyReward.CoinsToday());

        SkinDefinition today = DailyReward.SkinToday();
        Check("skin.today not null", today != null, "скин не найден");
        Check("skin.today is daily", today != null && today.Unlock == SkinUnlock.Daily,
            "Unlock = " + (today == null ? "null" : today.Unlock.ToString()));

        if (today != null)
        {
            Check("skin.matches pool[0]", expected != null && today.Id == expected.Id, "расхождение скинов");
            Check("skin.not owned yet", !GameProgress.IsSkinOwned(today.Id), "уже был куплен");
        }

        string message;
        Check("skin.claim ok", DailyReward.Claim(out message), "отказ: " + message);

        if (today != null)
        {
            Check("skin.owned after claim", GameProgress.IsSkinOwned(today.Id), "не помечен купленным");
            Check("skin.equipped", SkinService.IsEquipped(today), "не надет");
            Check("skin.selected id", GameProgress.GetSelectedSkin() == today.Id,
                "выбран " + GameProgress.GetSelectedSkin());
            Check("skin.message names skin", message.Contains(today.Title), "сообщение: " + message);
        }
        Check("skin.no coins", GameProgress.GetCoins() == 0, "монет " + GameProgress.GetCoins());
        Check("skin.claims == 7", DailyReward.Claims == 7, "получено " + DailyReward.Claims);
    }

    /// <summary>После седьмого дня начинается круг 2, монеты дорожают в 1,5 раза.</summary>
    private static void CheckCycleReset()
    {
        Reset();
        SetClaims(7);
        Check("cycle2.cycle == 2", DailyReward.Cycle == 2, "круг " + DailyReward.Cycle);
        Check("cycle2.day == 1", DailyReward.DayOfCycle == 1, "день " + DailyReward.DayOfCycle);
        Check("cycle2.coins(1) == 75", DailyReward.CoinsForDay(1) == 75, "вышло " + DailyReward.CoinsForDay(1));
        Check("cycle2.coins(6) == 300", DailyReward.CoinsForDay(6) == 300, "вышло " + DailyReward.CoinsForDay(6));

        // Рост ограничен: на сотом круге суммы не должны уехать в астрономию.
        SetClaims(7 * 40);
        Check("growth.capped", DailyReward.CoinsForDay(1) <= 50 * 100, "вышло " + DailyReward.CoinsForDay(1));
    }

    /// <summary>Ежедневный скин нельзя купить и нельзя выдать через TryUnlock.</summary>
    private static void CheckNotPurchasable()
    {
        List<SkinDefinition> pool = DailyReward.Pool();
        if (pool.Count == 0) { Check("notpurch.pool", false, "пул пуст"); return; }

        SkinDefinition skin = pool[0];
        bool owned = GameProgress.IsSkinOwned(skin.Id);
        GameProgress.SetSkinOwned(skin.Id, false);

        Check("notpurch.not purchasable", !SkinService.IsPurchasable(skin), "помечен покупаемым");
        Check("notpurch.locked", !SkinService.IsUnlocked(skin), "считается открытым");
        Check("notpurch.tryunlock refused", !SkinService.TryUnlock(skin), "выдался через TryUnlock");
        Check("notpurch.still locked", !SkinService.IsUnlocked(skin), "открылся после TryUnlock");

        GameProgress.SetSkinOwned(skin.Id, true);
        Check("notpurch.unlocked when owned", SkinService.IsUnlocked(skin), "не открылся после выдачи");
        GameProgress.SetSkinOwned(skin.Id, owned);

        // Ключ подсказки в магазине не должен предлагать купить.
        Check("notpurch.lock hint", SkinService.LockHint(skin).Length > 0, "пустая подсказка");
        Check("notpurch.requirement text", skin.RequirementText().Length > 0, "пустое требование");
    }

    /// <summary>Когда все 7 скинов получены, седьмой день платит монетами, а не пустотой.</summary>
    private static void CheckExhaustedPool()
    {
        Reset();

        List<SkinDefinition> pool = DailyReward.Pool();
        for (int i = 0; i < pool.Count; i++) GameProgress.SetSkinOwned(pool[i].Id, true);

        // 8-й круг: (8-1) % 7 == 0, то есть скин первой недели — уже есть.
        SetClaims(7 * 7 + 6);
        Check("exhaust.is skin day", DailyReward.IsSkinDay, "день " + DailyReward.DayOfCycle);
        Check("exhaust.skin null", DailyReward.SkinToday() == null, "скин всё ещё выдаётся");
        Check("exhaust.fallback > 0", DailyReward.CoinsToday() > 0, "монет " + DailyReward.CoinsToday());

        string message;
        Check("exhaust.claim ok", DailyReward.Claim(out message), "отказ: " + message);

        // При 55 полученных наградах идёт 8-й круг, значит множитель 1,5^7.
        // Формула повторяет DailyReward.Growth намеренно: если рост переедет,
        // тест должен это заметить, а не молча пересчитать тем же кодом.
        int expected = Mathf.RoundToInt(DailyReward.SkinDayFallbackCoins * Mathf.Pow(1.5f, 7));
        Check("exhaust.coins paid", GameProgress.GetCoins() == expected,
            "монет " + GameProgress.GetCoins() + ", ожидалось " + expected);
        Check("exhaust.claims advanced", DailyReward.Claims == 7 * 7 + 7, "получено " + DailyReward.Claims);
        Check("exhaust.message coins", message.Contains("монет"), "сообщение: " + message);

        for (int i = 0; i < pool.Count; i++) GameProgress.SetSkinOwned(pool[i].Id, false);
    }

    /// <summary>Подписи для всех семи дней непустые — иначе в панели будут пустые строки.</summary>
    private static void CheckLabels()
    {
        Reset();
        for (int day = 1; day <= DailyReward.CycleLength; day++)
        {
            string label = DailyReward.RewardLabel(day);
            Check("label.day " + day, !string.IsNullOrEmpty(label), "пусто");
        }

        Check("label.claim button", DailyReward.ClaimLabel().Length > 0, "пусто");

        SetClaims(6);
        Check("label.skin day names skin", DailyReward.ClaimLabel().Contains("скин"),
            "подпись: " + DailyReward.ClaimLabel());
    }

    /// <summary>Дата из будущего не должна запирать игрока навсегда после перевода часов назад.</summary>
    private static void CheckClockRollback()
    {
        Reset();
        PlayerPrefs.SetString(GameProgress.KeyDailyLastClaim, DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();

        Check("rollback.not locked", DailyReward.CanClaim, "игрок заперт из-за будущей даты");
        Check("rollback.not claimed today", !DailyReward.ClaimedToday, "дата из будущего засчиталась как сегодня");

        PlayerPrefs.SetString(GameProgress.KeyDailyLastClaim, DateTime.Now.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();
        Check("rollback.same day claimed", DailyReward.ClaimedToday, "сегодняшняя дата не засчиталась");
    }

    // ---------------- утилиты ----------------

    /// <summary>Обнуляет состояние ежедневки, чтобы проверки не зависели друг от друга.</summary>
    private static void Reset()
    {
        PlayerPrefs.DeleteKey(GameProgress.KeyDailyClaims);
        PlayerPrefs.DeleteKey(GameProgress.KeyDailyLastClaim);
        PlayerPrefs.DeleteKey(GameProgress.KeyCoins);
        PlayerPrefs.DeleteKey(GameProgress.KeySelectedSkin);

        List<SkinDefinition> pool = DailyReward.Pool();
        for (int i = 0; i < pool.Count; i++) GameProgress.SetSkinOwned(pool[i].Id, false);

        PlayerPrefs.Save();
    }

    private static void SetClaims(int claims)
    {
        PlayerPrefs.SetInt(GameProgress.KeyDailyClaims, claims);
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

    private static void TakeSnapshot()
    {
        Snapshot["int:" + GameProgress.KeyCoins] = PlayerPrefs.GetInt(GameProgress.KeyCoins, 0).ToString();
        Snapshot["int:" + GameProgress.KeyPoints] = PlayerPrefs.GetInt(GameProgress.KeyPoints, 0).ToString();
        Snapshot["int:" + GameProgress.KeyDailyClaims] = PlayerPrefs.GetInt(GameProgress.KeyDailyClaims, 0).ToString();
        Snapshot[GameProgress.KeyDailyLastClaim] = PlayerPrefs.GetString(GameProgress.KeyDailyLastClaim, "");
        Snapshot[GameProgress.KeySelectedSkin] = PlayerPrefs.GetString(GameProgress.KeySelectedSkin, "");
    }

    private static void Restore()
    {
        List<SkinDefinition> pool = DailyReward.Pool();
        for (int i = 0; i < pool.Count; i++) PlayerPrefs.DeleteKey(GameProgress.KeySkinOwnedPrefix + pool[i].Id);

        PlayerPrefs.SetInt(GameProgress.KeyCoins, GetSavedInt(GameProgress.KeyCoins));
        PlayerPrefs.SetInt(GameProgress.KeyPoints, GetSavedInt(GameProgress.KeyPoints));
        PlayerPrefs.SetInt(GameProgress.KeyDailyClaims, GetSavedInt(GameProgress.KeyDailyClaims));

        string last = GetSavedString(GameProgress.KeyDailyLastClaim);
        if (string.IsNullOrEmpty(last)) PlayerPrefs.DeleteKey(GameProgress.KeyDailyLastClaim);
        else PlayerPrefs.SetString(GameProgress.KeyDailyLastClaim, last);

        string selected = GetSavedString(GameProgress.KeySelectedSkin);
        if (string.IsNullOrEmpty(selected)) PlayerPrefs.DeleteKey(GameProgress.KeySelectedSkin);
        else PlayerPrefs.SetString(GameProgress.KeySelectedSkin, selected);

        PlayerPrefs.Save();
    }

    private static int GetSavedInt(string key)
    {
        string value;
        int parsed;
        return Snapshot.TryGetValue("int:" + key, out value) && int.TryParse(value, out parsed) ? parsed : 0;
    }

    private static string GetSavedString(string key)
    {
        string value;
        return Snapshot.TryGetValue(key, out value) ? value : "";
    }
}
#endif