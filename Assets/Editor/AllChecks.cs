using UnityEditor;
using UnityEngine;

/// <summary>
/// Все проверки одним запуском Unity.
///
/// Каждая проверка отдельно — это отдельный холодный старт редактора, а на этом
/// проекте он занимает минуты и повторяется по мелочи: поменяли небо, запустили
/// две проверки, потратили лишний запуск на третью. Собирать их в один вход
/// дешевле, чем терпеть это на каждой правке.
///
/// Порядок важен: сперва ассеты и шейдеры, потом ступени. Если небо не
/// собралось, проверка кривой перехода всё равно пройдёт (она считает числа, а не
/// смотрит картинку), и будет казаться, что небо в порядке. Порядок этого не
/// отменяет, но помечает в логе, что смотреть надо с начала.
///
/// Возвращает код, которым завершится процесс: ноль, если всё чисто.
/// </summary>
public static class AllChecks
{
    public static void Run()
    {
        int failed = 0;

        Debug.Log("[T] === обзор камеры ===");
        failed += CameraLookCheck.Check();

        Debug.Log("[T] === ассеты неба ===");
        failed += SkyboxPackCheck.Check();

        Debug.Log("[T] === шейдер и кривая перехода ===");
        failed += SkyBlendCheck.Check();

        Debug.Log("[T] === ступени, ловушки, биомы ===");
        failed += StageCheck.Check();

        Debug.Log("[T] === внешние текстуры блоков ===");
        failed += BlockTextureCheck.Check();

        Debug.Log("[T] === формы площадок ===");
        failed += PlatformShapeCheck.Check();

        Debug.Log("[T] === дейлики, ачивки, батл-пасс ===");
        failed += MetaCheck.Check();

        Debug.Log("[T] === UI мета-панелей (smoke) ===");
        failed += MetaUISmokeCheck.Check();

        Debug.Log("[T] === раскладка интерфейса ===");
        failed += UILayoutCheck.Check();

        Debug.Log("[T] === ИТОГ провалов: " + failed + " ===");
        EditorApplication.Exit(failed);
    }
}
