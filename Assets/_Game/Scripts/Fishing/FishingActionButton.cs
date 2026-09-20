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

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointerId != int.MinValue) return;
        pointerId = eventData.pointerId;
        IsHeld = true;
        pressedQueued = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        pointerId = int.MinValue;
        IsHeld = false;
    }

    public bool ConsumePressed()
    {
        if (!pressedQueued) return false;
        pressedQueued = false;
        return true;
    }

    public void ResetInput()
    {
        pointerId = int.MinValue;
        IsHeld = false;
        pressedQueued = false;
    }

    private void OnDisable() => ResetInput();
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ResetInput();
    }
    private void OnApplicationPause(bool paused)
    {
        if (paused) ResetInput();
    }
}
