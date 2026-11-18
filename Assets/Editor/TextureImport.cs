#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Настройки импорта внешних текстур блоков. Картинки пользователя лежат в
/// Assets/Resources/Textures и импортируются не по умолчанию, а под наш путь:
/// Read/Write включён — код читает яркость картинки и строит из неё рельеф,
/// нормаль и цвет; заворот Repeat и mip-цепочка для близких и дальних площадок.
/// Ничего не правится руками: только код, как и весь остальной проект.
/// </summary>
public class TextureImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (assetPath == null || !assetPath.StartsWith("Assets/Resources/Textures/")) return;

        TextureImporter importer = assetImporter as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Compressed;
    }
}
#endif