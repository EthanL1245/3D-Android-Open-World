using UnityEngine;

/// <summary>
/// Runs immediately after LiplessCrankbaitWorldPresentation and doubles only the
/// lure's authored retrieve animation while REEL is held. The presentation script
/// remains authoritative for when the lure is visible/paused and for line/head
/// movement; this component only changes Animator.speed from 1x to 2x during an
/// active retrieve.
/// </summary>
[DefaultExecutionOrder(950)]
public sealed class LiplessCrankbaitRetrieveAnimationSpeed : MonoBehaviour
{
    private FishingHUD hud;
    private GameObject lureRoot;
    private Animator lureAnimator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FishingSystem[] systems = FindObjectsByType<FishingSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            FishingSystem system = systems[i];
            if (system != null && system.GetComponent<LiplessCrankbaitRetrieveAnimationSpeed>() == null)
                system.gameObject.AddComponent<LiplessCrankbaitRetrieveAnimationSpeed>();
        }
    }

    private void LateUpdate()
    {
        if (hud == null)
            hud = FindFirstObjectByType<FishingHUD>();

        if (hud == null || hud.ActionInput == null || !hud.ActionInput.IsHeld)
            return;

        if (lureRoot == null)
        {
            lureRoot = GameObject.Find("ActiveLiplessCrankbait");
            if (lureRoot != null)
                lureAnimator = lureRoot.GetComponentInChildren<Animator>(true);
        }

        if (lureRoot == null || !lureRoot.activeInHierarchy || lureAnimator == null)
            return;

        // LiplessCrankbaitWorldPresentation (execution order 900) sets 1x while
        // retrieving and 0x while resting. We run afterward and only promote the
        // active retrieve to 2x, leaving the resting/fight transition untouched.
        if (lureAnimator.speed > 0f)
            lureAnimator.speed = 2f;
    }

    private void OnDisable()
    {
        lureRoot = null;
        lureAnimator = null;
    }
}
