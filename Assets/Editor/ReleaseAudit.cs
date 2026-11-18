#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Релизный аудит: что реально уедет в сборку и что из этого мусор.
///
/// Считает зависимости всех сцен сборки и всех файлов Resources (Resources
/// тащит в билд свои зависимости даже вне сцен), суммирует размеры, ищет
/// временные файлы, примерный контент паков и пустые папки.
///
/// Пишет отчёт в ReleaseAuditReport.txt в корне проекта — в batchmode кириллица
/// в консоль приезжает кракозябрами, а файл читается как есть.
/// </summary>
public static class ReleaseAudit
{
    private const string ReportName = "ReleaseAuditReport.txt";

    public static void Run()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        StringBuilder sb = new StringBuilder();

        // --- 1. Что попадёт в билд: сцены + Resources и их зависимости ---
        List<string> seeds = new List<string>();

        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s != null && s.enabled && !string.IsNullOrEmpty(s.path))
                seeds.Add(s.path);
        }

        string[] resourceGuids = AssetDatabase.FindAssets("t:Object", new[] { "Assets/Resources" });
        foreach (string g in resourceGuids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (!string.IsNullOrEmpty(p)) seeds.Add(p);
        }

        string[] deps = AssetDatabase.GetDependencies(seeds.ToArray(), true);
        HashSet<string> included = new HashSet<string>(deps.Where(p => p.StartsWith("Assets/")));

        // Размер считаем не по исходному файлу, а по тому, во что Unity его
        // перекодирует: 8K-PNG неба на диске весит тридцать мегабайт, а в
        // сборке это сжатый куб на пару мегабайт. Иначе отчёт пугает числом,
        // которого в архиве не будет.
        List<KeyValuePair<string, long>> sized = new List<KeyValuePair<string, long>>();
        long total = 0;
        foreach (string p in included)
        {
            long len = EstimateSize(p, projectRoot);
            if (len <= 0) continue;
            total += len;
            sized.Add(new KeyValuePair<string, long>(p, len));
        }
        sized.Sort((a, b) => b.Value.CompareTo(a.Value));

        sb.AppendLine("=== RELEASE AUDIT  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " ===");
        sb.AppendLine();
        sb.AppendLine("-- IN BUILD --");
        sb.AppendLine("assets: " + included.Count + ", total: " + Mb(total) + " MB");
        sb.AppendLine();
        sb.AppendLine("-- TOP 25 LARGEST IN BUILD --");
        foreach (KeyValuePair<string, long> kv in sized.Take(25))
            sb.AppendLine(Mb(kv.Value).PadLeft(8) + " MB   " + kv.Key);

        // --- 2. Всё в Assets/Resources по отдельности (в билд идут целиком) ---
        sb.AppendLine();
        sb.AppendLine("-- ALL Assets/Resources (fully included) --");
        long resTotal = 0;
        foreach (KeyValuePair<string, long> kv in sized.Where(k => k.Key.StartsWith("Assets/Resources"))
                                                       .OrderByDescending(k => k.Value))
        {
            resTotal += kv.Value;
            sb.AppendLine(Mb(kv.Value).PadLeft(8) + " MB   " + kv.Key);
        }
        sb.AppendLine("Resources total: " + Mb(resTotal) + " MB");

        // --- 3. Мусор: временные файлы, примерный контент, дубли ---
        sb.AppendLine();
        sb.AppendLine("-- JUNK CANDIDATES --");
        string[] junkExt = { ".bak", ".tmp", ".orig", ".old" };
        foreach (string p in AllAssetFiles(projectRoot))
        {
            string ext = Path.GetExtension(p).ToLowerInvariant();
            if (junkExt.Contains(ext) || p.EndsWith(".cs.bak", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("temp:   " + p);

            if (p.IndexOf("Example Scenes", StringComparison.OrdinalIgnoreCase) >= 0)
                sb.AppendLine("demo:   " + p);
        }

        // Пустые папки в Assets.
        foreach (string dir in Directory.GetDirectories(Path.Combine(projectRoot, "Assets"), "*", SearchOption.AllDirectories))
        {
            if (Directory.GetFileSystemEntries(dir).Length == 0)
                sb.AppendLine("empty:  " + dir.Replace(projectRoot + Path.DirectorySeparatorChar, "").Replace('\\', '/'));
        }

        // --- 4. Debug.Log в рантайме (не в Assets/Editor) ---
        sb.AppendLine();
        sb.AppendLine("-- Debug.Log IN RUNTIME CODE --");
        int logs = 0;
        foreach (string p in Directory.GetFiles(Path.Combine(projectRoot, "Assets"), "*.cs", SearchOption.AllDirectories))
        {
            string rel = p.Replace(projectRoot + Path.DirectorySeparatorChar, "").Replace('\\', '/');
            if (rel.StartsWith("Assets/Editor/")) continue;
            string[] lines = File.ReadAllLines(p);
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i];
                if (t.Contains("Debug.Log(") || t.Contains("Debug.LogWarning(") || t.Contains("Debug.LogError("))
                {
                    sb.AppendLine(rel + ":" + (i + 1) + "  " + t.Trim());
                    logs++;
                }
            }
        }
        if (logs == 0) sb.AppendLine("(none)");

        // --- 5. Настройки, важные для релиза ---
        sb.AppendLine();
        sb.AppendLine("-- PLAYER SETTINGS --");
        sb.AppendLine("companyName:   " + PlayerSettings.companyName);
        sb.AppendLine("productName:   " + PlayerSettings.productName);
        sb.AppendLine("bundleVersion: " + PlayerSettings.bundleVersion);
        sb.AppendLine("orientation:   " + PlayerSettings.defaultInterfaceOrientation);
        sb.AppendLine("web w x h:     " + PlayerSettings.defaultWebScreenWidth + " x " + PlayerSettings.defaultWebScreenHeight);
        sb.AppendLine("webGL: decompression " + PlayerSettings.WebGL.decompressionFallback);
        sb.AppendLine("scenes in build: " + EditorBuildSettings.scenes.Length);

        // --- 6. Сцены пака неба и их статус ---
        sb.AppendLine();
        sb.AppendLine("-- SKYBOX PACK --");
        long packBytes = 0;
        string packDir = Path.Combine(projectRoot, "Assets/8K Skybox Pack Free");
        if (Directory.Exists(packDir))
        {
            foreach (string f in Directory.GetFiles(packDir, "*", SearchOption.AllDirectories))
                packBytes += new FileInfo(f).Length;
        }
        sb.AppendLine("pack on disk: " + Mb(packBytes) + " MB");

        int inBuild = 0;
        foreach (string p in included)
            if (p.StartsWith("Assets/8K Skybox Pack Free")) inBuild++;
        sb.AppendLine("pack files pulled into build: " + inBuild);
        foreach (string p in included.Where(x => x.StartsWith("Assets/8K Skybox Pack Free")).OrderBy(x => x))
            sb.AppendLine("   " + p);

        string report = Path.Combine(projectRoot, ReportName);
        File.WriteAllText(report, sb.ToString(), new UTF8Encoding(true));
        Debug.Log("[Audit] report: " + report);

        EditorApplication.Exit(0);
    }

    private static List<string> AllAssetFiles(string projectRoot)
    {
        List<string> result = new List<string>();
        string assets = Path.Combine(projectRoot, "Assets");
        foreach (string f in Directory.GetFiles(assets, "*", SearchOption.AllDirectories))
            result.Add(f.Replace(projectRoot + Path.DirectorySeparatorChar, "").Replace('\\', '/'));
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>Оценка веса ассета в сборке. Текстуры меряем по тому, сколько
    /// они занимают после перекодирования в формат платформы (для неба это в
    /// разы меньше исходного PNG); всё остальное — по файлу на диске.</summary>
    private static long EstimateSize(string assetPath, string projectRoot)
    {
        string ext = Path.GetExtension(assetPath).ToLowerInvariant();
        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".psd")
        {
            Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
            if (tex != null)
            {
                long runtime = Profiler.GetRuntimeMemorySizeLong(tex);
                if (runtime > 0) return runtime;
            }
        }

        string abs = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(abs) ? new FileInfo(abs).Length : 0;
    }

    private static string Mb(long bytes)
    {
        return (bytes / (1024.0 * 1024.0)).ToString("0.0");
    }
}
#endif
