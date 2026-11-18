using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Проверка неба на скайбоксах пакета: шейдер перехода, загрузка кубов и
/// сама кривая перехода между поясами.
///
/// Главное здесь — не «посчиталось ли», а не меняется ли картинка на стыке
/// поясов. Если на границе высоты видимый куб скачет, игрок получает щелчок
/// ровно там, где решает, перешёл ли он на новую ступень, и вся работа по
/// сглаживанию оказывается сделана впустую.
///
/// Чего проверка не делает и почему: она не видит картинку. Запустить игру и
/// посмотреть на небо в этом проекте нечем — сцена собирается кодом, скриншота
/// никто не снимал. Поэтому проверяется всё, что можно проверить числами: что
/// шейдер собрался, что свойства на месте, что переход непрерывен.
/// </summary>
public static class SkyBlendCheck
{
    public static int Check()
    {
        var sb = new StringBuilder();
        bool ok = true;
        int total = 0;

        ok &= CheckShader(sb, ref total);
        ok &= CheckInstall(sb, ref total);
        ok &= CheckCurve(sb, ref total);

        // Итог считается, а не прикидывается константой: расхождение между
        // числом проверок и напечатанным итогом означало бы, что половина
        // проверок молча выпала, а отчёт об этом не говорит. Такое уже было
        // с другой проверкой, где стояло 19 при фактических 42.
        if (ok) sb.AppendLine("[T] RESULT passed=" + total + " failed=0");
        else sb.AppendLine("[T] RESULT FAILED");

        Debug.Log(sb.ToString());
        return ok ? 0 : 1;
    }

    public static void Run()
    {
        EditorApplication.Exit(Check());
    }

    /// <summary>Шейдер должен существовать, собираться без ошибок и держать пять свойств.</summary>
    private static bool CheckShader(StringBuilder sb, ref int total)
    {
        bool ok = true;
        Shader s = Shader.Find("Parkour/SkyBlend");

        if (s == null)
        {
            sb.AppendLine("[T] FAIL SHADER_MISSING :: Parkour/SkyBlend не найден, небо уйдёт в откат");
            total++;
            return false;
        }

        total++;

        if (ShaderUtil.ShaderHasError(s))
        {
            sb.AppendLine("[T] FAIL SHADER_ERRORS :: Parkour/SkyBlend не собрался, небо будет пустым");
            ok = false;

            // Сообщение компилятора обязано попасть в лог. Сам факт «шейдер
            // сломан» бесполезен: причина может быть в любой строке, а в
            // batchmode текст ошибки шейдера из консоли проглатывается.
            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(s);
            for (int i = 0; i < messages.Length; i++)
            {
                ShaderMessage m = messages[i];
                sb.AppendLine("[T] SHADER_MSG " + m.severity + " line " + m.line +
                    " :: " + m.message + " " + m.messageDetails);
            }
        }
        else
        {
            sb.AppendLine("[T] PASS shader compiles :: Parkour/SkyBlend");
        }

        // Свойства проверяются поштучно и с точными именами: если хоть одно
        // переименовать в шейдере и забыть в коде, SetFloat начнёт молча
        // ничего не делать, и небо останется непереключённым без единой
        // ошибки в консоли.
        //
        // Свойства спрашиваются у материала, а не у шейдера: HasProperty есть
        // только у Material, у самого шейдера такого метода нет.
        var probe = new Material(s);
        string[] required = { "_TexA", "_TexB", "_Blend", "_Exposure", "_Tint" };
        for (int i = 0; i < required.Length; i++)
        {
            bool has = probe.HasProperty(required[i]);
            sb.AppendLine("[T] " + (has ? "PASS" : "FAIL") + " prop " + required[i]);
            total++;
            if (!has) ok = false;
        }

        Object.DestroyImmediate(probe);
        return ok;
    }

