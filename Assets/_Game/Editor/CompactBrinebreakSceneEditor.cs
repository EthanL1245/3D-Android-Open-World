using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Records the one-time saved-scene conversion. Prevents repeated reshaping after
/// the player manually edits the island or surrounding ocean in the Scene editor.
/// </summary>
[DisallowMultipleComponent]
public sealed class CompactBrinebreakSavedMarker : MonoBehaviour
{
    [SerializeField, HideInInspector] private bool baked;
    public bool IsBaked => baked;
    public void MarkBaked() { baked = true; }
}

/// <summary>
/// One-time conversion of the CURRENT editable PrototypeWorld scene. Generates
/// persistent TerrainData assets; does not rebuild the world, relocate Bluewater,
/// or touch USER PLACED SCENERY. Also removes the retired coral prefab instances.
/// </summary>
[InitializeOnLoad]
public static class CompactBrinebreakSceneEditor
{
    private const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    private const string FolderRoot = "Assets/_Game/EditableFishingMap";
    private static readonly Vector2 OriginalRadii = new Vector2(95f, 65f);
    private const float Shrink = 3f;
    private static bool offered, running;

    static CompactBrinebreakSceneEditor()
    {
        EditorApplication.delayCall += OfferOnceIfNeeded;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += OfferOnceIfNeeded;
    }

