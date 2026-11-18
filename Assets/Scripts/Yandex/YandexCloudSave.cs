using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class YandexCloudSave
{
    private const float SaveDelay = 4f;
    private const float SaveInterval = 20f;

    private static bool _savePending;
    private static float _nextSaveAt;
    private static float _lastSaveAt = -999f;

    public static event Action Synced;
    public static bool Loaded { get; private set; }
    public static bool Syncing { get; private set; }

    public static void BeginLoad()
    {
        Loaded = false;
        if (YandexGames.Ready)
        {
            YandexSdk.LoadData();
            Syncing = true;
        }
    }

    /// <summary>Немедленная отправка — для покупок, скинов и других редких
    /// действий, где нельзя терять результат.</summary>
    public static void Save()
    {
        if (!Loaded || !YandexGames.Ready)
        {
            RequestSave();
            return;
        }
        SaveNow();
    }

    /// <summary>Откладывает облачную отправку. Несколько изменений за кадр
    /// (например, рекорд + монеты + очки на чекпоинте) соберутся в один
    /// снапшот, а не отправятся отдельными запросами.</summary>
    public static void RequestSave()
    {
        if (_savePending) return;
        _savePending = true;
        _nextSaveAt = Mathf.Max(Time.unscaledTime + SaveDelay,
            _lastSaveAt + SaveInterval);
    }

    public static void FlushIfDue()
    {
        if (!_savePending || Time.unscaledTime < _nextSaveAt) return;
        Flush();
    }

    /// <summary>Сбрасывает отложенную отправку. Если облако ещё не загружено,
    /// изменения остаются в очереди до окончания загрузки.</summary>
    public static void Flush()
    {
        if (!_savePending || !Loaded) return;
        SaveNow();
    }

    private static void SaveNow()
    {
        if (!YandexGames.Ready || !Loaded) return;
        _savePending = false;
        _lastSaveAt = Time.unscaledTime;
        string json = BuildSnapshotString();
        YandexSdk.SaveData(json);
    }

    public static void HandleDataLoaded(string raw)
    {
        Syncing = false;
        try
        {
            Dictionary<string, string> cloud = ParseLines(raw);
            Dictionary<string, string> local = BuildLocalSnapshot();
            Dictionary<string, string> merged = new Dictionary<string, string>(cloud);
            foreach (var kv in MergeKeys())
            {
                string k = kv.Key;
                MergeValues(merged, k, local, cloud, kv.Value);
            }
            ApplySnapshot(merged);
        }
        catch
        {
            // ignore
        }
        Loaded = true;
        Synced?.Invoke();
    }

    public static void HandleSaved(string p)
    {
        // no-op
    }

    public static void HandleError(string msg)
    {
        Syncing = false;
        Loaded = true;
        Synced?.Invoke();
    }

    private class KeyType
    {
        public bool IsFloat;
        public bool IsString;
    }

    private static void MergeValues(Dictionary<string, string> merged, string key, Dictionary<string, string> local, Dictionary<string, string> cloud, KeyType t)
    {
        local.TryGetValue(key, out string l);
        cloud.TryGetValue(key, out string c);
        if (t.IsString)
        {
            string m = !string.IsNullOrEmpty(l) ? l : c;
            if (m == null) m = string.Empty;
            merged[key] = m;
            return;
        }
        if (t.IsFloat)
        {
            float fl = ParseFloat(l, 0f);
            float fc = ParseFloat(c, 0f);
            float fm = fl > fc ? fl : fc;
            merged[key] = fm.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return;
        }
        // int
        long il = ParseLong(l, 0);
        long ic = ParseLong(c, 0);
        long im = il > ic ? il : ic;
        merged[key] = im.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, KeyType> MergeKeys()
    {
        var keys = new Dictionary<string, KeyType>();
        // ints
        AddInt(keys, GameProgress.KeyBestHeight);
        AddInt(keys, GameProgress.KeyBestCheckpoint);
        AddInt(keys, GameProgress.KeyCoins);
        AddInt(keys, GameProgress.KeyPoints);
        AddInt(keys, GameProgress.KeyCoinMilestone);
        AddInt(keys, GameProgress.KeyDailyClaims);
        AddInt(keys, GameProgress.KeyStageReached);
        AddInt(keys, GameProgress.KeyFinished);
        AddInt(keys, MetaSystems.KeyLifeClimb);
        AddInt(keys, MetaSystems.KeyLifeCoins);
        AddInt(keys, MetaSystems.KeyLifeRuns);
        AddInt(keys, MetaSystems.KeyLifeJumps);
        int bestCp = GameProgress.GetBestCheckpoint();
        for (int i = 1; i <= bestCp; i++) AddFloat(keys, GameProgress.KeyCpHeightPrefix + i);
        // skins
        var allSkins = SkinCatalog.All;
        for (int i = 0; i < allSkins.Count; i++) AddInt(keys, GameProgress.KeySkinOwnedPrefix + allSkins[i].Id);
        // pass
        string season = SeasonPass.SeasonId;
        AddInt(keys, "pass_xp_" + season);
        AddInt(keys, "pass_premium_" + season);
        for (int l = 1; l <= SeasonPass.LevelCount; l++)
        {
            AddInt(keys, "pass_free_" + season + "_" + l);
            AddInt(keys, "pass_premium_claim_" + season + "_" + l);
        }
        // ach
        var ach = Achievements.All;
        for (int i = 0; i < ach.Count; i++) AddInt(keys, "ach_done_" + ach[i].Id);
        // strings
        AddString(keys, GameProgress.KeySelectedSkin);
        AddString(keys, GameProgress.KeyDailyLastClaim);
        return keys;
    }

    private static void AddInt(Dictionary<string, KeyType> k, string key) { k[key] = new KeyType { IsFloat = false, IsString = false }; }
    private static void AddFloat(Dictionary<string, KeyType> k, string key) { k[key] = new KeyType { IsFloat = true, IsString = false }; }
    private static void AddString(Dictionary<string, KeyType> k, string key) { k[key] = new KeyType { IsString = true, IsFloat = false }; }

    private static string BuildSnapshotString()
    {
        Dictionary<string, string> snap = BuildLocalSnapshot();
        StringBuilder sb = new StringBuilder(4096);
        foreach (var kv in snap)
        {
            sb.Append(kv.Key).Append('\t').Append(kv.Value.Replace("\n", " ").Replace("\r", " ")).Append('\n');
        }
        return sb.ToString();
    }

    private static Dictionary<string, string> BuildLocalSnapshot()
    {
        var d = new Dictionary<string, string>();
        d[GameProgress.KeyBestHeight] = GameProgress.GetBestHeight().ToString();
        d[GameProgress.KeyBestCheckpoint] = GameProgress.GetBestCheckpoint().ToString();
        d[GameProgress.KeyCoins] = GameProgress.GetCoins().ToString();
        d[GameProgress.KeyPoints] = GameProgress.GetPoints().ToString();
        d[GameProgress.KeyCoinMilestone] = GameProgress.GetCoinMilestone().ToString();
        d[GameProgress.KeyDailyClaims] = GameProgress.GetDailyClaims().ToString();
        d[GameProgress.KeyStageReached] = GameProgress.GetStageReached().ToString();
        d[GameProgress.KeyFinished] = (GameProgress.IsFinished() ? 1 : 0).ToString();
        d[MetaSystems.KeyLifeClimb] = MetaSystems.LifeClimb.ToString();
        d[MetaSystems.KeyLifeCoins] = MetaSystems.LifeCoins.ToString();
        d[MetaSystems.KeyLifeRuns] = MetaSystems.LifeRuns.ToString();
        d[MetaSystems.KeyLifeJumps] = MetaSystems.LifeJumps.ToString();
        int bestCp = GameProgress.GetBestCheckpoint();
        for (int i = 1; i <= bestCp; i++) d[GameProgress.KeyCpHeightPrefix + i] = GameProgress.GetCheckpointHeight(i).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var allSkins = SkinCatalog.All;
        for (int i = 0; i < allSkins.Count; i++) d[GameProgress.KeySkinOwnedPrefix + allSkins[i].Id] = GameProgress.IsSkinOwned(allSkins[i].Id) ? "1" : "0";
        string season = SeasonPass.SeasonId;
        d["pass_xp_" + season] = PlayerPrefs.GetInt("pass_xp_" + season, 0).ToString();
        d["pass_premium_" + season] = (PlayerPrefs.GetInt("pass_premium_" + season, 0) == 1 ? 1 : 0).ToString();
        for (int l = 1; l <= SeasonPass.LevelCount; l++)
        {
            d["pass_free_" + season + "_" + l] = (PlayerPrefs.GetInt("pass_free_" + season + "_" + l, 0) == 1 ? 1 : 0).ToString();
            d["pass_premium_claim_" + season + "_" + l] = (PlayerPrefs.GetInt("pass_premium_claim_" + season + "_" + l, 0) == 1 ? 1 : 0).ToString();
        }
        var ach = Achievements.All;
        for (int i = 0; i < ach.Count; i++) d["ach_done_" + ach[i].Id] = (PlayerPrefs.GetInt("ach_done_" + ach[i].Id, 0) == 1 ? 1 : 0).ToString();
        d[GameProgress.KeySelectedSkin] = GameProgress.GetSelectedSkin() ?? string.Empty;
        d[GameProgress.KeyDailyLastClaim] = GameProgress.GetDailyLastClaim() ?? string.Empty;
        return d;
    }

    private static void ApplySnapshot(Dictionary<string, string> merged)
    {
        foreach (var kv in merged)
        {
            string k = kv.Key;
            string v = kv.Value ?? string.Empty;
            if (k.StartsWith(GameProgress.KeyCpHeightPrefix))
            {
                if (int.TryParse(k.Substring(GameProgress.KeyCpHeightPrefix.Length), out int cp))
                {
                    if (float.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float fv))
                        GameProgress.SetCheckpointHeight(cp, fv);
                }
                continue;
            }
            if (k == GameProgress.KeySelectedSkin) { GameProgress.SetSelectedSkin(v); continue; }
            if (k == GameProgress.KeyDailyLastClaim) { GameProgress.SetDailyLastClaim(v); continue; }
            if (k == GameProgress.KeyBestHeight || k == GameProgress.KeyBestCheckpoint || k == GameProgress.KeyCoins || k == GameProgress.KeyPoints || k == GameProgress.KeyCoinMilestone || k == GameProgress.KeyDailyClaims || k == GameProgress.KeyStageReached || k == GameProgress.KeyFinished)
            {
                TrySetInt(k, v);
                continue;
            }
            if (k.StartsWith(GameProgress.KeySkinOwnedPrefix)) { TrySetInt(k, v); continue; }
            if (k == MetaSystems.KeyLifeClimb || k == MetaSystems.KeyLifeCoins || k == MetaSystems.KeyLifeRuns || k == MetaSystems.KeyLifeJumps) { TrySetInt(k, v); continue; }
            if (k.StartsWith("pass_")) { TrySetInt(k, v); continue; }
            if (k.StartsWith("ach_done_")) { TrySetInt(k, v); continue; }
        }
        PlayerPrefs.Save();
    }

    private static void TrySetInt(string k, string v)
    {
        if (!long.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out long lv)) return;
        int iv = (int)Mathf.Clamp(lv, -1000000000, 1000000000);
        int cur = PlayerPrefs.GetInt(k, int.MinValue / 2);
        if (cur < iv) PlayerPrefs.SetInt(k, iv);
    }

    private static Dictionary<string, string> ParseLines(string raw)
    {
        var d = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(raw)) return d;
        string[] lines = raw.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int idx = line.IndexOf('\t');
            if (idx < 0) continue;
            string key = line.Substring(0, idx);
            string val = line.Substring(idx + 1);
            if (!string.IsNullOrEmpty(key)) d[key] = val;
        }
        return d;
    }

    private static float ParseFloat(string s, float def) { if (float.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float f)) return f; return def; }
    private static long ParseLong(string s, long def) { if (long.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out long l)) return l; return def; }
}
