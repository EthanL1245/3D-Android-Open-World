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
    private int pinchPointerId = int.MinValue;
    private Vector2 primaryPosition, pinchPosition;
    private float pinchSeparation;
    private FishingThirdPersonView thirdPerson;
    private FishingBurstDamageRuntime skillOwner;
    private bool skillSwipe;
    private const float SkillLookSensitivity = 0.2f;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
        {
            if (thirdPerson == null) thirdPerson = FindFirstObjectByType<FishingThirdPersonView>();
            // Both fingers must begin on this look surface. Joystick, reel,
            // drag slider and menu touches never participate in camera zoom.
            if (eventData.pointerId != activePointerId && pinchPointerId == int.MinValue &&
                thirdPerson != null && thirdPerson.IsThirdPerson)
            {
                pinchPointerId = eventData.pointerId;
                pinchPosition = eventData.position;
                pinchSeparation = Vector2.Distance(primaryPosition, pinchPosition);
                accumulatedDelta = Vector2.zero;
                skillSwipe = false;
            }
            return;
        }

        activePointerId = eventData.pointerId;
        primaryPosition = eventData.position;
        skillSwipe = false;
        accumulatedDelta = Vector2.zero;
        if(skillOwner==null)skillOwner=FindFirstObjectByType<FishingBurstDamageRuntime>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (pinchPointerId != int.MinValue)
        {
            if (eventData.pointerId == activePointerId) primaryPosition = eventData.position;
            else if (eventData.pointerId == pinchPointerId) pinchPosition = eventData.position;
            else return;
            float separation = Vector2.Distance(primaryPosition, pinchPosition);
            if (thirdPerson != null) thirdPerson.PinchZoom(pinchSeparation, separation);
            pinchSeparation = separation;
            accumulatedDelta = Vector2.zero;
            return; // A pinch must not rotate the view or trigger a fishing skill.
        }
        if (eventData.pointerId != activePointerId)
            return;

        primaryPosition = eventData.position;
        bool skillReady = skillOwner != null && skillOwner.IsSkillReady;
        // Keep the full gesture for skill detection, but damp camera rotation.
        // Finish this swipe at low sensitivity to avoid a jump as charge is spent.
        if (skillOwner != null && skillOwner.TrySkillLook(eventData.delta)) skillSwipe = true;
        accumulatedDelta += eventData.delta *
            ((skillReady || skillSwipe) ? SkillLookSensitivity : 1f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId && eventData.pointerId != pinchPointerId)
            return;

        activePointerId = int.MinValue;
        pinchPointerId = int.MinValue;
        pinchSeparation = 0f;
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
        pinchPointerId = int.MinValue;
        pinchSeparation = 0f;
        skillSwipe = false;
        accumulatedDelta = Vector2.zero;
    }
}

