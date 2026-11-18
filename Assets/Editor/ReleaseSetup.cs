#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Релизные настройки плеера, которые держим кодом, а не в окне.
///
/// PlayerSettings правится здесь по той же причине, что и импорт текстур:
/// настройка, сделанная галочкой в инспекторе, нигде не записана и теряется
/// при переносе проекта. А имя сборки — это не косметика: файлы Unity
/// получают имя из productName, и кириллица или пробел в них проваливают
/// модерацию площадки (требование 1.22).
///
/// Название игры на экране и в карточке при этом кириллическое — «Паркур в
/// рай»; здесь только техническое имя файлов.
/// </summary>
public static class ReleaseSetup
{
    /// <summary>Имя файлов сборки. Только латиница, без пробелов.</summary>
    public const string ProductName = "ParkourInParadise";

    /// <summary>Разработчик в настройках плеера.</summary>
    public const string CompanyName = "Tanks in Greece";

    /// <summary>
    /// Применить настройки. Отдельно от <see cref="Run"/>, чтобы сборщик
    /// (WebGLShip) мог позвать их в общем процессе и не завершить редактор
    /// на середине сборки.
    /// </summary>
    public static void Apply()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;

        // Стартовый размер холста. На площадке размер пересчитывается под
        // область (см. YandexGamesBuildHook), это лишь запас на случай запуска
        // сборки локально.
        PlayerSettings.defaultWebScreenWidth = 1920;
        PlayerSettings.defaultWebScreenHeight = 1080;

        // Сжатие. Brotli даёт самый маленький архив, но браузер распакует его
        // сам только если сервер площадки отдаёт Content-Encoding: br. Заголовки
        // нам не подконтрольны, поэтому включён запасной распаковщик: он тянет
        // файл как есть и разжимает в JS. Иначе игра не стартует там, где
        // заголовка нет, а требование 1.20 — запуск во всех браузерах списка.
        // Если на слабых телефонах старт окажется долгим, менять здесь одну
        // строку на WebGLCompressionFormat.Gzip (разжимается браузером быстрее).
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;

        // Отключение стриппера кода движка.
        //
        // Причина конкретная: GameObject.CreatePrimitive(PrimitiveType.Capsule)
        // встречается только в резервном пути сборки игрока (LevelManager), и
        // стриппер не видит повода оставить класс CapsuleCollider. В билде
        // CreatePrimitive падает с "Can't add component because class
        // 'CapsuleCollider' doesn't exist!" — игрок остаётся без тела. В
        // редакторе ошибка не видна: там стриппинг не работает.
        //
        // Чинится здесь, а не галочкой в окне плеера (см. заголовок файла),
        // и ценой всего кода движка — сборка с ним 29 МБ при лимите 100 МБ,
        // экономить тут нечего.
        PlayerSettings.stripEngineCode = false;

        AssetDatabase.SaveAssets();
    }

    public static void Run()
    {
        Apply();

        Debug.Log("[ReleaseSetup] product=" + PlayerSettings.productName +
                  " company=" + PlayerSettings.companyName +
                  " web=" + PlayerSettings.defaultWebScreenWidth + "x" + PlayerSettings.defaultWebScreenHeight +
                  " compression=" + PlayerSettings.WebGL.compressionFormat +
                  " fallback=" + PlayerSettings.WebGL.decompressionFallback);

        EditorApplication.Exit(0);
    }
}
#endif
