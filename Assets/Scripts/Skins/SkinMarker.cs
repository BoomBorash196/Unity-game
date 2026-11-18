using UnityEngine;

/// <summary>
/// Вешается на корень модели в Assets/Resources/Skins — превращает импортированный FBX в скин.
/// Без него модель всё равно появится в списке, но с дефолтными условиями.
/// </summary>
public class SkinMarker : MonoBehaviour
{
    [Header("Идентификация")]
    public string SkinId = "";
    public string Title = "";
    [TextArea] public string Description = "";

    [Header("Цвета (для превью и покраски без текстур)")]
    public Color BodyColor = new Color(0.85f, 0.85f, 0.90f, 1f);
    public Color AccentColor = new Color(0.35f, 0.40f, 0.50f, 1f);
    public Color TrimColor = new Color(0.10f, 0.11f, 0.14f, 1f);

    [Header("Модель")]
    [Tooltip("Покрасить всю модель в BodyColor, если на ней нет текстур")]
    public bool Tint = true;
    [Tooltip("Поворот модели, если она в FBX смотрит не в +Z")]
    public float ModelYaw;
    [Range(0.5f, 1.5f)] public float ModelScale = 1f;

    [Header("Разблокировка")]
    public SkinUnlock Unlock = SkinUnlock.Height;
    [Tooltip("Рекорд в метрах или номер чекпоинта — зависит от Unlock")]
    public int UnlockValue = 100;
    public int CoinCost = 200;
    public int PointCost = 10;
}
