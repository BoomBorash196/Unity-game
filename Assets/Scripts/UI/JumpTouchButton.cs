using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JumpTouchButton : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        if (InputService.Instance != null)
            InputService.Instance.RequestJump();
    }
}