using UnityEngine;

public static class YandexAnalytics
{
    public static void Goal(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        YandexSdk.MetricaGoal(name);
#if UNITY_EDITOR
        Debug.Log("[Yandex] goal: " + name);
#endif
    }
}
