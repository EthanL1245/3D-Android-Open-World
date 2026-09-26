using UnityEngine;
using UnityEngine.UI;

public sealed class IslandDiscovery : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        var player=other.GetComponentInParent<FirstPersonController>();
        var world=IslandExpansionWorld.Active;
        if(player==null || world==null || !world.Ready || ReefCatalog.Zones[1].Unlocked)return;
        if(ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.InDimension)return;
        var capsule=player.GetComponent<CharacterController>();var passenger=player.GetComponent<BoatPassenger>();
        if(capsule==null || !capsule.isGrounded || player.IsSwimming || (passenger!=null && passenger.Boat!=null))return;
        Vector3 p=player.transform.position;
        if(IslandGeometry.Ellipse(p,world.NewCenter,world.Config.IslandRadii)>=.99f)return;
        float floor=world.Terrain.SampleHeight(p)+world.Terrain.transform.position.y;
        if(floor<world.Water.BaseWaterLevel+.2f || Mathf.Abs(p.y-floor)>.6f)return;
        var progress=player.GetComponent<ShopProgress>();if(progress==null || progress.ReadOnly)return;
        progress.Data.brinebreakDiscovered=true;progress.Save();ReefCatalog.BrinebreakDiscovered=true;
        var go=new GameObject("Brinebreak discovered",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=150;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        var label=new GameObject("Discovery",typeof(RectTransform),typeof(Image));label.transform.SetParent(go.transform,false);label.GetComponent<Image>().color=new Color(.035f,.14f,.2f,.95f);label.GetComponent<Image>().raycastTarget=false;
        var rect=label.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.8f);rect.sizeDelta=new Vector2(800,130);
        var textObject=new GameObject("Text",typeof(RectTransform),typeof(Text));textObject.transform.SetParent(label.transform,false);
        var text=textObject.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=30;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(1,.89f,.59f);text.raycastTarget=false;text.text="BRINEBREAK ISLE DISCOVERED\nFast travel unlocked in MENU / TRAVEL";text.rectTransform.sizeDelta=rect.sizeDelta;
        Destroy(go,6f);
    }
}
