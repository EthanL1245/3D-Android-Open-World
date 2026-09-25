using UnityEngine;

/// <summary>
/// Keeps the active permanent-lure variant synchronized before ShopPreview resolves
/// the legacy Bait4 key. Rendering itself is now owned by ShopPreview so there is
/// only one preview request/cached RenderTexture per UI image.
/// </summary>
[DefaultExecutionOrder(-450)]
public sealed class LureGraphicOverride : MonoBehaviour
{
    private ShopProgress progress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        ShopProgress saved=FindFirstObjectByType<ShopProgress>();
        if(saved!=null)ShopCatalog.SetActiveLureVariant(saved.Data.lureEquipped);

        ShopWorldHUD target=FindFirstObjectByType<ShopWorldHUD>();
        if(target!=null && target.GetComponent<LureGraphicOverride>()==null)
            target.gameObject.AddComponent<LureGraphicOverride>();
    }

    private void Awake()
    {
        progress=FindFirstObjectByType<ShopProgress>();
        Sync();
    }

    private void Update(){Sync();}

    private void Sync()
    {
        if(progress==null)return;
        ShopCatalog.SetActiveLureVariant(progress.Data.lureEquipped);
    }
}