    private static void OfferOnceIfNeeded()
    {
        if (offered || running || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
            return;
        Scene scene = SceneManager.GetActiveScene();
        if (!TryGetWorld(scene, out IslandExpansionWorld world)) return;
        if (world.GetComponent<CompactBrinebreakSavedMarker>()?.IsBaked == true)
            return;
        offered = true;
        if (EditorUtility.DisplayDialog("Finish saved Brinebreak Island update",
            "The latest GitHub scene has retired coral, but Unity must bake the new Brinebreak terrain.\n\n" +
            "Apply the requested 3x narrower island now? This creates a separate scene and terrain backup, saves the updated map, and never moves your protected hand-placed scenery.",
            "Back up and apply", "Later"))
            Apply();
    }

    [MenuItem("Tools/Open World/Apply 3x Smaller Brinebreak and Remove Coral")]
    public static void Apply()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning("Brinebreak conversion requires Unity Scene edit mode and no pending compilation.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!TryGetWorld(scene, out IslandExpansionWorld world))
        {
            EditorUtility.DisplayDialog("Open editable fishing map",
                "Open Assets/Scenes/PrototypeWorld.unity (not a shop scene), then run Make Main Fishing Map Editable if necessary.", "OK");
            return;
        }
        var marker = world.GetComponent<CompactBrinebreakSavedMarker>();
        if (marker != null && marker.IsBaked)
        {
            EditorUtility.DisplayDialog("Brinebreak already saved",
                "The 3x smaller island is already baked into this scene. The tool will not reset any scenery or terrain edits.", "OK");
            return;
        }

        // The user's unsaved Scene changes must be committed or explicitly saved
        // before a destructive terrain bake can safely create its own backup.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Terrain terrain = world.Terrain;
        OceanWater water = Find<OceanWater>(scene);
        if (terrain == null || terrain.terrainData == null || water == null)
            throw new InvalidOperationException("Editable fishing terrain and ocean are required.");

        var source = terrain.terrainData;
        var sourceCollider = terrain.GetComponent<TerrainCollider>();
        Vector3 center = world.NewCenter;
        float sea = water.BaseWaterLevel;
        var currentSize = source.size;
        if (center.x < terrain.transform.position.x || center.z < terrain.transform.position.z ||
            center.x > terrain.transform.position.x + currentSize.x ||
            center.z > terrain.transform.position.z + currentSize.z)
            throw new InvalidOperationException("Brinebreak center does not lie inside the saved terrain; conversion cancelled.");

        running = true;
        Directory.CreateDirectory(FolderRoot);
        AssetDatabase.Refresh();
        string folder = FolderRoot + "/CompactBrinebreak-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        AssetDatabase.CreateFolder(FolderRoot, Path.GetFileName(folder));
        TerrainData resultData = null;
        CompactBrinebreakSavedMarker createdMarker = null;
        try
        {
            EditorUtility.DisplayProgressBar("Compact Brinebreak", "Backing up your entire scene and original terrain...", .08f);
            // Scene backups must reference an independent terrain copy, not the
            // original TerrainData that remains in use by other scene objects.
            TerrainData frozen = Object.Instantiate(source);
            frozen.name = "Brinebreak before shrink - terrain backup";
            AssetDatabase.CreateAsset(frozen, folder + "/TerrainBeforeShrink.asset");
            try
            {
                terrain.terrainData = frozen;
                if (sourceCollider != null) sourceCollider.terrainData = frozen;
                if (!EditorSceneManager.SaveScene(scene, folder + "/SceneBeforeShrink.unity", true))
                    throw new IOException("Couldn't save the independent Brinebreak scene backup.");
            }
            finally
            {
                terrain.terrainData = source;
                if (sourceCollider != null) sourceCollider.terrainData = source;
            }

            EditorUtility.DisplayProgressBar("Compact Brinebreak", "Sculpting only the former Brinebreak footprint...", .25f);
            resultData = Object.Instantiate(source);
            resultData.name = "Editable Brinebreak - one third width and length";
            AssetDatabase.CreateAsset(resultData, folder + "/CompactBrinebreakTerrain.asset");
            int heightSamples = SculptIsland(terrain, source, resultData, center, sea);
            int paintedPixels = RepaintFootprint(terrain, source, resultData, center, sea);

            // Install the new asset only after all geometry and painting have
            // succeeded. The original TerrainData remains unmodified.
            Undo.RecordObject(terrain, "Replace terrain with compact Brinebreak");
            if (sourceCollider != null)
                Undo.RecordObject(sourceCollider, "Bind compact Brinebreak terrain collider");
            terrain.terrainData = resultData;
            if (sourceCollider != null) sourceCollider.terrainData = resultData;
            terrain.Flush();
            Physics.SyncTransforms();

            EditorUtility.DisplayProgressBar("Compact Brinebreak", "Rearranging only generated Brinebreak objects...", .65f);
            int removedProps = CompactGeneratedScenery(world, terrain, center);
            int removedCoral = RemoveCoralPrefabs(scene);
            MoveGeneratedArrivalAndSign(world, terrain, center);
            SetDiscoveryVolume(world, center);

            // Mark AFTER successful conversion so future scene loads can't
            // rerun the island reshaping over manual terrain paint.
            createdMarker = Undo.AddComponent<CompactBrinebreakSavedMarker>(world.gameObject);
            createdMarker.MarkBaked();
            EditorUtility.SetDirty(createdMarker);
            EditorUtility.SetDirty(resultData);
            EditorUtility.SetDirty(terrain);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Unable to save the updated PrototypeWorld scene.");
            SceneView.RepaintAll();
            Debug.Log("[BRINEBREAK] Permanently baked one-third width and length: " +
                heightSamples + " terrain samples, " + paintedPixels +
                " splat pixels, " + removedProps + " generated props removed, " +
                removedCoral + " remaining coral objects removed. Backup and assets: " + folder);

            EditorUtility.DisplayDialog("Brinebreak saved in Unity",
                "Brinebreak's shoreline is now 3x smaller along both axes.\n\n" +
                "The saved scene uses independent permanent TerrainData. The nearby generator-owned rocks and arrival/discovery markers are adjusted. Snapper, Suncrest, Bluewater and USER PLACED SCENERY are untouched.\n\n" +
                "Commit and push PrototypeWorld.unity and the new CompactBrinebreak-* folder to GitHub. Local Unity saves are not uploaded automatically.", "OK");
        }
        catch (Exception error)
        {
            terrain.terrainData = source;
            if (sourceCollider != null) sourceCollider.terrainData = source;
            if (createdMarker != null) Object.DestroyImmediate(createdMarker);
            Debug.LogError("[BRINEBREAK] Update failed. Original terrain reference restored. " +
                "The independent scene/terrain backups are at " + folder +
                ". If some scenery moved before failure, reopen SceneBeforeShrink.unity. " + error);
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            running = false;
        }
    }

    private static int SculptIsland(Terrain terrain, TerrainData original, TerrainData output,
        Vector3 center, float sea)
    {
        int n = original.heightmapResolution;
        Vector3 origin = terrain.transform.position, size = original.size;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - OriginalRadii.x * 1.30f - origin.x) / size.x * (n - 1)), 0, n - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + OriginalRadii.x * 1.30f - origin.x) / size.x * (n - 1)), 0, n - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - OriginalRadii.y * 1.30f - origin.z) / size.z * (n - 1)), 0, n - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + OriginalRadii.y * 1.30f - origin.z) / size.z * (n - 1)), 0, n - 1);

        float[,] heights = output.GetHeights(x0, z0, x1-x0+1, z1-z0+1);
        int changed = 0;
        for (int z = 0; z < heights.GetLength(0); z++)
        for (int x = 0; x < heights.GetLength(1); x++)
        {
            Vector3 p = origin + new Vector3((x0+x)/(float)(n-1)*size.x,0f,(z0+z)/(float)(n-1)*size.z);
            Vector3 d = p-center;
            float q = IslandGeometry.Ellipse(p,center,OriginalRadii);
            if (q >= 1.27f) continue;
            // Extra guard against touching Snapper's already customized shelf.
            if (SnapperIslandGeometry.Ellipse(p,SnapperIslandRuntime.Center)<1.2f)continue;
            float previous = origin.y+heights[z,x]*size.y;
            float next;
            if (q<=1f/Shrink)
            {
                // Map the complete old island shape, including its authored
                // rolling height variation, into the smaller footprint.
                Vector3 sample = center + d * Shrink;
                float sampled = HeightAt(terrain,original,sample);
                next = sea + Mathf.Max(.035f,(sampled-sea)*.76f);
            }
            else
            {
                float horizontal = Mathf.Sqrt(d.x*d.x+d.z*d.z);
                if (horizontal<.0001f)continue;
                Vector3 dir = d/horizontal;
                float oldShore = 1f/Mathf.Sqrt(dir.x*dir.x/(OriginalRadii.x*OriginalRadii.x)+
                    dir.z*dir.z/(OriginalRadii.y*OriginalRadii.y));
                float newShore=oldShore/Shrink;
                Vector3 sample = center+dir*(oldShore*1.30f);
                float offshore=HeightAt(terrain,original,sample);
                offshore=Mathf.Min(sea-1.3f,offshore);
                float dist=horizontal-newShore;
                float descent=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(dist/36f));
                float relief=(Mathf.PerlinNoise(p.x*.085f+32f,p.z*.071f+84f)-.5f)*.80f;
                float shelf=Mathf.Lerp(sea-.04f,offshore,descent)+relief*Mathf.Sin(Mathf.PI*descent);
                float blendToExisting=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1.10f,1.27f,q));
                next=Mathf.Lerp(shelf,previous,blendToExisting);
            }
            if (Mathf.Abs(next-previous)<.003f)continue;
            heights[z,x]=Mathf.Clamp01((next-origin.y)/size.y);
            changed++;
        }
        if (changed==0)
            throw new InvalidOperationException("No Brinebreak terrain cells changed. Check the saved scene and configuration.");
        output.SetHeights(x0,z0,heights);
        return changed;
    }

    private static int RepaintFootprint(Terrain terrain, TerrainData original, TerrainData output,
        Vector3 center, float sea)
    {
        int width = original.alphamapWidth, height = original.alphamapHeight;
        int layerCount = original.alphamapLayers;
        float[,,] prev=original.GetAlphamaps(0,0,width,height);
        float[,,] paint=output.GetAlphamaps(0,0,width,height);
        TerrainLayer[] layers=original.terrainLayers;
        int sand=FindLayer(layers,"sand",0);
        int offshoreStone=FindLayer(layers,"underwater",FindLayer(layers,"cliff",layerCount-1));
        int onshoreStone=FindLayer(layers,"recovered island stone",FindLayer(layers,"brinebreak",offshoreStone));
        Vector3 origin=terrain.transform.position, size=original.size;
        int changed=0;

        for(int z=0;z<height;z++)
        for(int x=0;x<width;x++)
        {
            Vector3 p=origin+new Vector3(x/(float)(width-1)*size.x,0f,z/(float)(height-1)*size.z);
            float q=IslandGeometry.Ellipse(p,center,OriginalRadii);
            if(q>=1.27f)continue;
            if(SnapperIslandGeometry.Ellipse(p,SnapperIslandRuntime.Center)<1.2f)continue;
            float nextHeight=HeightAt(terrain,output,p);
            var d=p-center;
            if(q<=1f/Shrink)
            {
                Vector3 sample=center+d*Shrink;
                float su=Mathf.Clamp01((sample.x-origin.x)/size.x),sv=Mathf.Clamp01((sample.z-origin.z)/size.z);
                int sx=Mathf.Clamp(Mathf.RoundToInt(su*(width-1)),0,width-1);
                int sz=Mathf.Clamp(Mathf.RoundToInt(sv*(height-1)),0,height-1);
                for(int i=0;i<layerCount;i++)paint[z,x,i]=prev[sz,sx,i];
            }
            else
            {
                float shallow=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1.6f,7f,sea-nextHeight));
                float rockiness=Mathf.Clamp01(.15f+.65f*shallow);
                float blendBack=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1.10f,1.27f,q));
                for(int i=0;i<layerCount;i++)paint[z,x,i]=prev[z,x,i]*blendBack;
                paint[z,x,sand]+=(1f-rockiness)*(1f-blendBack);
                paint[z,x,offshoreStone]+=rockiness*(1f-blendBack);
                // Don't leave the former full-sized island's exposed stone paint
                // floating on now-submerged stretches of the seabed.
            }
            changed++;
        }
        if(changed>0)output.SetAlphamaps(0,0,paint);
        return changed;
    }

    private static int FindLayer(TerrainLayer[] layers,string role,int fallback)
    {
        for(int i=0;i<layers.Length;i++)
            if(layers[i]!=null && layers[i].name.IndexOf(role,StringComparison.OrdinalIgnoreCase)>=0)
                return i;
        return Mathf.Clamp(fallback,0,layers.Length-1);
    }

    private static float HeightAt(Terrain terrain,TerrainData data,Vector3 world)
    {
        Vector3 basePos=terrain.transform.position,size=data.size;
        float u=Mathf.Clamp01((world.x-basePos.x)/size.x);
        float v=Mathf.Clamp01((world.z-basePos.z)/size.z);
        return basePos.y+data.GetInterpolatedHeight(u,v);
    }

    private static int CompactGeneratedScenery(IslandExpansionWorld world,Terrain terrain,Vector3 center)
    {
        // Only the procedural children attached directly to Island Expansion.
        // UserPlacedScenery.Contains is a hard ownership boundary.
        Transform[] props=world.transform.Cast<Transform>()
            .Where(t=>t.name=="Weathered rock" || t.name=="Wind scrub")
            .Where(t=>!UserPlacedScenery.Contains(t)).ToArray();
        int removed=0;
        for(int i=0;i<props.Length;i++)
        {
            Transform prop=props[i];
            if (i%9!=0)
            {
                Undo.DestroyObjectImmediate(prop.gameObject);
                removed++;
                continue;
            }
            Undo.RecordObject(prop,"Move generated Brinebreak scenery onto compact island");
            Vector3 offset=prop.position-center;
            prop.localScale*=prop.name=="Wind scrub"?.78f:.64f;
            Vector3 pos=center+new Vector3(offset.x/Shrink,0f,offset.z/Shrink);
            float ground=terrain.SampleHeight(pos)+terrain.transform.position.y;
            Renderer renderer=prop.GetComponentInChildren<Renderer>();
            prop.position=new Vector3(pos.x,ground+(renderer!=null?renderer.bounds.extents.y*.38f:0f),pos.z);
        }
        return removed;
    }

    private static int RemoveCoralPrefabs(Scene scene)
    {
        var coral=scene.GetRootGameObjects()
            .SelectMany(root=>root.GetComponentsInChildren<Transform>(true))
            .Where(t=>t!=null && !UserPlacedScenery.Contains(t))
            .Where(t=>string.Equals(t.name,"Coral",StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(t.name,"Reef coral garden",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        int deleted=0;
        foreach(var t in coral)
        {
            if(t==null)continue;
            Undo.DestroyObjectImmediate(t.gameObject);
            deleted++;
        }
        return deleted;
    }

    private static void MoveGeneratedArrivalAndSign(IslandExpansionWorld world,Terrain terrain,Vector3 center)
    {
        Vector3 arrival=center+new Vector3(-OriginalRadii.x/Shrink*.82f,0f,-3f);
        arrival.y=terrain.SampleHeight(arrival)+terrain.transform.position.y+.20f;
        Transform target=world.Arrival;
        if(target!=null && !UserPlacedScenery.Contains(target))
        {
            Undo.RecordObject(target,"Move Brinebreak arrival");
            target.position=arrival;
        }

        var sign=world.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t=>t.name=="BRINEBREAK ISLE" && !UserPlacedScenery.Contains(t));
        if(sign!=null)
        {
            Vector3 position=arrival+new Vector3(3.5f,0f,3.5f);
            position.y=terrain.SampleHeight(position)+terrain.transform.position.y;
            Undo.RecordObject(sign,"Move Brinebreak welcome sign");
            sign.position=position;
        }
    }

    private static void SetDiscoveryVolume(IslandExpansionWorld world,Vector3 center)
    {
        var zone=world.GetComponentsInChildren<BoxCollider>(true)
            .FirstOrDefault(t=>t.gameObject.name=="Brinebreak Land Discovery" &&
                !UserPlacedScenery.Contains(t.transform));
        if(zone==null)return;
        Undo.RecordObject(zone,"Shrink Brinebreak discovery region");
        zone.size=new Vector3(OriginalRadii.x/Shrink*2f,30f,OriginalRadii.y/Shrink*2f);
        Undo.RecordObject(zone.transform,"Move Brinebreak discovery region");
        zone.transform.position=new Vector3(center.x,zone.transform.position.y,center.z);
    }

    private static bool TryGetWorld(Scene scene,out IslandExpansionWorld world)
    {
        world=null;
        if (!scene.IsValid() || !scene.isLoaded || scene.path!=ScenePath ||
            PrefabStageUtility.GetCurrentPrefabStage()!=null)return false;
        world=Find<IslandExpansionWorld>(scene);
        return world!=null && world.HasSavedLayout;
    }

    private static T Find<T>(Scene scene) where T:Component
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            T found=root.GetComponentInChildren<T>(true);
            if(found!=null)return found;
        }
        return null;
    }
}
