using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Existing saved Shop World scenes were authored with cumulative-height cubes
/// for aquarium stairs. When those cubes became translucent they looked like tall
/// blue panes reaching all the way to the floor. Convert every AccessStep into a
/// thin tread while preserving the exact top surface/walkable stair height.
/// </summary>
public sealed class AquariumStepGeometryFix : MonoBehaviour
{
    private static AquariumStepGeometryFix instance;
    private const float TreadThickness = 0.09f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AquariumStepGeometryFix");
        instance = host.AddComponent<AquariumStepGeometryFix>();
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
        StartCoroutine(ApplyAfterSceneStart());
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
        StartCoroutine(ApplyAfterSceneStart());
    }

    private IEnumerator ApplyAfterSceneStart()
    {
        // ShopHabitat adds AquariumAccessStyle during Start. Run after it as well
        // so this remains authoritative regardless of component start ordering.
        yield return null;
        yield return null;
        Apply();
    }

    private static void Apply()
    {
        ShopHabitat[] habitats = FindObjectsByType<ShopHabitat>(FindObjectsSortMode.None);
        for (int h = 0; h < habitats.Length; h++)
        {
            ShopHabitat habitat = habitats[h];
            if (habitat == null)
                continue;

            Transform[] nodes = habitat.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                Transform step = nodes[i];
                if (step == null || step.name != "AccessStep")
                    continue;

                // Preserve the top of the old cumulative block exactly, then
                // collapse only the underside into a normal thin stair tread.
                float top = step.localPosition.y + step.localScale.y * 0.5f;
                Vector3 scale = step.localScale;
                Vector3 position = step.localPosition;
                scale.y = TreadThickness;
                position.y = top - TreadThickness * 0.5f;
                step.localScale = scale;
                step.localPosition = position;
            }
        }
    }
}
