using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private MobileJoystick moveJoystick;
    [SerializeField] private TouchLookArea touchLookArea;
    [SerializeField] private MobileActionButton jumpButton;
    [SerializeField] private OceanWater oceanWater;

    [Header("Walking")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 7.5f;
    [SerializeField] private float groundAcceleration = 18f;
    [SerializeField] private float groundDeceleration = 24f;
    [SerializeField] private float airAcceleration = 6f;
    [SerializeField] private float gravity = -22f;
    [SerializeField] private float groundStickForce = 3f;
    [SerializeField] private float jumpHeight = 1.25f;

    [Header("Mobile Sprint")]
    [SerializeField] private float mobileSprintMagnitude = 0.92f;
    [SerializeField] private float mobileSprintForward = 0.72f;

    [Header("Swimming")]
    [SerializeField] private float swimSpeed = 3.8f;
    [SerializeField] private float swimAcceleration = 8f;
    [SerializeField] private float swimEnterDepth = 0.45f;
    [SerializeField] private float floatingBodyDepth = 0.55f;
    [SerializeField] private float buoyancyStrength = 1.6f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float touchSensitivity = 0.25f;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float sprintFovBoost = 4f;
    [SerializeField] private float fovLerpSpeed = 7f;

    private CharacterController controller;
    private Camera playerCamera;

    private Vector3 planarVelocity;
    private Vector3 swimVelocity;

    private float verticalVelocity;
    private float cameraPitch;
    private float baseFov = 72f;

    private bool previousSwimming;

    public bool IsSwimming { get; private set; }
    public bool IsSprinting { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform != null)
        {
            playerCamera = cameraTransform.GetComponent<Camera>();

            if (playerCamera != null)
                baseFov = playerCamera.fieldOfView;
        }

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

        previousSwimming = IsSwimming;
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();
        UpdateCameraFov();
    }

    public void SetOceanWater(OceanWater water)
    {
        oceanWater = water;
    }

    public void SetJumpButton(MobileActionButton button)
    {
        jumpButton = button;
    }

    private void HandleMovement()
    {
        Vector2 input =
            Vector2.ClampMagnitude(
                ReadMovementInput(),
                1f
            );

        bool jumpPressed =
            ReadJumpPressed();

        bool swimUpHeld =
            ReadSwimUpHeld();

        float swimDownInput =
            ReadKeyboardSwimDownInput();

        UpdateSwimmingState();
        HandleSwimmingTransition();

        if (IsSwimming)
        {
            IsSprinting = false;

            HandleSwimming(
                input,
                swimUpHeld,
                swimDownInput
            );
        }
        else
        {
            HandleWalking(
                input,
                jumpPressed
            );
        }
    }

    private void HandleWalking(
        Vector2 input,
        bool jumpPressed)
    {
        bool grounded = controller.isGrounded;

        float inputMagnitude =
            Mathf.Clamp01(input.magnitude);

        bool wantsSprint =
            WantsToSprint(input);

        IsSprinting =
            grounded &&
            wantsSprint &&
            input.y > 0.15f &&
            inputMagnitude > 0.15f;

        float targetSpeed =
            IsSprinting
                ? sprintSpeed
                : moveSpeed;

        Vector3 desiredDirection =
            transform.right * input.x +
            transform.forward * input.y;

        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            desiredDirection.Normalize();

            if (grounded &&
                TryGetGroundNormal(
                    out Vector3 groundNormal))
            {
                Vector3 slopeDirection =
                    Vector3.ProjectOnPlane(
                        desiredDirection,
                        groundNormal
                    );

                if (slopeDirection.sqrMagnitude > 0.0001f)
                {
                    desiredDirection =
                        slopeDirection.normalized;
                }
            }
        }

        Vector3 targetVelocity =
            desiredDirection *
            targetSpeed *
            inputMagnitude;

        float acceleration;

        if (!grounded)
        {
            acceleration = airAcceleration;
        }
        else if (inputMagnitude > 0.01f)
        {
            acceleration = groundAcceleration;
        }
        else
        {
            acceleration = groundDeceleration;
        }

        planarVelocity =
            Vector3.MoveTowards(
                planarVelocity,
                targetVelocity,
                acceleration * Time.deltaTime
            );

        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity =
                -groundStickForce;
        }

        if (grounded && jumpPressed)
        {
            verticalVelocity =
                Mathf.Sqrt(
                    jumpHeight *
                    -2f *
                    gravity
                );
        }
        else
        {
            verticalVelocity +=
                gravity * Time.deltaTime;
        }

        Vector3 velocity =
            planarVelocity +
            Vector3.up * verticalVelocity;

        controller.Move(
            velocity * Time.deltaTime
        );
    }

    private void HandleSwimming(
        Vector2 input,
        bool swimUpHeld,
        float swimDownInput)
    {
        verticalVelocity = 0f;

        Transform lookTransform =
            cameraTransform != null
                ? cameraTransform
                : transform;

        Vector3 desiredDirection =
            lookTransform.forward * input.y +
            lookTransform.right * input.x;

        if (swimUpHeld)
        {
            desiredDirection += Vector3.up;
        }

        if (swimDownInput > 0f)
        {
            desiredDirection +=
                Vector3.down *
                swimDownInput;
        }

        desiredDirection =
            Vector3.ClampMagnitude(
                desiredDirection,
                1f
            );

        Vector3 targetSwimVelocity =
            desiredDirection * swimSpeed;

        swimVelocity =
            Vector3.MoveTowards(
                swimVelocity,
                targetSwimVelocity,
                swimAcceleration * Time.deltaTime
            );

        float waterHeight =
            oceanWater.GetSurfaceHeight(
                transform.position
            );

        float bodyHeight =
            transform.position.y +
            controller.center.y;

        float targetBodyHeight =
            waterHeight -
            floatingBodyDepth;

        float surfaceAssist =
            Mathf.Clamp(
                (targetBodyHeight - bodyHeight) *
                buoyancyStrength,
                -1.2f,
                1.4f
            );

        bool intentionalVerticalMovement =
            swimUpHeld ||
            swimDownInput > 0.01f ||
            Mathf.Abs(
                lookTransform.forward.y *
                input.y
            ) > 0.18f;

        float assistMultiplier =
            desiredDirection.sqrMagnitude > 0.04f ||
            intentionalVerticalMovement
                ? 0.42f
                : 1f;

        Vector3 velocity =
            swimVelocity +
            Vector3.up *
            surfaceAssist *
            assistMultiplier;

        controller.Move(
            velocity * Time.deltaTime
        );
    }

    private void HandleSwimmingTransition()
    {
        if (IsSwimming == previousSwimming)
            return;

        if (IsSwimming)
        {
            swimVelocity =
                planarVelocity +
                Vector3.up *
                Mathf.Clamp(
                    verticalVelocity,
                    -2f,
                    1f
                );

            planarVelocity =
                Vector3.zero;

            verticalVelocity = 0f;
        }
        else
        {
            planarVelocity =
                Vector3.ProjectOnPlane(
                    swimVelocity,
                    Vector3.up
                );

            swimVelocity =
                Vector3.zero;

            verticalVelocity =
                Mathf.Min(
                    verticalVelocity,
                    -1f
                );
        }

        previousSwimming = IsSwimming;
    }

    private void UpdateSwimmingState()
    {
        if (oceanWater == null)
        {
            oceanWater =
                FindFirstObjectByType<OceanWater>();

            if (oceanWater == null)
            {
                IsSwimming = false;
                return;
            }
        }

        float waterHeight =
            oceanWater.GetSurfaceHeight(
                transform.position
            );

        float bodyHeight =
            transform.position.y +
            controller.center.y;

        if (IsSwimming)
        {
            IsSwimming =
                bodyHeight <
                waterHeight + 0.30f;
        }
        else
        {
            IsSwimming =
                bodyHeight <
                waterHeight -
                swimEnterDepth;
        }
    }

    private Vector2 ReadMovementInput()
    {
        Vector2 keyboardInput =
            Vector2.zero;

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
            moveJoystick != null
                ? moveJoystick.Value
                : Vector2.zero;

        return mobileInput.sqrMagnitude >
               keyboardInput.sqrMagnitude
            ? mobileInput
            : keyboardInput;
    }

    private bool ReadJumpPressed()
    {
        bool keyboardPressed =
            Keyboard.current != null &&
            Keyboard.current.spaceKey
                .wasPressedThisFrame;

        bool mobilePressed =
            jumpButton != null &&
            jumpButton.ConsumePressed();

        return keyboardPressed ||
               mobilePressed;
    }

    private bool ReadSwimUpHeld()
    {
        bool keyboardHeld =
            Keyboard.current != null &&
            Keyboard.current.spaceKey
                .isPressed;

        bool mobileHeld =
            jumpButton != null &&
            jumpButton.IsHeld;

        return keyboardHeld ||
               mobileHeld;
    }

    private float ReadKeyboardSwimDownInput()
    {
        if (Keyboard.current == null)
            return 0f;

        return Keyboard.current.leftCtrlKey
            .isPressed
            ? 1f
            : 0f;
    }

    private bool WantsToSprint(
        Vector2 movementInput)
    {
        bool keyboardSprint =
            Keyboard.current != null &&
            Keyboard.current.leftShiftKey
                .isPressed;

        bool mobileSprint =
            moveJoystick != null &&
            moveJoystick.Value.magnitude >=
                mobileSprintMagnitude &&
            moveJoystick.Value.y >=
                mobileSprintForward;

        return keyboardSprint ||
               mobileSprint;
    }

    private bool TryGetGroundNormal(
        out Vector3 normal)
    {
        Vector3 origin =
            transform.position +
            Vector3.up * 0.30f;

        if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                0.75f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            normal = hit.normal;
            return true;
        }

        normal = Vector3.up;
        return false;
    }

    private void UpdateCameraFov()
    {
        if (playerCamera == null)
            return;

        float targetFov =
            baseFov +
            (IsSprinting
                ? sprintFovBoost
                : 0f);

        playerCamera.fieldOfView =
            Mathf.Lerp(
                playerCamera.fieldOfView,
                targetFov,
                1f -
                Mathf.Exp(
                    -fovLerpSpeed *
                    Time.deltaTime
                )
            );
    }

    private void HandleLook()
    {
        Vector2 lookDelta =
            Vector2.zero;

        if (!Application.isMobilePlatform &&
            Mouse.current != null)
        {
            lookDelta +=
                Mouse.current.delta
                    .ReadValue() *
                mouseSensitivity;
        }

        if (touchLookArea != null)
        {
            Vector2 touchDelta =
                touchLookArea
                    .ConsumeLookDelta();

            float dpi =
                Screen.dpi > 0f
                    ? Screen.dpi
                    : 160f;

            float dpiScale =
                Mathf.Max(
                    1f,
                    dpi / 160f
                );

            lookDelta +=
                (touchDelta / dpiScale) *
                touchSensitivity;
        }

        transform.Rotate(
            Vector3.up * lookDelta.x
        );

        cameraPitch -=
            lookDelta.y;

        cameraPitch =
            Mathf.Clamp(
                cameraPitch,
                -maxLookAngle,
                maxLookAngle
            );

        if (cameraTransform != null)
        {
            cameraTransform.localRotation =
                Quaternion.Euler(
                    cameraPitch,
                    0f,
                    0f
                );
        }
    }
}
