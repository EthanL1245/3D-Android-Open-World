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
    private bool skillSwipe;
    private const float SkillLookSensitivity = 0.2f;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        skillSwipe = false;
        accumulatedDelta = Vector2.zero;
        if(skillOwner==null)skillOwner=FindFirstObjectByType<FishingBurstDamageRuntime>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        bool skillReady = skillOwner != null && skillOwner.IsSkillReady;
        // Keep the full gesture for skill detection, but damp camera rotation.
        // Finish this swipe at low sensitivity to avoid a jump as charge is spent.
        if (skillOwner != null && skillOwner.TrySkillLook(eventData.delta)) skillSwipe = true;
        accumulatedDelta += eventData.delta *
            ((skillReady || skillSwipe) ? SkillLookSensitivity : 1f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        activePointerId = int.MinValue;
        skillSwipe = false;
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
        skillSwipe = false;
        accumulatedDelta = Vector2.zero;
    }
}

