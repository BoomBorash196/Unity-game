using System;
using UnityEngine;

public static class YandexAds
{
    public static event Action<bool> InterstitialClosed; // true if shown
    public static event Action RewardedCompleted;
    public static event Action RewardedClosed;

    private static bool _rewarded;

    public static bool CanShowRewarded => YandexGames.Ready;
    public static bool CanShowInterstitial => YandexGames.Ready;

    public static void ShowInterstitial()
    {
        if (!YandexGames.Ready) return;
        YandexSdk.ShowFullscreen();
    }

    public static void ShowRewarded()
    {
        if (!YandexGames.Ready) return;
        _rewarded = false;
        YandexSdk.ShowRewarded();
    }

    public static void HandleInterstitialClosed(bool wasShown)
    {
        InterstitialClosed?.Invoke(wasShown);
        if (wasShown)
        {
            YandexAnalytics.Goal("interstitial");
        }
    }

    public static void HandleRewardedCompleted()
    {
        _rewarded = true;
        RewardedCompleted?.Invoke();
    }

    public static void HandleRewardedClosed()
    {
        RewardedClosed?.Invoke();
    }
}
