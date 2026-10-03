using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Ensures Snapper Island replaces the core biome catch during Update, before the
/// existing cast-quality runtime snapshots the fish's original weight. The main
/// SnapperIslandFishingRuntime still owns the actual species/weight rules and later
/// shoreline-quality correction; this component only guarantees event ordering.
/// </summary>
[DefaultExecutionOrder(1400)]
public sealed class SnapperIslandCatchOrderRuntime : MonoBehaviour
{
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly FieldInfo StateField=typeof(FishingSystem).GetField("state",Flags);
    private static readonly FieldInfo CastPointField=typeof(FishingSystem).GetField("castPoint",Flags);
    private static readonly FieldInfo PondCastField=typeof(FishingSystem).GetField("pondCast",Flags);
    private static readonly MethodInfo ApplyCatchMethod=typeof(SnapperIslandFishingRuntime).GetMethod("ApplySnapperCatch",Flags);
    private static readonly FieldInfo AppliedField=typeof(SnapperIslandFishingRuntime).GetField("appliedToCast",Flags);
    private static readonly FieldInfo CorrectedField=typeof(SnapperIslandFishingRuntime).GetField("correctedCastQuality",Flags);

    private FishingSystem fishing;
    private SnapperIslandFishingRuntime rules;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
        {
            if(system==null)continue;
            if(system.GetComponent<SnapperIslandFishingRuntime>()==null)
                system.gameObject.AddComponent<SnapperIslandFishingRuntime>();
            if(system.GetComponent<SnapperIslandCatchOrderRuntime>()==null)
                system.gameObject.AddComponent<SnapperIslandCatchOrderRuntime>();
        }
    }

    private void Awake()
    {
        fishing=GetComponent<FishingSystem>();
        rules=GetComponent<SnapperIslandFishingRuntime>();
        if(fishing==null || rules==null || StateField==null || CastPointField==null || PondCastField==null ||
           ApplyCatchMethod==null || AppliedField==null || CorrectedField==null)
        {
            Debug.LogError("SnapperIslandCatchOrderRuntime could not bind snapper catch ordering and was disabled.");
            enabled=false;
        }
    }

    private void Update()
    {
        if(!enabled || fishing==null || rules==null)return;
        object stateValue=StateField.GetValue(fishing);
        string state=stateValue!=null?stateValue.ToString():string.Empty;

        if(state=="Idle" || state=="Charging" || state=="Casting")
        {
            AppliedField.SetValue(rules,false);
            CorrectedField.SetValue(rules,false);
            return;
        }
        if(state=="Waiting" || (bool)AppliedField.GetValue(rules))return;
        if(state!="Bite" && state!="Fighting")return;
        if((bool)PondCastField.GetValue(fishing))return;
        if(!SnapperIslandRuntime.Ready)return;

        Vector3 castPoint=(Vector3)CastPointField.GetValue(fishing);
        if(!SnapperIslandGeometry.ContainsFishingWater(castPoint,SnapperIslandRuntime.Center))return;

        ApplyCatchMethod.Invoke(rules,new object[]{state});
        AppliedField.SetValue(rules,true);
    }
}
