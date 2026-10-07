using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Authored-style modular rock pass for Snapper Island.
/// Builds a few dense formations from the six supplied FBX rocks instead of
/// scattering isolated props across the sand.
/// </summary>
public sealed class SnapperIslandRockscape : MonoBehaviour
{
    private const string Root="Islands/SnapperIsland/Rocks/";

    private readonly GameObject[] rocks=new GameObject[6];
    private Terrain terrain;
    private float sea;
    private Vector3 center;
    private Vector3 arrival;
    private Transform holder;
    private Material material;
    private System.Random rng;

    private struct RockSpec
    {
        public int asset;
        public Vector3 localPosition;
        public Vector3 localEuler;
        public Vector3 localScale;
        public float bury;
        public bool collider;
        public bool shadow;
        public float submerged;

        public RockSpec(int rock,float x,float y,float z,float pitch,float yaw,float roll,
            float sx,float sy,float sz,float burial,bool collision=true,bool castShadow=true,float submerge=-1f)
        {
            asset=rock;
            localPosition=new Vector3(x,y,z);
            localEuler=new Vector3(pitch,yaw,roll);
            localScale=new Vector3(sx,sy,sz);
            bury=burial;
            collider=collision;
            shadow=castShadow;
            submerged=submerge;
        }
    }

    public bool Build(Terrain islandTerrain,float waterLevel,Vector3 islandCenter,Vector3 arrivalPoint)
    {
        terrain=islandTerrain;
        sea=waterLevel;
        center=islandCenter;
        arrival=arrivalPoint;
        rng=new System.Random(77341);

        for(int i=0;i<rocks.Length;i++)
        {
            rocks[i]=Resources.Load<GameObject>(Root+"Rock"+(i+1));
            if(rocks[i]==null)
            {
                Debug.LogError("[SNAPPER ROCKS] Missing Rock"+(i+1)+" modular FBX.");
                return false;
            }
        }

        material=Resources.Load<Material>(Root+"WorldRock");
        Texture2D atlas=Resources.Load<Texture2D>(Root+"WeatheredRockAtlas");
        Shader projectionShader=Resources.Load<Shader>(Root+"SnapperRockBoxProjection");
        if(material==null || atlas==null || projectionShader==null)
        {
            Debug.LogError("[SNAPPER ROCKS] Weathered rock material, atlas or projection shader is missing.");
            return false;
        }

        // One shared material for every duplicate. The box projection keeps the
        // texture scale consistent even when the six source meshes are rotated/scaled.
        material.shader=projectionShader;
        material.SetTexture("_BaseMap",atlas);
        material.SetColor("_BaseColor",new Color(.90f,.88f,.84f,1f));
        material.SetFloat("_Tiling",.22f);
        material.SetFloat("_Smoothness",.16f);
        material.SetFloat("_AmbientLift",.16f);
        material.enableInstancing=true;

        holder=new GameObject("Snapper Rock Formations").transform;
        holder.SetParent(transform,false);
        holder.gameObject.isStatic=true;

        BuildMainRidge();
        BuildSecondaryPeaks();
        BuildSteppedSlopes();
        BuildShoreline();
        BuildSeamDetails();

        Physics.SyncTransforms();
        return true;
    }

