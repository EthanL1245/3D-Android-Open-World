using UnityEngine;
using UnityEngine.EventSystems;

public class MobileActionButton :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    public bool IsHeld { get; private set; }

    private bool pressedQueued;
    private int activePointerId = int.MinValue;

    public void OnPointerDown(
        PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
            return;

        activePointerId =
            eventData.pointerId;

        IsHeld = true;
        pressedQueued = true;
    }

    public void OnPointerUp(
        PointerEventData eventData)
    {
        if (eventData.pointerId !=
            activePointerId)
        {
            return;
        }

        Release();
    }

    public void OnPointerExit(
        PointerEventData eventData)
    {
        // Keep holding while the finger drifts slightly outside.
        // PointerUp still releases the action.
    }

    public bool ConsumePressed()
    {
        if (!pressedQueued)
            return false;

        pressedQueued = false;
        return true;
    }

    private void Release()
    {
        activePointerId = int.MinValue;
        IsHeld = false;
    }

    private void OnDisable()
    {
        activePointerId = int.MinValue;
        IsHeld = false;
        pressedQueued = false;
    }
}
