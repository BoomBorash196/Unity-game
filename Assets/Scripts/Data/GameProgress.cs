using UnityEngine;

public class GameProgress
{
    public const string KeyBestHeight = "parkour_best_height";
    public const string KeyBestCheckpoint = "parkour_best_cp";
    public const string KeyCpHeightPrefix = "parkour_cp_height_";
    public const string KeyCoins = "parkour_coins";
    public const string KeyPoints = "parkour_points";
    public const string KeySelectedSkin = "parkour_skin";
    public const string KeySkinOwnedPrefix = "parkour_skin_owned_";
    public const string KeyCoinMilestone = "parkour_coin_milestone";
    public const string KeyDailyClaims = "parkour_daily_claims";
    public const string KeyDailyLastClaim = "parkour_daily_last";
    public const string KeyStageReached = "parkour_stage_reached";
    public const string KeyFinished = "parkour_finished";

    public static int GetBestHeight()
    {
        return PlayerPrefs.GetInt(KeyBestHeight, 0);
    }

    public static int GetBestCheckpoint()
    {
        return PlayerPrefs.GetInt(KeyBestCheckpoint, 0);
    }

    public static float GetCheckpointHeight(int checkpoint)
    {
        return PlayerPrefs.GetFloat(KeyCpHeightPrefix + checkpoint, 0f);
    }

    public static void SetCheckpointHeight(int checkpoint, float height)
    {
        if (height > GetCheckpointHeight(checkpoint))
        {
            PlayerPrefs.SetFloat(KeyCpHeightPrefix + checkpoint, height);
            SaveService.MarkLocal();
        }
    }

    public static bool SetBest(int bestHeight, int bestCheckpoint)
    {
        int oldBestHeight = GetBestHeight();
        int oldBestCheckpoint = GetBestCheckpoint();
        bool improved = bestHeight > oldBestHeight || bestCheckpoint > oldBestCheckpoint;

        if (bestHeight > oldBestHeight)
            PlayerPrefs.SetInt(KeyBestHeight, bestHeight);
        if (bestCheckpoint > oldBestCheckpoint)
            PlayerPrefs.SetInt(KeyBestCheckpoint, bestCheckpoint);

        if (improved)
        {
            SaveService.MarkLocal();
            YandexCloudSave.RequestSave();
            YandexLeaderboard.Submit(bestHeight);
        }
        return improved;
    }

    public static int GetCoins()
    {
        return PlayerPrefs.GetInt(KeyCoins, 0);
    }

    public static void AddCoins(int amount)
    {
        if (amount == 0) return;
        PlayerPrefs.SetInt(KeyCoins, Mathf.Max(0, GetCoins() + amount));
        SaveService.MarkLocal();
        YandexCloudSave.RequestSave();
    }

    public static bool SpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (GetCoins() < amount) return false;
        AddCoins(-amount);
        return true;
    }

    public static int GetPoints()
    {
        return PlayerPrefs.GetInt(KeyPoints, 0);
    }

    public static void AddPoints(int amount)
    {
        if (amount == 0) return;
        PlayerPrefs.SetInt(KeyPoints, Mathf.Max(0, GetPoints() + amount));
        SaveService.MarkLocal();
        YandexCloudSave.RequestSave();
    }

    public static bool SpendPoints(int amount)
    {
        if (amount <= 0) return true;
        if (GetPoints() < amount) return false;
        AddPoints(-amount);
        return true;
    }

    public static int GetCoinMilestone()
    {
        return PlayerPrefs.GetInt(KeyCoinMilestone, 0);
    }

    public static void SetCoinMilestone(int milestone)
    {
        PlayerPrefs.SetInt(KeyCoinMilestone, Mathf.Max(0, milestone));
        SaveService.MarkLocal();
    }

    public static string GetSelectedSkin()
    {
        return PlayerPrefs.GetString(KeySelectedSkin, string.Empty);
    }

    public static void SetSelectedSkin(string id)
    {
        PlayerPrefs.SetString(KeySelectedSkin, id ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static bool IsSkinOwned(string id)
    {
        return !string.IsNullOrEmpty(id) && PlayerPrefs.GetInt(KeySkinOwnedPrefix + id, 0) == 1;
    }

    public static void SetSkinOwned(string id, bool owned)
    {
        if (string.IsNullOrEmpty(id)) return;
        PlayerPrefs.SetInt(KeySkinOwnedPrefix + id, owned ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Сколько ежедневных наград получено всего. От этого числа
    /// считаются и номер круга, и день внутри круга.</summary>
    public static int GetDailyClaims()
    {
        return PlayerPrefs.GetInt(KeyDailyClaims, 0);
    }

    public static void AddDailyClaim()
    {
        PlayerPrefs.SetInt(KeyDailyClaims, Mathf.Max(0, GetDailyClaims()) + 1);
        PlayerPrefs.Save();
    }

    /// <summary>Дата последней полученной награды в формате yyyy-MM-dd.</summary>
    public static string GetDailyLastClaim()
    {
        return PlayerPrefs.GetString(KeyDailyLastClaim, string.Empty);
    }

    public static void SetDailyLastClaim(string day)
    {
        PlayerPrefs.SetString(KeyDailyLastClaim, day ?? string.Empty);
        PlayerPrefs.Save();
    }

    /// <summary>Самая высокая ступень подъёма, до которой доходил игрок.
    /// Растёт только вверх: вернуться назад нельзя, иначе спуск сбил бы
    /// прогресс и повторный забег переставал бы что-то значить.</summary>
    public static int GetStageReached()
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(KeyStageReached, 0));
    }

    /// <summary>Отмечает взятую ступень. true, если это действительно новая
    /// ступень — на этом держится выплата награды за неё, чтобы повторный
    /// забег не платил за то, что уже оплачено.</summary>
    public static bool SetStageReached(int stageIndex)
    {
        int current = GetStageReached();
        if (stageIndex <= current) return false;

        PlayerPrefs.SetInt(KeyStageReached, stageIndex);
        SaveService.MarkLocal();
        return true;
    }

    /// <summary>Достигнут ли рай. Отдельный флаг, а не высота: рекорд высоты
    /// общий и продолжает расти после финиша, а «проходил ли игрок конец» —
    /// раз и навсегда.</summary>
    public static bool IsFinished()
    {
        return PlayerPrefs.GetInt(KeyFinished, 0) > 0;
    }

    public static void SetFinished()
    {
        PlayerPrefs.SetInt(KeyFinished, 1);
        SaveService.MarkLocal();
    }

    public static void ClearAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
