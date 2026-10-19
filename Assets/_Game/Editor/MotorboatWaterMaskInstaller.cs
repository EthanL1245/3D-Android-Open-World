using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only, bounded-cost water masking. No mesh work or Update runs on phones.
// One 12-triangle mesh, one unlit stencil draw, no textures/RT/camera.
internal static class MotorboatWaterMaskInstaller
{
    private const string MaskName = "Motorboat interior water mask v1";
    private const string MeshPath = "Assets/Resources/Boats/MotorboatWaterMask.asset";
    private const string MaterialPath = "Assets/Resources/Boats/MotorboatWaterMask.mat";
    private const string ShaderPath = "Assets/_Game/Water/Shaders/BoatWaterMask.shader";

    internal static void Install(string prefabPath)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(prefab==null || prefab.transform.Find(MaskName)!=null)return;
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if(shader==null)throw new InvalidOperationException("Boat water-mask shader is missing.");
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var imported=root.transform.Find("ModelContainer/ImportedMotorboat");
            if(imported==null)throw new InvalidOperationException("Motorboat model is missing.");
            // Use the actual named interior meshes, not the outer hull's bounding
            // box: a broad outer mask would remove water beside the boat as well.
            var floor=InteriorBounds(imported,root.transform,"Cockpit_Recess_Floor");
            var walls=InteriorBounds(imported,root.transform,"Cockpit_Recess_Inner_Walls");
            float bottom=floor.max.y+0.01f;
            float top=walls.max.y-0.02f;
            if(top<=bottom)throw new InvalidOperationException("Motorboat cockpit has no maskable interior depth.");
            // Two inset rings make a tapered, closed volume. Its floor is just
            // above the real floor, so the depth test also works from inside it.
            // The mask inherits the boat's scale and all local/network tilt poses.
            var vertices=new List<Vector3>(8);
            AddRing(vertices,floor,bottom,0.005f);
            AddRing(vertices,walls,top,0.035f);
            var indices=new List<int>(36);
            for(int edge=0;edge<4;edge++)
                Quad(indices,edge,(edge+1)%4,4+(edge+1)%4,4+edge);
            Quad(indices,3,2,1,0);
            Quad(indices,4,5,6,7);

            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool newMesh=mesh==null;
            if(newMesh)mesh=new Mesh();
            else mesh.Clear();
            mesh.name="Motorboat interior water mask";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices,0);
            mesh.RecalculateBounds();
            if(newMesh)AssetDatabase.CreateAsset(mesh,MeshPath);
            else EditorUtility.SetDirty(mesh);
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null)
            {
                material=new Material(shader) { name="Motorboat water stencil" };
                AssetDatabase.CreateAsset(material,MaterialPath);
            }
            var mask=new GameObject(MaskName,typeof(MeshFilter),typeof(MeshRenderer));
            mask.transform.SetParent(root.transform,false);
            mask.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=mask.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
            // No collider: the mask is purely visual and cannot stop a boat/player.
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Motorboat water mask installed: " + indices.Count/3 + " triangles, one stencil draw.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Bounds InteriorBounds(Transform imported,Transform root,string objectName)
    {
        foreach(var filter in imported.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!filter.name.StartsWith(objectName,StringComparison.OrdinalIgnoreCase) || filter.sharedMesh==null)continue;
            var bounds=filter.sharedMesh.bounds;
            Matrix4x4 matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            var local=new Bounds(matrix.MultiplyPoint3x4(bounds.center),Vector3.zero);
            for(int x=-1;x<=1;x+=2)
            for(int y=-1;y<=1;y+=2)
            for(int z=-1;z<=1;z+=2)
                local.Encapsulate(matrix.MultiplyPoint3x4(bounds.center+
                    Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
            return local;
        }
        throw new InvalidOperationException("Missing named motorboat interior mesh: " + objectName);
    }

    private static void AddRing(List<Vector3> vertices,Bounds bounds,float y,float inset)
    {
        if(bounds.size.x<=2f*inset || bounds.size.z<=2f*inset)
            throw new InvalidOperationException("Motorboat interior is too narrow for its mask.");
        vertices.Add(new Vector3(bounds.min.x+inset,y,bounds.min.z+inset));
        vertices.Add(new Vector3(bounds.max.x-inset,y,bounds.min.z+inset));
        vertices.Add(new Vector3(bounds.max.x-inset,y,bounds.max.z-inset));
        vertices.Add(new Vector3(bounds.min.x+inset,y,bounds.max.z-inset));
    }

    private static void Quad(List<int> indices,int a,int b,int c,int d)
    {
        indices.Add(a);indices.Add(b);indices.Add(c);
        indices.Add(a);indices.Add(c);indices.Add(d);
    }
}
