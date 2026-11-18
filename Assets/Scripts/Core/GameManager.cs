using UnityEngine;

public class GameManager : MonoBehaviour
{
    public const int SceneBootstrapIndex = 0;
    public const int SceneMainMenuIndex = 1;
    public const int SceneGameplayIndex = 2;

    public static GameManager Instance { get; private set; }

    public GameState State { get; set; } = GameState.MainMenu;

    public int RequestedStartCheckpoint { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InputService.EnsureExists();

#if UNITY_WEBGL && !UNITY_EDITOR
        // На площадке важнее стабильные 60 fps, чем мягкие тени на полсотни
        // метров. Один жёсткий каскад в пределах 20 м даёт читаемую тень под
        // игроком и не тянет второй проход по всему уровню.
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.shadowCascades = 1;
        QualitySettings.shadowDistance = 20f;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
#endif
    }

    private void Update()
    {
        SaveService.Tick();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveService.FlushAll();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) SaveService.FlushAll();
    }

    private void OnApplicationQuit()
    {
        SaveService.FlushAll();
    }

    public void StartFromCheckpoint(int checkpoint)
    {
        RequestedStartCheckpoint = Mathf.Max(0, checkpoint);
        State = GameState.Playing;
        SceneLoader.RestartGameplay();
    }

    public void StartNewGame()
    {
        StartFromCheckpoint(0);
    }

    public void ToMainMenu()
    {
        SaveService.FlushAll();
        State = GameState.MainMenu;
        Time.timeScale = 1f;
        SceneLoader.LoadMainMenu();
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
        {
            State = GameState.Paused;
            Time.timeScale = 0f;
            SaveService.FlushAll();
        }
        else if (State == GameState.Paused)
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
        }
    }

    public void SetPaused(bool paused)
    {
        if (paused && State == GameState.Playing)
        {
            State = GameState.Paused;
            Time.timeScale = 0f;
            SaveService.FlushAll();
        }
        else if (!paused && State == GameState.Paused)
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
        }
    }
}