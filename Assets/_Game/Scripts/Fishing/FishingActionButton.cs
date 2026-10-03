using UnityEngine;
using UnityEngine.EventSystems;

public class FishingActionButton :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    public bool IsHeld { get; private set; }
    public event System.Action Pressed;
    public event System.Action Released;

    public bool Interactable {get;private set;}=true;
    public void SetInteractable(bool value){Interactable=value;if(!value)ResetInput();}
    private bool pressedQueued;
    private int pointerId = int.MinValue;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable || pointerId != int.MinValue) return;
        pointerId = eventData.pointerId;
        IsHeld = true;
        pressedQueued = true;
        Pressed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        pointerId = int.MinValue;
        IsHeld = false;
        Released?.Invoke();
    }

    public bool ConsumePressed()
    {
        if (!pressedQueued) return false;
        pressedQueued = false;
        return true;
    }

    public void ResetInput()
    {
        bool wasHeld = IsHeld;
        pointerId = int.MinValue;
        IsHeld = false;
        pressedQueued = false;
        if (wasHeld) Released?.Invoke();
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
