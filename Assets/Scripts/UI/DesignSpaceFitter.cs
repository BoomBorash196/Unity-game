using UnityEngine;

/// <summary>
/// Вписывает сцену макета фиксированного размера (1920x1080) в родителя
/// целиком: масштаб = min(ширина родителя / ширина сцены,
/// высота родителя / высота сцены).
///
/// Зачем. Холст растягивается под окно, а интерфейс расставлен в единых
/// координатах 1920x1080. При соотношении сторон ровно 16:9 единицы холста и
/// есть 1920x1080, но на широком окне высота холста в единицах меньше 1080 —
/// и нижние кнопки пропадают, хотя расставлены вроде бы верно. Сцена же всегда
/// влезает целиком, и видно весь макет, а не его верхнюю часть.
///
/// На 16:9 масштаб равен единице, то есть картинка совпадает с исходной до
/// пикселя: подгонка не меняет вид там, где он и так был верным.
/// </summary>
public class DesignSpaceFitter : MonoBehaviour
{
    /// <summary>Холст меньше этой стороны в единицах считается неразложенным.
    /// Настоящий холст всегда около 1920x1080 единиц (CanvasScaler держит
    /// постоянную площадь), а неразложенный — пустой или дефолтные сто.</summary>
    private const float MinSide = 200f;

    private RectTransform _self;
    private Vector2 _size = new Vector2(1920f, 1080f);

    public void Setup(RectTransform self, Vector2 size)
    {
        _self = self;
        _size = size;
        Apply();
    }

    private void LateUpdate()
    {
        // Каждый кадр, а не один раз: размер окна меняется, и в браузере это
        // обычное дело — игрок разворачивает игру, тянет окно, открывает
        // консоль площадки.
        Apply();
    }

    private void Apply()
    {
        RectTransform self = _self != null ? _self : transform as RectTransform;
        if (self == null) return;
        _self = self;

        RectTransform parent = self.parent as RectTransform;
        if (parent == null) return;

        float width = parent.rect.width;
        float height = parent.rect.height;
        if (width < MinSide || height < MinSide) return;

        float scale = Mathf.Min(width / _size.x, height / _size.y);
        if (scale <= 0f) return;
        if (Mathf.Approximately(self.localScale.x, scale)) return;

        self.localScale = new Vector3(scale, scale, 1f);
    }
}
