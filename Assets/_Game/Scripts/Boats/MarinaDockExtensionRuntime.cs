using UnityEngine;

/// <summary>
/// Extends the existing Suncrest Marina boardwalk into water deep enough to deploy
/// the boat catalog. The original setup boardwalk ends around local Z=13, which can
/// leave the player standing too far inland for the 18 m placement ray to reach a
/// valid full-hull clearance area. This adds a narrower walkable pier at runtime in
/// both Editor Play Mode and builds; no boat installer rerun is required.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class MarinaDockExtensionRuntime : MonoBehaviour
{
    private const string ExtensionName="Deep Water Dock Extension";
    private const float ExistingDockEnd=13f;
    private const float MinimumDockEnd=21f;
    private const float MaximumDockEnd=42f;
    private const float PlacementGap=8f;

    private BoatSystem boats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(BoatSystem system in FindObjectsByType<BoatSystem>(FindObjectsSortMode.None))
        {
            if(system!=null && system.GetComponent<MarinaDockExtensionRuntime>()==null)
                system.gameObject.AddComponent<MarinaDockExtensionRuntime>();
        }
    }

    private void Start()
    {
        boats=GetComponent<BoatSystem>();
        BuildExtension();
    }

    private void BuildExtension()
    {
        if(boats==null)return;
        GameObject marina=GameObject.Find("MarinaShop");
        if(marina==null)return;
        Transform root=marina.transform;
        if(root.Find(ExtensionName)!=null)return;

        Transform boardwalk=FindDeepChild(root,"Boardwalk");
        Renderer boardRenderer=boardwalk!=null?boardwalk.GetComponent<Renderer>():null;
        Material wood=boardRenderer!=null?boardRenderer.sharedMaterial:null;
        OceanWater water=boats.Water!=null?boats.Water:FindFirstObjectByType<OceanWater>();
        if(water==null)return;

        float end=FindSafeDockEnd(root,water);
        float length=Mathf.Max(2f,end-ExistingDockEnd);

        GameObject extension=new GameObject(ExtensionName);
        extension.transform.SetParent(root,false);

        // Slight overlap with the old boardwalk removes any seam while keeping the
        // deep-water part narrow enough that boats have clear water beside/ahead.
        CreatePart(extension.transform,"Pier Deck",
            new Vector3(0f,0f,ExistingDockEnd+length*.5f),
            new Vector3(4.5f,.30f,length+.35f),wood,true);

        CreatePart(extension.transform,"Pier End Cap",
            new Vector3(0f,0f,end),
            new Vector3(5.5f,.30f,1.8f),wood,true);

        for(float z=ExistingDockEnd+2f;z<=end+.01f;z+=4f)
        {
            CreatePart(extension.transform,"Dock Piling L",
                new Vector3(-2.05f,-1.65f,z),new Vector3(.26f,3.6f,.26f),wood,true);
            CreatePart(extension.transform,"Dock Piling R",
                new Vector3(2.05f,-1.65f,z),new Vector3(.26f,3.6f,.26f),wood,true);
        }
    }

    private float FindSafeDockEnd(Transform marina,OceanWater water)
    {
        float fallback=MinimumDockEnd;
        for(float end=MinimumDockEnd;end<=MaximumDockEnd;end+=1f)
        {
            Vector3 target=marina.TransformPoint(new Vector3(0f,0f,end+PlacementGap));
            target.y=water.GetSurfaceHeight(target);
            Quaternion heading=Quaternion.Euler(0f,marina.eulerAngles.y,0f);

            bool depthSafe=HasCatalogDepth(target,heading,water);
            if(depthSafe)fallback=end;
            if(!depthSafe)continue;

            bool allClear=true;
            if(boats.Catalog!=null)
            {
                foreach(BoatData data in boats.Catalog)
                {
                    if(data==null)continue;
                    if(!BoatClearance.Valid(data,target,heading,water,null,transform))
                    { allClear=false;break; }
                }
            }
            if(allClear)return end;
        }
        return Mathf.Clamp(fallback,MinimumDockEnd,MaximumDockEnd);
    }

    private bool HasCatalogDepth(Vector3 center,Quaternion rotation,OceanWater water)
    {
        float halfX=2.5f,halfZ=3.5f,requiredDepth=1.25f;
        if(boats.Catalog!=null)
        {
            foreach(BoatData data in boats.Catalog)
            {
                if(data==null)continue;
                halfX=Mathf.Max(halfX,data.HullSize.x*.5f+1f);
                halfZ=Mathf.Max(halfZ,data.HullSize.z*.5f+1f);
                requiredDepth=Mathf.Max(requiredDepth,data.Draft+.85f);
            }
        }

        for(int ix=-2;ix<=2;ix++)
        for(int iz=-3;iz<=3;iz++)
        {
            Vector3 sample=center+rotation*new Vector3(halfX*ix/2f,0f,halfZ*iz/3f);
            bool found=false;
            foreach(Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 local=sample-terrain.transform.position;
                Vector3 size=terrain.terrainData.size;
                if(local.x<0f || local.z<0f || local.x>size.x || local.z>size.z)continue;
                found=true;
                float ground=terrain.SampleHeight(sample)+terrain.transform.position.y;
                if(water.BaseWaterLevel-ground<requiredDepth)return false;
                break;
            }
            if(!found)return false;
        }
        return true;
    }

    private static GameObject CreatePart(Transform parent,string name,Vector3 localPosition,Vector3 localScale,Material material,bool collider)
    {
        GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name=name;
        go.transform.SetParent(parent,false);
        go.transform.localPosition=localPosition;
        go.transform.localScale=localScale;
        Renderer renderer=go.GetComponent<Renderer>();
        if(renderer!=null && material!=null)renderer.sharedMaterial=material;
        if(!collider)
        {
            Collider c=go.GetComponent<Collider>();
            if(c!=null)Destroy(c);
        }
        return go;
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
            if(child!=null && child.name==name)return child;
        return null;
    }
}
