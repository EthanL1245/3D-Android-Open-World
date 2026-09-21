using UnityEngine;

// Authored swim in three short attempts, followed by a quiet pendulum interval.
[DefaultExecutionOrder(900)]
public sealed class CaughtFishFlop : MonoBehaviour
{
    public System.Action PoseUpdated;
    public float SwingBoost { get; private set; }
    private Transform[] bones;
    private Vector3[] restPositions;
    private Quaternion[] restRotations;
    private Animator animator;
    private AnimationClip swim;
    private float nextBurst, burstStart=-100f;
    private const float Cycle=0.18f, Duration=Cycle*3;
    public void Initialize()
    {
        if(bones!=null)return;
        bones=GetComponentsInChildren<Transform>(true);
        restPositions=new Vector3[bones.Length];restRotations=new Quaternion[bones.Length];
        for(int i=0;i<bones.Length;i++){restPositions[i]=bones[i].localPosition;restRotations[i]=bones[i].localRotation;}
        animator=GetComponentInChildren<Animator>();
        if(animator!=null && animator.runtimeAnimatorController!=null)
        {
            foreach(var clip in animator.runtimeAnimatorController.animationClips)
                if(swim==null || clip.name.ToLowerInvariant().Contains("swim"))swim=clip;
            animator.enabled=false;
        }
        nextBurst=Time.time+Random.Range(5f,10f);
    }
    private void Awake()=>Initialize();
    private void LateUpdate()
    {
        if(Time.time>=nextBurst){burstStart=Time.time;nextBurst=burstStart+Random.Range(5f,10f);}
        float age=Time.time-burstStart;
        bool active=age>=0 && age<Duration;
        float phase=active?(age%Cycle)/Cycle:0;
        float envelope=active?Mathf.Sin(phase*Mathf.PI):0;
        SwingBoost=Mathf.MoveTowards(SwingBoost,active?1f:0f,Time.deltaTime*(active?9f:0.65f));
        Vector3 position=transform.localPosition,scale=transform.localScale;Quaternion rotation=transform.localRotation;
        for(int i=1;i<bones.Length;i++){bones[i].localPosition=restPositions[i];bones[i].localRotation=restRotations[i];}
        if(active && swim!=null)
        {
            swim.SampleAnimation(animator.gameObject,phase*swim.length);
            for(int i=1;i<bones.Length;i++)
            {bones[i].localPosition=Vector3.Lerp(restPositions[i],bones[i].localPosition,envelope);bones[i].localRotation=Quaternion.Slerp(restRotations[i],bones[i].localRotation,envelope);}
        }
        else if(active)
        {
            for(int i=1;i<bones.Length;i++)
                if(bones[i].name=="Bone.002" || bones[i].name=="Bone.003" || bones[i].name=="Bone.004" || bones[i].name=="Tail")
                    bones[i].localRotation=restRotations[i]*Quaternion.Euler(0,Mathf.Sin(phase*Mathf.PI*2-i*0.4f)*30f*envelope,0);
        }
        transform.localPosition=position;transform.localRotation=rotation;transform.localScale=scale;
        PoseUpdated?.Invoke();
    }
}
