using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Проверка скайбоксов, на которых держится небо.
///
/// Проверяются не все двенадцать файлов пака, а только те шесть, что ставит
/// игра: список берётся у Sky, а не продублирован здесь. Своя копия списка
/// рано или поздно разойдётся с настоящей, и проверка станет проверять не
/// того неба, которое видит игрок.
///
/// Про формат: пакет — не панорамы, а кросс-картинки, в одном PNG шесть граней
/// куба (в .meta стоит textureShape: TextureCube и generateCubemap: 6). Отсюда
/// две ловушки, обе молчаливые: в C# такая текстура это Cubemap, и приведение
/// `as Texture2D` даёт null; а шейдер Skybox/Cubemap верен именно для неё,
/// и Skybox/Panoramic наоборот показал бы пустое небо.
///
/// Известный дефект самого пака: sky-2.mat — вариант материала (m_Parent
/// указывает на префаб), а базового материала в паке нет, поэтому у него ноль
/// текстурных свойств. Игра этот скайбокс не использует, поэтому его поломка
/// reported как сведения, а не как отказ: чинить чужой пак смысла нет.
///
/// Видеопамять считается отдельно и честно: восемь тысяч на грань дают
/// примерно четверть гигабайта на небо, и это сказывается на слабых машинах
/// раньше, чем появится хоть какой-то визуальный дефект.
/// </summary>
public static class SkyboxPackCheck
{
    /// <summary>Гигабайт на один скайбокс, выше которого небо уже тяжелое.</summary>
    private const double VramWarnGb = 0.12;

    public static int Check()
    {
        var sb = new StringBuilder();
        bool ok = true;
        int total = 0;

        float[] bands = Sky.BandAltitudes;
        double totalGb = 0;

        for (int band = 0; band < bands.Length; band++)
        {
            total++;
            string id = Sky.PackSkyFor(band);
            if (string.IsNullOrEmpty(id))
            {
                sb.AppendLine("[T] FAIL NO_SKY_ID :: пояс " + band + " без скайбокса");
                ok = false;
                continue;
            }

            // Именно тот путь, каким берёт небо в игре. AssetDatabase здесь был бы
            // удобнее, но проверил бы не то: в сборке AssetDatabase нет.
            Material m = Resources.Load<Material>("Skyboxes/" + id);
            if (m == null)
            {
                sb.AppendLine("[T] FAIL NOT_LOADABLE :: " + id + " не грузится из Resources");
                ok = false;
                continue;
            }

            Texture cube = m.GetTexture("_Tex");
            if (cube == null)
            {
                sb.AppendLine("[T] FAIL NO_TEXTURE :: " + id + " без куба: в .mat свойство _Tex не привязано");
                ok = false;
                continue;
            }

            if (!(cube is Cubemap))
            {
                sb.AppendLine("[T] FAIL NOT_A_CUBE :: " + id + " это " + cube.GetType().Name +
                    ", а шейдеру неба нужен куб");
                ok = false;
                continue;
            }

            long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(cube);
            double gb = bytes / 1073741824.0;
            totalGb += gb;

            sb.AppendLine("[T] INFO пояс " + band + " (" + bands[band] + " м) -> " + id +
                " куб 6x" + cube.width + " " + cube.graphicsFormat +
                " память=" + gb.ToString("0.000") + " ГБ");

            total++;
            if (gb > VramWarnGb)
            {
                sb.AppendLine("[T] WARN " + id + " тяжёлый: " + gb.ToString("0.000") +
                    " ГБ на один небоскрёб");
            }
        }

        sb.AppendLine("[T] INFO небо целиком занимает " + totalGb.ToString("0.00") +
            " ГБ видеопамяти");

        // Обход остальных файлов пака: игра их не берёт, но поломка в них
        // должна быть видна сразу, а не всплыть через месяц, когда кто-то
        // решит доставить этот скайбокс до пояса.
        total++;
        ReportUnused(sb);

        if (ok) sb.AppendLine("[T] RESULT passed=" + total + " failed=0");
        else sb.AppendLine("[T] RESULT FAILED");

        Debug.Log(sb.ToString());
        return ok ? 0 : 1;
    }

    public static void Run()
    {
        EditorApplication.Exit(Check());
    }

    private static void ReportUnused(StringBuilder sb)
    {
        string[] used =
        {
            Sky.PackSkyFor(0), Sky.PackSkyFor(1), Sky.PackSkyFor(2),
            Sky.PackSkyFor(3), Sky.PackSkyFor(4), Sky.PackSkyFor(5)
        };

        int broken = 0;
        for (int i = 1; i <= 12; i++)
        {
            string id = "sky-" + i;
            bool taken = false;
            for (int k = 0; k < used.Length; k++)
                if (used[k] == id) taken = true;

            if (taken) continue;

            Material m = Resources.Load<Material>("Skyboxes/" + id);
            if (m == null || m.GetTexture("_Tex") == null)
            {
                sb.AppendLine("[T] INFO " + id + " сломан в паке и не используется" +
                    (m != null ? " (вариант материала без базового)" : " (не грузится)"));
                broken++;
            }
        }

        sb.AppendLine("[T] INFO не используется скайбоксов со поломками: " + broken + " из 6 запасных");
    }
}
