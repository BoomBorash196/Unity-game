using UnityEngine;

public static class SceneSetup
{
    public static void EnsureLight()
    {
        Light light = Object.FindFirstObjectByType<Light>();
        if (light != null) return;

        GameObject go = new GameObject("Directional Light");
        Light l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.1f;
        l.color = new Color(1f, 0.96f, 0.9f, 1f);
        go.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
    }

    public static void EnsureBackgroundCamera(Color background)
    {
        if (Camera.main != null) return;

        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 300f;
        go.AddComponent<AudioListener>();
    }

    /// <summary>
    /// Включает небо на камере. Отдельный метод, а не флаг внутри EnsureLight,
    /// потому что небо нужно и в игровой сцене, и в меню, а свет — только в меню.
    /// </summary>
    public static void EnsureSky(Camera cam, Color fallbackBackground)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Sky.Install(cam, fallbackBackground);
    }
}