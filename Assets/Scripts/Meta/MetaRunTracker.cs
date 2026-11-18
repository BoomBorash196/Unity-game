using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Следит за жизнью одного забега (точнее — отрезка между респавнами) и
/// замыкает его на мета-системы: респавн от смерти — конец отрезка, рестарт,
/// выход в меню и выход из приложения — тоже. Высота отрезка = XP пасса и
/// метрики дейликов; собранные забеги и высоты копятся в MetaSystems.
///
/// Сидит на корне игрока рядом с PlayerRespawn — там есть все нужные события.
/// </summary>
public class MetaRunTracker : MonoBehaviour
{
    public static MetaRunTracker Current { get; private set; }

    private PlayerController _player;
    private PlayerRespawn _respawn;
    private float _segmentMax;
    private bool _ended;

    public void Init(PlayerController player, PlayerRespawn respawn)
    {
        _player = player;
        _respawn = respawn;

        if (_respawn != null) _respawn.Respawned += OnRespawned;
        if (_player != null) _player.Jumped += OnJumped;

        _segmentMax = _player != null ? _player.CurrentHeight : 0f;
    }

    private void OnEnable()
    {
        if (Current == null) Current = this;
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
        if (_respawn != null) _respawn.Respawned -= OnRespawned;
        if (_player != null) _player.Jumped -= OnJumped;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
        if (_player == null) return;

        if (_player.CurrentHeight > _segmentMax)
            _segmentMax = _player.CurrentHeight;
    }

    private void OnJumped()
    {
        MetaSystems.RegisterJump();
    }

    private void OnRespawned()
    {
        // Смерть или ручной респавн: отрезок закрыт, но игра продолжается.
        EndSegment(false, true);
    }

    /// <summary>Выход из забега (меню/рестарт). Вызывается LevelManager'ом.</summary>
    public void EndByLeaving()
    {
        EndSegment(true, false);
    }

    private void OnApplicationQuit()
    {
        EndSegment(true, false);
    }

    private void OnDestroy()
    {
        // Рестарт сцены или выход: замыкаем незакрытый отрезок.
        EndSegment(true, false);
    }

    private void EndSegment(bool final, bool showToast)
    {
        if (_ended) return;
        if (_player == null) { _ended = final; return; }

        int height = Mathf.Max(0, Mathf.FloorToInt(_segmentMax - StageCatalog.WorldOriginY));
        if (final) _ended = true;

        if (height > 0)
        {
            List<DailyQuest> fresh = MetaSystems.RegisterRunEnded(height);
            int best = GameProgress.GetBestHeight();
            YandexLeaderboard.Submit(best);
            YandexCloudSave.RequestSave();
            SaveService.FlushAll();
            YandexAnalytics.Goal("run_ended");
            if (showToast && HUD.Instance != null)
            {
                HUD.Instance.PushToast("Батл-пасс: +" + height + " XP");
                for (int i = 0; i < fresh.Count; i++)
                    HUD.Instance.PushToast("Задание выполнено: " + fresh[i].Title);
            }
        }

        _segmentMax = _player.CurrentHeight;
    }
}