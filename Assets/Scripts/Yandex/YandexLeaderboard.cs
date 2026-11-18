using System;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class YandexLeaderboard
{
    public static event Action<List<Entry>> Loaded;
    public static event Action Failed;

    public class Entry
    {
        public int Rank;
        public string Name;
        public int Score;
        public bool IsPlayer;
    }

    public static void Submit(int score)
    {
        if (!YandexGames.Ready) return;
        if (score < 0) score = 0;
        YandexSdk.SetLeaderboardScore(YandexConfig.LeaderboardBestHeight, score);
    }

    public static void Fetch()
    {
        if (!YandexGames.Ready) return;
        YandexSdk.GetLeaderboardEntries(YandexConfig.LeaderboardBestHeight, 10, 3);
    }

    public static void HandleScoreSubmitted()
    {
        // nothing
    }

    public static void HandleEntries(string json)
    {
        List<Entry> list = new List<Entry>(16);
        try
        {
            LeaderboardResponse resp = JsonUtility.FromJson<LeaderboardResponse>(json);
            if (resp != null && resp.entries != null)
            {
                for (int i = 0; i < resp.entries.Length; i++)
                {
                    var e = resp.entries[i];
                    Entry ent = new Entry
                    {
                        Rank = e.rank,
                        Score = e.score,
                        Name = e.player != null && !string.IsNullOrEmpty(e.player.publicName) ? e.player.publicName : "Игрок"
                    };
                    list.Add(ent);
                }
            }
        }
        catch
        {
        }
        Loaded?.Invoke(list);
    }

    public static void HandleError(string msg)
    {
        Failed?.Invoke();
    }

    [Serializable]
    private class Player
    {
        public string publicName;
        public string uniqueID;
    }

    [Serializable]
    private class EntryJson
    {
        public int score;
        public int rank;
        public Player player;
    }

    [Serializable]
    private class LeaderboardResponse
    {
        public int userRank;
        public EntryJson[] entries;
    }
}
