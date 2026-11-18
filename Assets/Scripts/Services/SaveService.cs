using UnityEngine;

public static class SaveService
{
    private const float LocalSaveDelay = 1.5f;

    private static bool _localPending;
    private static float _localDue;

    /// <summary>
    /// Откладывает запись PlayerPrefs. Частые события — прыжок, монета,
    /// прогресс задания — только помечают данные грязными, а на диск они
    /// попадают одним Save через короткую паузу.
    /// </summary>
    public static void MarkLocal()
    {
        if (_localPending) return;
        _localPending = true;
        _localDue = Time.unscaledTime + LocalSaveDelay;
    }

    /// <summary>Сбрасывает отложенную локальную запись немедленно.</summary>
    public static void FlushLocal()
    {
        if (!_localPending) return;
        _localPending = false;
        PlayerPrefs.Save();
    }

    /// <summary>Сбрасывает и локальные, и облачные изменения. Точки вызова —
    /// чекпоинт, смерть, пауза, выход в меню и закрытие вкладки.</summary>
    public static void FlushAll()
    {
        FlushLocal();
        YandexCloudSave.Flush();
    }

    /// <summary>Вызывается каждый кадр из GameManager. Сама по себе ничего
    /// не делает, пока нет отложенных изменений.</summary>
    public static void Tick()
    {
        if (_localPending && Time.unscaledTime >= _localDue)
            FlushLocal();
        YandexCloudSave.FlushIfDue();
    }

    public static bool TouchPreferred()
    {
        if (SystemInfo.deviceType == DeviceType.Handheld)
            return true;
        return Input.touchSupported;
    }

    public static void ResetAllProgress()
    {
        GameProgress.ClearAll();
    }
}