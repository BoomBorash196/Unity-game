using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    public static void LoadMainMenu()
    {
        SaveService.FlushAll();
        ResetCursor();
        ClearRuntimeCaches();
        SceneManager.LoadScene(GameManager.SceneMainMenuIndex);
    }

    public static void LoadBootstrap()
    {
        SaveService.FlushAll();
        ResetCursor();
        ClearRuntimeCaches();
        SceneManager.LoadScene(GameManager.SceneBootstrapIndex);
    }

    public static void RestartGameplay()
    {
        SaveService.FlushAll();
        ResetCursor();
        ClearRuntimeCaches();
        SceneManager.LoadScene(GameManager.SceneGameplayIndex);
    }

    /// <summary>
    /// Сцены не делят между собой процедурные спрайты и пулы объектов: старый
    /// интерфейс и геометрия всё равно выгружаются. Чистим кэши до загрузки,
    /// чтобы меню не тянуло в геймплей мегабайты UI-текстур, а пулы не хранили
    /// ссылки на объекты уничтоженной сцены.
    /// </summary>
    private static void ClearRuntimeCaches()
    {
        CloudStyle.ClearCache();
        MenuIcons.ClearCache();
        MenuArt.ClearCache();

        CoinPickup.ClearPool();
        Hazard.ClearPool();
        HazardBurst.ClearPool();
    }

    private static void ResetCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}