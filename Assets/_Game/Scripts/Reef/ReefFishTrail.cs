using System;
using System.Collections.Generic;
using UnityEngine;

// Distance-indexed head track, shared by all authored ambient rigs.
public sealed class ReefFishTrail : MonoBehaviour
{
    private struct Sample {public Vector3 forward;public float distance;}
    private readonly List<Sample> samples=new List<Sample>(96);
    private Action<Vector3,Vector3,Vector3,Vector3,Vector3> apply;
    private MonoBehaviour presentation;
    private Transform head;
    private Vector3 last;
    private float distance;
    public float BodyLength {get;private set;}=0.92f;
    private void Awake()
    {
        var snapper=GetComponent<RedSnapperPresentation>();var mackerel=GetComponent<MackerelPresentation>();
        var yellowtail=GetComponent<YellowtailPresentation>();var goat=GetComponent<GoatfishPresentation>();var tuna=GetComponent<YellowfinTunaPresentation>();
        if(snapper!=null){apply=snapper.SetAquariumTrail;presentation=snapper;BodyLength=snapper.GetBodyLengthWorld();}
        else if(mackerel!=null){apply=mackerel.SetAquariumTrail;presentation=mackerel;BodyLength=mackerel.GetBodyLengthWorld();}
        else if(yellowtail!=null){apply=yellowtail.SetAquariumTrail;presentation=yellowtail;BodyLength=yellowtail.GetBodyLengthWorld();}
        else if(goat!=null){apply=goat.SetAquariumTrail;presentation=goat;BodyLength=goat.GetBodyLengthWorld();}
        else if(tuna!=null){apply=tuna.SetAquariumTrail;presentation=tuna;BodyLength=tuna.GetBodyLengthWorld();}
        foreach(var t in GetComponentsInChildren<Transform>())if(t.name=="Bone"){head=t;break;}
        BodyLength=Mathf.Max(0.3f,BodyLength);ResetTrail();
    }
    public void SetActive(bool active){if(presentation!=null)presentation.enabled=active;}
    public void ResetTrail(){samples.Clear();distance=0;last=head!=null?head.position:transform.position;samples.Add(new Sample{forward=transform.forward,distance=0});}
    public void Record()
    {
        if(apply==null)return;
        Vector3 position=head!=null?head.position:transform.position;
        float moved=Vector3.Distance(position,last);
        if(moved>2f){ResetTrail();return;}
        if(moved>0.015f)
        {
            distance+=moved;last=position;samples.Add(new Sample{forward=transform.forward,distance=distance});
            while(samples.Count>2 && (samples[1].distance<distance-BodyLength*1.6f || samples.Count>96))samples.RemoveAt(0);
        }
        if(presentation!=null&&presentation.enabled)
            apply(transform.forward,At(.25f),At(.5f),At(.75f),At(1f));
    }
    private Vector3 At(float fraction)
    {
        float target=distance-BodyLength*fraction;
        for(int i=samples.Count-1;i>0;i--)
            if(samples[i-1].distance<=target)
                return Vector3.Slerp(samples[i-1].forward,samples[i].forward,Mathf.InverseLerp(samples[i-1].distance,samples[i].distance,target));
        return samples[0].forward;
    }
}
