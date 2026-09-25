using UnityEngine;
using UnityEngine.UI;

// ShopWorldHUD already asks Refresh(false) to preserve position, but extensions
// can add rows after that refresh has measured the content. Remember the actual
// content offset when the save changes and restore it for the next few layout
// passes so buying/equipping an item never throws the player back to the top.
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class ShopMenuScrollKeeper : MonoBehaviour
{
    private ShopProgress progress;
    private ScrollRect scroll;
    private Vector2 savedPosition;
    private int restoreFrames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        ShopWorldHUD hud = FindFirstObjectByType<ShopWorldHUD>();
        if (hud != null && hud.GetComponent<ShopMenuScrollKeeper>() == null)
            hud.gameObject.AddComponent<ShopMenuScrollKeeper>();
    }

    private void Start()
    {
        progress = FindFirstObjectByType<ShopProgress>();
        if (progress != null)
            progress.Changed += RememberBeforeRefresh;
        ResolveScroll();
    }

    private void OnDestroy()
    {
        if (progress != null)
            progress.Changed -= RememberBeforeRefresh;
    }

    private void RememberBeforeRefresh()
    {
        if (!ShopWorldHUD.MenuOpen)
            return;

        ResolveScroll();
        if (scroll == null || scroll.content == null)
            return;

        savedPosition = scroll.content.anchoredPosition;
        restoreFrames = 4;
    }

    private void LateUpdate()
    {
        if (restoreFrames <= 0 || !ShopWorldHUD.MenuOpen)
            return;

        ResolveScroll();
        if (scroll == null || scroll.content == null || scroll.viewport == null)
        {
            restoreFrames = 0;
            return;
        }

        // HUD rebuilding and the lure extension both use deferred Destroy/layout,
        // so keep the same pixel offset through several end-of-frame passes.
        Canvas.ForceUpdateCanvases();
        float maxY = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
        Vector2 target = savedPosition;
        target.y = Mathf.Clamp(target.y, 0f, maxY);
        target.x = scroll.content.anchoredPosition.x;
        scroll.StopMovement();
        scroll.content.anchoredPosition = target;
        restoreFrames--;
    }

    private void ResolveScroll()
    {
        if (scroll != null && scroll.content != null)
            return;

        ScrollRect[] candidates = GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            ScrollRect candidate = candidates[i];
            if (candidate != null && candidate.content != null && candidate.content.name == "Rows")
            {
                scroll = candidate;
                return;
            }
        }
    }
}
