using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Final material cleanup for the user's exact raft mesh after v16.
/// The top oar rests are now correct, but the screenshots show a thin side/underside
/// layer and a slightly protruding stationary plank still living in the oar submesh.
/// v17 treats the ACTUAL moving oars as two robust 3D lines, then returns stationary
/// geometry inside the raft footprint that is not part of either moving-oar corridor.
/// No transforms, UVs, animation, colliders, vertices, or gameplay are changed.
/// </summary>
public static class RaftOnlyMovingOarsV17
{
    private const string PrefabPath="Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath="ModelContainer/Uploaded Raft Model";
    private const string RecoveryFolder="Assets/_Game/Boats/Raft/UserTextures";
    private const string MeshPath=RecoveryFolder+"/RaftOnlyMovingOars_v17.asset";
    private const string MarkerName="RaftOnlyMovingOars_v17";
    private const string V16Marker="RaftOarRestCleanup_v16";

    private sealed class Tri
    {
        public int ordinal;
        public Vector3 center,a,b,c;
        public Bounds bounds;
        public float maxEdge;
    }

    private struct OarLine
    {
        public bool valid;
        public Vector3 point,dir;
        public float span;
        public int inliers;
    }

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        EditorApplication.delayCall+=()=>EditorApplication.delayCall+=()=>EditorApplication.delayCall+=()=>Apply(false);
    }

    [MenuItem("Tools/Open World/Final Raft Texture Cleanup - ONLY Moving Oars (v17)")]
    private static void Force()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {EditorUtility.DisplayDialog("Raft v17","Exit Play Mode first.","OK");return;}
        if(!Apply(true))EditorUtility.DisplayDialog("Raft v17","No change was saved. Check Console for [RAFT V17].","OK");
    }

    private static bool Apply(bool force)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return false;
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform readWrapper=prefab!=null?prefab.transform.Find(WrapperPath):null;
        if(readWrapper==null)return false;
        if(!force&&readWrapper.Find(MarkerName)!=null)return false;
        if(readWrapper.Find(V16Marker)==null)
        {Debug.Log("[RAFT V17] Waiting for successful v16 cleanup first.");return false;}

        GameObject root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper=root.transform.Find(WrapperPath);if(wrapper==null)return false;
            Renderer renderer=FindRenderer(wrapper);
            Mesh source=GetMesh(renderer);
            if(renderer==null||source==null||source.subMeshCount<2)
            {Debug.LogError("[RAFT V17] Could not find current split raft renderer.");return false;}

            int oarSlot=source.subMeshCount-1;
            if(source.GetTopology(0)!=MeshTopology.Triangles||source.GetTopology(oarSlot)!=MeshTopology.Triangles)
            {Debug.LogError("[RAFT V17] Expected triangle topology.");return false;}
            Material[] mats=renderer.sharedMaterials??Array.Empty<Material>();
            if(mats.Length<source.subMeshCount||mats[0]==null||mats[oarSlot]==null||IsOar(mats[0])||!IsOar(mats[oarSlot]))
            {Debug.LogError("[RAFT V17] Material slots are not raft + oar as expected.");return false;}

            Vector3[] vertices;
            try{vertices=source.vertices;}catch(Exception e){Debug.LogError("[RAFT V17] Mesh is not readable: "+e.Message);return false;}
            int[] raft=source.GetIndices(0),oar=source.GetIndices(oarSlot);
            int total=oar.Length/3;
            if(total<16){Debug.LogError("[RAFT V17] Oar slot unexpectedly small ("+total+").");return false;}

            Bounds raftBounds;
            if(!Measure(raft,vertices,renderer.transform,root.transform,out raftBounds))return false;
            List<Tri> tris=Build(oar,vertices,renderer.transform,root.transform);
            OarLine left=FitRobustLine(tris.Where(t=>t.center.x<raftBounds.center.x).ToList());
            OarLine right=FitRobustLine(tris.Where(t=>t.center.x>=raftBounds.center.x).ToList());
            if(!left.valid||!right.valid||left.span<.70f||right.span<.70f)
            {
                Debug.LogError("[RAFT V17] Could not prove both moving oars. left span="+left.span.ToString("0.000")+" right span="+right.span.ToString("0.000")+". No change saved.");
                return false;
            }

            // The actual oar shaft is narrow. Anything inside the physical raft
            // footprint but clearly outside the fitted shaft corridor is stationary
            // raft structure: exactly the sandwiched side layer / protruding plank
            // visible in the user's closeups.
            Bounds stationaryVolume=raftBounds;
            stationaryVolume.Expand(new Vector3(.10f,.16f,.10f));
            HashSet<int> reclaim=new HashSet<int>();
            foreach(Tri t in tris)
            {
                if(!stationaryVolume.Intersects(t.bounds))continue;
                OarLine line=t.center.x<raftBounds.center.x?left:right;
                float centerDistance=DistanceToLine(t.center,line);
                float vertexDistance=Mathf.Min(DistanceToLine(t.a,line),Mathf.Min(DistanceToLine(t.b,line),DistanceToLine(t.c,line)));
                float alignment=DominantAlignment(t,line.dir);

                // Protect the moving shaft even where it physically passes through
                // the stationary support. Long faces aligned with the oar get a wider
                // protection corridor; box/plank faces do not.
                float protectedRadius=alignment>.80f?.095f:.060f;
                if(centerDistance<=protectedRadius||vertexDistance<=.035f)continue;

                // Exterior paddle/blade geometry remains oar-textured. A stationary
                // raft layer must overlap the established raft volume in X/Z.
                if(t.center.x<raftBounds.min.x-.06f||t.center.x>raftBounds.max.x+.06f||
                   t.center.z<raftBounds.min.z-.06f||t.center.z>raftBounds.max.z+.06f)continue;

                reclaim.Add(t.ordinal);
            }

            int returned=reclaim.Count;
            int remaining=total-returned;
            if(returned<2)
            {Debug.LogError("[RAFT V17] Found no additional stationary side/underside raft faces to reclaim. No change saved.");Log(tris,raftBounds,left,right,reclaim);return false;}
            if(returned>14||remaining<12||returned>=Mathf.CeilToInt(total*.55f))
            {Debug.LogError("[RAFT V17] Safety stop: total="+total+" return="+returned+" moving="+remaining+". No change saved.");Log(tris,raftBounds,left,right,reclaim);return false;}

            // Final proof: after classification each side must still retain several
            // triangles distributed along a long line. This prevents a stationary
            // plank cleanup from ever deleting an oar.
            List<Tri> kept=tris.Where(t=>!reclaim.Contains(t.ordinal)).ToList();
            OarLine keptLeft=FitRobustLine(kept.Where(t=>t.center.x<raftBounds.center.x).ToList());
            OarLine keptRight=FitRobustLine(kept.Where(t=>t.center.x>=raftBounds.center.x).ToList());
            if(!keptLeft.valid||!keptRight.valid||keptLeft.span<.70f||keptRight.span<.70f||keptLeft.inliers<3||keptRight.inliers<3)
            {Debug.LogError("[RAFT V17] Post-cleanup oar proof failed. Nothing saved.");return false;}

            List<int> keepOar=new List<int>(),backToRaft=new List<int>();
            for(int i=0,ord=0;i+2<oar.Length;i+=3,ord++)
            {
                List<int> dst=reclaim.Contains(ord)?backToRaft:keepOar;
                dst.Add(oar[i]);dst.Add(oar[i+1]);dst.Add(oar[i+2]);
            }
            Mesh refined=Object.Instantiate(source);refined.name="RaftOnlyMovingOars_v17";
            List<int> raftCombined=new List<int>(raft.Length+backToRaft.Count);raftCombined.AddRange(raft);raftCombined.AddRange(backToRaft);
            refined.SetTriangles(raftCombined,0,false);refined.SetTriangles(keepOar,oarSlot,false);refined.RecalculateBounds();

            Directory.CreateDirectory(RecoveryFolder);AssetDatabase.Refresh();
            if(AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)!=null)AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(refined,MeshPath);Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);if(saved==null)return false;
            MeshFilter mf=renderer.GetComponent<MeshFilter>();SkinnedMeshRenderer smr=renderer as SkinnedMeshRenderer;
            if(mf!=null)mf.sharedMesh=saved;else if(smr!=null)smr.sharedMesh=saved;else return false;
            Transform old=wrapper.Find(MarkerName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            new GameObject(MarkerName).transform.SetParent(wrapper,false);
            EditorUtility.SetDirty(renderer);if(mf!=null)EditorUtility.SetDirty(mf);if(smr!=null)EditorUtility.SetDirty(smr);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();AssetDatabase.Refresh();

            string report=string.Join(" | ",tris.Where(t=>reclaim.Contains(t.ordinal)).Select(t=>"ord="+t.ordinal+" c="+t.center.ToString("F3")));
            Debug.Log("[RAFT V17] SUCCESS — returned "+returned+" remaining stationary side/underside/support-layer triangles to Raft Texture; "+remaining+" proven moving-oar triangles remain on Oar Texture. "+report);
            if(force)EditorUtility.DisplayDialog("Raft v17 Complete","The remaining sandwiched side/underside support layer now uses the raft wood texture. Only the actual moving oars retain the oar texture.","OK");
            return true;
        }
        catch(Exception e){Debug.LogError("[RAFT V17] Exception: "+e);return false;}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    private static List<Tri> Build(int[] indices,Vector3[] v,Transform rt,Transform root)
    {
        List<Tri> result=new List<Tri>(indices.Length/3);
        for(int i=0,ord=0;i+2<indices.Length;i+=3,ord++)
        {
            Vector3 a=ToBoat(v[indices[i]],rt,root),b=ToBoat(v[indices[i+1]],rt,root),c=ToBoat(v[indices[i+2]],rt,root);
            Bounds bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
            result.Add(new Tri{ordinal=ord,a=a,b=b,c=c,center=(a+b+c)/3f,bounds=bounds,maxEdge=Mathf.Max(Vector3.Distance(a,b),Mathf.Max(Vector3.Distance(b,c),Vector3.Distance(c,a)))});
        }
        return result;
    }

    private static OarLine FitRobustLine(List<Tri> tris)
    {
        if(tris==null||tris.Count<3)return default;
        OarLine best=default;float bestScore=float.NegativeInfinity;
        for(int i=0;i<tris.Count;i++)for(int j=i+1;j<tris.Count;j++)
        {
            Vector3 delta=tris[j].center-tris[i].center;float span=delta.magnitude;if(span<.30f)continue;
            Vector3 dir=delta/span;Vector3 point=(tris[i].center+tris[j].center)*.5f;
            int inliers=0;float projectedMin=float.PositiveInfinity,projectedMax=float.NegativeInfinity;
            foreach(Tri t in tris)
            {
                float d=DistanceToLine(t.center,new OarLine{valid=true,point=point,dir=dir});
                if(d>.14f)continue;
                inliers++;float projection=Vector3.Dot(t.center-point,dir);projectedMin=Mathf.Min(projectedMin,projection);projectedMax=Mathf.Max(projectedMax,projection);
            }
            float fittedSpan=inliers>0?projectedMax-projectedMin:0f;
            float score=inliers*10f+fittedSpan*4f;
            if(score>bestScore){bestScore=score;best=new OarLine{valid=inliers>=3,point=point,dir=dir.normalized,span=fittedSpan,inliers=inliers};}
        }
        return best;
    }

    private static float DominantAlignment(Tri t,Vector3 direction)
    {
        Vector3 ab=(t.b-t.a).normalized,bc=(t.c-t.b).normalized,ca=(t.a-t.c).normalized;
        return Mathf.Max(Mathf.Abs(Vector3.Dot(ab,direction)),Mathf.Max(Mathf.Abs(Vector3.Dot(bc,direction)),Mathf.Abs(Vector3.Dot(ca,direction))));
    }
    private static float DistanceToLine(Vector3 p,OarLine line)
    {Vector3 d=p-line.point;Vector3 closest=line.point+line.dir*Vector3.Dot(d,line.dir);return Vector3.Distance(p,closest);}
    private static Vector3 ToBoat(Vector3 v,Transform rt,Transform root)=>root.InverseTransformPoint(rt.TransformPoint(v));

    private static bool Measure(int[] indices,Vector3[] vertices,Transform rt,Transform root,out Bounds bounds)
    {
        bounds=default;bool have=false;
        foreach(int i in indices){if(i<0||i>=vertices.Length)continue;Vector3 p=ToBoat(vertices[i],rt,root);if(!have){bounds=new Bounds(p,Vector3.zero);have=true;}else bounds.Encapsulate(p);}return have;
    }

    private static void Log(List<Tri> tris,Bounds raft,OarLine left,OarLine right,HashSet<int> reclaim)
    {
        Debug.Log("[RAFT V17] diagnostic raft="+raft.center.ToString("F3")+"/"+raft.size.ToString("F3")+
            " left span="+left.span.ToString("0.000")+" inliers="+left.inliers+" right span="+right.span.ToString("0.000")+" inliers="+right.inliers+
            " candidates="+reclaim.Count+"; oar tris: "+string.Join(" | ",tris.Select(t=>"#"+t.ordinal+" "+t.center.ToString("F3")+" edge="+t.maxEdge.ToString("0.000"))));
    }

    private static Renderer FindRenderer(Transform wrapper)
    {
        Renderer best=null;long bestScore=long.MinValue;
        foreach(Renderer r in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh m=GetMesh(r);if(m==null||m.subMeshCount<2)continue;
            Material[] mats=r.sharedMaterials??Array.Empty<Material>();if(mats.Length<m.subMeshCount||!IsOar(mats[m.subMeshCount-1]))continue;
            long score=m.vertexCount;string n=m.name??string.Empty;
            if(n.IndexOf("v16",StringComparison.OrdinalIgnoreCase)>=0)score+=30000000L;
            if(n.IndexOf("v15",StringComparison.OrdinalIgnoreCase)>=0)score+=20000000L;
            if(n.IndexOf("OarSplit",StringComparison.OrdinalIgnoreCase)>=0)score+=10000000L;
            if(score>bestScore){bestScore=score;best=r;}
        }return best;
    }
    private static bool IsOar(Material m)
    {
        if(m==null)return false;string n=m.name??string.Empty;if(n.IndexOf("oar",StringComparison.OrdinalIgnoreCase)>=0||n.IndexOf("paddle",StringComparison.OrdinalIgnoreCase)>=0)return true;
        Texture t=m.mainTexture;return t!=null&&(t.name.IndexOf("oar",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("paddle",StringComparison.OrdinalIgnoreCase)>=0);
    }
    private static Mesh GetMesh(Renderer r)
    {if(r==null)return null;SkinnedMeshRenderer s=r as SkinnedMeshRenderer;if(s!=null)return s.sharedMesh;MeshFilter f=r.GetComponent<MeshFilter>();return f!=null?f.sharedMesh:null;}
}