    private void BuildMainRidge()
    {
        // Three connected 5-7 rock masses form one broad rear-biased ridge.
        // Their overlap hides the flat source silhouettes and gives the island
        // a single dominant high point with broken cliff faces.
        Formation("Main Crown",new Vector2(2f,-11f),12f,1.10f,new[]
        {
            R(1,-2.4f,0f, .2f,  5f,-18f,-5f,1.28f,1.20f,1.30f,.34f,true),
            R(4, 1.9f,0f,-.5f, -6f, 48f, 7f,1.30f,1.20f,1.26f,.36f,true),
            R(5, 0f, .2f,1.7f,  4f,112f,-7f,1.24f,1.16f,1.28f,.38f,true),
            R(0,-.7f,1.9f,-.4f,-8f, 24f, 6f,1.28f,1.30f,1.22f,.40f,false),
            R(2, .9f,3.7f,-.1f, 6f,152f,-5f,1.18f,1.28f,1.16f,.42f,false),
            R(3,-2.0f,1.2f,1.0f, 8f,232f, 8f,1.08f,1.10f,1.14f,.40f,false),
            R(5, 2.1f,1.1f,1.0f,-5f,305f,-6f,1.02f,1.06f,1.10f,.39f,false)
        });

        Formation("Main West Shoulder",new Vector2(-8f,-7f),-20f,1.03f,new[]
        {
            R(0,-2.0f,0f, .2f,  7f,-20f,-7f,1.20f,1.12f,1.22f,.36f,true),
            R(4, 1.5f,0f,-.3f, -7f, 42f, 6f,1.25f,1.12f,1.18f,.37f,true),
            R(1,-.2f,.3f,1.6f,  5f,108f,-6f,1.16f,1.08f,1.22f,.39f,true),
            R(3,-1.0f,1.6f,-.8f,-6f,174f, 7f,1.10f,1.14f,1.08f,.41f,false),
            R(5, 1.4f,1.3f,.8f,  8f,246f,-8f,1.04f,1.06f,1.12f,.40f,false),
            R(2, .1f,2.8f,-.2f,-5f,316f, 5f,.92f,1.08f,.96f,.42f,false)
        });

        Formation("Main East Shoulder",new Vector2(11f,-9f),28f,1.02f,new[]
        {
            R(4,-1.8f,0f, .4f,  7f,-12f,-5f,1.22f,1.12f,1.20f,.36f,true),
            R(1, 1.7f,0f,-.4f, -6f, 54f, 7f,1.20f,1.10f,1.24f,.37f,true),
            R(5, .2f,.2f,1.5f,  5f,126f,-7f,1.14f,1.06f,1.20f,.39f,true),
            R(0,-1.1f,1.5f,-.7f,-7f,190f, 6f,1.04f,1.14f,1.10f,.40f,false),
            R(3, 1.3f,1.4f,.8f,  8f,252f,-6f,1.02f,1.06f,1.08f,.40f,false),
            R(2, .3f,2.8f,-.1f,-5f,324f, 5f,.90f,1.06f,.94f,.42f,false)
        });
    }

    private void BuildSecondaryPeaks()
    {
        Formation("West Peak",new Vector2(-27f,-6f),-32f,1.04f,new[]
        {
            R(0,-1.5f,0f,0f, 7f,-15f,-5f,1.20f,1.10f,1.22f,.36f,true),
            R(4, 1.4f,0f,.2f,-6f, 53f, 7f,1.18f,1.08f,1.18f,.37f,true),
            R(5, 0f,.3f,1.4f, 5f,123f,-7f,1.10f,1.05f,1.16f,.39f,true),
            R(3,-.5f,1.6f,-.5f,-7f,206f, 6f,1.02f,1.08f,1.04f,.41f,false),
            R(2, .6f,2.7f,-.1f, 6f,304f,-5f,.90f,1.02f,.92f,.42f,false)
        });

        Formation("East Peak",new Vector2(29f,-9f),31f,1.03f,new[]
        {
            R(1,-1.5f,0f,0f,-6f,-12f, 6f,1.20f,1.08f,1.22f,.36f,true),
            R(4, 1.5f,0f,.1f, 7f, 58f,-6f,1.17f,1.08f,1.17f,.37f,true),
            R(5, 0f,.2f,1.4f,-5f,130f, 7f,1.10f,1.04f,1.15f,.39f,true),
            R(0,-.7f,1.5f,-.6f, 6f,212f,-5f,1.00f,1.08f,1.02f,.41f,false),
            R(2, .6f,2.6f,-.2f,-5f,310f, 5f,.89f,1.02f,.91f,.42f,false)
        });
    }

    private void BuildSteppedSlopes()
    {
        // Medium formations step down from the high ridge toward the coast.
        Formation("West Step A",new Vector2(-20f,7f),-18f,.96f,StepSetA());
        Formation("West Step B",new Vector2(-36f,-18f),-44f,.90f,StepSetB());
        Formation("East Step A",new Vector2(20f,7f),24f,.95f,StepSetB());
        Formation("East Step B",new Vector2(36f,-19f),42f,.90f,StepSetA());
        Formation("South Step",new Vector2(-10f,-29f),8f,.88f,StepSetB());
    }

