using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runtime correction for both already-installed and newly-generated Tideglass
// Quay scenes. Tideglass is loaded additively, so this fixer must survive the
// initial island scene and run again after the Quay scene actually arrives.
public sealed class FreshCatchMarketVisibilityFix : MonoBehaviour
{
    private const float DisplayDrop = 0.55f;
    private static FreshCatchMarketVisibilityFix instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("FreshCatchMarketVisibilityFix");
        instance = host.AddComponent<FreshCatchMarketVisibilityFix>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(ApplyWhenReady());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyWhenReady());
    }

    private IEnumerator ApplyWhenReady()
    {
        // ShopMarketDisplay.Start() builds the trays and frozen fish. Additive
        // sceneLoaded can fire before Start, so retry for several frames rather
        // than applying once during the original island load and disappearing.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            yield return null;

            ShopMarketDisplay[] markets =
                FindObjectsByType<ShopMarketDisplay>(FindObjectsSortMode.None);

            if (markets.Length == 0)
                continue;

            for (int i = 0; i < markets.Length; i++)
                FixMarket(markets[i]);

            // One extra pass catches render bounds after animator/skinned-mesh
            // initialization without leaving any polling work behind.
            yield return null;
            for (int i = 0; i < markets.Length; i++)
                FixMarket(markets[i]);
            yield break;
        }
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

        // Keep the complete display aligned with the lowered counter.
        display.localPosition = Vector3.down * DisplayDrop;
        display.gameObject.SetActive(true);

        Renderer[] displayRenderers =
            display.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < displayRenderers.Length; i++)
            if (displayRenderers[i] != null)
                displayRenderers[i].enabled = true;

        float countertopTop = 0.86f;
        if (countertop != null)
        {
            Renderer counterRenderer = countertop.GetComponent<Renderer>();
            if (counterRenderer != null)
                countertopTop = counterRenderer.bounds.max.y;
            else
                countertopTop = countertop.position.y + 0.06f;
        }

        for (int i = 0; i < display.childCount; i++)
        {
            Transform child = display.GetChild(i);
            if (child == null || !child.name.StartsWith("Fresh "))
                continue;

            child.gameObject.SetActive(true);

            Renderer[] fishRenderers =
                child.GetComponentsInChildren<Renderer>(true);

            bool found = false;
            Bounds bounds = default;

            for (int r = 0; r < fishRenderers.Length; r++)
            {
                Renderer renderer = fishRenderers[r];
                if (renderer == null)
                    continue;

                renderer.enabled = true;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!found)
                continue;

            // Put the lowest point of every frozen fish visibly above the actual
            // countertop, independent of old hierarchy offsets or prefab scale.
            float desiredBottom = countertopTop + 0.20f;
            child.position += Vector3.up * (desiredBottom - bounds.min.y);
        }
    }
}
