using UnityEngine;
using UnityEngine.EventSystems;

public class MobileJoystick :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    public Vector2 Value { get; private set; }

    private int activePointerId = int.MinValue;

    private void Awake()
    {
        if (background == null)
        {
            background = transform as RectTransform;
        }

        ResetJoystick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        UpdateJoystick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        UpdateJoystick(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        activePointerId = int.MinValue;
        ResetJoystick();
    }

    private void UpdateJoystick(PointerEventData eventData)
    {
        if (background == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Vector2 halfSize = background.rect.size * 0.5f;

        if (halfSize.x <= 0f || halfSize.y <= 0f)
            return;

        Vector2 normalized = new Vector2(
            localPoint.x / halfSize.x,
            localPoint.y / halfSize.y
        );

        Value = Vector2.ClampMagnitude(normalized, 1f);

        if (handle != null)
        {
            float backgroundRadius =
                Mathf.Min(background.rect.width, background.rect.height) * 0.5f;

            float handleRadius =
                Mathf.Min(handle.rect.width, handle.rect.height) * 0.5f;

            float travelRadius =
                Mathf.Max(0f, backgroundRadius - handleRadius);

            handle.anchoredPosition = Value * travelRadius;
        }
    }

    private void ResetJoystick()
    {
        Value = Vector2.zero;

        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }
    }

    private void OnDisable()
    {
        activePointerId = int.MinValue;
        ResetJoystick();
    }
}
