using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

/// <summary>
/// Правки готового index.html под Яндекс Игры.
///
/// Шаблон Unity рисует игру центрированным прямоугольником 960x600, а площадке
/// нужен холст на всю доступную область (требование 1.6.2.1). Здесь же
/// прячется мобильное «WebGL-уведомление» шаблона (требование 1.6.1.7) и
/// снимается выделение/контекстное меню по долгому тапу (1.6.1.8, 1.6.2.7).
///
/// Всё делается кодом после сборки и не трогает сам шаблон: своя копия
/// шаблона в проекте рано или поздно разойдётся с апстримом Unity, а правка
/// готовой страницы воспроизводится при каждой сборке.
///
/// Каждая вставка проверяется на маркер, поэтому повторный запуск сборки
/// ничего не дублирует.
/// </summary>
public class YandexGamesBuildHook
{
    private const string SdkTag = "<script src=\"https://yandex.ru/games/sdk/v2\"></script>";
    private const string MarkStyle = "id=\"yandex-games-fixes\"";
    private const string MarkScript = "id=\"yandex-games-input\"";

    [PostProcessBuild(1)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.WebGL) return;

        string index = ResolveIndex(pathToBuiltProject);
        if (index == null) return;

        string html = File.ReadAllText(index);
        bool changed = false;

        if (!html.Contains("yandex.ru/games/sdk/v2"))
        {
            html = InsertInHead(html, SdkTag + "\n");
            changed = true;
        }

        if (!html.Contains(MarkStyle))
        {
            html = InsertInHead(html, StyleBlock());
            changed = true;
        }

        if (!html.Contains(MarkScript))
        {
            html = InsertBeforeBodyEnd(html, ScriptBlock());
            changed = true;
        }

        if (changed) File.WriteAllText(index, html);
    }

    /// <summary>Путь к index.html: сборка отдаёт то файл, то папку.</summary>
    private static string ResolveIndex(string path)
    {
        if (File.Exists(path) && path.EndsWith(".html")) return path;
        if (!Directory.Exists(path)) return null;

        string candidate = Path.Combine(path, "index.html");
        if (File.Exists(candidate)) return candidate;

        string[] files = Directory.GetFiles(path, "*.html", SearchOption.TopDirectoryOnly);
        return files.Length > 0 ? files[0] : null;
    }

    private static string InsertInHead(string html, string payload)
    {
        if (html.Contains("</head>")) return html.Replace("</head>", payload + "</head>");
        if (html.Contains("<head>")) return html.Replace("<head>", "<head>\n" + payload);
        return payload + html;
    }

    private static string InsertBeforeBodyEnd(string html, string payload)
    {
        if (html.Contains("</body>")) return html.Replace("</body>", payload + "\n</body>");
        return html + "\n" + payload;
    }

    /// <summary>Холст на всю область площадки, без выделения и без уведомлений.</summary>
    private static string StyleBlock()
    {
        return
            "<style " + MarkStyle + ">\n" +
            "  html, body { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background: #0b0e14; }\n" +
            "  #unity-container, #unity-container.unity-desktop, #unity-container.unity-mobile {\n" +
            "    position: absolute; left: 0; top: 0; width: 100% !important; height: 100% !important;\n" +
            "    transform: none !important; margin: 0; box-shadow: none;\n" +
            "  }\n" +
            "  #unity-canvas, #unity-canvas.unity-mobile { width: 100% !important; height: 100% !important; }\n" +
            "  #unity-canvas { -webkit-user-select: none; user-select: none; -webkit-touch-callout: none; }\n" +
            "  #unity-footer, #unity-logo-title-footer, #unity-build-title { display: none !important; }\n" +
            "  #unity-warning, .unity-mobile-warning { display: none !important; }\n" +
            "</style>\n";
    }

    /// <summary>Долгий тап не должен открывать контекстное меню или выделять
    /// текст, а холст должен пересчитываться под размер области площадки.</summary>
    private static string ScriptBlock()
    {
        return
            "<script " + MarkScript + ">\n" +
            "  (function () {\n" +
            "    function block(e) {\n" +
            "      if (e.target && e.target.tagName === 'CANVAS') e.preventDefault();\n" +
            "    }\n" +
            "    document.addEventListener('contextmenu', block, false);\n" +
            "    document.addEventListener('selectstart', block, false);\n" +
            "\n" +
            "    // Шаблон Unity держит холст фиксированного размера и растягивается\n" +
            "    // только через CSS — с искажением пропорций. Считаем размер сами:\n" +
            "    // область площадки даёт и разрешение отрисовки, и пропорции.\n" +
            "    function fit() {\n" +
            "      var canvas = document.querySelector('#unity-canvas');\n" +
            "      if (!canvas) return;\n" +
            "      var box = document.querySelector('#unity-container') || document.body;\n" +
            "      var w = Math.max(1, Math.floor(box.clientWidth));\n" +
            "      var h = Math.max(1, Math.floor(box.clientHeight));\n" +
            "      if (canvas.width !== w || canvas.height !== h) {\n" +
            "        canvas.width = w;\n" +
            "        canvas.height = h;\n" +
            "      }\n" +
            "    }\n" +
            "    window.addEventListener('resize', fit, false);\n" +
            "    window.addEventListener('orientationchange', fit, false);\n" +
            "    if (typeof ResizeObserver !== 'undefined') {\n" +
            "      new ResizeObserver(fit).observe(document.body);\n" +
            "    }\n" +
            "    fit();\n" +
            "  })();\n" +
            "</script>";
    }
}
