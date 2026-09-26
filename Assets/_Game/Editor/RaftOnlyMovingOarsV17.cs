using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>
/// Final material cleanup after v16. The screenshots show one thin side/underside
/// support layer plus a slightly protruding stationary plank still in the oar slot.
/// Fit the two real moving oars as robust 3D lines, protect their shaft corridors,
/// and return only stationary geometry inside the established raft footprint.
/// </summary>
public static class RaftOnlyMovingOarsV17
{
    const string PrefabPath="Assets/Resources/Boats/BaseBoat.prefab";
    const string WrapperPath="ModelContainer/Uploaded Raft Model";
    const string Folder="Assets/_Game/Boats/Raft/UserTextures";
    const string MeshPath=Folder+"/RaftOnlyMovingOars_v17.asset";
    const string Marker="RaftOnlyMovingOars_v17";
    const string V16="RaftOarRestCleanup_v16";

    sealed class Tri
    {
        public int ord; public Vector3 a,b,c,center; public Bounds bounds; public float maxEdge;
    }
    struct Line
    {
        public bool valid; public Vector3 point,dir; public float span; public int inliers;
    }

    [InitializeOnLoadMethod]
    static void Queue()=>EditorApplication.delayCall+=()=>EditorApplication.delayCall+=()=>EditorApplication.delayCall+=()=>Apply(false);

