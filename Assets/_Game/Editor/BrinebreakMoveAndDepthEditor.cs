using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// One-time, nonprocedural edit of the permanent, scene-authored fishing map.
/// Copies ONLY the compact Brinebreak terrain to a new position 100m toward
/// Suncrest, clears its old footprint, and deepens its local fishing shelf.
/// Independent scene/TerrainData backups are saved before any changes.
/// UserPlacedScenery is never moved or deleted.
/// </summary>
[InitializeOnLoad]
public static class BrinebreakMoveAndDepthEditor
{
    private const string MainScene = "Assets/Scenes/PrototypeWorld.unity";
    private const string BackupRoot = "Assets/_Game/EditableFishingMap";
    private const float MoveMetres = 100f;
    private const float OldRadiusX = 95f / 3f, OldRadiusZ = 65f / 3f;
    private static bool prompted, working;

    static BrinebreakMoveAndDepthEditor()
    {
        EditorApplication.delayCall += Offer;
        EditorSceneManager.sceneOpened += (scene,mode) => EditorApplication.delayCall += Offer;
    }

    private static void Offer()
    {
        if(prompted || working || EditorApplication.isPlayingOrWillChangePlaymode ||
           EditorApplication.isCompiling || EditorApplication.isUpdating ||
           PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
        var scene=SceneManager.GetActiveScene();
        if(!FindWorld(scene,out var world))return;
        if(world.GetComponent<BrinebreakRelocationMarker>()?.Completed==true)return;
        if(world.GetComponent<CompactBrinebreakSavedMarker>()?.IsBaked!=true)return;
        prompted=true;
        if(EditorUtility.DisplayDialog("Move Brinebreak Isle 100 m toward Suncrest",
            "Permanently relocate the compact Brinebreak Isle 100 metres closer to the starter island, and deepen its surrounding water by approximately 1 m?\n\n"+
            "A separate backup scene and TerrainData will be created. Snapper Island, Bluewater Cay, Suncrest Reef and your hand-placed scenery are protected. This edit will be saved to the Unity scene.",
            "Back up and apply","Later"))Apply();
    }

    [MenuItem("Tools/Open World/Move Brinebreak 100 m Closer and Deepen Water")]
    public static void Apply()
    {
        if(working || EditorApplication.isPlayingOrWillChangePlaymode ||
           EditorApplication.isCompiling || EditorApplication.isUpdating)
        {Debug.LogWarning("[BRINEBREAK MOVE] Wait for compilation and exit Play Mode.");return;}

        Scene scene=SceneManager.GetActiveScene();
        if(!FindWorld(scene,out var world))
        {
            EditorUtility.DisplayDialog("Editable map required",
                "Open Assets/Scenes/PrototypeWorld.unity with the saved editable fishing map.", "OK");
            return;
        }
        if(world.GetComponent<BrinebreakRelocationMarker>()?.Completed==true)
        {EditorUtility.DisplayDialog("Already applied","Brinebreak has already been relocated. Your saved edits will not be overwritten.","OK");return;}
        if(world.GetComponent<CompactBrinebreakSavedMarker>()?.IsBaked!=true)
        {
            EditorUtility.DisplayDialog("Finish compact island first",
                "Apply the existing 3x smaller Brinebreak scene bake before relocating this saved island. No map changes were made.","OK");
            return;
        }
        var reef=Find<ReefZone>(scene);
        var ocean=Find<OceanWater>(scene);
        var terrain=world.Terrain;
        if(reef==null || ocean==null || terrain==null || terrain.terrainData==null)
            throw new InvalidOperationException("Brinebreak edit requires ReefZone, OceanWater and a saved TerrainData.");
        Vector3 oldCenter=world.NewCenter, direction=reef.center-oldCenter;
        direction.y=0f;
        if(direction.magnitude<MoveMetres+70f)
            throw new InvalidOperationException("Brinebreak is too close to Suncrest for a safe 100m relocation.");
        Vector3 delta=direction.normalized*MoveMetres, newCenter=oldCenter+delta;
        Vector3 snapperCenter=SnapperIslandGeometry.Center(reef), bluewater=world.PelagicCenter;
        if(!Inside(terrain,newCenter,OldRadiusX+108f,OldRadiusZ+108f))
            throw new InvalidOperationException("The moved Brinebreak would extend outside the editable terrain.");

        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;

        working=true;
        string folder=BackupRoot+"/BrinebreakMove-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        if(!AssetDatabase.IsValidFolder(BackupRoot))throw new InvalidOperationException("EditableFishingMap folder is missing.");
        AssetDatabase.CreateFolder(BackupRoot,Path.GetFileName(folder));
        TerrainData source=terrain.terrainData;
        var terrainCollider=terrain.GetComponent<TerrainCollider>();
        try
        {
            EditorUtility.DisplayProgressBar("Brinebreak relocation","Backing up scene and original terrain...",.08f);
            TerrainData backup=Object.Instantiate(source);
            backup.name="Brinebreak before 100m move";
            AssetDatabase.CreateAsset(backup,folder+"/TerrainBeforeMove.asset");
            try
            {
                terrain.terrainData=backup;
                if(terrainCollider!=null)terrainCollider.terrainData=backup;
                if(!EditorSceneManager.SaveScene(scene,folder+"/SceneBeforeMove.unity",true))
                    throw new IOException("Could not save independent backup scene.");
            }
            finally
            {
                terrain.terrainData=source;
                if(terrainCollider!=null)terrainCollider.terrainData=source;
            }

            var output=Object.Instantiate(source);
            output.name="Editable Brinebreak moved and local waters deepened";
            AssetDatabase.CreateAsset(output,folder+"/TerrainMoved.asset");
            EditorUtility.DisplayProgressBar("Brinebreak relocation","Moving island terrain and sculpting its 1 m deeper coastal water...",.24f);
            int cells=Sculpt(terrain,source,output,oldCenter,newCenter,reef.center,snapperCenter,bluewater,ocean.BaseWaterLevel);
            int painted=Paint(terrain,source,output,oldCenter,newCenter,reef.center,snapperCenter,bluewater);

            Undo.RecordObject(terrain,"Replace terrain with moved Brinebreak");
            if(terrainCollider!=null)Undo.RecordObject(terrainCollider,"Update moved terrain collider");
            terrain.terrainData=output;
            if(terrainCollider!=null)terrainCollider.terrainData=output;
            terrain.Flush();

            EditorUtility.DisplayProgressBar("Brinebreak relocation","Moving generated island arrival, props, sign and discovery volume...",.78f);
            int props=MoveGenerated(world,source,terrain,oldCenter,delta);
            var field=typeof(IslandExpansionWorld).GetField("<NewCenter>k__BackingField",
                BindingFlags.Instance|BindingFlags.NonPublic);
            if(field==null)throw new MissingFieldException("Serialized IslandExpansionWorld.NewCenter field not found.");
            Undo.RecordObject(world,"Move saved Brinebreak world center");
            field.SetValue(world,newCenter);
            EditorUtility.SetDirty(world);

            var marker=Undo.AddComponent<BrinebreakRelocationMarker>(world.gameObject);
            marker.MarkCompleted();
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(output);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))
                throw new IOException("Brinebreak relocation could not save the main scene.");
            SceneView.RepaintAll();
            Debug.Log("[BRINEBREAK MOVE] Saved at "+newCenter+" (shift "+delta+"). "+
                cells+" height samples, "+painted+" terrain paint cells, "+props+" generated objects. "+
                "Bluewater, Suncrest, Snapper and user-placed scenery remain intact. Backup: "+folder);
            EditorUtility.DisplayDialog("Brinebreak moved and saved",
                "Brinebreak has moved 100m toward Suncrest and its coastal seabed is ~1m deeper. "+
                "The scene was saved, with backups in:\n"+folder+
                "\n\nCommit the updated scene and this new folder to GitHub so future pulls preserve the map.","OK");
        }
        catch(Exception e)
        {
            terrain.terrainData=source;
            if(terrainCollider!=null)terrainCollider.terrainData=source;
            Debug.LogError("[BRINEBREAK MOVE] Conversion failed. Original terrain reference restored; "+
                "reopen the independent backup scene in "+folder+" if necessary. "+e);
            throw;
        }
        finally{EditorUtility.ClearProgressBar();working=false;}
    }

    // Both heightmap sampling and paint use source-only data. The old island
    // is removed before the new land is inserted into the output buffer.
    private static int Sculpt(Terrain terrain,TerrainData src,TerrainData dst,
        Vector3 old,Vector3 moved,Vector3 starter,Vector3 snapper,Vector3 blue,float sea)
    {
        int n=src.heightmapResolution;var origin=terrain.transform.position;var size=src.size;
        var previous=src.GetHeights(0,0,n,n);
        var result=dst.GetHeights(0,0,n,n);
        int changed=0;
        for(int z=0;z<n;z++)
        for(int x=0;x<n;x++)
        {
            var p=origin+new Vector3(x/(float)(n-1)*size.x,0f,z/(float)(n-1)*size.z);
            float qo=Ellipse(p,old),qn=Ellipse(p,moved);
            if(qo>1.75f && DistanceFromShore(p,moved)>100f)continue;
            if(Protected(p,starter,snapper,blue))continue;

            float h=origin.y+previous[z,x]*size.y;
            float next=h;
            if(qo<1.75f)
            {
                // Reconstruct the existing offshore bottom outside the former
                // shoreline instead of leaving an above-water ghost island.
                float angle=Mathf.Atan2(p.z-old.z,p.x-old.x);
                Vector3 outer=old+new Vector3(Mathf.Cos(angle)*OldRadiusX*1.8f,0,
                                                  Mathf.Sin(angle)*OldRadiusZ*1.8f);
                float floor=Mathf.Min(sea-2.2f,HeightAt(terrain,src,outer));
                float mask=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,1.75f,qo));
                next=Mathf.Lerp(next,floor,mask);
            }
            if(qn<1.36f)
            {
                var sourcePos=old+(p-moved);
                float island=HeightAt(terrain,src,sourcePos);
                float copy=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1f,1.36f,qn));
                next=Mathf.Lerp(next,island,copy);
            }
            float coast=DistanceFromShore(p,moved);
            if(coast>1f && coast<98f && next<sea-.12f)
            {
                // Wide ~1.15m plateau plus gentle inner/outer fades yields about
                // 1m more depth across the useful 10-85m coastal fishing zone.
                float inner=Mathf.SmoothStep(0f,1f,coast/10f);
                float outer=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(76f,98f,coast));
                next-=1.15f*inner*outer;
            }
            if(Mathf.Abs(h-next)<.004f)continue;
            result[z,x]=Mathf.Clamp01((next-origin.y)/size.y);
            changed++;
        }
        if(changed==0)throw new InvalidOperationException("No island terrain cells changed.");
        dst.SetHeights(0,0,result);
        return changed;
    }

    private static int Paint(Terrain terrain,TerrainData src,TerrainData dst,
        Vector3 old,Vector3 moved,Vector3 starter,Vector3 snapper,Vector3 blue)
    {
        int w=src.alphamapWidth,h=src.alphamapHeight,l=src.alphamapLayers;
        float[,,] oldMap=src.GetAlphamaps(0,0,w,h),newMap=dst.GetAlphamaps(0,0,w,h);
        var origin=terrain.transform.position;var size=src.size;
        int count=0;
        for(int z=0;z<h;z++)
        for(int x=0;x<w;x++)
        {
            Vector3 p=origin+new Vector3(x/(float)(w-1)*size.x,0,z/(float)(h-1)*size.z);
            float qo=Ellipse(p,old),qn=Ellipse(p,moved);
            if(qo>1.75f && qn>1.36f || Protected(p,starter,snapper,blue))continue;
            float[] paints=new float[l];
            for(int i=0;i<l;i++)paints[i]=oldMap[z,x,i];
            if(qo<1.75f)
            {
                float angle=Mathf.Atan2(p.z-old.z,p.x-old.x);
                var far=old+new Vector3(Mathf.Cos(angle)*OldRadiusX*1.8f,0,Mathf.Sin(angle)*OldRadiusZ*1.8f);
                float remove=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,1.75f,qo));
                SamplePaint(src,oldMap,origin,size,far,paints,remove);
            }
            if(qn<1.36f)
            {
                float copy=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1f,1.36f,qn));
                SamplePaint(src,oldMap,origin,size,old+(p-moved),paints,copy);
            }
            float sum=paints.Sum();
            if(sum<=.00001f)continue;
            for(int i=0;i<l;i++)newMap[z,x,i]=paints[i]/sum;
            count++;
        }
        if(count>0)dst.SetAlphamaps(0,0,newMap);
        return count;
    }

    private static void SamplePaint(TerrainData src,float[,,] data,Vector3 origin,Vector3 size,
        Vector3 p,float[] accum,float factor)
    {
        int w=src.alphamapWidth,h=src.alphamapHeight;
        int x=Mathf.Clamp(Mathf.RoundToInt((p.x-origin.x)/size.x*(w-1)),0,w-1);
        int z=Mathf.Clamp(Mathf.RoundToInt((p.z-origin.z)/size.z*(h-1)),0,h-1);
        for(int i=0;i<accum.Length;i++)accum[i]=Mathf.Lerp(accum[i],data[z,x,i],factor);
    }

    private static int MoveGenerated(IslandExpansionWorld world,TerrainData old,Terrain terrain,Vector3 center,Vector3 delta)
    {
        var t=world.GetComponentsInChildren<Transform>(true)
            .Where(x=>x!=world.transform &&
                (x==world.Arrival || x.name=="Weathered rock" || x.name=="Wind scrub" ||
                 x.name=="BRINEBREAK ISLE" || x.name=="Brinebreak Land Discovery"))
            .Where(x=>!UserPlacedScenery.Contains(x)).ToArray();
        int moved=0;
        foreach(Transform item in t)
        {
            Vector3 from=item.position;
            // Other world objects should never be captured by name alone.
            if(Vector2.Distance(new Vector2(from.x,from.z),new Vector2(center.x,center.z))>125f)continue;
            Vector3 target=from+delta;
            float oldFloor=HeightAt(terrain,old,from);
            float newFloor=terrain.SampleHeight(target)+terrain.transform.position.y;
            if(item.name!="Brinebreak Land Discovery")target.y+=newFloor-oldFloor;
            Undo.RecordObject(item,"Relocate generated Brinebreak scenery");
            item.position=target;
            moved++;
        }
        return moved;
    }

    private static float Ellipse(Vector3 p,Vector3 c)
    {
        float x=(p.x-c.x)/OldRadiusX,z=(p.z-c.z)/OldRadiusZ;
        return Mathf.Sqrt(x*x+z*z);
    }
    private static float DistanceFromShore(Vector3 p,Vector3 c)
    {
        var d=p-c;float length=new Vector2(d.x,d.z).magnitude;
        if(length<.001f)return 0;
        float ux=d.x/length,uz=d.z/length;
        float edge=1f/Mathf.Sqrt(ux*ux/(OldRadiusX*OldRadiusX)+uz*uz/(OldRadiusZ*OldRadiusZ));
        return Mathf.Max(0,length-edge);
    }
    private static bool Protected(Vector3 p,Vector3 starter,Vector3 snapper,Vector3 blue)
    {
        // Large no-edit buffers avoid erasing neighboring saved geology/materials.
        if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(starter.x,starter.z))<105f)return true;
        if(SnapperIslandGeometry.DistanceFromShore(p,snapper)<SnapperIslandGeometry.UniformSeabedOuterDistance+15f)return true;
        if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(blue.x,blue.z))<PelagicIslandGeometry.Radius+PelagicIslandGeometry.FishingMargin+15f)return true;
        return false;
    }
    private static float HeightAt(Terrain terrain,TerrainData data,Vector3 p)
    {
        Vector3 origin=terrain.transform.position,size=data.size;
        float u=Mathf.Clamp01((p.x-origin.x)/size.x),v=Mathf.Clamp01((p.z-origin.z)/size.z);
        return origin.y+data.GetInterpolatedHeight(u,v);
    }
    private static bool Inside(Terrain terrain,Vector3 p,float mx,float mz)
    {
        var origin=terrain.transform.position;var size=terrain.terrainData.size;
        return p.x-mx>=origin.x && p.x+mx<=origin.x+size.x &&
               p.z-mz>=origin.z && p.z+mz<=origin.z+size.z;
    }
    private static bool FindWorld(Scene scene,out IslandExpansionWorld world)
    {
        world=null;
        if(!scene.IsValid() || !scene.isLoaded || scene.path!=MainScene)return false;
        world=Find<IslandExpansionWorld>(scene);
        return world!=null && world.HasSavedLayout;
    }
    private static T Find<T>(Scene scene) where T:Component
    {
        foreach(var go in scene.GetRootGameObjects())
        {
            var found=go.GetComponentInChildren<T>(true);
            if(found!=null)return found;
        }
        return null;
    }
}
