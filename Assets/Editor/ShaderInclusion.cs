using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Добавляет в Always Included Shaders всё, что игра ищет через Shader.Find.
///
/// Проблема ровно в том, что игра не ссылается на шейдеры ассетами: небо
/// собирается в коде через Shader.Find, материалы строятся там же. Шейдер,
/// на который нет ни одной ссылки в сцене и ни одной в Always Included, из
/// сборки вырезается целиком. В редакторе он находится и всё выглядит
/// правильно, а в игре Shader.Find вернёт null — и небо станет пустым.
///
/// Правка в Always Included Shaders делается здесь, кодом перед сборкой.
/// Трогать ProjectSettings вручную смысла нет: список всё равно пришлось бы
/// держать в голове, и следующий, кто откроет файл, ничего об этом не узнает.
///
/// Класс лежит в папке Editor, поэтому в саму игру не попадает вовсе.
/// </summary>
public sealed class ShaderInclusion : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    /// <summary>Шейдеры, которые игра ищет по имени.
    ///
    /// Небо перечислено тремя строками не избыточностью, а откатами: Sky
    /// пробует сначала свой шейдер перехода, потом встроенный куб, потом
    /// процедурный, и любой из них обязан быть в сборке, иначе вместо отката
    /// получится пустое небо.</summary>
    private static readonly string[] Required =
    {
        "Parkour/SkyBlend",
        "Skybox/Cubemap",
        "Skybox/Procedural",
        "Standard",
        "Diffuse",
        "Sprites/Default"
    };

    public void OnPreprocessBuild(BuildReport report)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[BUILD] GraphicsSettings.asset не найден — шейдеры не добавлены, небо может пропасть");
            return;
        }

        var settings = new SerializedObject(assets[0]);
        SerializedProperty list = settings.FindProperty("m_AlwaysIncludedShaders");
        if (list == null)
        {
            Debug.LogError("[BUILD] в GraphicsSettings нет m_AlwaysIncludedShaders — шейдеры не добавлены");
            return;
        }

        var have = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object s = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (s != null) have.Add(s);
        }

        int added = 0;
        foreach (string name in Required)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning("[BUILD] шейдер не найден, пропускаю: " + name);
                continue;
            }

            if (have.Contains(shader)) continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            have.Add(shader);
            added++;
        }

        if (added == 0) return;

        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        Debug.Log("[BUILD] в Always Included добавлено шейдеров: " + added);
    }
}
