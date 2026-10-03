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
    private Vector2 swipeDelta;
    private float swipeStart;
    private FishingBurstDamageRuntime skillOwner;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        accumulatedDelta = Vector2.zero;
        swipeDelta=Vector2.zero;
        swipeStart=Time.unscaledTime;
        if(skillOwner==null)skillOwner=FindFirstObjectByType<FishingBurstDamageRuntime>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        accumulatedDelta += eventData.delta;
        swipeDelta+=eventData.delta;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        if(skillOwner!=null)skillOwner.TrySkillSwipe(swipeDelta,Time.unscaledTime-swipeStart);
        activePointerId = int.MinValue;
        accumulatedDelta = Vector2.zero;
        swipeDelta=Vector2.zero;
    }

    public Vector2 ConsumeLookDelta()
    {
        Vector2 delta = accumulatedDelta;
        accumulatedDelta = Vector2.zero;
        return delta;
    }

    private void OnDisable()
    {
        swipeDelta=Vector2.zero;
        activePointerId = int.MinValue;
        accumulatedDelta = Vector2.zero;
    }
}