    private void BuildShoreline()
    {
        ShoreFormation("West Headland",new Vector2(-49f,2f),-78f,1.00f);
        ShoreFormation("East Headland",new Vector2(49f,-7f),76f,.98f);
        ShoreFormation("Southwest Headland",new Vector2(-37f,-33f),-32f,.94f);
        ShoreFormation("Southeast Headland",new Vector2(37f,-32f),34f,.94f);
        ShoreFormation("Northwest Rocks",new Vector2(-40f,25f),-112f,.84f);
        ShoreFormation("Northeast Rocks",new Vector2(40f,24f),112f,.82f);
    }

    private void BuildSeamDetails()
    {
        // Only a few small pieces: these fill visible seams at the bases of
        // formations instead of creating a uniform ring of individual rocks.
        Detail(-15f,-18f,5,.88f,18f);
        Detail( 17f,-20f,3,.84f,202f);
        Detail(-31f,  9f,6,.80f,72f);
        Detail( 31f, 10f,4,.82f,286f);
        Detail(-45f,-18f,5,.76f,124f,.25f);
        Detail( 45f,-20f,3,.74f,238f,.32f);
        Detail(-26f,-35f,6,.78f,308f,.28f);
        Detail( 26f,-36f,4,.76f,48f,.35f);
    }

    private RockSpec[] StepSetA()
    {
        return new[]
        {
            R(1,-1.4f,0f,0f, 6f,-12f,-6f,1.10f,1.02f,1.12f,.38f,true),
            R(4, 1.2f,0f,.2f,-6f, 58f, 7f,1.05f,1.00f,1.10f,.39f,true),
            R(5, 0f,.1f,1.2f, 5f,128f,-6f,.96f,.96f,1.02f,.40f,false),
            R(3,-.3f,1.0f,-.5f,-5f,218f, 6f,.88f,.96f,.92f,.42f,false)
        };
    }

    private RockSpec[] StepSetB()
    {
        return new[]
        {
            R(4,-1.3f,0f,.1f,-6f,-18f, 7f,1.08f,1.00f,1.10f,.38f,true),
            R(1, 1.3f,0f,-.1f, 7f, 52f,-6f,1.04f,1.00f,1.08f,.39f,true),
            R(5, 0f,.2f,1.2f,-5f,122f, 6f,.95f,.96f,1.00f,.40f,false),
            R(3, .2f,1.0f,-.5f, 5f,228f,-5f,.86f,.94f,.90f,.42f,false)
        };
    }

    private void ShoreFormation(string name,Vector2 offset,float yaw,float scale)
    {
        Formation(name,offset,yaw,scale,new[]
        {
            R(3,-1.0f,0f, .1f, 6f,-10f,-7f,1.05f,.98f,1.08f,.32f,true,true,.18f),
            R(5, 1.1f,-.2f,-.2f,-6f, 72f, 7f,.96f,.92f,1.02f,.34f,false,true,.34f),
            R(4, .1f,-.3f,1.2f, 5f,158f,-6f,.88f,.90f,.94f,.36f,false,false,.46f)
        });
    }

    private void Formation(string name,Vector2 offset,float yaw,float scale,RockSpec[] pieces)
    {
        Vector3 root=center+new Vector3(offset.x,0f,offset.y);
        if(IsArrivalClear(root,13f))return;

        Transform formation=new GameObject(name).transform;
        formation.SetParent(holder,false);
        formation.position=root;
        formation.rotation=Quaternion.Euler(0f,yaw,0f);
        formation.gameObject.isStatic=true;

        float baseGround=Ground(root);
        for(int i=0;i<pieces.Length;i++)
            PlacePiece(formation,baseGround,scale,pieces[i],i);
    }

