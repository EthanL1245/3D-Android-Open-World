using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the requested second 12 px downward nudge to each Island / Fish Index card.
/// The existing theme pass already moves the yellow island heading and its description
/// down 12 px; this companion applies one additional 12 px to those same two labels.
/// Images, card bounds and right-side buttons are not moved.
/// </summary>
[DefaultExecutionOrder(7250)]
public sealed class IslandIndexAdditionalSpacingRuntime : MonoBehaviour
{
    private const float ExtraDrop = 12f;
    private const string MarkerName = "__IslandIndexExtraTextSpacing_v1";

    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo PageField = typeof(ShopWorldHUD).GetField("page", PrivateInstance);
    private static readonly FieldInfo ListField = typeof(ShopWorldHUD).GetField("list", PrivateInstance);
    private static readonly FieldInfo ModalField = typeof(ShopWorldHUD).GetField("modal", PrivateInstance);

    private ShopWorldHUD hud;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        foreach (ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if (target != null && target.GetComponent<IslandIndexAdditionalSpacingRuntime>() == null)
                target.gameObject.AddComponent<IslandIndexAdditionalSpacingRuntime>();
    }

    private void Awake()
    {
        hud = GetComponent<ShopWorldHUD>();
        if (hud == null || PageField == null || ListField == null || ModalField == null)
            enabled = false;
    }

    private void LateUpdate()
    {
        if (!enabled) return;

        string page = PageField.GetValue(hud) as string;
        if (page != "islands") return;

        GameObject modal = ModalField.GetValue(hud) as GameObject;
        if (modal == null || !modal.activeInHierarchy) return;

        RectTransform rows = ListField.GetValue(hud) as RectTransform;
        if (rows == null) return;

        for (int i = 0; i < rows.childCount; i++)
        {
            Transform card = rows.GetChild(i);
            if (card == null || !card.gameObject.activeInHierarchy ||
                !card.name.StartsWith("BiomeCard_", System.StringComparison.Ordinal) ||
                card.Find(MarkerName) != null)
                continue;

            // IslandBiomeIndexPatchRuntime creates exactly two direct Text children
            // on these cards: the yellow island/unlock heading and its description.
            // Shift both by the same additional amount so their spacing is preserved.
            for (int childIndex = 0; childIndex < card.childCount; childIndex++)
            {
                Transform child = card.GetChild(childIndex);
                if (child == null || child.GetComponent<Button>() != null) continue;

                Text text = child.GetComponent<Text>();
                if (text == null) continue;

                RectTransform rect = text.rectTransform;
                rect.offsetMin += new Vector2(0f, -ExtraDrop);
                rect.offsetMax += new Vector2(0f, -ExtraDrop);
            }

            GameObject marker = new GameObject(MarkerName, typeof(RectTransform));
            marker.transform.SetParent(card, false);
            marker.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        }
    }
}
