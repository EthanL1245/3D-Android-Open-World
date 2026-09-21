using UnityEngine;

public static class FishWorldSize
{
    public static GameObject Create(string name,Transform parent,int species,float kg)
    {
        var fish=FishVisualFactory.CreateFish(name,parent,species,1f);
        SetLength(fish,ShopCatalog.FishLength(species,kg));return fish;
    }
    public static void SetLength(GameObject fish,float metres)
    {
        if(fish==null)return;
        Vector3 forward=fish.transform.forward;
        float min=float.PositiveInfinity,max=float.NegativeInfinity;
        foreach(var renderer in fish.GetComponentsInChildren<Renderer>())
        {
            Mesh mesh=null;bool temporary=false;
            var skin=renderer as SkinnedMeshRenderer;
            if(skin!=null){mesh=new Mesh();skin.BakeMesh(mesh);temporary=true;}
            else {var filter=renderer.GetComponent<MeshFilter>();if(filter!=null)mesh=filter.sharedMesh;}
            if(mesh==null)continue;
            if(mesh.isReadable)
                foreach(var v in mesh.vertices){float z=Vector3.Dot(renderer.transform.TransformPoint(v)-fish.transform.position,forward);min=Mathf.Min(min,z);max=Mathf.Max(max,z);}
            else
            {
                var b=renderer.localBounds;
                for(int i=0;i<8;i++){var v=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));float z=Vector3.Dot(renderer.transform.TransformPoint(v)-fish.transform.position,forward);min=Mathf.Min(min,z);max=Mathf.Max(max,z);}
            }
            if(temporary)Object.Destroy(mesh);
        }
        if(max-min>0.0001f && !float.IsInfinity(max-min))fish.transform.localScale*=metres/(max-min);
    }
}
