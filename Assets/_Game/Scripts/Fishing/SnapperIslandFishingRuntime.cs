using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Restricts catches within 50 m of Snapper Island's coast to the three snapper
/// species currently in the game: Red Snapper, Yellowtail Snapper and Mutton Snapper.
/// Relative bait/lure preference is inherited from the existing Suncrest tuning and
/// renormalized across only those three species, so no non-snapper can be selected.
///
/// Weight rolls use each snapper's existing Suncrest min/max tuning. The dedicated
/// island also gets its own 30 m shoreline depth reference so the global shallow-cast
/// size/difficulty penalty remains consistent around this new island.
/// </summary>
[DefaultExecutionOrder(1500)]
public sealed class SnapperIslandFishingRuntime : MonoBehaviour
{
    private static readonly int[] SnapperSpecies={
        FishCatalog.RedSnapperId,
        FishCatalog.YellowtailSnapperId,
        FishCatalog.MuttonSnapperId
    };

    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly FieldInfo StateField=typeof(FishingSystem).GetField("state",Flags);
    private static readonly FieldInfo CastPointField=typeof(FishingSystem).GetField("castPoint",Flags);
    private static readonly FieldInfo PondCastField=typeof(FishingSystem).GetField("pondCast",Flags);
    private static readonly FieldInfo ActiveBaitField=typeof(FishingSystem).GetField("activeBait",Flags);
    private static readonly FieldInfo FightBiomeField=typeof(FishingSystem).GetField("fightBiome",Flags);
    private static readonly FieldInfo HookedSpeciesField=typeof(FishingSystem).GetField("hookedSpeciesId",Flags);
    private static readonly FieldInfo HookedWeightField=typeof(FishingSystem).GetField("hookedWeightKg",Flags);
    private static readonly FieldInfo FishMaxHealthField=typeof(FishingSystem).GetField("fishMaxHealth",Flags);
    private static readonly FieldInfo FishHealthPointsField=typeof(FishingSystem).GetField("fishHealthPoints",Flags);
    private static readonly FieldInfo FishHealthField=typeof(FishingSystem).GetField("fishHealth",Flags);
    private static readonly FieldInfo OceanWaterField=typeof(FishingSystem).GetField("oceanWater",Flags);

