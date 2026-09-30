using UnityEngine;

[DisallowMultipleComponent]
public sealed class RowboatPaddleAnimator : MonoBehaviour
{
    public int SourceVersion=1;
    public GameObject SoloOars,TandemOars;
    public Transform[] SoloParts,TandemParts;
    public RowboatStrokeData SoloStroke,TandemStroke;
    private BoatController boat;
    private float time;
#if UNITY_EDITOR
    // Editor-only presentation test; cannot grant speed or passengers in a build.
    private int previewPaddlers=-1;
    [ContextMenu("Preview one paddler (Play Mode)")] private void PreviewSolo(){previewPaddlers=1;}
    [ContextMenu("Preview two paddlers (Play Mode)")] private void PreviewTandem(){previewPaddlers=2;}
    [ContextMenu("End rowing preview")] private void EndPreview(){previewPaddlers=-1;}
#endif
    private void Awake(){boat=GetComponent<BoatController>();}
    private void Update()
    {
        int count=boat!=null?boat.ActivePaddlers:0;
#if UNITY_EDITOR
        if(previewPaddlers>=0)count=previewPaddlers;
#endif
        // No duplicated stationary hull. The rear set exists only in tandem mode.
        if(SoloOars!=null)SoloOars.SetActive(count==1);
        if(TandemOars!=null)TandemOars.SetActive(count>=2);
        if(count==0)return;
        var data=count>=2?TandemStroke:SoloStroke;
        var parts=count>=2?TandemParts:SoloParts;
        if(data==null || data.Tracks==null || parts==null)return;
        time=Mathf.Repeat(time+Time.deltaTime,data.Duration);
        float sample=time*data.SamplesPerSecond;
        for(int n=0;n<parts.Length && n<data.Tracks.Length;n++)
        {
            var track=data.Tracks[n];
            int i=Mathf.Min(Mathf.FloorToInt(sample),track.Positions.Length-2);
            float t=sample-i;
            parts[n].localPosition=Vector3.LerpUnclamped(track.Positions[i],track.Positions[i+1],t);
            parts[n].localRotation=Quaternion.SlerpUnclamped(track.Rotations[i],track.Rotations[i+1],t);
        }
    }
}
