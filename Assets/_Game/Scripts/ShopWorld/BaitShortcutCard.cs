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
        // Scale the whole card uniformly, including its preview, text and hit area.
        card.localScale=new Vector3(.85f,.85f,1f);
        card.anchoredPosition+=new Vector2(0,16f);
        // Keep authored font sizes; reserve two lines for multi-word names.
        nameLabel.resizeTextForBestFit=false;
        quantityLabel.resizeTextForBestFit=false;
        nameLabel.fontStyle=FontStyle.Bold;
        nameLabel.horizontalOverflow=HorizontalWrapMode.Wrap;
        quantityLabel.horizontalOverflow=HorizontalWrapMode.Overflow;
        nameLabel.alignment=quantityLabel.alignment=TextAnchor.MiddleCenter;
        PlaceText(nameLabel.rectTransform,78,138);
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
        float nameWidth=MeasureName(lastName);
        // Find the narrowest two-line layout at a word boundary. Leave the actual
        // label text intact so the equipped-lure name companion still recognizes it.
        for(int i=1;i<lastName.Length-1;i++)
        {
            if(lastName[i]!=' ')continue;
            float width=Mathf.Max(MeasureName(lastName.Substring(0,i)),
                MeasureName(lastName.Substring(i+1)));
            nameWidth=Mathf.Min(nameWidth,width);
        }
        float textWidth=Mathf.Ceil(Mathf.Max(nameWidth,quantityLabel.preferredWidth));
        card.sizeDelta=new Vector2(Mathf.Max(270,156+textWidth+12+18),150);
    }

    private float MeasureName(string value)
    {
        var settings=nameLabel.GetGenerationSettings(Vector2.zero);
        settings.horizontalOverflow=HorizontalWrapMode.Overflow;
        return nameLabel.cachedTextGeneratorForLayout.GetPreferredWidth(value,settings)/nameLabel.pixelsPerUnit;
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
