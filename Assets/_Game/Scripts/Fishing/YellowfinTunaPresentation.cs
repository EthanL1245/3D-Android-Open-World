using UnityEngine;

public class YellowfinTunaPresentation : MonoBehaviour
{
    [SerializeField]
    private Renderer[] renderers;

    [SerializeField]
    private Transform visualRoot;

    [Header("Aquarium Swim")]
    [SerializeField]
    private float swimStrength = 0.18f;

    [SerializeField]
    private float swimSpeed = 6.8f;

    [SerializeField]
    private float bodyYawDegrees = 4.8f;

    [SerializeField]
    private float bodyRollDegrees = 0.9f;

    [SerializeField]
    private float turnStrength = 0.34f;

    [Header("Held Fish")]
    [SerializeField]
    private float heldStrength = 0.095f;

    [SerializeField]
    private float heldSpeed = 5.0f;

    [SerializeField]
    private float heldYawDegrees = 4.0f;

    [SerializeField]
    private float heldRollDegrees = 0.9f;

    private bool held;
    private float phase;

    private MaterialPropertyBlock block;
    private Quaternion visualBaseRotation;

    private bool hasPreviousForward;
    private Vector3 previousForward;
    private float smoothedTurn;
    private float externalTurnTarget;
    private bool hasExternalTurnTarget;

    private static readonly int SwimStrengthId =
        Shader.PropertyToID(
            "_SwimStrength"
        );

    private static readonly int SwimSpeedId =
        Shader.PropertyToID(
            "_SwimSpeed"
        );

    private static readonly int SwimPhaseId =
        Shader.PropertyToID(
            "_SwimPhase"
        );

    private static readonly int TurnBendId =
        Shader.PropertyToID(
            "_TurnBend"
        );

    private static readonly int TurnStrengthId =
        Shader.PropertyToID(
            "_TurnStrength"
        );

    private void Awake()
    {
        ResolveReferences();

        phase =
            Random.Range(
                0f,
                Mathf.PI * 2f
            );

        block =
            new MaterialPropertyBlock();

        previousForward =
            GetHorizontalForward();

        hasPreviousForward =
            previousForward.sqrMagnitude >
            0.0001f;

        ApplyMaterialSettings();
    }

    public void Configure(
        Renderer[] targetRenderers)
    {
        renderers =
            targetRenderers;

        ResolveReferences();

        if (block == null)
        {
            block =
                new MaterialPropertyBlock();
        }

        ApplyMaterialSettings();
    }

    public void SetHeld(bool value)
    {
        held = value;

        if (held)
        {
            smoothedTurn = 0f;
            externalTurnTarget = 0f;
            hasExternalTurnTarget = false;
        }

        ApplyMaterialSettings();
    }

    public void SetAquariumTurn(float normalizedTurn)
    {
        if (held)
            return;

        externalTurnTarget =
            Mathf.Clamp(
                normalizedTurn,
                -1f,
                1f
            );

        hasExternalTurnTarget = true;
    }

    private void OnEnable()
    {
        ResolveReferences();

        previousForward =
            GetHorizontalForward();

        hasPreviousForward =
            previousForward.sqrMagnitude >
            0.0001f;

        smoothedTurn = 0f;

        ApplyMaterialSettings();
    }

    private void LateUpdate()
    {
        if (visualRoot == null)
            return;

        float speed =
            held
                ? heldSpeed
                : swimSpeed;

        float beat =
            Mathf.Sin(
                Time.time *
                speed +
                phase
            );

        UpdateTurnBend();

        float yaw =
            beat *
            (
                held
                    ? heldYawDegrees
                    : bodyYawDegrees
            );

        float roll =
            Mathf.Sin(
                Time.time *
                speed *
                0.5f +
                phase +
                0.8f
            ) *
            (
                held
                    ? heldRollDegrees
                    : bodyRollDegrees
            );

        if (!held)
        {
            roll +=
                -smoothedTurn *
                1.4f;
        }

        visualRoot.localRotation =
            visualBaseRotation *
            Quaternion.Euler(
                0f,
                yaw,
                roll
            );

        ApplyMaterialSettings();
    }

    private void UpdateTurnBend()
    {
        if (held)
        {
            smoothedTurn =
                Mathf.Lerp(
                    smoothedTurn,
                    0f,
                    1f -
                    Mathf.Exp(
                        -8f *
                        Time.deltaTime
                    )
                );

            return;
        }

        Vector3 currentForward =
            GetHorizontalForward();

        if (currentForward.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        if (!hasPreviousForward)
        {
            previousForward =
                currentForward;

            hasPreviousForward =
                true;

            return;
        }

        float signedAngle =
            Vector3.SignedAngle(
                previousForward,
                currentForward,
                Vector3.up
            );

        float degreesPerSecond =
            Time.deltaTime > 0.0001f
                ? signedAngle /
                  Time.deltaTime
                : 0f;

        float measuredTurn =
            Mathf.Clamp(
                degreesPerSecond /
                70f,
                -1f,
                1f
            );

        float targetTurn =
            hasExternalTurnTarget
                ? externalTurnTarget
                : measuredTurn;

        smoothedTurn =
            Mathf.Lerp(
                smoothedTurn,
                targetTurn,
                1f -
                Mathf.Exp(
                    -9.0f *
                    Time.deltaTime
                )
            );

        externalTurnTarget =
            Mathf.Lerp(
                externalTurnTarget,
                0f,
                1f -
                Mathf.Exp(
                    -4.0f *
                    Time.deltaTime
                )
            );

        if (Mathf.Abs(
                externalTurnTarget) <
            0.01f)
        {
            hasExternalTurnTarget =
                false;
        }

        previousForward =
            currentForward;
    }

    private Vector3 GetHorizontalForward()
    {
        Vector3 forward =
            transform.forward;

        forward =
            Vector3.ProjectOnPlane(
                forward,
                Vector3.up
            );

        if (forward.sqrMagnitude >
            0.0001f)
        {
            forward.Normalize();
        }

        return forward;
    }

    private void ResolveReferences()
    {
        if (renderers == null ||
            renderers.Length == 0)
        {
            renderers =
                GetComponentsInChildren<Renderer>(
                    true
                );
        }

        if (visualRoot == null)
        {
            visualRoot =
                transform.Find(
                    "Visual"
                );
        }

        if (visualRoot != null)
        {
            visualBaseRotation =
                visualRoot.localRotation;
        }
    }

    private void ApplyMaterialSettings()
    {
        if (renderers == null ||
            renderers.Length == 0)
        {
            return;
        }

        if (block == null)
        {
            block =
                new MaterialPropertyBlock();
        }

        float strength =
            held
                ? heldStrength
                : swimStrength;

        float speed =
            held
                ? heldSpeed
                : swimSpeed;

        float turn =
            held
                ? 0f
                : smoothedTurn;

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(
                block
            );

            block.SetFloat(
                SwimStrengthId,
                strength
            );

            block.SetFloat(
                SwimSpeedId,
                speed
            );

            block.SetFloat(
                SwimPhaseId,
                phase
            );

            block.SetFloat(
                TurnBendId,
                turn
            );

            block.SetFloat(
                TurnStrengthId,
                turnStrength
            );

            renderer.SetPropertyBlock(
                block
            );
        }
    }
}
