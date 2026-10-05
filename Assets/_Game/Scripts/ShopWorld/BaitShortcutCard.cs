using UnityEngine;
using UnityEngine.UI;

// Presentation only. Run after the lure companion updates the selected name.
[DefaultExecutionOrder(10000)]
public sealed class BaitShortcutCard : MonoBehaviour
{
    private Text nameLabel, quantityLabel;
    private RectTransform card;
    private string lastName, lastQuantity;

    public void Initialize(Text name,Text quantity,RawImage preview)
    {
        nameLabel=name;quantityLabel=quantity;card=(RectTransform)transform;
        // Keep the existing 24-unit name and 30-unit quantity; grow the card instead
        // of shrinking or wrapping either of the two text rows.
        nameLabel.resizeTextForBestFit=false;
        quantityLabel.resizeTextForBestFit=false;
        nameLabel.fontStyle=FontStyle.Bold;
        nameLabel.horizontalOverflow=HorizontalWrapMode.Overflow;
        quantityLabel.horizontalOverflow=HorizontalWrapMode.Overflow;
        nameLabel.alignment=quantityLabel.alignment=TextAnchor.MiddleCenter;
        PlaceText(nameLabel.rectTransform,80,132);
        PlaceText(quantityLabel.rectTransform,18,68);

        var frame=CreateImage("BaitPreviewFrame",transform);
        frame.raycastTarget=false;FishingHudTheme.Panel(frame.gameObject);
        var rect=frame.rectTransform;
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;
        rect.anchoredPosition=new Vector2(12,12);rect.sizeDelta=new Vector2(126,126);

        var clip=CreateImage("RoundedPreviewMask",frame.transform);
        clip.raycastTarget=false;FishingHudTheme.Fill(clip);
        Stretch(clip.rectTransform,6);
        clip.gameObject.AddComponent<Mask>().showMaskGraphic=false;
        preview.transform.SetParent(clip.transform,false);
        Stretch(preview.rectTransform,0);
        preview.raycastTarget=false;

        var divider=CreateImage("BaitTextDivider",transform);
        divider.raycastTarget=false;divider.color=FishingHudTheme.Cyan;
        divider.rectTransform.anchorMin=new Vector2(0,0);
        divider.rectTransform.anchorMax=new Vector2(1,0);
        divider.rectTransform.offsetMin=new Vector2(156,74);
        divider.rectTransform.offsetMax=new Vector2(-18,76);
        RefreshWidth();
    }

    private void LateUpdate()
    {
        if(nameLabel!=null && (lastName!=nameLabel.text || lastQuantity!=quantityLabel.text))
            RefreshWidth();
    }

    private void RefreshWidth()
    {
        lastName=nameLabel.text;lastQuantity=quantityLabel.text;
        float textWidth=Mathf.Ceil(Mathf.Max(nameLabel.preferredWidth,quantityLabel.preferredWidth));
        card.sizeDelta=new Vector2(Mathf.Max(270,156+textWidth+12+18),150);
    }

    private static void PlaceText(RectTransform rect,float bottom,float top)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(1,0);
        rect.offsetMin=new Vector2(156,bottom);rect.offsetMax=new Vector2(-18,top);
    }

    private static Image CreateImage(string name,Transform parent)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));
        go.transform.SetParent(parent,false);return go.GetComponent<Image>();
    }

    private static void Stretch(RectTransform rect,float inset)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(inset,inset);rect.offsetMax=new Vector2(-inset,-inset);
    }
}
