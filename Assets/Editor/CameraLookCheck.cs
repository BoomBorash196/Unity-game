using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Проверка обзора камеры: можно ли посмотреть вверх и не теряется ли игрок.
///
/// Смысл проверки в том, что «вверх нельзя» — это не ошибка ввода, а зажим,
/// и снаружи он не виден: мышь двигается, наклон меняется, просто не туда.
/// Поэтому берётся тот же код, что и в игре, и замеряется, куда в итоге смотрит
/// камера и остался ли игрок в кадре.
///
/// Порядок вызовов важен и повторяет игровой: сначала LateUpdate — он и есть
/// единственное место, где наклон зажимается, — потом Snap, который ставит
/// камеру в точное положение. Без LateUpdate проверка зажима прошла бы вхолостую,
/// потому что Snap зажим не делает вовсе. А LateUpdate один SmoothDamp'ом
/// позицию ещё не доводит, поэтому мерить углы после него рано.
/// </summary>
public static class CameraLookCheck
{
    /// <summary>Угол обзора камеры из SceneSetup. Дублируется здесь намеренно:
    /// проверка обязана ловить случай, когда сцену перенастроили, а вместе с ней
    /// разъехалась геометрия обзора.</summary>
    private const float Fov = 65f;

    public static int Check()
    {
        var sb = new StringBuilder();
        bool ok = true;
        int total = 0;

        var target = new GameObject("[T] target");
        var go = new GameObject("[T] cam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = Fov;

        var follow = go.AddComponent<CameraFollow>();
        follow.Target = target.transform;

        FieldInfo pitch = typeof(CameraFollow).GetField(
            "_pitch", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo late = typeof(CameraFollow).GetMethod(
            "LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);

        if (pitch == null || late == null)
        {
            sb.AppendLine("[T] FAIL NO_REFLECTION :: у CameraFollow нет _pitch или LateUpdate;" +
                " проверка ослепла бы и молча ничего не проверяла");
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(go);
            return 1;
        }

        total++;

        // Цель высоко над нулем: камера не должна проваливаться сквозь пол,
        // иначе проверка измеряла бы не то.
        target.transform.position = new Vector3(0f, 100f, 0f);

        float halfFov = Fov * 0.5f;

        // --- нижняя граница должна быть отрицательной ---
        // Это и есть исходная поломка: положительный минимум не даёт наклониться
        // вверх вообще. Проверяется значением поля, а не поведением, чтобы
        // поломка была видна даже если LateUpdate перестанет вызываться.
        float min = follow.PitchMin;
        total++;
        ok &= Report(sb, "PitchMin < 0 (взгляд вверх достижим)", min < 0f, "PitchMin=" + min);

        // --- зажим сверху и снизу ---
        // Просим наклоны за пределами диапазона и смотрим, куда их срезало.
        float[] asks = { -90f, -45f, -31f, 76f, 120f };
        float[] want = { min, min, min, follow.PitchMax, follow.PitchMax };
        for (int i = 0; i < asks.Length; i++)
        {
            float got = Drive(follow, pitch, late, asks[i]);
            total++;
            ok &= Report(sb, "clamp " + asks[i], Mathf.Abs(got - want[i]) < 0.01f,
                "стало " + got.ToString("0.0") + ", ждали " + want[i].ToString("0.0"));
        }

        // --- смотрит ли камера вверх на нижней границе ---
        Drive(follow, pitch, late, min);
        follow.Snap();

        Vector3 fwd = go.transform.forward;

        // Угол отсчитывается от мировой вертикали, а наклон вверх — это
        // дополнение к нему до 90. Считать одно и называть другим нельзя:
        // на горизонте угол от вертикали равен 90, то есть проверка на
        // «наклон вверх от 10 до 45» отклонила бы идеально ровную камеру.
        float upTilt = 90f - Vector3.Angle(Vector3.up, fwd);

        total++;
        ok &= Report(sb, "камера смотрит вверх (fwd.y > 0)", fwd.y > 0f,
            "fwd.y=" + fwd.y.ToString("0.000"));

        total++;
        ok &= Report(sb, "наклон вверх в разумных пределах", upTilt >= 10f && upTilt <= 45f,
            "вверх от горизонта " + upTilt.ToString("0.0") + " градусов");

        // --- игрок обязан остаться в кадре ---
        // Это то, чем приходится платить за обзор вверх: чем сильнее камера
        // задирается, тем ближе игрок к нижней кромке кадра. Уйти за неё нельзя,
        // иначе игрок во время прыжка просто пропадёт с экрана.
        Vector3 toTarget = (target.transform.position - go.transform.position).normalized;
        float offCenter = Vector3.Angle(fwd, toTarget);
        total++;
        ok &= Report(sb, "игрок в кадре на максимуме вверх", offCenter < halfFov,
            offCenter.ToString("0.0") + " градусов от центра при " + halfFov.ToString("0.0"));

        // --- камера не проваливается под игрока ---
        // Отрицательный наклон по построению уводит камеру вниз: на -30 это
        // примерно на два метра ниже ног. Если увести сильнее, камера окажется
        // под платформой, на которой стоит игрок.
        float drop = target.transform.position.y - go.transform.position.y;
        total++;
        ok &= Report(sb, "камера не глубже 3 м под игроком", drop < 3f,
                "ниже игрока на " + drop.ToString("0.00") + " м");

        // --- верхняя граница не сломалась ---
        Drive(follow, pitch, late, follow.PitchMax);
        follow.Snap();
        fwd = go.transform.forward;
        toTarget = (target.transform.position - go.transform.position).normalized;
        offCenter = Vector3.Angle(fwd, toTarget);

        total++;
        ok &= Report(sb, "игрок в кадре на максимуме вниз", offCenter < halfFov,
            offCenter.ToString("0.0") + " градусов от центра");

        total++;
        ok &= Report(sb, "вниз камера над игроком",
            go.transform.position.y > target.transform.position.y,
            "камера выше игрока на " + (go.transform.position.y - target.transform.position.y).ToString("0.00") + " м");

        if (ok) sb.AppendLine("[T] RESULT passed=" + total + " failed=0");
        else sb.AppendLine("[T] RESULT FAILED");

        Debug.Log(sb.ToString());
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(go);
        return ok ? 0 : 1;
    }

    /// <summary>
    /// Прогоняет наклон через игровой код и возвращает, что осталось после
    /// зажима: LateUpdate применяет Clamp, и иначе проверка зажима проверяла бы
    /// саму себя, а не игру.
    /// </summary>
    private static float Drive(CameraFollow follow, FieldInfo pitch, MethodInfo late, float ask)
    {
        pitch.SetValue(follow, ask);
        late.Invoke(follow, null);
        return (float)pitch.GetValue(follow);
    }

    private static bool Report(StringBuilder sb, string what, bool pass, string detail)
    {
        sb.AppendLine("[T] " + (pass ? "PASS" : "FAIL") + " " + what + " :: " + detail);
        return pass;
    }

    public static void Run()
    {
        EditorApplication.Exit(Check());
    }
}