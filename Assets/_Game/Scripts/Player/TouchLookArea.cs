using UnityEngine;
using UnityEngine.EventSystems;

public class TouchLookArea :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    private int activePointerId = int.MinValue;
    private Vector2 accumulatedDelta;
    private FishingBurstDamageRuntime skillOwner;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        accumulatedDelta = Vector2.zero;
        if(skillOwner==null)skillOwner=FindFirstObjectByType<FishingBurstDamageRuntime>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        accumulatedDelta += eventData.delta;
        if(skillOwner!=null)skillOwner.TrySkillLook(eventData.delta);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        activePointerId = int.MinValue;
        accumulatedDelta = Vector2.zero;
    }

    public Vector2 ConsumeLookDelta()
    {
        Vector2 delta = accumulatedDelta;
        accumulatedDelta = Vector2.zero;
        return delta;
    }

    private void OnDisable()
    {
        activePointerId = int.MinValue;
        accumulatedDelta = Vector2.zero;
    }
}

