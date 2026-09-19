using UnityEngine;
using UnityEngine.EventSystems;

public class FishingActionButton :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    public bool IsHeld { get; private set; }

    private bool pressedQueued;
    private int pointerId = int.MinValue;

    public void OnPointerDown(
        PointerEventData eventData)
    {
        if (pointerId != int.MinValue)
            return;

        pointerId = eventData.pointerId;
        IsHeld = true;
        pressedQueued = true;
    }

    public void OnPointerUp(
        PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId)
            return;

        pointerId = int.MinValue;
        IsHeld = false;
    }

    public bool ConsumePressed()
    {
        if (!pressedQueued)
            return false;

        pressedQueued = false;
        return true;
    }

    private void OnDisable()
    {
        pointerId = int.MinValue;
        IsHeld = false;
        pressedQueued = false;
    }
}
