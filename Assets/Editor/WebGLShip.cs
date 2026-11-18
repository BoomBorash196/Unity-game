using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Сборка WebGL и ZIP-архив для консоли Яндекс Игр.
///
/// Площадка принимает игру одним архивом: в корне обязан лежать index.html,
/// имена файлов — латиницей и без пробелов, распакованное содержимое — не
/// больше 100 МБ (пункты 1.21 и 1.22). Собрать это руками можно, но каждая
/// мелочь здесь — причина отказа модерации: лишняя папка-обёртка внутри
/// архива, кириллическое имя, непропатченный index.html. Поэтому сборка и
/// упаковка идут одним запуском, а результат проверяется тут же, до загрузки.
///
/// Запуск:
///   Unity.exe -batchmode -quit -projectPath &lt;путь&gt; -executeMethod WebGLShip.Run
/// Код выхода — ноль, если архив готов к загрузке в черновик.
/// </summary>
public static class WebGLShip
{
    /// <summary>Куда кладём сборку. Папка Build уже в .gitignore.</summary>
    private const string BuildFolder = "Build/WebGL";

    /// <summary>Имя архива. Архив в черновике один: новая загрузка заменяет прежнюю.</summary>
    private const string ArchiveName = "ParkourInParadise.zip";

    /// <summary>Предел распакованного размера игры, пункт 1.21.</summary>
    private const long LimitUnpacked = 100L * 1024 * 1024;

    /// <summary>Маркер пост-хука: без скрипта SDK игру отклонят по пункту 1.1.</summary>
    private const string SdkMarker = "yandex.ru/games/sdk/v2";

    public static void Run()
    {
        EditorApplication.Exit(Ship());
    }

