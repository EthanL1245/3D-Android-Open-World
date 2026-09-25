using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small watchdog for locally generated Tideglass scenes. The actual market
/// rendering lives in ShopMarketDisplay. This class only makes sure that exact
/// scene-linked component exists/enables/rebuilds after an additive scene load.
/// Keeping a single builder avoids the previous race where two different market
/// systems deleted and recreated each other's fish.
/// </summary>
public sealed class FreshCatchMarketRuntimeRepair : MonoBehaviour
{
    private static FreshCatchMarketRuntimeRepair instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("FreshCatchMarketRuntimeRepair");
        instance = host.AddComponent<FreshCatchMarketRuntimeRepair>();
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

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(RepairLoadedScenes());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(RepairLoadedScenes());
    }

    private IEnumerator RepairLoadedScenes()
    {
        // Additive sceneLoaded occurs before Start on scene behaviours. Wait until
        // the generated Tideglass hierarchy is fully live, then request one
        // authoritative rebuild from its serialized market component.
        yield return null;
        yield return null;
        yield return null;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Transform market = all[i];
                    if (market == null || market.name != "FRESH CATCH MARKET")
                        continue;

                    ShopMarketDisplay display = market.GetComponent<ShopMarketDisplay>();
                    if (display == null)
                        display = market.gameObject.AddComponent<ShopMarketDisplay>();

                    display.enabled = true;
                    display.RebuildNow();
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
