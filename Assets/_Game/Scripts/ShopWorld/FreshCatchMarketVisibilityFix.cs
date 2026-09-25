using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runtime correction for both already-installed and newly-generated Tideglass
// Quay scenes. Tideglass is loaded additively, so this fixer survives the
// initial island scene and runs again after the Quay scene arrives.
public sealed class FreshCatchMarketVisibilityFix : MonoBehaviour
{
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
        // ShopMarketDisplay.Start() rebuilds the authoritative trays/ice/fish.
        // Wait until that work exists, then only make sure it is visible. This
        // fixer deliberately never changes fish height anymore.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            yield return null;

            ShopMarketDisplay[] markets =
                FindObjectsByType<ShopMarketDisplay>(FindObjectsSortMode.None);

            if (markets.Length == 0)
                continue;

            bool foundDisplay = false;
            for (int i = 0; i < markets.Length; i++)
                foundDisplay |= FixMarket(markets[i]);

            if (!foundDisplay)
                continue;

            yield return null;
            for (int i = 0; i < markets.Length; i++)
                FixMarket(markets[i]);
            yield break;
        }
    }

    private static bool FixMarket(ShopMarketDisplay market)
    {
        if (market == null)
            return false;

        Transform display = null;
        Transform[] all = market.GetComponentsInChildren<Transform>(true);

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
            return false;

        // ShopMarketDisplay now authors every tray/fish coordinate directly in
        // lowered-counter space. Reset any stale offset from the previous fixer.
        display.localPosition = Vector3.zero;
        display.localRotation = Quaternion.identity;
        display.localScale = Vector3.one;
        display.gameObject.SetActive(true);

        // Only touch renderer visibility. Do not force every child GameObject on:
        // imported fish may intentionally keep alternate meshes/LODs inactive.
        Renderer[] renderers = display.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].gameObject.activeInHierarchy)
                renderers[i].enabled = true;

        return true;
    }
}
