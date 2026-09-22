using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SeaBassImporter
{
    private const string Folder="Assets/_Game/Reef/Source";
    public const string PrefabPath="Assets/Resources/Fishing/SeaBass.prefab";
    public static void Install()
    {
        const string fbx=Folder+"/SeaBass.fbx", png=Folder+"/SeaBassTexture.png";
        AssetDatabase.ImportAsset(fbx,ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(png,ImportAssetOptions.ForceSynchronousImport);
        var textureImporter=AssetImporter.GetAtPath(png) as TextureImporter;
        if(textureImporter==null)throw new InvalidOperationException("Sea bass texture missing. Pull the complete Suncrest Reef update.");
        textureImporter.textureType=TextureImporterType.Default;
        textureImporter.sRGBTexture=true;textureImporter.mipmapEnabled=true;textureImporter.maxTextureSize=2048;
        textureImporter.textureCompression=TextureImporterCompression.Compressed;
        textureImporter.SaveAndReimport();
        var importer=AssetImporter.GetAtPath(fbx) as ModelImporter;
        if(importer==null)throw new InvalidOperationException("SeaBass.fbx was not imported.");
        importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Generic;
        importer.importCameras=false;importer.importLights=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.optimizeGameObjects=false;importer.isReadable=true;importer.SaveAndReimport();
        var take=importer.defaultClipAnimations.OrderByDescending(c=>c.lastFrame-c.firstFrame).FirstOrDefault();
        if(take==null)throw new InvalidOperationException("Sea bass authored animation take is missing.");
        take.name="Swim";take.loopTime=true;take.loopPose=true;
        importer.clipAnimations=new[]{take};importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c=>c.name=="Swim");
        string controllerPath=Folder+"/SeaBass.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine=controller.layers[0].stateMachine;
        var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Swim")??machine.AddState("Swim");
        state.motion=clip;machine.defaultState=state;EditorUtility.SetDirty(controller);
        string matPath=Folder+"/SeaBass.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(material==null)
        {
            var shader=Resources.Load<Shader>("Fishing/FishingEquipment");
            if(shader==null)throw new InvalidOperationException("FishingEquipment shader missing.");
            material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);
        }
        // Explicit atlas sampling uses the same verified shader as the authored rod.
        material.shader=Resources.Load<Shader>("Fishing/FishingEquipment");
        if(material.shader==null)throw new InvalidOperationException("FishingEquipment shader missing.");
        var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        if(atlas==null)throw new InvalidOperationException("Sea bass atlas could not be loaded.");
        material.SetTexture("_BaseMap",atlas);
        material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",0.4f);material.SetFloat("_Metallic",0.06f);
        EditorUtility.SetDirty(material);
        var root=new GameObject("SeaBass");
        try
        {
            var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx),visual.transform,false);
            model.name="AuthoredModel";
            if(PrefabUtility.IsPartOfPrefabInstance(model))
                PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials=Enumerable.Repeat(material,Mathf.Max(1,renderer.sharedMaterials.Length)).ToArray();
            var animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
            var head=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Bone");
            var tail=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Bone.004");
            if(head==null||tail==null)throw new InvalidOperationException("Sea bass five-bone rig is missing.");
            var forward=(head.position-tail.position).normalized;
            var up=Vector3.ProjectOnPlane(model.transform.up,forward).normalized;
            if(up.sqrMagnitude<0.01f)up=Vector3.up;
            visual.transform.rotation=Quaternion.Inverse(Quaternion.LookRotation(forward,up))*visual.transform.rotation;
            var bounds=BoundsOf(root);
            visual.transform.localScale*=0.92f/Mathf.Max(0.001f,bounds.size.z);
            bounds=BoundsOf(root);visual.transform.position+=root.transform.position-bounds.center;
            // Reuse the proven Red Snapper controller: aquarium trail updates,
            // held-fish mouth anchors, and ambient trails already support it.
            root.AddComponent<RedSnapperPresentation>().Configure(animator);
            // Bind an actual lip vertex to the animated head, instead of projecting
            // the snapper's longer snout beyond this model's mouth.
            var points=new System.Collections.Generic.List<Vector3>();
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(skin.sharedMesh.uv.Length==0)throw new InvalidOperationException("Sea bass UV0 missing.");
                // Evaluate skinning explicitly in world space. This avoids FBX
                // unit-scale ambiguity when baking a scaled renderer hierarchy.
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                var bind=mesh.bindposes;var bones=skin.bones;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];Vector3 world=Vector3.zero;
                    int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                    float[] values={w.weight0,w.weight1,w.weight2,w.weight3};
                    for(int j=0;j<4;j++)if(values[j]>0)
                        world+=(bones[ids[j]].localToWorldMatrix*bind[ids[j]]).MultiplyPoint3x4(vertices[i])*values[j];
                    points.Add(root.transform.InverseTransformPoint(world));
                }
            }
            if(points.Count==0)throw new InvalidOperationException("Sea bass skinned mesh missing.");
            float front=points.Max(p=>p.z),back=points.Min(p=>p.z);
            var lips=points.Where(p=>p.z>=front-(front-back)*.012f).ToArray();
            Vector3 lip=Vector3.zero;foreach(var p in lips)lip+=p;lip/=lips.Length;
            var mouth=new GameObject("AuthoredMouthAnchor").transform;
            mouth.SetParent(head,false);mouth.position=root.transform.TransformPoint(lip);
            if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
            if(!AssetDatabase.IsValidFolder("Assets/Resources/Fishing"))AssetDatabase.CreateFolder("Assets/Resources","Fishing");
            var saved=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            AssetDatabase.SaveAssets();
            if(saved==null || !AssetDatabase.GetDependencies(PrefabPath,true).Contains(png))
                throw new InvalidOperationException("Saved sea bass prefab lost its texture dependency.");
            foreach(var renderer in saved.GetComponentsInChildren<Renderer>(true))
                foreach(var mat in renderer.sharedMaterials)
                    if(mat==null || mat.GetTexture("_BaseMap")!=atlas)
                        throw new InvalidOperationException("Saved sea bass material is missing the authored atlas.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
    }
    private static Bounds BoundsOf(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)throw new InvalidOperationException("Sea bass mesh missing.");
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
    }
}
