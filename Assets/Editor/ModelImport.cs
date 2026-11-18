#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Настройки импорта моделей скинов (Assets/Resources/Skins).
///
/// У моделей Dota в FBX лежит около 256 анимационных клипов, а игра их не
/// использует: движение скинов процедурное (SkinAnimator). Клипы только раздувают
/// импорт и билд (FBX лежит в Resources и попал бы в сборку целиком), поэтому
/// импорт клипов выключается.
///
/// Тип рига при этом остаётся `Generic`: с `None` Unity не строит скелет и
/// разбирает модель на статичные меши — кости есть, но крутить их бессмысленно.
/// Скелет и материалы нужны, поэтому «нет анимации» ≠ «нет рига».
/// </summary>
public class ModelImport : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (assetPath == null || !assetPath.StartsWith("Assets/Resources/Skins/")) return;

        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null) return;

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;

        // SkinApplier считает габариты модели по вершинам, скиняя их вручную по
        // костям: bind-pose bounds не отражают позу (крюк, пересаженный костью в
        // руку, мерился бы у ног), а меш из SkinnedMeshRenderer.BakeMesh приходит
        // в неочевидном пространстве. Значит, вершины должны читаться.
        importer.isReadable = true;
    }
}
#endif
