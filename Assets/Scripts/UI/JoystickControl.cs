using UnityEngine;
using UnityEngine.EventSystems;

public class JoystickControl : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool IsActive { get; private set; }

    private RectTransform _canvasRect;
    private RectTransform _baseRect;
    private RectTransform _knobRect;
    private float _radius = 110f;
    private Vector2 _centerLocal;

    public void Setup(RectTransform canvasRect, RectTransform uiBase, RectTransform knob)
    {
        _canvasRect = canvasRect;
        _baseRect = uiBase;
        _knobRect = knob;
        Show(false);
    }

    public Vector2 Output
    {
        get
        {
            if (!IsActive || _knobRect == null)
                return Vector2.zero;
            Vector2 delta = (Vector2)_knobRect.anchoredPosition - _centerLocal;
            if (delta.magnitude <= 0.1f)
                return Vector2.zero;
            return Vector2.ClampMagnitude(delta / _radius, 1f);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == -1) return;
        if (!InsideScreen(eventData) || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, eventData.position, null, out Vector2 local))
            return;

        Vector2 bottomLeft = local + _canvasRect.rect.size * 0.5f;
        Show(true);
        _baseRect.anchoredPosition = ClampToSafeZone(bottomLeft);
        _centerLocal = _baseRect.anchoredPosition;
        _knobRect.anchoredPosition = _centerLocal;
        PushToInput();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsActive || eventData.pointerId == -1) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, eventData.position, null, out Vector2 local))
            return;

        Vector2 bottomLeft = local + _canvasRect.rect.size * 0.5f;
        Vector2 delta = bottomLeft - _centerLocal;
        Vector2 clamped = delta.magnitude > _radius ? (delta.normalized * _radius) : delta;
        _knobRect.anchoredPosition = _centerLocal + clamped;
        PushToInput();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ResetDrag();
        PushToInput();
    }

    public void ResetDrag()
    {
        Show(false);
        if (_knobRect != null)
            _knobRect.anchoredPosition = _centerLocal;
    }

    private void PushToInput()
    {
        if (InputService.Instance != null)
            InputService.Instance.SetJoystick(Output);
    }

    private void Show(bool show)
    {
        IsActive = show;
        if (_baseRect != null) _baseRect.gameObject.SetActive(show);
        if (_knobRect != null) _knobRect.gameObject.SetActive(show);
    }

    private bool InsideScreen(PointerEventData eventData)
    {
        return eventData.position.x >= 0f && eventData.position.y >= 0f &&
               eventData.position.x <= Screen.width && eventData.position.y <= Screen.height;
    }

    private Vector2 ClampToSafeZone(Vector2 local)
    {
        float minX = 120f;
        float maxX = 320f;
        float minY = 120f;
        float maxY = 420f;
        return new Vector2(Mathf.Clamp(local.x, minX, maxX), Mathf.Clamp(local.y, minY, maxY));
    }
}