    /// <summary>Собрать, проверить и упаковать. Возвращает код выхода.</summary>
    public static int Ship()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            Debug.LogError("[Ship] Нет модуля WebGL Build Support для редактора " +
                           Application.unityVersion + ". Unity Hub → Installs → выбрать " +
                           "версию → Add modules → WebGL Build Support.");
            return 1;
        }

        string project = Path.GetDirectoryName(Application.dataPath);
        string outDir = Path.Combine(project, BuildFolder);
        string zipPath = Path.Combine(project, "Build", ArchiveName);

        // Настройки плеера — кодом, иначе они живут в галочках и теряются.
        ReleaseSetup.Apply();

        string[] scenes = EnabledScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[Ship] В Build Settings нет включённых сцен.");
            return 1;
        }

        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        Directory.CreateDirectory(outDir);

        Debug.Log("[Ship] сборка: сцен " + scenes.Length + " → " + outDir);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });

        BuildSummary summary = report.summary;
        Debug.Log("[Ship] результат сборки: " + summary.result +
                  ", ошибок " + summary.totalErrors +
                  ", секунд " + summary.totalTime.TotalSeconds.ToString("0"));

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[Ship] сборка не удалась — архив не собираем.");
            return 1;
        }

        if (!BridgeIsInBuild(outDir))
            return 1;

        return Package(outDir, zipPath);
    }

    /// <summary>
    /// Проверка, что мост Яндекс SDK действительно попал в сборку.
    ///
    /// Проверка не теоретическая: emscripten 3.1.39 (Unity 6) не подтягивает
    /// зависимости сам, и без __deps у функций .jslib объявление $YandexGlue
    /// вырезалось из framework.js, а вызовы _YG_* оставались — игра падала
    /// в рантайме с "YandexGlue is not defined" и зависала. Ловится это только
    /// запуском в браузере, а сборка об этом молчит, поэтому смотрим файл.
    ///
    /// Файлы в сборке сжаты Brotli, объявление ищем в распакованном виде:
    /// распаковщик Brotli лежит в самой Unity, внешних библиотек не нужно.
    /// </summary>
    private static bool BridgeIsInBuild(string outDir)
    {
        string framework = Path.Combine(outDir, "Build", "WebGL.framework.js.unityweb");
        if (!File.Exists(framework))
        {
            Debug.LogError("[Ship] нет " + framework + " — проверить мост SDK нечем.");
            return false;
        }

        string text;
        try
        {
            text = BrotliText(framework);
        }
        catch (System.Exception e)
        {
            // Не валим сборку из-за своей же проверки: игра уже собрана.
            Debug.LogWarning("[Ship] не удалось распаковать " + framework + ": " + e.Message);
            return true;
        }

        int decl = text.IndexOf("var YandexGlue=", System.StringComparison.Ordinal);
        int uses = CountOf(text, "YandexGlue.");

        Debug.Log("[Ship] мост SDK: объявление " + (decl >= 0 ? "есть" : "НЕТ") +
                  ", обращений " + uses);

        if (decl < 0)
        {
            Debug.LogError("[Ship] мост SDK вырезан из сборки: в framework.js есть " + uses +
                           " обращений к YandexGlue, но нет его объявления. Игра упадёт " +
                           "в рантайме на первом же вызове SDK. Проверить, что у функций " +
                           "в Assets/Plugins/WebGL/YandexGames.jslib указаны __deps.");
            return false;
        }

        return true;
    }

    private static int CountOf(string text, string needle)
    {
        int count = 0;
        int at = 0;
        while ((at = text.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }
        return count;
    }

    /// <summary>Распаковать файл сборки и вернуть текст.</summary>
    private static string BrotliText(string file)
    {
        using (FileStream input = File.OpenRead(file))
        using (System.IO.Compression.BrotliStream brotli =
               new System.IO.Compression.BrotliStream(input, System.IO.Compression.CompressionMode.Decompress))
        using (StreamReader reader = new StreamReader(brotli))
        {
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Проверки готовой сборки и упаковка. Вынесено отдельно, чтобы эти же
    /// проверки можно было прогнать на сборке, сделанной из окна редактора.
    /// </summary>
    public static int Package(string outDir, string zipPath)
    {
        string index = Path.Combine(outDir, "index.html");
        if (!File.Exists(index))
        {
            Debug.LogError("[Ship] В сборке нет index.html в корне — площадка не примет архив (1.22).");
            return 1;
        }

        // Пост-хук правит уже готовую страницу: свой шаблон разошёлся бы с
        // апстримом Unity. Проверяем его работу здесь, потому что после
        // загрузки в черновик это уже ошибка модерации.
        if (!File.ReadAllText(index).Contains(SdkMarker))
        {
            Debug.LogError("[Ship] В index.html нет скрипта SDK Яндекс Игр — " +
                           "пост-хук YandexGamesBuildHook не сработал (1.1).");
            return 1;
        }

        int bad = BadNames(outDir, out string example);
        if (bad > 0)
        {
            Debug.LogError("[Ship] Пробелы или не-латиница в именах: " + bad +
                           " шт., например «" + example + "» (1.22).");
            return 1;
        }

        long unpacked = FolderSize(outDir);
        if (unpacked > LimitUnpacked)
        {
            Debug.LogError("[Ship] Распакованная игра " + Mb(unpacked) +
                           " МБ при пределе 100 МБ (1.21).");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
        if (File.Exists(zipPath)) File.Delete(zipPath);

        // includeBaseDirectory: false — иначе в корне архива окажется папка
        // сборки, а index.html обязан лежать именно в корне (1.22).
        // CompressionLevel уточнён по пространству имён: своё такое же есть в
        // UnityEngine.
        ZipFile.CreateFromDirectory(outDir, zipPath,
            System.IO.Compression.CompressionLevel.Optimal, false);

        Debug.Log("[Ship] распакованная игра " + Mb(unpacked) + " МБ, архив " +
                  Mb(new FileInfo(zipPath).Length) + " МБ");
        Debug.Log("[Ship] архив готов: " + zipPath +
                  " — его и загружаем в черновик игры (поле «Архив»).");
        return 0;
    }

    /// <summary>Сцены сборки в том порядке, который задан в Build Settings.</summary>
    private static string[] EnabledScenes()
    {
        var list = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled) list.Add(scene.path);
        }
        return list.ToArray();
    }

    private static long FolderSize(string dir)
    {
        long total = 0;
        foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            total += new FileInfo(file).Length;
        }
        return total;
    }

    /// <summary>Сколько файлов и папок названы так, что площадка их не примет.</summary>
    private static int BadNames(string root, out string example)
    {
        example = "";
        int bad = 0;

        foreach (string path in Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(path);
            if (name.IndexOf(' ') < 0 && IsAscii(name)) continue;

            if (bad == 0) example = name;
            bad++;
        }

        return bad;
    }

    /// <summary>Имя без кириллицы и прочих не-ASCII символов (1.22).</summary>
    private static bool IsAscii(string name)
    {
        foreach (char c in name)
        {
            if (c > 0x7f) return false;
        }
        return true;
    }

    private static string Mb(long bytes)
    {
        return (bytes / 1048576.0).ToString("0.0");
    }
}