    private void PlacePiece(Transform formation,float baseGround,float formationScale,RockSpec spec,int index)
    {
        Vector3 worldOffset=formation.rotation*new Vector3(spec.localPosition.x,0f,spec.localPosition.z);
        Vector3 p=formation.position+worldOffset;
        float terrainGround=Ground(p);
        float targetShelf=Mathf.Max(terrainGround,baseGround+spec.localPosition.y*formationScale);

        GameObject go=Instantiate(rocks[Mathf.Clamp(spec.asset,0,rocks.Length-1)],holder);
        go.name=formation.name+" Rock "+(spec.asset+1)+"."+index;

        Vector3 jitter=new Vector3(N(.97f,1.03f),N(.98f,1.03f),N(.97f,1.03f));
        Vector3 s=Vector3.Scale(spec.localScale,jitter)*formationScale;
        go.transform.localScale=s;
        go.transform.rotation=formation.rotation*Quaternion.Euler(spec.localEuler);
        go.transform.position=new Vector3(p.x,targetShelf,p.z);

        Renderer[] renderers=go.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length==0)
        {
            Destroy(go);
            return;
        }

        Bounds bounds=renderers[0].bounds;
        for(int i=0;i<renderers.Length;i++)
        {
            Renderer r=renderers[i];
            r.sharedMaterial=material;
            r.shadowCastingMode=spec.shadow?ShadowCastingMode.On:ShadowCastingMode.Off;
            r.receiveShadows=true;
            r.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
            if(i>0)bounds.Encapsulate(r.bounds);
        }

        float targetBottom=targetShelf-bounds.size.y*Mathf.Clamp(spec.bury,.18f,.52f);
        if(spec.submerged>=0f)
            targetBottom=Mathf.Max(targetBottom,sea-bounds.size.y*Mathf.Clamp(spec.submerged,.20f,.50f));
        go.transform.position+=Vector3.up*(targetBottom-bounds.min.y);

        foreach(Collider c in go.GetComponentsInChildren<Collider>(true))Destroy(c);
        if(spec.collider)AddSimpleCollider(go);
        SetStatic(go.transform);
    }

    private void Detail(float x,float z,int asset,float scale,float yaw,float submerged=-1f)
    {
        Vector3 p=center+new Vector3(x,0f,z);
        if(IsArrivalClear(p,12f))return;

        Transform detailRoot=new GameObject("Rock Seam Detail").transform;
        detailRoot.SetParent(holder,false);
        detailRoot.position=p;
        detailRoot.rotation=Quaternion.Euler(0f,yaw,0f);
        detailRoot.gameObject.isStatic=true;

        RockSpec spec=R(asset,0f,0f,0f,N(-9f,9f),N(-12f,12f),N(-9f,9f),
            scale,scale*N(.96f,1.04f),scale*N(.96f,1.05f),.39f,false,scale>.80f,submerged);
        PlacePiece(detailRoot,Ground(p),1f,spec,0);
    }

    private static RockSpec R(int asset,float x,float y,float z,float pitch,float yaw,float roll,
        float sx,float sy,float sz,float bury,bool collider=true,bool shadow=true,float submerged=-1f)
    {
        return new RockSpec(asset,x,y,z,pitch,yaw,roll,sx,sy,sz,bury,collider,shadow,submerged);
    }

    private static void AddSimpleCollider(GameObject go)
    {
        MeshFilter filter=go.GetComponentInChildren<MeshFilter>();
        if(filter==null || filter.sharedMesh==null)return;

        BoxCollider collider=filter.gameObject.AddComponent<BoxCollider>();
        Bounds b=filter.sharedMesh.bounds;
        collider.center=b.center;
        collider.size=new Vector3(
            Mathf.Max(.65f,b.size.x*.78f),
            Mathf.Max(.65f,b.size.y*.82f),
            Mathf.Max(.65f,b.size.z*.78f));
    }

    private float Ground(Vector3 p)
    {
        return terrain.SampleHeight(p)+terrain.transform.position.y;
    }

    private bool IsArrivalClear(Vector3 p,float radius)
    {
        return Vector3.ProjectOnPlane(p-arrival,Vector3.up).sqrMagnitude<radius*radius;
    }

    private static void SetStatic(Transform t)
    {
        t.gameObject.isStatic=true;
        for(int i=0;i<t.childCount;i++)SetStatic(t.GetChild(i));
    }

    private float N(float min,float max)
    {
        return Mathf.Lerp(min,max,(float)rng.NextDouble());
    }
}
