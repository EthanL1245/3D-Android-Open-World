using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileJoystick :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    [Header("Visuals")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image handleImage;
    [SerializeField] private GameObject sprintIndicator;

    [SerializeField] private Color normalBackgroundColor =
        new Color(0.05f, 0.10f, 0.14f, 0.48f);

    [SerializeField] private Color sprintBackgroundColor =
        new Color(0.05f, 0.42f, 0.62f, 0.68f);

    [SerializeField] private Color normalHandleColor =
        new Color(0.76f, 0.91f, 1f, 0.94f);

    [SerializeField] private Color sprintHandleColor =
        new Color(0.74f, 0.98f, 1f, 1f);

    public Vector2 Value { get; private set; }

    private int activePointerId = int.MinValue;
    private bool isSprinting;
    private float sprintVisualBlend;

    private void Awake()
    {
        if (background == null)
        {
            background = transform as RectTransform;
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (handleImage == null &&
            handle != null)
        {
            handleImage =
                handle.GetComponent<Image>();
        }

        if (sprintIndicator != null)
        {
            sprintIndicator.SetActive(false);
        }

        ApplySprintVisuals(0f);
        ResetJoystick();
    }

    private void Update()
    {
        float target =
            isSprinting ? 1f : 0f;

        sprintVisualBlend =
            Mathf.MoveTowards(
                sprintVisualBlend,
                target,
                Time.deltaTime * 8f
            );

        ApplySprintVisuals(
            sprintVisualBlend
        );
    }

    public void ConfigureVisuals(
        Image backgroundVisual,
        Image handleVisual,
        GameObject sprintVisual)
    {
        backgroundImage =
            backgroundVisual;

        handleImage =
            handleVisual;

        sprintIndicator =
            sprintVisual;

        ApplySprintVisuals(
            sprintVisualBlend
        );
    }

    public void SetSprinting(bool sprinting)
    {
        if (isSprinting == sprinting)
            return;

        isSprinting = sprinting;

        if (sprintIndicator != null)
        {
            sprintIndicator.SetActive(
                sprinting
            );
        }
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
        isSprinting = false;
        sprintVisualBlend = 0f;

        if (sprintIndicator != null)
        {
            sprintIndicator.SetActive(false);
        }

        ApplySprintVisuals(0f);
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

    private void ApplySprintVisuals(
        float blend)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color =
                Color.Lerp(
                    normalBackgroundColor,
                    sprintBackgroundColor,
                    blend
                );
        }

        if (handleImage != null)
        {
            handleImage.color =
                Color.Lerp(
                    normalHandleColor,
                    sprintHandleColor,
                    blend
                );
        }

        if (handle != null)
        {
            float scale =
                Mathf.Lerp(
                    1f,
                    1.10f,
                    blend
                );

            handle.localScale =
                Vector3.one * scale;
        }
    }

    private void OnDisable()
    {
        activePointerId = int.MinValue;
        ResetJoystick();
    }
}
