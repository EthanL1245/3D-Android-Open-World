using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The supplied wide wave artwork is a normal background image, not a nine-slice.
/// LargeShopBackdropTheme owns the image itself; this late presentation pass forces
/// that image back to a simple stretched UI sprite before Canvas rendering. This
/// avoids Unity's repeated sliced-sprite padding exception while preserving all of
/// the existing modal sizing, transparency, controls and gameplay behavior.
/// </summary>
[DefaultExecutionOrder(7200)]
public sealed class LargeShopBackdropSimpleImageFixRuntime : MonoBehaviour
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo PageField = typeof(ShopWorldHUD).GetField("page", PrivateInstance);
    private static readonly FieldInfo ModalField = typeof(ShopWorldHUD).GetField("modal", PrivateInstance);

    private ShopWorldHUD hud;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        foreach (ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if (target != null && target.GetComponent<LargeShopBackdropSimpleImageFixRuntime>() == null)
                target.gameObject.AddComponent<LargeShopBackdropSimpleImageFixRuntime>();
    }

    private void Awake()
    {
        hud = GetComponent<ShopWorldHUD>();
    }

    private void LateUpdate()
    {
        if (hud == null || PageField == null || ModalField == null) return;

        string page = PageField.GetValue(hud) as string;
        if (page != "gear" && page != "islands" && page != "reef-fish") return;

        GameObject modalObject = ModalField.GetValue(hud) as GameObject;
        if (modalObject == null || !modalObject.activeInHierarchy) return;

        Image image = modalObject.GetComponent<Image>();
        if (image == null || image.sprite == null) return;

        // The artwork is intentionally scaled to the existing large modal rectangle.
        // Do not preserve its very wide source aspect ratio; fill the phone panel.
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.fillCenter = true;
        image.color = new Color(1f, 1f, 1f, .84f);
    }
}