    private FishingSystem fishing;
    private Terrain terrain;
    private OceanWater ocean;
    private bool appliedToCast;
    private bool correctedCastQuality;
    private float snapperReferenceDepth=-1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if(system!=null && system.GetComponent<SnapperIslandFishingRuntime>()==null)
                system.gameObject.AddComponent<SnapperIslandFishingRuntime>();
    }

    private void Awake()
    {
        fishing=GetComponent<FishingSystem>();
        terrain=Terrain.activeTerrain;
        ocean=fishing!=null && OceanWaterField!=null?OceanWaterField.GetValue(fishing) as OceanWater:null;
        if(fishing==null || StateField==null || CastPointField==null || PondCastField==null ||
           ActiveBaitField==null || FightBiomeField==null || HookedSpeciesField==null ||
           HookedWeightField==null || FishMaxHealthField==null || FishHealthPointsField==null || FishHealthField==null)
        {
            Debug.LogError("SnapperIslandFishingRuntime could not bind FishingSystem fields and was disabled.");
            enabled=false;
        }
    }

    private void LateUpdate()
    {
        if(!enabled || fishing==null)return;
        if(terrain==null)terrain=Terrain.activeTerrain;
        if(ocean==null && OceanWaterField!=null)ocean=OceanWaterField.GetValue(fishing) as OceanWater;

        string state=StateName();
        if(state=="Idle" || state=="Charging" || state=="Casting" || state=="Waiting")
        {
            if(state!="Waiting")
            {
                appliedToCast=false;
                correctedCastQuality=false;
            }
            return;
        }

        if((bool)PondCastField.GetValue(fishing))return;
        Vector3 castPoint=(Vector3)CastPointField.GetValue(fishing);
        if(!SnapperIslandRuntime.Ready || !SnapperIslandGeometry.ContainsFishingWater(castPoint,SnapperIslandRuntime.Center))return;

        // Core FishingSystem has already selected a normal biome fish by this point.
        // Replace that result once, before the fight/cast-quality companion consumes it.
        if(!appliedToCast && (state=="Bite" || state=="Fighting"))
        {
            ApplySnapperCatch(state);
            appliedToCast=true;
        }
    }

    private void ApplySnapperCatch(string state)
    {
        int bait=(int)ActiveBaitField.GetValue(fishing);
        int species=RollSnapperSpecies(bait,UnityEngine.Random.value);

        // This island is adjacent to Suncrest, so its size distribution intentionally
        // uses the existing Suncrest min/max row for each snapper instead of inventing
        // a second hidden balance table.
        float weight;
        FishingTuning.RememberBiome(0);
        if(!FishingTuning.TryRollWeight(species,0,UnityEngine.Random.value,out weight))
        {
            FishSpeciesDefinition definition=FishCatalog.Get(species);
            weight=Mathf.Lerp(definition.MinWeightKg,definition.MaxWeightKg,UnityEngine.Random.value);
        }

        HookedSpeciesField.SetValue(fishing,species);
        HookedWeightField.SetValue(fishing,weight);
        // Keep core fight logic on a safe existing biome ID; species selection is now
        // authoritative in this companion and the 50 m island boundary is cast-based.
        FightBiomeField.SetValue(fishing,0);

        if(state=="Fighting")
            ResetFightHealth(species,weight);

        Debug.Log("[SNAPPER ISLAND CATCH] "+FishCatalog.Get(species).Name+" / "+
                  FishCatalog.FormatWeight(weight)+" / within 50 m of Snapper Island shore.");
    }

    private static int RollSnapperSpecies(int bait,float random01)
    {
        float total=0f;
        float[] weights=new float[SnapperSpecies.Length];
        for(int i=0;i<SnapperSpecies.Length;i++)
        {
            weights[i]=Mathf.Max(0f,ReefCatalog.EquippedChance(SnapperSpecies[i],bait,0));
            total+=weights[i];
        }

        if(total<=0.0001f)
            return SnapperSpecies[Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(random01)*SnapperSpecies.Length),0,SnapperSpecies.Length-1)];

        float pick=Mathf.Clamp01(random01)*total;
        for(int i=0;i<SnapperSpecies.Length;i++)
        {
            pick-=weights[i];
            if(pick<0f)return SnapperSpecies[i];
        }
        return SnapperSpecies[SnapperSpecies.Length-1];
    }

    private void ResetFightHealth(int species,float weight)
    {
        int hp;
        if(!FishingTuning.TryGetHealth(species,weight,out hp))
        {
            FishSpeciesDefinition definition=FishCatalog.Get(species);
            hp=Mathf.Max(1,Mathf.RoundToInt(Mathf.Lerp(35f,280f,
                Mathf.InverseLerp(definition.MinWeightKg,definition.MaxWeightKg,weight))));
        }
        FishMaxHealthField.SetValue(fishing,hp);
        FishHealthPointsField.SetValue(fishing,hp);
        FishHealthField.SetValue(fishing,1f);
    }

    private string StateName()
    {
        object state=StateField.GetValue(fishing);
        return state!=null?state.ToString():string.Empty;
    }

    /// <summary>
    /// Called from the later companion below once FishingCastQualityRuntime has applied
    /// its normal biome result. Replaces that result with this island's own shoreline
    /// depth reference instead of leaving Snapper Island classified as Deep Ocean.
    /// </summary>
    internal bool TryCorrectCastQuality()
    {
        if(correctedCastQuality || !appliedToCast || StateName()!="Fighting")return false;
        Vector3 castPoint=(Vector3)CastPointField.GetValue(fishing);
        if(!SnapperIslandRuntime.Ready || !SnapperIslandGeometry.ContainsFishingWater(castPoint,SnapperIslandRuntime.Center))return false;

        FishingCastQualityRuntime qualityRuntime=GetComponent<FishingCastQualityRuntime>();
        if(qualityRuntime==null || qualityRuntime.OriginalWeight<=0f)return false;
        if(terrain==null || ocean==null)return false;

        float reference=ReferenceDepth();
        float castDepth=StableWaterDepth(castPoint);
        float ratio=reference>0.001f?Mathf.Clamp01(castDepth/reference):1f;
        float sizeQuality=FishingCastQualityRuntime.QualityFromDepthRatio(ratio);

        int species=(int)HookedSpeciesField.GetValue(fishing);
        float originalWeight=qualityRuntime.OriginalWeight;
        float adjustedWeight=Mathf.Max(.001f,originalWeight*sizeQuality);
        HookedWeightField.SetValue(fishing,adjustedWeight);

        int baseHp;
        if(!FishingTuning.TryGetHealth(species,originalWeight,out baseHp))
            baseHp=Mathf.Max(1,(int)FishMaxHealthField.GetValue(fishing));
        int adjustedHp=Mathf.Max(1,Mathf.RoundToInt(baseHp*FishingCastQualityRuntime.FightQuality(sizeQuality)));
        FishMaxHealthField.SetValue(fishing,adjustedHp);
        FishHealthPointsField.SetValue(fishing,adjustedHp);
        FishHealthField.SetValue(fishing,1f);

        correctedCastQuality=true;
        Debug.Log("[SNAPPER ISLAND CAST QUALITY] depth="+castDepth.ToString("0.00")+"m / shore30mRef="+
                  reference.ToString("0.00")+"m / quality="+(sizeQuality*100f).ToString("0")+"% / weight="+
                  originalWeight.ToString("0.###")+"->"+adjustedWeight.ToString("0.###")+"kg / HP="+adjustedHp);
        return true;
    }

    private float ReferenceDepth()
    {
        if(snapperReferenceDepth>0.001f)return snapperReferenceDepth;
        float best=0f;
        const int directions=96;
        for(int i=0;i<directions;i++)
        {
            float angle=i*Mathf.PI*2f/directions;
            Vector3 edge=SnapperIslandRuntime.Center+new Vector3(
                Mathf.Cos(angle)*SnapperIslandGeometry.RadiusX,0f,
                Mathf.Sin(angle)*SnapperIslandGeometry.RadiusZ);
            Vector3 outward=edge-SnapperIslandRuntime.Center;
            outward.y=0f;
            outward.Normalize();
            for(float d=0f;d<=30.01f;d+=1f)
            {
                Vector3 sample=edge+outward*d;
                best=Mathf.Max(best,StableWaterDepth(sample));
            }
        }
        snapperReferenceDepth=Mathf.Max(1f,best);
        return snapperReferenceDepth;
    }

    private float StableWaterDepth(Vector3 position)
    {
        if(terrain==null || ocean==null)return 0f;
        TerrainData data=terrain.terrainData;
        Vector3 local=position-terrain.transform.position;
        if(local.x<0f || local.z<0f || local.x>data.size.x || local.z>data.size.z)return 0f;
        float ground=terrain.SampleHeight(position)+terrain.transform.position.y;
        return Mathf.Max(0f,ocean.BaseWaterLevel-ground);
    }
}

/// <summary>Runs after FishingCastQualityRuntime so Snapper Island can replace its fallback Deep Ocean reference.</summary>
[DefaultExecutionOrder(2100)]
public sealed class SnapperIslandCastQualityRuntime : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if(system!=null && system.GetComponent<SnapperIslandCastQualityRuntime>()==null)
                system.gameObject.AddComponent<SnapperIslandCastQualityRuntime>();
    }

    private SnapperIslandFishingRuntime rules;
    private void Awake(){rules=GetComponent<SnapperIslandFishingRuntime>();}
    private void LateUpdate()
    {
        if(rules==null)rules=GetComponent<SnapperIslandFishingRuntime>();
        if(rules!=null)rules.TryCorrectCastQuality();
    }
}
