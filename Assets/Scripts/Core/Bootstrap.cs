using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Стартовая сцена: поднимает общие сервисы, показывает экран загрузки и ждёт,
/// пока Яндекс SDK ответит и облачные сохранения сольются с локальными.
///
/// Раньше здесь сразу загружалось меню, и на медленной сети главный экран
/// успевал показать локальные цифры, а через секунду они менялись на облачные.
/// Теперь меню строится уже после слияния, а экран загрузки растворяется поверх
/// готового интерфейса.
/// </summary>
public class Bootstrap : MonoBehaviour
{
    private const float MinShow = 0.9f;
    private const float MaxWait = 5f;

    private bool _sdkFailed;

    private IEnumerator Start()
    {
        if (GameManager.Instance == null)
        {
            GameObject app = new GameObject("[App] GameManager");
            app.AddComponent<GameManager>();
        }
        InputService.EnsureExists();

        LoadingScreen.Show("Пробуждаем мир");
        YandexGames.Failed += OnSdkFailed;

        float started = Time.realtimeSinceStartup;

        while (true)
        {
            float elapsed = Time.realtimeSinceStartup - started;

            LoadingScreen.SetStatus(StatusText());
            LoadingScreen.SetProgress(Mathf.Clamp01(0.12f + elapsed / MaxWait * 0.8f));

            if (elapsed >= MinShow && (ReadyForMenu() || elapsed >= MaxWait))
                break;

            yield return null;
        }

        YandexGames.Failed -= OnSdkFailed;

        LoadingScreen.SetProgress(1f);
        LoadingScreen.SetStatus("Открываем главное меню...");

        // Площадка убирает свой загрузчик только после этого вызова. Делаем его
        // до смены сцены: экран загрузки уже закрывает переход.
        if (YandexGames.Instance != null)
            YandexGames.Instance.RequestLoadingReady();

        LoadingScreen.Hide(0.25f);
        SceneManager.LoadScene(GameManager.SceneMainMenuIndex);
    }

    private bool ReadyForMenu()
    {
        if (!YandexSdk.Supported) return true;
        if (_sdkFailed) return true;
        if (!YandexGames.Ready) return false;
        return YandexCloudSave.Loaded;
    }

    private string StatusText()
    {
        if (!YandexSdk.Supported || _sdkFailed) return "Готовим меню...";
        if (!YandexGames.Ready) return "Подключаемся к Яндексу...";
        if (!YandexCloudSave.Loaded) return "Загружаем сохранения...";
        return "Открываем главное меню...";
    }

    private void OnSdkFailed(string message)
    {
        _sdkFailed = true;
    }
}
