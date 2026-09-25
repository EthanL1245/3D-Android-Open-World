using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Kept for compatibility with projects that already imported this script/meta.
// It no longer edits market transforms or renderer hierarchy itself. All market
// construction is owned by ShopMarketDisplay so there is only one source of truth.
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

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(RefreshAfterLoad());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(RefreshAfterLoad());
    }

    private IEnumerator RefreshAfterLoad()
    {
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        ShopMarketDisplay[] markets =
            FindObjectsByType<ShopMarketDisplay>(FindObjectsSortMode.None);

        for (int i = 0; i < markets.Length; i++)
        {
            if (markets[i] != null && markets[i].isActiveAndEnabled)
                markets[i].RebuildNow();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
