using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Replaces the visible bobber with the authored lipless crankbait whenever a
/// permanent lure is being cast/retrieved. The authored model, hook placement
/// and animation remain intact; only the entire lure assembly is aimed so the
/// line pulls from the head and the body trails behind during retrieve.
/// </summary>
[DefaultExecutionOrder(900)]
public sealed class LiplessCrankbaitWorldPresentation : MonoBehaviour
{
    private const string PrefabResource = "Fishing/LiplessCrankbaitGreenStriped";
    private const float RetrieveDepth = 0.20f;
    private const float FloatDepth = 0.015f;

    private FishingSystem fishing;
    private ShopProgress shopProgress;
    private FishingHUD hud;
    private OceanWater oceanWater;

    private FieldInfo stateField;
    private FieldInfo activeBaitField;
    private FieldInfo bobberField;
    private FieldInfo fishingLineField;
    private FieldInfo castPointField;
    private FieldInfo oceanWaterField;

    private GameObject lureRoot;
    private Transform lineAttach;
    private Animator lureAnimator;
    private Renderer[] bobberRenderers;

    private bool showingLure;
    private bool wasReeling;
    private float sinkDepth;
    private Quaternion authoredRootRotation = Quaternion.identity;
    private Vector3 localHeadDirection = Vector3.forward;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FishingSystem[] systems = FindObjectsByType<FishingSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            FishingSystem system = systems[i];
            if (system != null && system.GetComponent<LiplessCrankbaitWorldPresentation>() == null)
                system.gameObject.AddComponent<LiplessCrankbaitWorldPresentation>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        shopProgress = GetComponent<ShopProgress>();
        CacheMembers();
    }

    private void LateUpdate()
    {
        if (fishing == null || stateField == null || bobberField == null)
            return;

        string state = GetStateName();
        GameObject bobber = bobberField.GetValue(fishing) as GameObject;
        if (bobber == null)
            return;

        if (!ShouldShowLure(state, bobber))
        {
            StopShowingLure();
            return;
        }

        if (!EnsureLure())
            return;

        if (!showingLure)
        {
            showingLure = true;
            lureRoot.transform.rotation = authoredRootRotation;
            wasReeling = false;
        }

        SetBobberRenderers(false);
        lureRoot.SetActive(true);

        bool waiting = string.Equals(state, "Waiting", StringComparison.Ordinal);
        bool reeling = waiting && IsReeling();

        Vector3 logicalPosition = waiting && castPointField != null
            ? (Vector3)castPointField.GetValue(fishing)
            : bobber.transform.position;

        float targetDepth = reeling ? RetrieveDepth : FloatDepth;
        float depthSpeed = reeling ? 0.70f : 0.38f;
        sinkDepth = Mathf.MoveTowards(sinkDepth, targetDepth, depthSpeed * Time.deltaTime);

        Vector3 position = logicalPosition;
        if (waiting && oceanWater != null)
        {
            float surface = oceanWater.GetSurfaceHeight(logicalPosition);
            position.y = surface - sinkDepth;
            if (!reeling)
                position.y += Mathf.Sin(Time.time * 2.6f) * 0.012f;
        }

        if (reeling)
        {
            Vector3 towardPlayer = fishing.transform.position - position;
            towardPlayer.y = 0f;

            if (towardPlayer.sqrMagnitude > 0.0001f)
            {
                Vector3 authoredHeadWorld = authoredRootRotation * localHeadDirection;
                Quaternion targetRotation =
                    Quaternion.FromToRotation(authoredHeadWorld, towardPlayer.normalized) *
                    authoredRootRotation;

                // Snap immediately when REEL first goes down so the head points
                // at the player from the very first retrieve frame. After that,
                // track smoothly as the player walks around while reeling.
                if (!wasReeling)
                {
                    lureRoot.transform.rotation = targetRotation;
                }
                else
                {
                    float rotateT = 1f - Mathf.Exp(-18f * Time.deltaTime);
                    lureRoot.transform.rotation = Quaternion.Slerp(
                        lureRoot.transform.rotation,
                        targetRotation,
                        rotateT);
                }
            }
        }

        wasReeling = reeling;

        // Put the LINE-TIE/HEAD itself at FishingSystem's logical lure point.
        // Because LineAttach is not necessarily at the prefab root, first place
        // the root, then offset the whole rigid lure so its head lands exactly on
        // the line endpoint. The belly/tail/hooks therefore trail behind the head.
        lureRoot.transform.position = position;
        if (lineAttach != null)
        {
            Vector3 headOffset = lineAttach.position - lureRoot.transform.position;
            lureRoot.transform.position -= headOffset;
        }

        bobber.transform.position = position;

        // Use the supplied lure/hook animation exactly. No child bones or hook
        // transforms are procedurally edited here.
        if (lureAnimator != null)
            lureAnimator.speed = reeling ? 1f : 0f;

        LineRenderer line = fishingLineField != null
            ? fishingLineField.GetValue(fishing) as LineRenderer
            : null;
        if (line != null && line.enabled && line.positionCount >= 2)
            line.SetPosition(1, lineAttach != null ? lineAttach.position : position);
    }

    private bool ShouldShowLure(string state, GameObject bobber)
    {
        if (bobber == null || !bobber.activeSelf)
            return false;

        if (string.Equals(state, "Waiting", StringComparison.Ordinal))
        {
            if (activeBaitField == null) return false;
            return (int)activeBaitField.GetValue(fishing) == ShopCatalog.StarterLure;
        }

        if (string.Equals(state, "Casting", StringComparison.Ordinal))
            return shopProgress != null && shopProgress.Data.baitEquipped == ShopCatalog.StarterLure;

        return false;
    }

    private bool IsReeling()
    {
        if (hud == null)
            hud = FindFirstObjectByType<FishingHUD>();
        return hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
    }

    private bool EnsureLure()
    {
        if (lureRoot != null)
            return true;

        GameObject prefab = Resources.Load<GameObject>(PrefabResource);
        if (prefab == null)
            return false;

        lureRoot = Instantiate(prefab);
        lureRoot.name = "ActiveLiplessCrankbait";
        authoredRootRotation = lureRoot.transform.rotation;

        lineAttach = FindDeepChild(lureRoot.transform, "LineAttach");
        if (lineAttach == null)
        {
            GameObject attach = new GameObject("LineAttach");
            lineAttach = attach.transform;
            lineAttach.SetParent(lureRoot.transform, false);
        }

        localHeadDirection = MeasureLocalHeadDirection();

        lureAnimator = lureRoot.GetComponentInChildren<Animator>(true);
        if (lureAnimator != null)
        {
            lureAnimator.applyRootMotion = false;
            lureAnimator.speed = 0f;
            lureAnimator.Update(0f);
        }

        lureRoot.SetActive(false);
        return true;
    }

    private Vector3 MeasureLocalHeadDirection()
    {
        if (lureRoot == null || lineAttach == null)
            return Vector3.forward;

        Renderer[] renderers = lureRoot.GetComponentsInChildren<Renderer>(true);
        Renderer body = null;
        float largestVolume = -1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            Vector3 size = renderer.bounds.size;
            float volume = Mathf.Abs(size.x * size.y * size.z);
            if (volume > largestVolume)
            {
                largestVolume = volume;
                body = renderer;
            }
        }

        if (body == null)
            return Vector3.forward;

        Vector3 bodyToHeadWorld = lineAttach.position - body.bounds.center;
        if (bodyToHeadWorld.sqrMagnitude < 0.000001f)
            return Vector3.forward;

        Vector3 local = lureRoot.transform.InverseTransformDirection(bodyToHeadWorld.normalized);
        return local.sqrMagnitude > 0.0001f ? local.normalized : Vector3.forward;
    }

    private void StopShowingLure()
    {
        if (!showingLure)
            return;

        showingLure = false;
        wasReeling = false;
        sinkDepth = 0f;
        if (lureRoot != null)
        {
            lureRoot.transform.rotation = authoredRootRotation;
            lureRoot.SetActive(false);
        }
        if (lureAnimator != null) lureAnimator.speed = 0f;
        SetBobberRenderers(true);
    }

    private void SetBobberRenderers(bool visible)
    {
        GameObject bobber = bobberField != null ? bobberField.GetValue(fishing) as GameObject : null;
        if (bobber == null) return;
        if (bobberRenderers == null || bobberRenderers.Length == 0)
            bobberRenderers = bobber.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < bobberRenderers.Length; i++)
            if (bobberRenderers[i] != null) bobberRenderers[i].enabled = visible;
    }

    private string GetStateName()
    {
        object value = stateField.GetValue(fishing);
        return value != null ? value.ToString() : string.Empty;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private void CacheMembers()
    {
        Type type = typeof(FishingSystem);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        stateField = type.GetField("state", flags);
        activeBaitField = type.GetField("activeBait", flags);
        bobberField = type.GetField("bobber", flags);
        fishingLineField = type.GetField("fishingLine", flags);
        castPointField = type.GetField("castPoint", flags);
        oceanWaterField = type.GetField("oceanWater", flags);
        if (oceanWaterField != null && fishing != null)
            oceanWater = oceanWaterField.GetValue(fishing) as OceanWater;

        if (stateField == null || activeBaitField == null || bobberField == null ||
            fishingLineField == null || castPointField == null)
        {
            Debug.LogError("LiplessCrankbaitWorldPresentation could not bind to FishingSystem.");
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (lureRoot != null) lureRoot.SetActive(false);
        if (fishing != null && bobberField != null) SetBobberRenderers(true);
    }

    private void OnDestroy()
    {
        if (lureRoot != null) Destroy(lureRoot);
    }
}
