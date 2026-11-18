using System;
using UnityEngine;

public class YandexGames : MonoBehaviour
{
    public static YandexGames Instance { get; private set; }

    public static event Action Initialized;
    public static event Action<string> Failed;

    public static bool Ready => Instance != null && Instance.IsReady;
    public static bool Available => YandexSdk.Supported;

    public bool IsReady { get; private set; }
    public string Lang { get; private set; } = "ru";

    private bool _wantReady;
    private bool _readySent;
    private bool _paused;
    private float _unpauseTime;
    private bool _autoAdShown;
    private float _lastAdAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (!YandexSdk.Supported) return;
        GameObject go = new GameObject("YandexGames");
        DontDestroyOnLoad(go);
        go.AddComponent<YandexGames>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (YandexSdk.Supported)
        {
            YandexSdk.Init();
        }
        else
        {
            IsReady = false;
        }
    }

    private void Update()
    {
        if (_paused && Time.realtimeSinceStartup >= _unpauseTime)
        {
            _paused = false;
            if (GameManager.Instance != null)
                GameManager.Instance.SetPaused(false);
            else
                Time.timeScale = 1f;
        }
    }

    public void RequestLoadingReady()
    {
        _wantReady = true;
        TrySendReady();
    }

    /// <summary>LoadingAPI.ready() можно звать только один раз: площадка
    /// пишет предупреждение в консоль и может посчитать повтор ошибкой.
    /// Bootstrap и меню просят готовность независимо — здесь стоит защёлка.</summary>
    private void TrySendReady()
    {
        if (!_wantReady || _readySent || !IsReady) return;
        _readySent = true;
        YandexSdk.Ready();
    }

    public void ShowInterstitialIfNeeded()
    {
        if (!IsReady) return;
        if (_autoAdShown) return;
        float nextAllowed = _lastAdAt + YandexConfig.InterstitialCooldown;
        if (Time.realtimeSinceStartup < nextAllowed) return;
        _autoAdShown = true;
        _lastAdAt = Time.realtimeSinceStartup;
        PauseForAd(0.8f);
        YandexAds.ShowInterstitial();
    }

    private void PauseForAd(float duration)
    {
        _paused = true;
        _unpauseTime = Time.realtimeSinceStartup + duration;
        if (GameManager.Instance != null)
            GameManager.Instance.SetPaused(true);
        else
            Time.timeScale = 0f;
    }

    // JS callbacks
    public void OnSdkInit(string lang)
    {
        Lang = string.IsNullOrEmpty(lang) ? "ru" : lang;
        IsReady = true;
        _lastAdAt = Time.realtimeSinceStartup - YandexConfig.InterstitialCooldown * 0.5f;
        TrySendReady();
        YandexCloudSave.BeginLoad();
        YandexPayments.BeginLoad();
        YandexLeaderboard.Submit(GameProgress.GetBestHeight());
        Initialized?.Invoke();
        YandexAnalytics.Goal("sdk_ready");
    }

    public void OnSdkError(string msg)
    {
        Failed?.Invoke(msg ?? string.Empty);
    }

    public void OnFullscreenOpen(string p)
    {
        PauseForAd(YandexConfig.InterstitialCooldown * 0.5f);
    }

    public void OnFullscreenClose(string wasShown)
    {
        // unpause
        _paused = false;
        if (GameManager.Instance != null)
            GameManager.Instance.SetPaused(false);
        else
            Time.timeScale = 1f;
        YandexAds.HandleInterstitialClosed(wasShown == "1");
    }

    public void OnFullscreenError(string msg)
    {
        _paused = false;
        if (GameManager.Instance != null)
            GameManager.Instance.SetPaused(false);
        else
            Time.timeScale = 1f;
        YandexAds.HandleInterstitialClosed(false);
    }

    public void OnRewardedOpen(string p)
    {
        PauseForAd(10f);
    }

    public void OnRewarded(string p)
    {
        YandexAds.HandleRewardedCompleted();
    }

    public void OnRewardedClose(string p)
    {
        _paused = false;
        if (GameManager.Instance != null)
            GameManager.Instance.SetPaused(false);
        else
            Time.timeScale = 1f;
        YandexAds.HandleRewardedClosed();
    }

    public void OnRewardedError(string msg)
    {
        _paused = false;
        if (GameManager.Instance != null)
            GameManager.Instance.SetPaused(false);
        else
            Time.timeScale = 1f;
        YandexAds.HandleRewardedClosed();
    }

    public void OnDataLoaded(string json)
    {
        YandexCloudSave.HandleDataLoaded(json);
    }

    public void OnDataSaved(string p)
    {
        YandexCloudSave.HandleSaved(p);
    }

    public void OnDataError(string msg)
    {
        YandexCloudSave.HandleError(msg);
    }

    public void OnScoreSet(string score)
    {
        YandexLeaderboard.HandleScoreSubmitted();
    }

    public void OnLeaderboardEntries(string json)
    {
        YandexLeaderboard.HandleEntries(json);
    }

    public void OnLeaderboardError(string msg)
    {
        YandexLeaderboard.HandleError(msg);
    }

    public void OnPurchasesLoaded(string json)
    {
        YandexPayments.HandlePurchasesLoaded(json);
    }

    /// <summary>
    /// Список покупок не пришёл. Отдельный колбэк от OnPurchaseError:
    /// первый про загрузку списка, второй про саму покупку. Без него
    /// сообщение "does not have receiver" сыпалось в консоль, а флаг
    /// загрузки в YandexPayments оставался поднятым навсегда.
    /// </summary>
    public void OnPurchasesError(string msg)
    {
        YandexPayments.HandlePurchasesError(msg);
    }

    public void OnPurchaseResult(string json)
    {
        YandexPayments.HandlePurchaseResult(json);
    }

    public void OnPurchaseError(string msg)
    {
        YandexPayments.HandlePurchaseError(msg);
    }

    public void OnPurchaseConsumed(string p)
    {
        // no-op
    }
}
