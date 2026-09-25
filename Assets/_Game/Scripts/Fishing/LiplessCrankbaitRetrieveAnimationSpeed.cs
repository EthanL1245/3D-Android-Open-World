using UnityEngine;

/// <summary>
/// Runs immediately after LiplessCrankbaitWorldPresentation and doubles only the
/// equipped crankbait's authored retrieve animation while REEL is held.
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

        if (lureRoot == null || !lureRoot.activeInHierarchy)
            FindActiveCrankbait();

        if (lureRoot == null || !lureRoot.activeInHierarchy || lureAnimator == null)
            return;

        // WorldPresentation sets 1x while retrieving and 0x while resting.
        // We run afterward and promote only the active retrieve to 2x.
        if (lureAnimator.speed > 0f)
            lureAnimator.speed = 2f;
    }

    private void FindActiveCrankbait()
    {
        lureRoot = GameObject.Find("ActiveLiplessCrankbait"); // legacy name
        if (lureRoot == null)
        {
            for (int i = 0; i < ShopCatalog.LureNames.Length; i++)
            {
                string objectName = "Active_" + ShopCatalog.LureNames[i].Replace(" ", string.Empty);
                lureRoot = GameObject.Find(objectName);
                if (lureRoot != null) break;
            }
        }
        lureAnimator = lureRoot != null ? lureRoot.GetComponentInChildren<Animator>(true) : null;
    }

    private void OnDisable()
    {
        lureRoot = null;
        lureAnimator = null;
    }
}
