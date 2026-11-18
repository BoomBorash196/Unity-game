using UnityEngine;

public static class YandexConfig
{
    // Yandex Games leaderboard technical name for best height (metres).
    public const string LeaderboardBestHeight = "best_height";

    // Yandex Games product ID for season pass (non-consumable by default; adjust in console).
    public const string ProductSeasonPass = "season_pass";

    // Yandex Metrica counter ID. Set to >0 to enable reachGoal calls via ym().
    public const int MetricaCounterId = 0;

    // Minimum interval between fullscreen interstitials (seconds).
    public const float InterstitialCooldown = 90f;
}
