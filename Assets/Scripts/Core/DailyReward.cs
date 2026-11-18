using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ежедневная награда: цикл из 7 дней. Дни 1–6 — монеты, 7-й — скин.
///
/// Зачем это вообще нужно. До ежедневки в экономике было две дыры:
/// монеты копились в никуда (потратить можно было 1800 — ровно на три скина),
/// а повторное прохождение не приносило вообще ничего: рекорд не растёт,
/// значит не капают ни очки, ни вехи, ни награда за чекпоинт. Ежедневка
/// даёт причину заходить каждый день и наконец создаёт сток для монет.
///
/// Состояние — два значения в PlayerPrefs: сколько наград получено и дата
/// последней. Сервера нет, поэтому честность держится только на системных
/// часах игрока. Это осознанный размен: без сервера работает офлайн и без
/// аккаунтов, но перевод часов позволяет набить награды. В поле даты
/// специально лежит защита от перевода назад (см. ClaimedToday).
/// </summary>
public static class DailyReward
{
    public const int CycleLength = 7;
    public const int SkinDay = 7;

    /// <summary>Монеты дней 1–6 первого круга. Сумма 700 — ровно цена скина gold.</summary>
    private static readonly int[] WeekOneCoins = { 50, 75, 100, 125, 150, 200 };

    /// <summary>Каждый следующий круг монеты дороже — иначе второй круг неинтересен.</summary>
    public const float CoinGrowth = 1.5f;
    public const int MaxGrowthCycle = 8;

    /// <summary>Запасная выплата за 7-й день, если скин этой недели уже есть.</summary>
    public const int SkinDayFallbackCoins = 600;

    private static string DayKey(DateTime day)
    {
        return day.ToString("yyyy-MM-dd");
    }

    private static string TodayKey { get { return DayKey(DateTime.Now); } }

    /// <summary>Сколько наград получено всего. От этого и день цикла, и цена монет.</summary>
    public static int Claims { get { return GameProgress.GetDailyClaims(); } }

    /// <summary>Номер круга, начиная с 1.</summary>
    public static int Cycle { get { return Claims / CycleLength + 1; } }

    /// <summary>День внутри круга, 1..7.</summary>
    public static int DayOfCycle { get { return Claims % CycleLength + 1; } }

    public static bool IsSkinDay { get { return DayOfCycle == SkinDay; } }

    /// <summary>
    /// Забрана ли награда сегодня. Дата из будущего считается «не забрано»:
    /// если игрок перевёл часы вперёд, а потом назад, он иначе заперт был бы
    /// навсегда — дата в прошлом, а «сегодня» ещё не наступило.
    /// </summary>
    public static bool ClaimedToday
    {
        get
        {
            string stored = GameProgress.GetDailyLastClaim();
            if (string.IsNullOrEmpty(stored)) return false;
            int cmp = string.CompareOrdinal(stored, TodayKey);
            return cmp == 0;
        }
    }

    public static bool CanClaim { get { return !ClaimedToday; } }

    /// <summary>
    /// Множитель монет для текущего круга. Считается от номера круга, а не от
    /// числа полученных наград: иначе цены ползут каждый день, и «первая неделя»
    /// стоила бы не 700, а 3087 — предпросмотр недель в панели врал бы, потому
    /// что суммировал бы уже выданное.
    ///
    /// Рост ограничен, иначе на сотом круге выплаты уходят в астрономию.
    /// </summary>
    private static float Growth
    {
        get { return Mathf.Pow(CoinGrowth, Mathf.Min(Cycle - 1, MaxGrowthCycle)); }
    }

    /// <summary>Монеты за конкретный день круга. 7-й день монет не даёт.</summary>
    public static int CoinsForDay(int dayOfCycle)
    {
        if (dayOfCycle < 1 || dayOfCycle >= SkinDay) return 0;
        return Mathf.RoundToInt(WeekOneCoins[dayOfCycle - 1] * Growth);
    }

