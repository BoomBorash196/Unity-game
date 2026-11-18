using System;
using UnityEngine;

/// <summary>
/// Батл-пасс. Сезон — календарный месяц: смена месяца обнуляет XP, премиум и
/// отметки наград (ключи хранилища привязаны к сезону, поэтому старые данные
/// просто перестают читаться, ничего стирать не надо).
///
/// Прогресс — только высота забегов (XP живёт в MetaSystems); никаких
/// «сделай N прыжков за пасс». Уровней 40, кривая need(n) = 40 + 20n.
/// Награда есть на каждом уровне обеих дорожек: монеты, очки и скины.
///
/// Премиум — флаг в сохранении. Покупки пока нет: флаг ставит будущий магазин
/// (Google Play billing). Сама механика «премиум-дорожки» при этом полностью
/// готова и тестируется проверкой в редакторе.
/// </summary>
public static class SeasonPass
{
    public const int LevelCount = 40;

    public static Func<DateTime> NowSource = delegate { return DateTime.Now; };

    private const string KeySeason = "pass_season";
    private const string KeyXpPrefix = "pass_xp_";
    private const string KeyPremiumPrefix = "pass_premium_";
    private const string KeyFreePrefix = "pass_free_";
    private const string KeyPremiumClaimPrefix = "pass_premium_claim_";

    /// <summary>XP, нужный для уровня n отдельно (не накопительно).</summary>
    public static int Need(int level)
    {
        return 40 + 20 * level;
    }

    /// <summary>Суммарный XP до конца уровня n включительно.</summary>
    public static int Cumulative(int level)
    {
        if (level <= 0) return 0;
        int total = 0;
        for (int i = 1; i <= level; i++) total += Need(i);
        return total;
    }

    public static string SeasonId
    {
        get
        {
            DateTime now = NowSource();
            return now.Year.ToString() + "-" + now.Month.ToString("00");
        }
    }

    private static string XpKey { get { return KeyXpPrefix + SeasonId; } }
    private static string PremiumKey { get { return KeyPremiumPrefix + SeasonId; } }

    private static string FreeClaimKey(int level) { return KeyFreePrefix + SeasonId + "_" + level; }
    private static string PremiumClaimKey(int level) { return KeyPremiumClaimPrefix + SeasonId + "_" + level; }

    private static void EnsureRollover()
    {
        string stored = PlayerPrefs.GetString(KeySeason, "");
        string now = SeasonId;
        if (stored != now)
        {
            PlayerPrefs.SetString(KeySeason, now);
            PlayerPrefs.Save();
        }
    }

    // ---------------- состояние ----------------

    public static int Xp
    {
        get
        {
            EnsureRollover();
            return PlayerPrefs.GetInt(XpKey, 0);
        }
    }

    public static bool IsPremium
    {
        get
        {
            EnsureRollover();
            return PlayerPrefs.GetInt(PremiumKey, 0) == 1;
        }
    }

    /// <summary>Уровень 0..40. Ноль — XP меньше стоимости первого уровня.</summary>
    public static int Level
    {
        get
        {
            int xp = Xp;
            int level = 0;
            while (level < LevelCount && xp >= Cumulative(level + 1)) level++;
            return level;
        }
    }

    /// <summary>XP внутрь текущего уровня (для шкалы или текста N/M).</summary>
    public static int XpIntoLevel
    {
        get { return Xp - Cumulative(Level); }
    }

    /// <summary>XP, которого не хватает до следующего уровня. Ноль на потолке.</summary>
    public static int XpToNext
    {
        get
        {
            int level = Level;
            if (level >= LevelCount) return 0;
            return Cumulative(level + 1) - Cumulative(level);
        }
    }

    /// <summary>Осталось дней до конца сезона (0 — последний день).</summary>
    public static int DaysLeft
    {
        get
        {
            DateTime now = NowSource();
            DateTime firstNext = new DateTime(now.Year, now.Month, 1).AddMonths(1);
            return Math.Max(0, (int)(firstNext - now.Date).TotalDays);
        }
    }

    // Награда есть на каждом уровне обеих дорожек: монеты растут с уровнем,
    // очки выдаются на круглых уровнях, скины — на ключевых. Премиум-дорожка
    // заметно щедрее бесплатной, иначе покупка не имела бы смысла.
    private static int Round5(int value)
    {
        return ((value + 2) / 5) * 5;
    }

    private static int FreeCoins(int level)
    {
        if (FreeSkin(level) != null) return 0;
        if (level >= LevelCount) return 600;
        return Round5(10 + 4 * level);
    }

    private static int FreePoints(int level)
    {
        switch (level)
        {
            case 5: return 15;
            case 15: return 20;
            case 20: return 25;
            case 30: return 35;
            case 35: return 40;
            case 40: return 50;
            default: return 0;
        }
    }

    private static string FreeSkin(int level)
    {
        switch (level)
        {
            case 10: return "pass_verdant";
            case 25: return "pass_sunray";
            default: return null;
        }
    }

    private static int PremiumCoins(int level)
    {
        if (PremiumSkin(level) != null) return 0;
        if (level >= LevelCount) return 900;
        return Round5(30 + 8 * level);
    }

    private static int PremiumPoints(int level)
    {
        switch (level)
        {
            case 10: return 25;
            case 20: return 30;
            case 25: return 40;
            case 35: return 55;
            case 40: return 70;
            default: return 0;
        }
    }

