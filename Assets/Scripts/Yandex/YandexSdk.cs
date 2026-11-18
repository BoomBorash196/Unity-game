using System.Runtime.InteropServices;
using UnityEngine;

public static class YandexSdk
{
    public static bool Supported
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void YG_Init();
    [DllImport("__Internal")] private static extern void YG_Ready();
    [DllImport("__Internal")] private static extern void YG_ShowFullscreen();
    [DllImport("__Internal")] private static extern void YG_ShowRewarded();
    [DllImport("__Internal")] private static extern void YG_LoadData();
    [DllImport("__Internal")] private static extern void YG_SaveData(string json);
    [DllImport("__Internal")] private static extern void YG_SetLeaderboardScore(string name, int score);
    [DllImport("__Internal")] private static extern void YG_GetLeaderboardEntries(string name, int top, int around);
    [DllImport("__Internal")] private static extern void YG_LoadPayments();
    [DllImport("__Internal")] private static extern void YG_Purchase(string id);
    [DllImport("__Internal")] private static extern void YG_Consume(string token);
    [DllImport("__Internal")] private static extern void YG_MetricaGoal(string name);
    [DllImport("__Internal")] private static extern int YG_IsAvailable();
#endif

    public static void Init()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        YG_Init();
#endif
    }

    public static void Ready()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        YG_Ready();
#endif
    }

    public static void ShowFullscreen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        YG_ShowFullscreen();
#endif
    }

    public static void ShowRewarded()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        YG_ShowRewarded();
#endif
    }

    public static void LoadData()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        YG_LoadData();
#endif
    }

    public static void SaveData(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (json == null) json = string.Empty;
        YG_SaveData(json);
#endif
    }

    public static void SetLeaderboardScore(string name, int score)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (name == null) name = string.Empty;
        YG_SetLeaderboardScore(name, score);
#endif
    }

    public static void GetLeaderboardEntries(string name, int top, int around)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (name == null) name = string.Empty;
        YG_GetLeaderboardEntries(name, top, around);
#endif
    }

    public static void LoadPayments()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        YG_LoadPayments();
#endif
    }

    public static void Purchase(string id)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (id == null) id = string.Empty;
        YG_Purchase(id);
#endif
    }

    public static void Consume(string token)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (token == null) token = string.Empty;
        YG_Consume(token);
#endif
    }

    public static void MetricaGoal(string name)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (name == null) name = string.Empty;
        YG_MetricaGoal(name);
#endif
    }

    public static bool IsAvailable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return YG_IsAvailable() == 1;
#else
        return false;
#endif
    }
}
