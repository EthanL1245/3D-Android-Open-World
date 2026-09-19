using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private MobileJoystick moveJoystick;
    [SerializeField] private TouchLookArea touchLookArea;
    [SerializeField] private OceanWater oceanWater;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Swimming")]
    [SerializeField] private float swimSpeed = 3.8f;
    [SerializeField] private float swimEnterDepth = 0.45f;
    [SerializeField] private float floatingBodyDepth = 0.55f;
    [SerializeField] private float buoyancyStrength = 1.6f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float touchSensitivity = 0.15f;
    [SerializeField] private float maxLookAngle = 80f;

    private CharacterController controller;
    private float verticalVelocity;
    private float cameraPitch;

    public bool IsSwimming { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (oceanWater == null)
        {
            oceanWater = FindFirstObjectByType<OceanWater>();
        }
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

    public void SetOceanWater(OceanWater water)
    {
        oceanWater = water;
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.ClampMagnitude(ReadMovementInput(), 1f);

        UpdateSwimmingState();

        if (IsSwimming)
        {
            HandleSwimming(input);
        }
        else
        {
            HandleWalking(input);
        }
    }

    private void HandleWalking(Vector2 input)
    {
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

    private void HandleSwimming(Vector2 input)
    {
        verticalVelocity = 0f;

        Transform lookTransform =
            cameraTransform != null ? cameraTransform : transform;

        Vector3 swimDirection =
            lookTransform.forward * input.y +
            lookTransform.right * input.x;

        float verticalInput = ReadKeyboardSwimVerticalInput();
        swimDirection += Vector3.up * verticalInput;

        swimDirection = Vector3.ClampMagnitude(swimDirection, 1f);

        float waterHeight = oceanWater.GetSurfaceHeight(transform.position);
        float bodyHeight = transform.position.y + controller.center.y;
        float targetBodyHeight = waterHeight - floatingBodyDepth;

        float surfaceAssist = Mathf.Clamp(
            (targetBodyHeight - bodyHeight) * buoyancyStrength,
            -1.2f,
            1.4f
        );

        // Let intentional movement overpower buoyancy so looking down and
        // swimming forward naturally dives beneath the surface.
        float assistMultiplier =
            input.sqrMagnitude > 0.04f || Mathf.Abs(verticalInput) > 0.01f
                ? 0.45f
                : 1f;

        Vector3 velocity =
            swimDirection * swimSpeed +
            Vector3.up * surfaceAssist * assistMultiplier;

        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateSwimmingState()
    {
        if (oceanWater == null)
        {
            oceanWater = FindFirstObjectByType<OceanWater>();

            if (oceanWater == null)
            {
                IsSwimming = false;
                return;
            }
        }

        float waterHeight = oceanWater.GetSurfaceHeight(transform.position);
        float bodyHeight = transform.position.y + controller.center.y;

        if (IsSwimming)
        {
            // Small hysteresis prevents rapid switching while bobbing at the surface.
            IsSwimming = bodyHeight < waterHeight + 0.30f;
        }
        else
        {
            IsSwimming = bodyHeight < waterHeight - swimEnterDepth;
        }
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

    private float ReadKeyboardSwimVerticalInput()
    {
        if (Keyboard.current == null)
            return 0f;

        float value = 0f;

        if (Keyboard.current.spaceKey.isPressed)
            value += 1f;

        if (Keyboard.current.leftCtrlKey.isPressed)
            value -= 1f;

        return value;
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

        if (cameraTransform != null)
        {
            cameraTransform.localRotation =
                Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }
}
