using UnityEngine;

public static class GameSettings
{
    public const string KeyMouseLook = "controls_look_mouse";
    public const string KeyTouchLook = "controls_look_touch";

    public const float MouseDefault = 7f;
    public const float MouseMin = 0.7f;
    public const float MouseMax = 70f;
    public const float MouseStep = 1f;

    public const float TouchDefault = 1f;
    public const float TouchMin = 0.1f;
    public const float TouchMax = 10f;
    public const float TouchStep = 0.2f;

    public static float MouseLookSensitivity
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(KeyMouseLook, MouseDefault), MouseMin, MouseMax); }
        set
        {
            value = Mathf.Clamp(Round(value), MouseMin, MouseMax);
            PlayerPrefs.SetFloat(KeyMouseLook, value);
            PlayerPrefs.Save();
        }
    }

    public static float TouchLookSensitivity
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(KeyTouchLook, TouchDefault), TouchMin, TouchMax); }
        set
        {
            value = Mathf.Clamp(Round(value), TouchMin, TouchMax);
            PlayerPrefs.SetFloat(KeyTouchLook, value);
            PlayerPrefs.Save();
        }
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KeyMouseLook);
        PlayerPrefs.DeleteKey(KeyTouchLook);
        PlayerPrefs.Save();
    }

    private static float Round(float value)
    {
        return Mathf.Round(value * 10f) / 10f;
    }
}