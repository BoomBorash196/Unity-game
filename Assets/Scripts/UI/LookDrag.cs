using UnityEngine;
using UnityEngine.EventSystems;

public class LookDrag : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (InputService.Instance == null) return;
        if (eventData.pointerId == -1) return;
        Vector2 delta = eventData.delta;
        InputService.Instance.AddLookDelta(delta.x, delta.y);
    }
}