    [MenuItem("Tools/Open World/Final Raft Texture Cleanup - ONLY Moving Oars (v17)")]
    static void Force()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorUtility.DisplayDialog("Raft v17","Exit Play Mode first.","OK");return;}
        if(!Apply(true))EditorUtility.DisplayDialog("Raft v17","No change was saved. Check Console for [RAFT V17].","OK");
    }

    static bool Apply(bool force)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return false;
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform read=prefab!=null?prefab.transform.Find(WrapperPath):null;
        if(read==null)return false;
        if(!force&&read.Find(Marker)!=null)return false;
        if(read.Find(V16)==null){Debug.Log("[RAFT V17] Waiting for successful v16 cleanup first.");return false;}

        GameObject root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper=root.transform.Find(WrapperPath); if(wrapper==null)return false;
            Renderer renderer=FindRenderer(wrapper); Mesh source=GetMesh(renderer);
            if(renderer==null||source==null||source.subMeshCount<2){Debug.LogError("[RAFT V17] Current raft/oar renderer not found.");return false;}
            int oarSlot=source.subMeshCount-1;
            if(source.GetTopology(0)!=MeshTopology.Triangles||source.GetTopology(oarSlot)!=MeshTopology.Triangles){Debug.LogError("[RAFT V17] Expected triangle topology.");return false;}
            Material[] mats=renderer.sharedMaterials??Array.Empty<Material>();
            if(mats.Length<source.subMeshCount||mats[0]==null||mats[oarSlot]==null||IsOar(mats[0])||!IsOar(mats[oarSlot])){Debug.LogError("[RAFT V17] Expected raft material in slot 0 and oar material in final slot.");return false;}

            Vector3[] verts;
            try{verts=source.vertices;}catch(Exception e){Debug.LogError("[RAFT V17] Mesh is not readable: "+e.Message);return false;}
            int[] raft=source.GetIndices(0),oar=source.GetIndices(oarSlot); int total=oar.Length/3;
            if(total<16){Debug.LogError("[RAFT V17] Oar slot unexpectedly small: "+total);return false;}
            Bounds raftBounds; if(!Measure(raft,verts,renderer.transform,root.transform,out raftBounds))return false;
            List<Tri> tris=Build(oar,verts,renderer.transform,root.transform);
            Line left=Fit(tris.Where(t=>t.center.x<raftBounds.center.x).ToList());
            Line right=Fit(tris.Where(t=>t.center.x>=raftBounds.center.x).ToList());
            if(!ValidLine(left)||!ValidLine(right)){Debug.LogError("[RAFT V17] Could not prove both moving oars; no change saved. left="+left.span.ToString("0.000")+"/"+left.inliers+" right="+right.span.ToString("0.000")+"/"+right.inliers);return false;}

            Bounds volume=raftBounds; volume.Expand(new Vector3(.10f,.16f,.10f));
            HashSet<int> reclaim=new HashSet<int>();
            foreach(Tri t in tris)
            {
                if(!volume.Intersects(t.bounds))continue;
                Line line=t.center.x<raftBounds.center.x?left:right;
                float d=Distance(t.center,line);
                float vd=Mathf.Min(Distance(t.a,line),Mathf.Min(Distance(t.b,line),Distance(t.c,line)));
                float alignment=Alignment(t,line.dir);
                float radius=alignment>.80f?.095f:.060f;
                if(d<=radius||vd<=.035f)continue; // actual moving shaft
                if(t.center.x<raftBounds.min.x-.06f||t.center.x>raftBounds.max.x+.06f||t.center.z<raftBounds.min.z-.06f||t.center.z>raftBounds.max.z+.06f)continue; // blade/end outside raft
                reclaim.Add(t.ord);
            }

            int returned=reclaim.Count,remaining=total-returned;
            if(returned<2){Debug.LogError("[RAFT V17] No remaining stationary support-layer faces were identified.");Log(tris,raftBounds,left,right,reclaim);return false;}
            if(returned>14||remaining<12||returned>=Mathf.CeilToInt(total*.55f)){Debug.LogError("[RAFT V17] Safety stop total="+total+" return="+returned+" moving="+remaining);Log(tris,raftBounds,left,right,reclaim);return false;}

            List<Tri> kept=tris.Where(t=>!reclaim.Contains(t.ord)).ToList();
            Line keptLeft=Fit(kept.Where(t=>t.center.x<raftBounds.center.x).ToList());
            Line keptRight=Fit(kept.Where(t=>t.center.x>=raftBounds.center.x).ToList());
            if(!ValidLine(keptLeft)||!ValidLine(keptRight)){Debug.LogError("[RAFT V17] Post-cleanup proof of both moving oars failed. Nothing saved.");return false;}

            List<int> keepOar=new List<int>(),back=new List<int>();
            for(int i=0,ord=0;i+2<oar.Length;i+=3,ord++)
            {
                List<int> dst=reclaim.Contains(ord)?back:keepOar; dst.Add(oar[i]);dst.Add(oar[i+1]);dst.Add(oar[i+2]);
            }
            Mesh refined=Object.Instantiate(source); refined.name="RaftOnlyMovingOars_v17";
            List<int> raftCombined=new List<int>(raft.Length+back.Count); raftCombined.AddRange(raft);raftCombined.AddRange(back);
            refined.SetTriangles(raftCombined,0,false); refined.SetTriangles(keepOar,oarSlot,false); refined.RecalculateBounds();

            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            if(AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)!=null)AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(refined,MeshPath); Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);if(saved==null)return false;
            MeshFilter mf=renderer.GetComponent<MeshFilter>(); SkinnedMeshRenderer smr=renderer as SkinnedMeshRenderer;
            if(mf!=null)mf.sharedMesh=saved;else if(smr!=null)smr.sharedMesh=saved;else return false;
            Transform old=wrapper.Find(Marker);if(old!=null)Object.DestroyImmediate(old.gameObject);new GameObject(Marker).transform.SetParent(wrapper,false);
            EditorUtility.SetDirty(renderer);if(mf!=null)EditorUtility.SetDirty(mf);if(smr!=null)EditorUtility.SetDirty(smr);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();AssetDatabase.Refresh();

            string report=string.Join(" | ",tris.Where(t=>reclaim.Contains(t.ord)).Select(t=>"#"+t.ord+" "+t.center.ToString("F3")));
            Debug.Log("[RAFT V17] SUCCESS — returned "+returned+" remaining stationary side/underside/support-layer triangles to Raft Texture; "+remaining+" proven moving-oar triangles remain on Oar Texture. "+report);
            if(force)EditorUtility.DisplayDialog("Raft v17 Complete","The sandwiched side/underside support layer and protruding stationary plank now use the raft wood texture. Only moving oars retain the oar texture.","OK");
            return true;
        }
        catch(Exception e){Debug.LogError("[RAFT V17] Exception: "+e);return false;}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    static bool ValidLine(Line l)=>l.valid&&l.span>=.70f&&l.inliers>=3;
    static List<Tri> Build(int[] idx,Vector3[] v,Transform rt,Transform root)
    {
        List<Tri> r=new List<Tri>(idx.Length/3);
        for(int i=0,o=0;i+2<idx.Length;i+=3,o++)
        {
            Vector3 a=Boat(v[idx[i]],rt,root),b=Boat(v[idx[i+1]],rt,root),c=Boat(v[idx[i+2]],rt,root);Bounds q=new Bounds(a,Vector3.zero);q.Encapsulate(b);q.Encapsulate(c);
            r.Add(new Tri{ord=o,a=a,b=b,c=c,center=(a+b+c)/3f,bounds=q,maxEdge=Mathf.Max(Vector3.Distance(a,b),Mathf.Max(Vector3.Distance(b,c),Vector3.Distance(c,a)))});
        }return r;
    }
    static Line Fit(List<Tri> tris)
    {
        if(tris==null||tris.Count<3)return default;Line best=default;float bestScore=float.NegativeInfinity;
        for(int i=0;i<tris.Count;i++)for(int j=i+1;j<tris.Count;j++)
        {
            Vector3 delta=tris[j].center-tris[i].center;float pairSpan=delta.magnitude;if(pairSpan<.30f)continue;
            Vector3 dir=delta/pairSpan,point=(tris[i].center+tris[j].center)*.5f;int inliers=0;float lo=float.PositiveInfinity,hi=float.NegativeInfinity;
            Line candidate=new Line{valid=true,point=point,dir=dir};
            foreach(Tri t in tris){if(Distance(t.center,candidate)>.14f)continue;inliers++;float p=Vector3.Dot(t.center-point,dir);lo=Mathf.Min(lo,p);hi=Mathf.Max(hi,p);}
            float span=inliers>0?hi-lo:0f,score=inliers*10f+span*4f;
            if(score>bestScore){bestScore=score;best=new Line{valid=inliers>=3,point=point,dir=dir.normalized,span=span,inliers=inliers};}
        }return best;
    }
    static float Alignment(Tri t,Vector3 d)
    {
        Vector3 ab=(t.b-t.a).normalized,bc=(t.c-t.b).normalized,ca=(t.a-t.c).normalized;
        return Mathf.Max(Mathf.Abs(Vector3.Dot(ab,d)),Mathf.Max(Mathf.Abs(Vector3.Dot(bc,d)),Mathf.Abs(Vector3.Dot(ca,d))));
    }
    static float Distance(Vector3 p,Line l){Vector3 d=p-l.point;Vector3 c=l.point+l.dir*Vector3.Dot(d,l.dir);return Vector3.Distance(p,c);}
    static Vector3 Boat(Vector3 p,Transform rt,Transform root)=>root.InverseTransformPoint(rt.TransformPoint(p));
    static bool Measure(int[] idx,Vector3[] v,Transform rt,Transform root,out Bounds b)
    {
        b=default;bool have=false;foreach(int i in idx){if(i<0||i>=v.Length)continue;Vector3 p=Boat(v[i],rt,root);if(!have){b=new Bounds(p,Vector3.zero);have=true;}else b.Encapsulate(p);}return have;
    }
    static void Log(List<Tri> tris,Bounds b,Line l,Line r,HashSet<int> reclaim)
    {
        Debug.Log("[RAFT V17] diagnostic raft="+b.center.ToString("F3")+"/"+b.size.ToString("F3")+" L="+l.span.ToString("0.000")+"/"+l.inliers+" R="+r.span.ToString("0.000")+"/"+r.inliers+" reclaim="+reclaim.Count+" tris="+string.Join(" | ",tris.Select(t=>"#"+t.ord+" "+t.center.ToString("F3")+" edge="+t.maxEdge.ToString("0.000"))));
    }
    static Renderer FindRenderer(Transform w)
    {
        Renderer best=null;long score=long.MinValue;foreach(Renderer r in w.GetComponentsInChildren<Renderer>(true))
        {
            Mesh m=GetMesh(r);if(m==null||m.subMeshCount<2)continue;Material[] mats=r.sharedMaterials??Array.Empty<Material>();if(mats.Length<m.subMeshCount||!IsOar(mats[m.subMeshCount-1]))continue;
            long s=m.vertexCount;string n=m.name??string.Empty;if(n.IndexOf("v16",StringComparison.OrdinalIgnoreCase)>=0)s+=30000000L;if(n.IndexOf("v15",StringComparison.OrdinalIgnoreCase)>=0)s+=20000000L;if(n.IndexOf("OarSplit",StringComparison.OrdinalIgnoreCase)>=0)s+=10000000L;if(s>score){score=s;best=r;}
        }return best;
    }
    static bool IsOar(Material m)
    {
        if(m==null)return false;string n=m.name??string.Empty;if(n.IndexOf("oar",StringComparison.OrdinalIgnoreCase)>=0||n.IndexOf("paddle",StringComparison.OrdinalIgnoreCase)>=0)return true;Texture t=m.mainTexture;return t!=null&&(t.name.IndexOf("oar",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("paddle",StringComparison.OrdinalIgnoreCase)>=0);
    }
    static Mesh GetMesh(Renderer r){if(r==null)return null;SkinnedMeshRenderer s=r as SkinnedMeshRenderer;if(s!=null)return s.sharedMesh;MeshFilter f=r.GetComponent<MeshFilter>();return f!=null?f.sharedMesh:null;}
}
