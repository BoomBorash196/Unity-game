#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Уменьшает вес скайбоксов пака до релизного.
///
/// Пак идёт кросс-картинками 8192x4096, и в таком виде одно небо — четверть
/// гигабайта видеопамяти, а все двенадцать ещё и уезжают в сборку, потому что
/// лежат материалами в Resources. Для веба это двойной удар: игра не проходит
/// по лимиту размера (у Яндекс Игр — 100 МБ на все файлы) и падает по памяти
/// на слабых машинах.
///
/// Настройка ведётся кодом, а не руками в .meta, по той же причине, что и
/// TextureImport: импорт чужого пака должен быть описан в одном месте и
/// воспроизводиться при переимпорте, а не жить в двенадцати файлах.
///
/// Куб на две тысячи (по тысяче на грань) небо не портит: это фон, а не то,
/// на что смотрят в упор, зато вес падает примерно в шестнадцать раз.
/// </summary>
public class SkyboxImport : AssetPostprocessor
{
    private const string PackTexturePath = "Assets/8K Skybox Pack Free/Skyboxes/Texture/";

    /// <summary>Сколько ставить на скайбокс. Хватает с запасом, лимит не рвёт.</summary>
    public const int MaxSize = 1024;

    private void OnPreprocessTexture()
    {
        if (assetPath == null || !assetPath.StartsWith(PackTexturePath)) return;

        TextureImporter importer = assetImporter as TextureImporter;
        if (importer == null) return;

        importer.maxTextureSize = MaxSize;
        importer.textureCompression = TextureImporterCompression.Compressed;

        // У пака на каждую платформу прописаны свои восемь тысяч. Убираем
        // переопределения, чтобы везде работал общий размер выше.
        string[] platforms = { "Standalone", "Android", "iPhone" };
        foreach (string platform in platforms)
        {
            try { importer.ClearPlatformTextureSettings(platform); }
            catch { /* платформы нет в этом модуле — не наша забота */ }
        }
    }

    /// <summary>Переимпорт пака, чтобы новые настройки применились к уже
    /// импортированным текстурам. Вызывается из командной строки.</summary>
    public static void ReimportAll()
    {
        AssetDatabase.ImportAsset("Assets/8K Skybox Pack Free/Skyboxes/Texture",
            ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        Debug.Log("[SkyboxImport] текстуры пака переимпортированы под " + MaxSize);
        EditorApplication.Exit(0);
    }
}
#endif
