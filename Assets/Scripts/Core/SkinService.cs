using System;
using UnityEngine;

public static class SkinService
{
    public const int CheckpointCoins = 25;
    public const int MetersPerPoint = 10;
    public const int MetersPerMilestone = 100;
    public const int MilestoneCoins = 100;

    public static event Action Changed;

    public static int Coins
    {
        get { return GameProgress.GetCoins(); }
    }

    public static int Points
    {
        get { return GameProgress.GetPoints(); }
    }

    public static SkinDefinition SelectedSkin
    {
        get
        {
            SkinDefinition skin = SkinCatalog.Get(GameProgress.GetSelectedSkin());
            if (skin != null && IsUnlocked(skin)) return skin;
            return SkinCatalog.Default;
        }
    }

    public static bool IsUnlocked(SkinDefinition skin)
    {
        if (skin == null) return false;
        switch (skin.Unlock)
        {
            case SkinUnlock.Free:
                return true;
            case SkinUnlock.Height:
                return GameProgress.GetBestHeight() >= skin.UnlockValue;
            case SkinUnlock.Checkpoint:
                return GameProgress.GetBestCheckpoint() >= skin.UnlockValue;
            default:
                return GameProgress.IsSkinOwned(skin.Id);
        }
    }

    public static bool IsPurchasable(SkinDefinition skin)
    {
        return skin != null && (skin.Unlock == SkinUnlock.Coins || skin.Unlock == SkinUnlock.Points);
    }

    public static bool IsEquipped(SkinDefinition skin)
    {
        return skin != null && SelectedSkin != null && SelectedSkin.Id == skin.Id;
    }

    /// <summary>Купить скин за монеты/очки. Для скинов за рекорд покупка не нужна.</summary>
    public static bool TryUnlock(SkinDefinition skin)
    {
        if (skin == null || IsUnlocked(skin)) return false;

        if (skin.Unlock == SkinUnlock.Coins)
        {
            if (!GameProgress.SpendCoins(skin.UnlockValue)) return false;
        }
        else if (skin.Unlock == SkinUnlock.Points)
        {
            if (!GameProgress.SpendPoints(skin.UnlockValue)) return false;
        }
        else
        {
            return false;
        }

        GameProgress.SetSkinOwned(skin.Id, true);
        Select(skin.Id);
        YandexCloudSave.Save();
        return true;
    }

    public static bool Select(string id)
    {
        SkinDefinition skin = SkinCatalog.Get(id);
        if (skin == null || !IsUnlocked(skin)) return false;
        if (GameProgress.GetSelectedSkin() == skin.Id) return true;
        GameProgress.SetSelectedSkin(skin.Id);
        YandexCloudSave.Save();
        Raise();
        return true;
    }

    public static void AddCoins(int amount)
    {
        if (amount == 0) return;
        GameProgress.AddCoins(amount);
        Raise();
    }

    public static void AddPoints(int amount)
    {
        if (amount == 0) return;
        GameProgress.AddPoints(amount);
        Raise();
    }

    /// <summary>Новый рекорд: очки за каждый подъём и монеты за каждые 100 м.</summary>
    public static void AddRecord(int gainedMeters)
    {
        if (gainedMeters <= 0) return;

        GameProgress.AddPoints(Mathf.Max(1, gainedMeters / MetersPerPoint));

        int milestone = GameProgress.GetBestHeight() / Mathf.Max(1, MetersPerMilestone);
        int reached = GameProgress.GetCoinMilestone();
        if (milestone > reached)
        {
            GameProgress.SetCoinMilestone(milestone);
            GameProgress.AddCoins((milestone - reached) * MilestoneCoins);
        }

        Raise();
    }

    public static string LockHint(SkinDefinition skin)
    {
        if (skin == null) return "";

        switch (skin.Unlock)
        {
            case SkinUnlock.Height:
                return "Нужен рекорд " + skin.UnlockValue + " м (сейчас " + GameProgress.GetBestHeight() + " м)";
            case SkinUnlock.Checkpoint:
                return "Нужен чекпоинт " + skin.UnlockValue + " (сейчас " + GameProgress.GetBestCheckpoint() + ")";
            case SkinUnlock.Coins:
                return "Нужно " + skin.UnlockValue + " монет (есть " + Coins + ")";
            case SkinUnlock.Points:
                return "Нужно " + skin.UnlockValue + " очков (есть " + Points + ")";
            case SkinUnlock.Daily:
                return "Награда за 7-й день ежедневной награды. Купить нельзя.";
            case SkinUnlock.Pass:
                return "Награда батл-пасса. Купить нельзя.";
        }
        return "";
    }

    public static string SummaryLine()
    {
        SkinDefinition skin = SelectedSkin;
        string title = skin != null ? skin.Title : "—";
        return "Монеты: " + Coins + "    Очки: " + Points +
               "    Рекорд: " + GameProgress.GetBestHeight() + " м    Чекпоинт: " + GameProgress.GetBestCheckpoint() +
               "\nСкин: " + title;
    }

    public static void Raise()
    {
        Action handler = Changed;
        if (handler != null) handler();
    }
}
