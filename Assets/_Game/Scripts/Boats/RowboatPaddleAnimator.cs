using UnityEngine;

[DisallowMultipleComponent]
public sealed class RowboatPaddleAnimator : MonoBehaviour
{
    public int SourceVersion=3;
    public GameObject SoloOars,TandemOars;
    public Transform[] SoloParts,TandemParts;
    public RowboatStrokeData SoloStroke,TandemStroke;
    private BoatController boat;
    private float frontTime,rearTime;
#if UNITY_EDITOR
    private int previewPaddlers=-1;
    [ContextMenu("Preview one paddler (Play Mode)")] private void PreviewSolo(){previewPaddlers=1;}
    [ContextMenu("Preview two paddlers (Play Mode)")] private void PreviewTandem(){previewPaddlers=2;}
    [ContextMenu("End rowing preview")] private void EndPreview(){previewPaddlers=-1;}
#endif
    private void Awake()
    {
        boat=GetComponent<BoatController>();
        if(SoloOars!=null)SoloOars.SetActive(true);
        if(TandemOars!=null)TandemOars.SetActive(true);
        // Solo supplies the front pair; keep only the rear pair of the tandem set.
        // Both pairs start at authored frame zero and stay visible while resting.
        if(TandemParts!=null)
            for(int i=0;i<TandemParts.Length;i++)
                if(TandemParts[i]!=null)TandemParts[i].gameObject.SetActive(i>=2);
        Apply(SoloStroke,SoloParts,0f,0);
        Apply(TandemStroke,TandemParts,0f,2);
    }
    private void Update()
    {
        bool front=boat!=null && IsRowing(boat.Driver);
        bool rear=boat!=null && IsRowing(boat.SecondRower);
#if UNITY_EDITOR
        if(previewPaddlers>=0){front=previewPaddlers>=1;rear=previewPaddlers>=2;}
#endif
        // Independent clocks preserve each rower's last pose when their input stops.
        if(front && SoloStroke!=null && SoloStroke.Duration>0)
        {
            frontTime=Mathf.Repeat(frontTime+Time.deltaTime,SoloStroke.Duration);
            Apply(SoloStroke,SoloParts,frontTime,0);
        }
        if(rear && TandemStroke!=null && TandemStroke.Duration>0)
        {
            rearTime=Mathf.Repeat(rearTime+Time.deltaTime,TandemStroke.Duration);
            Apply(TandemStroke,TandemParts,rearTime,2);
        }
    }
    private static bool IsRowing(BoatPassenger p)=>p!=null && p.Controls!=null && p.Controls.BoatInput.sqrMagnitude>.0025f;
    private static void Apply(RowboatStrokeData data,Transform[] parts,float time,int first)
    {
        if(data==null || data.Tracks==null || parts==null)return;
        float sample=time*data.SamplesPerSecond;
        for(int n=first;n<parts.Length && n<data.Tracks.Length;n++)
        {
            var track=data.Tracks[n];
            if(parts[n]==null || track==null || track.Positions==null || track.Rotations==null)continue;
            int length=Mathf.Min(track.Positions.Length,track.Rotations.Length);
            if(length<2)continue;
            int i=Mathf.Clamp(Mathf.FloorToInt(sample),0,length-2);
            float t=Mathf.Clamp01(sample-i);
            parts[n].localPosition=Vector3.LerpUnclamped(track.Positions[i],track.Positions[i+1],t);
            parts[n].localRotation=Quaternion.SlerpUnclamped(track.Rotations[i],track.Rotations[i+1],t);
        }
    }
}