    /// <summary>Установка неба обязана закончиться каким-то небом, а не ничем.</summary>
    private static bool CheckInstall(StringBuilder sb, ref int total)
    {
        total++;
        var go = new GameObject("[T] camera");
        var cam = go.AddComponent<Camera>();

        Sky.Reset();
        Sky.Install(cam, Color.magenta);

        bool ok = Sky.Mode != 0 && RenderSettings.skybox != null;

        sb.AppendLine("[T] " + (ok ? "PASS" : "FAIL") + " INSTALL :: mode=" + Sky.Mode +
            " кубов=" + Sky.LoadedCubemaps + " материал=" +
            (RenderSettings.skybox == null ? "null" : RenderSettings.skybox.name));

        if (!ok)
        {
            sb.AppendLine("[T] FAIL NO_SKY :: после Install небо не назначено");
            Object.DestroyImmediate(go);
            return false;
        }

        // Если кубы не загрузились, небо обязано уйти в откат, а не остаться
        // пустым. В этом и смысл проверки: пак может пропасть, и игра должна
        // остаться играбельной.
        if (Sky.Mode == 1 && Sky.LoadedCubemaps == 6)
        {
            sb.AppendLine("[T] INFO небо идёт через кубы с переходом, режим 1");
        }
        else
        {
            sb.AppendLine("[T] INFO небо ушло в откат, режим " + Sky.Mode +
                " при кубах " + Sky.LoadedCubemaps + " — это допустимо, но перехода нет");
        }

        Object.DestroyImmediate(go);
        return true;
    }

    /// <summary>
    /// Кривая перехода. Проверяется не сама формула, а её следствие: на стыке
    /// поясов видимый куб обязан остаться тем же, что и чуть ниже.
    /// </summary>
    private static bool CheckCurve(StringBuilder sb, ref int total)
    {
        bool ok = true;
        float[] a = Sky.BandAltitudes;

        // Середина пояса: перехода ещё нет, видно свой куб.
        for (int i = 0; i + 1 < a.Length; i++)
        {
            float mid = (a[i] + a[i + 1]) * 0.5f;
            total += 2;
            ok &= Report(sb, "mid " + i + " blend 0", Mathf.Abs(At(mid)) < 1e-4f, At(mid).ToString("0.000"));
            ok &= Report(sb, "mid " + i + " cube " + i, Cube(mid) == i, Cube(mid).ToString());
        }

        // Граница пояса: ровно на ней и сразу над ней переход ещё не начался,
        // и виден верхний куб. Чуть ниже границы переход предыдущего пояса уже
        // дошёл до конца и тоже показывает этот куб. Равенство этих трёх чисел
        // и есть доказательство, что щелчка нет.
        for (int i = 1; i < a.Length; i++)
        {
            float at = a[i];
            // Небо намеренно не пересчитывается при смещении меньше FollowStep:
            // на глаз 5 см незаметны, а лишние SetColor каждый кадр стоят
            // дороже. Поэтому проверяем ровно за пределами этого шага.
            float below = at - Sky.FollowStep - 0.01f;
            float above = at + Sky.FollowStep + 0.01f;
            total += 4;

            ok &= Report(sb, "edge " + i + " blend at 0", Mathf.Abs(At(at)) < 1e-4f, At(at).ToString("0.000"));
            ok &= Report(sb, "edge " + i + " blend below done", At(below) > 0.99f, At(below).ToString("0.000"));
            ok &= Report(sb, "edge " + i + " blend above 0", Mathf.Abs(At(above)) < 1e-4f, At(above).ToString("0.000"));

            ok &= Report(sb, "edge " + i + " cube continuous",
                Cube(below) == i && Cube(at) == i && Cube(above) == i,
                (Cube(below).ToString() + "/" + Cube(at).ToString() + "/" + Cube(above).ToString()));
        }

        // Внутри окна перехода доля обязана расти, а не прыгать: иначе небо
        // мигнёт вместо плавной смены картинки.
        //
        // Верхняя граница пояса не берётся: там переход следующего пояса
        // начинается с нуля, и значение ноль не из-за рывка, а потому что
        // пара кубов сменилась. Этот сброс и есть то, что делает стык
        // непрерывным, и проверять его тут нельзя — непрерывность стыка
        // проверяется отдельно, выше.
        for (int i = 0; i + 1 < a.Length; i++)
        {
            float span = a[i + 1] - a[i];
            float prev = -1f;
            bool rising = true;
            for (int k = 75; k <= 99; k += 4)
            {
                float h = a[i] + span * (k / 100f);
                float b = At(h);
                if (b < prev - 1e-5f) rising = false;
                prev = b;
            }

            total++;
            ok &= Report(sb, "band " + i + " fade monotonic", rising && prev > 0.5f, prev.ToString("0.000"));
        }

        return ok;
    }

    private static float At(float altitude)
    {
        Sky.Follow(new Vector3(0f, altitude, 0f));
        return Sky.CurrentBlend;
    }

    private static int Cube(float altitude)
    {
        Sky.Follow(new Vector3(0f, altitude, 0f));
        return Sky.DominantCube;
    }

    private static bool Report(StringBuilder sb, string what, bool pass, string detail)
    {
        sb.AppendLine("[T] " + (pass ? "PASS" : "FAIL") + " " + what + " :: " + detail);
        return pass;
    }
}
