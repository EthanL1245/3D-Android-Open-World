using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private MobileJoystick moveJoystick;
    [SerializeField] private TouchLookArea touchLookArea;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float touchSensitivity = 0.15f;
    [SerializeField] private float maxLookAngle = 80f;

    private CharacterController controller;
    private float verticalVelocity;
    private float cameraPitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();
    }

    private void HandleMovement()
    {
        Vector2 input = ReadMovementInput();
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 movement =
            transform.right * input.x +
            transform.forward * input.y;

        controller.Move(movement * moveSpeed * Time.deltaTime);

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private Vector2 ReadMovementInput()
    {
        Vector2 keyboardInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                keyboardInput.y += 1f;

            if (Keyboard.current.sKey.isPressed)
                keyboardInput.y -= 1f;

            if (Keyboard.current.dKey.isPressed)
                keyboardInput.x += 1f;

            if (Keyboard.current.aKey.isPressed)
                keyboardInput.x -= 1f;
        }

        Vector2 mobileInput =
            moveJoystick != null ? moveJoystick.Value : Vector2.zero;

        return mobileInput.sqrMagnitude > keyboardInput.sqrMagnitude
            ? mobileInput
            : keyboardInput;
    }

    private void HandleLook()
    {
        Vector2 lookDelta = Vector2.zero;

        if (!Application.isMobilePlatform && Mouse.current != null)
        {
            lookDelta += Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        if (touchLookArea != null)
        {
            Vector2 touchDelta = touchLookArea.ConsumeLookDelta();

            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            float dpiScale = Mathf.Max(1f, dpi / 160f);

            lookDelta += (touchDelta / dpiScale) * touchSensitivity;
        }

        transform.Rotate(Vector3.up * lookDelta.x);

        cameraPitch -= lookDelta.y;
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}
