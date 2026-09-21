using UnityEngine;

public sealed class ShopMarketDisplay : MonoBehaviour
{
    private void Start()
    {
        for(int i=0;i<5;i++)
        {
            var species=FishCatalog.Get(i%4);
            var fish=FishWorldSize.Create("HangingMarketFish",transform,i%4,(species.MinWeightKg+species.MaxWeightKg)*0.5f);
            foreach(var b in fish.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
            foreach(var c in fish.GetComponentsInChildren<Collider>())c.enabled=false;
            fish.transform.localRotation=Quaternion.Euler(90,0,i%2==0?7:-7);
            var renderers=fish.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
            Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            Vector3 hook=transform.TransformPoint(new Vector3((i-2)*2.1f,4.7f,0.6f));
            fish.transform.position+=hook-new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
            var rope=GameObject.CreatePrimitive(PrimitiveType.Cylinder);rope.name="HangingCord";rope.transform.SetParent(transform,false);
            rope.transform.localPosition=new Vector3((i-2)*2.1f,5.25f,0.6f);rope.transform.localScale=new Vector3(0.018f,0.55f,0.018f);
            Destroy(rope.GetComponent<Collider>());
            // Reuse the kiosk's dark canopy material; no extra per-fish material allocations.
            var canopy=transform.Find("Canopy");if(canopy!=null)rope.GetComponent<Renderer>().sharedMaterial=canopy.GetComponent<Renderer>().sharedMaterial;
        }
    }
}