    private static string PremiumSkin(int level)
    {
        switch (level)
        {
            case 5: return "pass_argenteus";
            case 17: return "pass_aurora";
            case 30: return "pass_regal";
            default: return null;
        }
    }

    public static bool HasFreeReward(int level)
    {
        return FreeCoins(level) > 0 || FreePoints(level) > 0 || FreeSkin(level) != null;
    }

    public static bool HasPremiumReward(int level)
    {
        return PremiumCoins(level) > 0 || PremiumPoints(level) > 0 || PremiumSkin(level) != null;
    }

    private static string SkinName(string id)
    {
        SkinDefinition skin = SkinCatalog.Get(id);
        return skin != null ? skin.Title : id;
    }

    private static string RewardLabel(string skin, int coins, int points)
    {
        if (skin != null) return "Скин «" + SkinName(skin) + "»";

        string label = "";
        if (coins > 0) label = "+" + coins + " монет";
        if (points > 0) label += (label.Length > 0 ? " · " : "") + "+" + points + " очков";
        return label.Length > 0 ? label : "—";
    }

    public static string FreeRewardLabel(int level)
    {
        return RewardLabel(FreeSkin(level), FreeCoins(level), FreePoints(level));
    }

    public static string PremiumRewardLabel(int level)
    {
        return RewardLabel(PremiumSkin(level), PremiumCoins(level), PremiumPoints(level));
    }

    // ---------------- заявка наград ----------------

    public static bool FreeClaimed(int level)
    {
        return PlayerPrefs.GetInt(FreeClaimKey(level), 0) == 1;
    }

    public static bool PremiumClaimed(int level)
    {
        return PlayerPrefs.GetInt(PremiumClaimKey(level), 0) == 1;
    }

    public static bool CanClaimFree(int level)
    {
        return level >= 1 && level <= Level && HasFreeReward(level) && !FreeClaimed(level);
    }

    public static bool CanClaimPremium(int level)
    {
        return IsPremium && level >= 1 && level <= Level && HasPremiumReward(level) && !PremiumClaimed(level);
    }

    public static bool ClaimFree(int level, out string message)
    {
        message = "";
        if (!CanClaimFree(level))
        {
            message = "Забрать пока нельзя";
            return false;
        }

        string skin = FreeSkin(level);
        int coins = FreeCoins(level);
        int points = FreePoints(level);

        if (skin != null)
        {
            GameProgress.SetSkinOwned(skin, true);
            message = "Скин «" + SkinName(skin) + "» открыт";
        }
        if (coins > 0)
        {
            SkinService.AddCoins(coins);
            if (message.Length > 0) message += " · +" + coins + " монет";
            else message = "+" + coins + " монет";
        }
        if (points > 0)
        {
            SkinService.AddPoints(points);
            if (message.Length > 0) message += " · +" + points + " очков";
            else message = "+" + points + " очков";
        }
        PlayerPrefs.SetInt(FreeClaimKey(level), 1);
        PlayerPrefs.Save();
        MetaEvents.RaisePassChanged();
        return true;
    }

    public static bool ClaimPremium(int level, out string message)
    {
        message = "";
        if (!IsPremium)
        {
            message = "Премиум недоступен";
            return false;
        }
        if (!CanClaimPremium(level))
        {
            message = "Забрать пока нельзя";
            return false;
        }

        string skin = PremiumSkin(level);
        int coins = PremiumCoins(level);
        int points = PremiumPoints(level);

        if (skin != null)
        {
            GameProgress.SetSkinOwned(skin, true);
            message = "Скин «" + SkinName(skin) + "» открыт";
        }
        if (coins > 0)
        {
            SkinService.AddCoins(coins);
            if (message.Length > 0) message += " · +" + coins + " монет";
            else message = "+" + coins + " монет";
        }
        if (points > 0)
        {
            SkinService.AddPoints(points);
            if (message.Length > 0) message += " · +" + points + " очков";
            else message = "+" + points + " очков";
        }
        PlayerPrefs.SetInt(PremiumClaimKey(level), 1);
        PlayerPrefs.Save();
        MetaEvents.RaisePassChanged();
        return true;
    }

    /// <summary>Ставит премиум на весь текущий сезон. Вызывается магазином после
    /// покупки/восстановления. Не является чит-функцией: монеты не выдаются.</summary>
    public static void SetPremium(bool premium)
    {
        EnsureRollover();
        PlayerPrefs.SetInt(PremiumKey, premium ? 1 : 0);
        PlayerPrefs.Save();
        YandexCloudSave.Save();
        YandexAnalytics.Goal(premium ? "premium_granted" : "premium_revoked");
        MetaEvents.RaisePassChanged();
    }

    /// <summary>XP за закрытый отрезок забега (макс. высота отрезка в метрах).</summary>
    public static void AddRunHeight(int levelMeters)
    {
        if (levelMeters <= 0) return;
        EnsureRollover();
        PlayerPrefs.SetInt(XpKey, Xp + levelMeters);
        SaveService.MarkLocal();
        MetaEvents.RaisePassChanged();
    }

    /// <summary>Сколько уровней-наград сейчас можно забрать (для бейджа в меню).</summary>
    public static int ClaimableCount()
    {
        int count = 0;
        for (int i = 1; i <= LevelCount; i++)
        {
            if (CanClaimFree(i)) count++;
            if (CanClaimPremium(i)) count++;
        }
        return count;
    }
}