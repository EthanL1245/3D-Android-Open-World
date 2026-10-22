using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[CustomEditor(typeof(ShorelineFoam))]
public sealed class ShorelineFoamInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Regenerate after changing rocks, terrain, water level or width. Save the scene afterwards. Source scenery is read only.", MessageType.Info);
        if (GUILayout.Button("Regenerate Shoreline Foam")) ShorelineFoamEditor.Regenerate();
    }
}

/// <summary>Editor-only union of terrain and closed rock cross-sections. A narrow
/// signed-distance band avoids overlapping foam at rock/terrain joins. Work is
/// limited to tiles crossed by shorelines, never the entire ocean.</summary>
public static class ShorelineFoamEditor
{
    private const float Cell = .25f, Tile = 24f;
    private const int Cells = 96;
    private struct Segment
    {
        public Vector2 a, b;
        public Segment(Vector2 a, Vector2 b) { this.a = a; this.b = b; }
        public float Distance(Vector2 p)
        {
            Vector2 edge = b - a;
            return Vector2.Distance(p, a + edge * Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(1e-12f, edge.sqrMagnitude)));
        }
    }
    private sealed class Rock
    {
        public readonly List<Segment> edges = new List<Segment>();
        public Bounds bounds;
        public bool Contains(Vector2 p)
        {
            if (p.x < bounds.min.x || p.x > bounds.max.x || p.y < bounds.min.z || p.y > bounds.max.z) return false;
            int winding = 0;
            foreach (var e in edges)
                if ((e.a.y > p.y) != (e.b.y > p.y) &&
                    p.x < (e.b.x - e.a.x) * (p.y - e.a.y) / (e.b.y - e.a.y) + e.a.x)
                    winding += e.b.y > e.a.y ? 1 : -1;
            // Nonzero winding also unions overlapping closed shells in a single
            // imported mesh, rather than cutting a parity hole through their join.
            return winding != 0;
        }
    }
    private struct Vertex
    {
        public Vector2 p; public float d;
        public Vertex(Vector2 p, float d) { this.p = p; this.d = d; }
    }

    [MenuItem("Tools/Open World/Shoreline Foam/Regenerate Shoreline Foam")]
    public static void Regenerate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
        { Debug.LogWarning("Exit Play Mode and open the world scene before baking foam."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path)) { Debug.LogError("Save the scene before baking foam."); return; }
        var terrains = new List<Terrain>(); var filters = new List<MeshFilter>();
        OceanWater ocean = null; ShorelineFoam previous = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (ocean == null) ocean = root.GetComponentInChildren<OceanWater>();
            if (previous == null) previous = root.GetComponentInChildren<ShorelineFoam>(true);
            terrains.AddRange(root.GetComponentsInChildren<Terrain>());
            filters.AddRange(root.GetComponentsInChildren<MeshFilter>());
        }
        if (ocean == null) { Debug.LogError("No OceanWater in the active scene."); return; }
        float level = ocean.BaseWaterLevel, width = previous != null ? previous.width : .65f;
        var segments = new List<Segment>(); var rocks = new List<Rock>();
        GameObject staging = null; string assetPath = null;
        var pendingMeshes = new List<Mesh>();
        try
        {
            terrains.RemoveAll(t => !t.isActiveAndEnabled || t.terrainData == null || Excluded(t.transform));
            for (int n = 0; n < terrains.Count; n++)
            {
                Progress("Reading terrain " + terrains[n].name, .1f);
                SliceTerrain(terrains[n], level, segments);
            }
            for (int n = 0; n < filters.Count; n++)
            {
                MeshFilter filter = filters[n];
                if (!IsRock(filter)) continue;
                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer.bounds.min.y > level || renderer.bounds.max.y < level) continue;
                Progress("Reading rock " + filter.name, .2f);
                var rock = new Rock { bounds = renderer.bounds };
                SliceMesh(filter, level, rock.edges);
                if (rock.edges.Count == 0) continue;
                rocks.Add(rock); segments.AddRange(rock.edges);
            }
            // Spatial bucket only the narrow shoreline band. Overlapping sources
            // contribute to one signed-distance field, not separate alpha layers.
            var tiles = new Dictionary<Vector2Int, List<Segment>>();
            float pad = width + Cell * 2;
            foreach (var s in segments)
            {
                Vector2 min = Vector2.Min(s.a, s.b) - Vector2.one * pad;
                Vector2 max = Vector2.Max(s.a, s.b) + Vector2.one * pad;
                for (int z = Mathf.FloorToInt(min.y / Tile); z <= Mathf.FloorToInt(max.y / Tile); z++)
                    for (int x = Mathf.FloorToInt(min.x / Tile); x <= Mathf.FloorToInt(max.x / Tile); x++)
                    {
                        var key = new Vector2Int(x, z);
                        if (!tiles.TryGetValue(key, out var bucket)) tiles.Add(key, bucket = new List<Segment>());
                        bucket.Add(s);
                    }
                if (tiles.Count > 20000) throw new InvalidOperationException("Bake exceeds 20,000 shoreline tiles. Check source bounds/scale.");
            }
            if (tiles.Count == 0) throw new InvalidOperationException("No shoreline found. Check water level and add ShorelineFoamSource to unusually named rocks.");
            staging = new GameObject("Shoreline Foam (baked, visual only)");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, scene);
            var controller = staging.AddComponent<ShorelineFoam>();
            controller.ocean = ocean; controller.width = width; controller.bakedWaterLevel = level;
            if (previous != null)
            { controller.cycleSeconds = previous.cycleSeconds; controller.fadeStart = previous.fadeStart; controller.fadeEnd = previous.fadeEnd; }
            var renderers = new List<MeshRenderer>(); int tileIndex = 0, triangles = 0;
            foreach (var tile in tiles)
            {
                Progress("Baking shoreline tile " + (++tileIndex) + " / " + tiles.Count, .25f + .65f * tileIndex / tiles.Count);
                Mesh mesh = BuildTile(tile.Key, tile.Value, rocks, terrains, level, width);
                if (mesh == null) continue;
                pendingMeshes.Add(mesh); triangles += (int)mesh.GetIndexCount(0) / 3;
                var child = new GameObject("Foam " + tile.Key.x + "," + tile.Key.y);
                child.transform.SetParent(staging.transform, false);
                child.transform.position = new Vector3(tile.Key.x * Tile, level, tile.Key.y * Tile);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderers.Add(renderer);
            }
            if (pendingMeshes.Count == 0) throw new InvalidOperationException("No exposed shoreline band was found.");
            Shader shader = Shader.Find("OpenWorld/ShorelineFoam");
            if (shader == null) throw new InvalidOperationException("Shoreline foam shader has not imported successfully.");
            // Keep earlier bake assets for scene Undo. All generated data is in a
            // single asset per bake; authored meshes/terrain/import settings stay intact.
            string directory = "Assets/_Game/Water/ShorelineBakes";
            Directory.CreateDirectory(directory); AssetDatabase.Refresh();
            assetPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + scene.name + "_Foam.asset");
            AssetDatabase.CreateAsset(pendingMeshes[0], assetPath);
            for (int i = 1; i < pendingMeshes.Count; i++) AssetDatabase.AddObjectToAsset(pendingMeshes[i], assetPath);
            var texture = CreateTexture(); AssetDatabase.AddObjectToAsset(texture, assetPath);
            var material = new Material(shader) { name = "Shared shoreline foam", mainTexture = texture };
            material.SetFloat("_BaseLevel", level); material.SetFloat("_FoamWidth", width);
            material.SetFloat("_CycleSeconds", controller.cycleSeconds);
            material.SetVector("_FadeRange", new Vector4(controller.fadeStart, controller.fadeEnd, 0, 0));
            AssetDatabase.AddObjectToAsset(material, assetPath);
            controller.foamMaterial = material; controller.chunks = renderers.ToArray();
            foreach (var renderer in renderers) renderer.sharedMaterial = material;
            AssetDatabase.SaveAssets();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Regenerate Shoreline Foam");
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            staging.SetActive(true); Undo.RegisterCreatedObjectUndo(staging, "Regenerate Shoreline Foam");
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = staging;
            Debug.Log($"[SHORELINE FOAM] Baked {rocks.Count} intersecting rock meshes and {terrains.Count} terrains into {renderers.Count} chunks / {triangles:N0} triangles. Save the scene and commit it plus {assetPath} and its .meta. No scenery or collision was changed.", staging);
            staging = null; assetPath = null; pendingMeshes.Clear();
        }
        catch (OperationCanceledException) { Debug.Log("Shoreline bake cancelled; previous foam retained."); }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (staging != null) Object.DestroyImmediate(staging);
            if (assetPath != null) AssetDatabase.DeleteAsset(assetPath);
            foreach (var mesh in pendingMeshes) if (mesh != null && !AssetDatabase.Contains(mesh)) Object.DestroyImmediate(mesh);
        }
    }

    private static void Progress(string text, float value)
    { if (EditorUtility.DisplayCancelableProgressBar("Regenerate Shoreline Foam", text, value)) throw new OperationCanceledException(); }

    private static bool Excluded(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.GetComponent<Rigidbody>() != null || t.GetComponent<BoatController>() != null ||
                t.GetComponent<OceanWater>() != null || t.GetComponent<ShorelineFoam>() != null) return true;
            var hint = t.GetComponent<ShorelineFoamSource>();
            if (hint != null && !hint.include) return true;
        }
        return false;
    }

    private static bool IsRock(MeshFilter filter)
    {
        var renderer = filter.GetComponent<MeshRenderer>();
        if (!filter.gameObject.activeInHierarchy || filter.sharedMesh == null || renderer == null || !renderer.enabled || Excluded(filter.transform)) return false;
        var lodGroup = filter.GetComponentInParent<LODGroup>();
        if (lodGroup != null)
        {
            var lods = lodGroup.GetLODs();
            for (int i = 1; i < lods.Length; i++)
                if (Array.IndexOf(lods[i].renderers, renderer) >= 0 &&
                    Array.IndexOf(lods[0].renderers, renderer) < 0) return false;
        }
        for (Transform t = filter.transform; t != null; t = t.parent)
            if (t.GetComponent<ShorelineFoamSource>() != null || HasRockName(t.name)) return true;
        return HasRockName(AssetDatabase.GetAssetPath(filter.sharedMesh));
    }
    private static bool HasRockName(string name)
    { return name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("cliff", StringComparison.OrdinalIgnoreCase) >= 0; }

    private static void SliceMesh(MeshFilter filter, float level, List<Segment> output)
    {
        // Editor mesh access does not require enabling Read/Write in player imports.
        var vertices = filter.sharedMesh.vertices;
        for (int i = 0; i < vertices.Length; i++) vertices[i] = filter.transform.TransformPoint(vertices[i]);
        var indices = filter.sharedMesh.triangles;
        for (int i = 0; i < indices.Length; i += 3)
            SliceTriangle(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]], level, output);
    }

    private static void SliceTerrain(Terrain terrain, float level, List<Segment> output)
    {
        TerrainData data = terrain.terrainData; int n = data.heightmapResolution;
        var h = data.GetHeights(0, 0, n, n); Vector3 o = terrain.transform.position, size = data.size;
        for (int z = 0; z < n - 1; z++)
        {
            if (z % 64 == 0) Progress("Slicing terrain " + terrain.name, .1f);
            for (int x = 0; x < n - 1; x++)
            {
                if (data.IsHole(x, z)) continue;
                float a = o.y + h[z, x] * size.y, b = o.y + h[z, x + 1] * size.y;
                float c = o.y + h[z + 1, x] * size.y, d = o.y + h[z + 1, x + 1] * size.y;
                if ((a > level && b > level && c > level && d > level) ||
                    (a < level && b < level && c < level && d < level)) continue;
                float x0 = o.x + x * size.x / (n - 1), x1 = o.x + (x + 1) * size.x / (n - 1);
                float z0 = o.z + z * size.z / (n - 1), z1 = o.z + (z + 1) * size.z / (n - 1);
                SliceTriangle(new Vector3(x0,a,z0),new Vector3(x0,c,z1),new Vector3(x1,b,z0),level,output);
                SliceTriangle(new Vector3(x1,b,z0),new Vector3(x0,c,z1),new Vector3(x1,d,z1),level,output);
            }
        }
    }

    private static void SliceTriangle(Vector3 a, Vector3 b, Vector3 c, float level, List<Segment> output)
    {
        Vector2 p = default, q = default; int count = 0;
        Cross(a,b,level,ref p,ref q,ref count); Cross(b,c,level,ref p,ref q,ref count); Cross(c,a,level,ref p,ref q,ref count);
        if (count == 2 && (p - q).sqrMagnitude > 1e-12f)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, Vector3.Cross(b-a,c-a));
            if (Vector2.Dot(q-p,new Vector2(tangent.x,tangent.z)) < 0)
            { Vector2 swap=p; p=q; q=swap; }
            output.Add(new Segment(p,q));
        }
    }
    private static void Cross(Vector3 a, Vector3 b, float level, ref Vector2 p, ref Vector2 q, ref int count)
    {
        if ((a.y > level) == (b.y > level)) return;
        Vector3 hit = Vector3.LerpUnclamped(a,b,(level-a.y)/(b.y-a.y));
        if (count++ == 0) p = new Vector2(hit.x,hit.z); else q = new Vector2(hit.x,hit.z);
    }

    private static bool Land(Vector2 p, List<Rock> rocks, List<Terrain> terrains, float level)
    {
        foreach (var rock in rocks) if (rock.Contains(p)) return true;
        foreach (var t in terrains)
        {
            Vector3 origin = t.transform.position, size = t.terrainData.size;
            float x = (p.x-origin.x)/size.x, z = (p.y-origin.z)/size.z;
            if (x < 0 || x > 1 || z < 0 || z > 1) continue;
            int n = t.terrainData.holesResolution;
            if (t.terrainData.IsHole(Mathf.Min(n-1,(int)(x*n)),Mathf.Min(n-1,(int)(z*n)))) continue;
            if (t.SampleHeight(new Vector3(p.x,0,p.y)) + origin.y >= level) return true;
        }
        return false;
    }

    private static Mesh BuildTile(Vector2Int tile, List<Segment> edges, List<Rock> rocks,
        List<Terrain> terrains, float level, float width)
    {
        Vector2 origin = new Vector2(tile.x * Tile, tile.y * Tile);
        var localRocks = rocks.FindAll(r => r.bounds.max.x >= origin.x && r.bounds.min.x <= origin.x+Tile && r.bounds.max.z >= origin.y && r.bounds.min.z <= origin.y+Tile);
        var field = new float[Cells+1,Cells+1];
        for (int z=0;z<=Cells;z++) for(int x=0;x<=Cells;x++)
        {
            Vector2 p=origin+new Vector2(x*Cell,z*Cell); float d=width+Cell*3;
            foreach(var edge in edges) d=Mathf.Min(d,edge.Distance(p));
            field[z,x]=Land(p,localRocks,terrains,level)?-Mathf.Max(.00001f,d):d;
        }
        var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var indices=new List<int>();
        for(int z=0;z<Cells;z++) for(int x=0;x<Cells;x++)
        {
            var a=new Vertex(new Vector2(x*Cell,z*Cell),field[z,x]);
            var b=new Vertex(new Vector2(x*Cell,(z+1)*Cell),field[z+1,x]);
            var c=new Vertex(new Vector2((x+1)*Cell,z*Cell),field[z,x+1]);
            var d=new Vertex(new Vector2((x+1)*Cell,(z+1)*Cell),field[z+1,x+1]);
            Emit(a,b,c,width,vertices,uv,indices); Emit(c,b,d,width,vertices,uv,indices);
        }
        if(vertices.Count==0)return null;
        var mesh=new Mesh {name="Shoreline "+tile.x+","+tile.y,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
        Bounds bounds=mesh.bounds; bounds.Expand(new Vector3(2,24,2)); mesh.bounds=bounds;
        return mesh;
    }

    private static void Emit(Vertex a,Vertex b,Vertex c,float width,List<Vector3> vertices,List<Vector2> uv,List<int> indices)
    {
        if(Mathf.Max(a.d,b.d,c.d)<=0 || Mathf.Min(a.d,b.d,c.d)>=width)return;
        var polygon=new List<Vertex>{a,b,c};
        polygon=Clip(polygon,0,true); polygon=Clip(polygon,width,false);
        int first=vertices.Count;
        foreach(var v in polygon){vertices.Add(new Vector3(v.p.x,0,v.p.y));uv.Add(new Vector2(v.d,0));}
        for(int i=1;i+1<polygon.Count;i++){indices.Add(first);indices.Add(first+i);indices.Add(first+i+1);}
    }
    private static List<Vertex> Clip(List<Vertex> input,float threshold,bool greater)
    {
        var output=new List<Vertex>(); if(input.Count==0)return output;
        Vertex a=input[input.Count-1]; bool insideA=greater?a.d>=threshold:a.d<=threshold;
        foreach(var b in input)
        {
            bool insideB=greater?b.d>=threshold:b.d<=threshold;
            if(insideA!=insideB)output.Add(new Vertex(Vector2.LerpUnclamped(a.p,b.p,(threshold-a.d)/(b.d-a.d)),threshold));
            if(insideB)output.Add(b); a=b; insideA=insideB;
        }
        return output;
    }

    private static Texture2D CreateTexture()
    {
        const int size=256; var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true)
        {name="Shoreline foam breakup 256",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,anisoLevel=1};
        var colors=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=(float)x/size,v=(float)y/size;
            float broad=Noise(u,v,7),fine=Noise(u,v,23);
            colors[y*size+x]=new Color(broad,fine,0,1);
        }
        texture.SetPixels32(colors); texture.Apply(true,true); return texture;
    }
    private static float Noise(float u,float v,float scale)
    {
        // Opposite edges blend the same noise values, including mip seams.
        float a=Mathf.PerlinNoise(u*scale,v*scale),b=Mathf.PerlinNoise((u-1)*scale,v*scale);
        float c=Mathf.PerlinNoise(u*scale,(v-1)*scale),d=Mathf.PerlinNoise((u-1)*scale,(v-1)*scale);
        return Mathf.Lerp(Mathf.Lerp(a,b,u),Mathf.Lerp(c,d,u),v);
    }

    [MenuItem("Tools/Open World/Shoreline Foam/Validate Baker Geometry")]
    public static void ValidateGeometry()
    {
        var sliced = new List<Segment>();
        SliceTriangle(new Vector3(0,-1,0),new Vector3(2,1,0),new Vector3(0,1,2),0,sliced);
        Require(sliced.Count==1 && sliced[0].Distance(new Vector2(.5f,.5f))<.0001f,"triangle plane intersection");
        sliced.Clear();
        SliceTriangle(Vector3.zero,Vector3.right,Vector3.forward,1,sliced);
        Require(sliced.Count==0,"fully submerged triangle");
        SliceTriangle(Vector3.zero,Vector3.right,Vector3.forward,0,sliced);
        Require(sliced.Count==0,"coplanar triangle is not an arbitrary shore");
        var rock = new Rock {bounds=new Bounds(Vector3.zero,new Vector3(2,2,2))};
        rock.edges.Add(new Segment(new Vector2(-1,-1),new Vector2(1,-1)));
        rock.edges.Add(new Segment(new Vector2(1,-1),new Vector2(1,1)));
        rock.edges.Add(new Segment(new Vector2(1,1),new Vector2(-1,1)));
        rock.edges.Add(new Segment(new Vector2(-1,1),new Vector2(-1,-1)));
        Require(rock.Contains(Vector2.zero) && !rock.Contains(new Vector2(2,0)),"closed rock inside/outside");
        rock.edges.AddRange(rock.edges.ToArray());
        Require(rock.Contains(Vector2.zero),"overlapping same-winding shells within one mesh");
        Require(Land(Vector2.zero,new List<Rock>{rock,rock},new List<Terrain>(),0),"overlapping rocks use union, not parity between objects");
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
        Emit(new Vertex(Vector2.zero,-1),new Vertex(Vector2.up,1),new Vertex(Vector2.right,1),.65f,vertices,uv,indices);
        Require(indices.Count>0,"shore band clipping produces triangles");
        foreach(var v in uv)Require(v.x>=-.00001f && v.x<=.65001f,"clipped vertices stay within the band");
        foreach(int index in indices)Require(index>=0 && index<vertices.Count,"mesh indices valid");
        for(int i=0;i<=20;i++)
        {
            float t=i/20f;
            Require(Mathf.Abs(Noise(0,t,7)-Noise(1,t,7))<.00001f &&
                Mathf.Abs(Noise(t,0,7)-Noise(t,1,7))<.00001f,"texture repeat seam");
        }
        Debug.Log("[SHORELINE FOAM] Geometry validation passed: plane cuts, closed shapes, overlapping-source union, band clipping, indices and repeating texture seams.");
    }
    private static void Require(bool condition,string name)
    {if(!condition)throw new InvalidOperationException("Shoreline foam validation failed: "+name);}
}