    /// <summary>Все скины ежедневки в порядке выдачи.</summary>
    public static List<SkinDefinition> Pool()
    {
        List<SkinDefinition> all = SkinCatalog.All;
        var result = new List<SkinDefinition>();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Unlock == SkinUnlock.Daily) result.Add(all[i]);
        }
        return result;
    }

    /// <summary>
    /// Скин, который выдаётся за 7-й день указанного круга. null, если такой
    /// уже получен: круги идут по кругу, а второй раз выдать один и тот же
    /// скин бессмысленно.
    /// </summary>
    public static SkinDefinition SkinForCycle(int cycle)
    {
        List<SkinDefinition> pool = Pool();
        if (pool.Count == 0) return null;
        SkinDefinition skin = pool[(Mathf.Max(1, cycle) - 1) % pool.Count];
        return SkinService.IsUnlocked(skin) ? null : skin;
    }

    /// <summary>Скин, который заберут сегодня, если сегодня 7-й день.</summary>
    public static SkinDefinition SkinToday()
    {
        return IsSkinDay ? SkinForCycle(Cycle) : null;
    }

    /// <summary>Монеты, которые заберут сегодня. Ноль, если сегодня скин.</summary>
    public static int CoinsToday()
    {
        if (IsSkinDay) return SkinToday() != null ? 0 : Mathf.RoundToInt(SkinDayFallbackCoins * Growth);
        return CoinsForDay(DayOfCycle);
    }

    /// <summary>Что показывать в строке дня — по кнопке «Забрать».</summary>
    public static string ClaimLabel()
    {
        SkinDefinition skin = SkinToday();
        if (skin != null) return "Забрать скин: " + skin.Title;
        int coins = CoinsToday();
        return coins > 0 ? "Забрать +" + coins + " монет" : "Награда получена";
    }

    /// <summary>Что показывать в строке дня в обзоре недель.</summary>
    public static string RewardLabel(int dayOfCycle)
    {
        if (dayOfCycle == SkinDay)
        {
            SkinDefinition skin = SkinForCycle(Cycle);
            return skin != null ? "Скин: " + skin.Title : "+" + SkinDayFallbackCoins + " монет";
        }
        return "+" + CoinsForDay(dayOfCycle) + " монет";
    }

    /// <summary>
    /// Выдать награду. Возвращает текст, что именно выдали — его показывает
    /// меню, иначе игрок не видит, куда делись монеты. Первое действие
    /// на сегодня всегда проходит, даже если записи о нём ещё не было.
    /// </summary>
    public static bool Claim(out string message)
    {
        if (ClaimedToday)
        {
            message = "Сегодня уже получено. Заходи завтра.";
            return false;
        }

        bool skinDay = IsSkinDay;
        SkinDefinition skin = skinDay ? SkinToday() : null;

        // Считаем ДО AddDailyClaim(): и DayOfCycle, и Growth выводятся из Claims,
        // а после прибавки сумма была бы уже от следующего дня.
        int coins;
        if (skin != null) coins = 0;
        else if (skinDay) coins = Mathf.RoundToInt(SkinDayFallbackCoins * Growth);
        else coins = CoinsForDay(DayOfCycle);

        if (skin != null)
        {
            GameProgress.SetSkinOwned(skin.Id, true);
            SkinService.Select(skin.Id);
            // Select поднимает событие только если скин сменился. Здесь игрок
            // почти наверняка переключился, но «почти» — не повод полагаться:
            // после гарантированного SetSkinOwned событие поднимаем сами.
            SkinService.Raise();
            message = "Скин «" + skin.Title + "» надет!";
        }
        else
        {
            SkinService.AddCoins(coins);   // само поднимает событие
            message = "+" + coins + " монет";
        }

        GameProgress.SetDailyLastClaim(TodayKey);
        GameProgress.AddDailyClaim();
        return true;
    }
}