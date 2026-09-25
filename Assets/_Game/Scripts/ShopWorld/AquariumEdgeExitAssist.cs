using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// Swimmable aquariums have solid glass/rims on all four sides. The physical
// staircase is still useful, but a player who swims to any wall and holds the
// swim-up/jump control should be able to climb/vault out there instead of being
// trapped against the glass.
[DefaultExecutionOrder(9500)]
[DisallowMultipleComponent]
public sealed class AquariumEdgeExitAssist : MonoBehaviour
{
    private const float EdgeReach = 0.45f;
    private const float OutsideClearance = 0.28f;
    private const float RimClearance = 0.42f;
    private const float SurfaceReach = 1.25f;
    private const float ExitCooldown = 0.65f;

    private FirstPersonController player;
    private CharacterController controller;
    private MobileActionButton jumpButton;
    private float cooldownUntil;

    private static readonly FieldInfo JumpButtonField =
        typeof(FirstPersonController).GetField(
            "jumpButton",
            BindingFlags.Instance | BindingFlags.NonPublic);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FirstPersonController target = FindFirstObjectByType<FirstPersonController>();
        if (target != null && target.GetComponent<AquariumEdgeExitAssist>() == null)
            target.gameObject.AddComponent<AquariumEdgeExitAssist>();
    }

    private void Awake()
    {
        player = GetComponent<FirstPersonController>();
        controller = GetComponent<CharacterController>();
        ResolveJumpButton();
    }

    private void LateUpdate()
    {
        if (player == null || controller == null || !controller.enabled || !player.IsSwimming)
            return;
        if (Time.unscaledTime < cooldownUntil || !SwimUpHeld())
            return;

        ShopWaterVolume volume;
        Vector3 local;
        if (!ShopWaterVolume.TryGetVolume(transform.position, out volume, out local) || volume == null)
            return;

        float halfX = volume.size.x * 0.5f;
        float halfZ = volume.size.z * 0.5f;
        float distanceX = Mathf.Max(0f, halfX - Mathf.Abs(local.x));
        float distanceZ = Mathf.Max(0f, halfZ - Mathf.Abs(local.z));
        float edgeDistance = Mathf.Min(distanceX, distanceZ);

        // Normal swimming keeps working everywhere else. The vault is only
        // offered close to a wall and close enough to the surface to plausibly
        // climb over the rim.
        if (edgeDistance > controller.radius + EdgeReach)
            return;
        if (local.y < volume.size.y - SurfaceReach)
            return;

        Vector3 outwardLocal;
        Vector3 targetLocal = local;
        Vector3 localForward = volume.transform.InverseTransformDirection(transform.forward);

        if (distanceX <= distanceZ)
        {
            float sign = NonZeroSign(local.x, localForward.x);
            outwardLocal = new Vector3(sign, 0f, 0f);
            targetLocal.x = sign * (halfX + controller.radius + OutsideClearance);
            // Stay away from the neighboring corner/rim when vaulting out.
            targetLocal.z = Mathf.Clamp(targetLocal.z,
                -Mathf.Max(0f, halfZ - controller.radius),
                 Mathf.Max(0f, halfZ - controller.radius));
        }
        else
        {
            float sign = NonZeroSign(local.z, localForward.z);
            outwardLocal = new Vector3(0f, 0f, sign);
            targetLocal.z = sign * (halfZ + controller.radius + OutsideClearance);
            targetLocal.x = Mathf.Clamp(targetLocal.x,
                -Mathf.Max(0f, halfX - controller.radius),
                 Mathf.Max(0f, halfX - controller.radius));
        }

        // CharacterController position is its Transform origin, not its bottom.
        // Place the capsule bottom above the aquarium top rim before turning
        // collision back on, then normal gravity drops the player outside.
        float originToBottom = controller.center.y - controller.height * 0.5f;
        targetLocal.y = volume.size.y + RimClearance - originToBottom;

        Vector3 destination = volume.transform.TransformPoint(targetLocal);
        Vector3 outwardWorld = volume.transform.TransformDirection(outwardLocal).normalized;

        // A tiny extra world-space nudge protects against glass thickness and
        // old generated tank variants whose wall is slightly outside SwimVolume.
        destination += outwardWorld * 0.12f;

        controller.enabled = false;
        transform.position = destination;
        controller.enabled = true;

        player.ResetMotion();
        cooldownUntil = Time.unscaledTime + ExitCooldown;
    }

    private bool SwimUpHeld()
    {
        bool keyboard = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        if (keyboard)
            return true;

        if (jumpButton == null)
            ResolveJumpButton();
        return jumpButton != null && jumpButton.IsHeld;
    }

    private void ResolveJumpButton()
    {
        if (player != null && JumpButtonField != null)
            jumpButton = JumpButtonField.GetValue(player) as MobileActionButton;
    }

    private static float NonZeroSign(float position, float fallbackDirection)
    {
        if (position > 0.001f) return 1f;
        if (position < -0.001f) return -1f;
        if (fallbackDirection < 0f) return -1f;
        return 1f;
    }
}
