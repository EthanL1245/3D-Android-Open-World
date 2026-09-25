using System.Collections;
using UnityEngine;

// Runtime correction for already-installed Tideglass Quay scenes. Some scene
// variants nest the counter pieces, so direct-child edits can miss the actual
// counter and leave the frozen display buried inside the old tall countertop.
public sealed class FreshCatchMarketVisibilityFix : MonoBehaviour
{
    private const float DisplayDrop = 0.55f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var host = new GameObject("FreshCatchMarketVisibilityFix");
        DontDestroyOnLoad(host);
        host.AddComponent<FreshCatchMarketVisibilityFix>();
    }

    private IEnumerator Start()
    {
        // Let ShopMarketDisplay.Start() rebuild its trays/fish first.
        yield return null;
        yield return null;
        Apply();
        Destroy(gameObject);
    }

    private static void Apply()
    {
        ShopMarketDisplay[] markets =
            FindObjectsByType<ShopMarketDisplay>(FindObjectsSortMode.None);

        for (int i = 0; i < markets.Length; i++)
            FixMarket(markets[i]);
    }

    private static void FixMarket(ShopMarketDisplay market)
    {
        if (market == null)
            return;

        Transform display = null;
        Transform countertop = null;

        Transform[] all =
            market.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Transform node = all[i];
            if (node == null)
                continue;

            if (node.name == "Counter")
            {
                Vector3 scale = node.localScale;
                scale.y = 0.75f;
                node.localScale = scale;

                Vector3 position = node.localPosition;
                position.y = 0.375f;
                node.localPosition = position;
            }
            else if (node.name == "Countertop")
            {
                Vector3 position = node.localPosition;
                position.y = 0.80f;
                node.localPosition = position;
                countertop = node;
            }
            else if (node.name == "CounterInlay")
            {
                Vector3 position = node.localPosition;
                position.y = 0.45f;
                node.localPosition = position;
            }
            else if (node.name == "FreshCatchOnIce")
            {
                display = node;
            }
        }

        if (display == null)
            return;

        // Keep the full frozen display lowered with the counter.
        display.localPosition = Vector3.down * DisplayDrop;
        display.gameObject.SetActive(true);

        Renderer[] displayRenderers =
            display.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < displayRenderers.Length; i++)
            displayRenderers[i].enabled = true;

        // Guarantee each frozen fish is physically above the real countertop,
        // even if an older scene hierarchy had different parent offsets.
        float countertopTop = 0.86f;
        if (countertop != null)
        {
            Renderer counterRenderer =
                countertop.GetComponent<Renderer>();

            if (counterRenderer != null)
                countertopTop = counterRenderer.bounds.max.y;
            else
                countertopTop = countertop.position.y + 0.06f;
        }

        for (int i = 0; i < display.childCount; i++)
        {
            Transform child = display.GetChild(i);
            if (child == null ||
                !child.name.StartsWith("Fresh "))
            {
                continue;
            }

            child.gameObject.SetActive(true);

            Renderer[] fishRenderers =
                child.GetComponentsInChildren<Renderer>(true);

            bool found = false;
            Bounds bounds = default;

            for (int r = 0; r < fishRenderers.Length; r++)
            {
                fishRenderers[r].enabled = true;
                if (!found)
                {
                    bounds = fishRenderers[r].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(fishRenderers[r].bounds);
                }
            }

            if (!found)
                continue;

            float desiredBottom =
                countertopTop + 0.20f;

            if (bounds.min.y < desiredBottom)
            {
                child.position +=
                    Vector3.up *
                    (desiredBottom - bounds.min.y);
            }
        }
    }
}
