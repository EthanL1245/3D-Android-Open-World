using UnityEngine;

// Explicit platform displacement avoids parenting/scaling a CharacterController
// under a Rigidbody and keeps normal walking/jumping intact on a moving boat.
[DefaultExecutionOrder(-20)]
public sealed class BoatPassenger : MonoBehaviour
{
    public FirstPersonController Controls {get;private set;}
    public BoatController Boat {get;private set;}
    public bool Driving => Boat!=null && (Boat.Driver==this || Boat.SecondRower==this);
    public bool GroundedOnBoat {get;private set;}

    private CharacterController capsule;
    private Vector3 lastPosition;
    private Quaternion lastRotation;

    private void Awake()
    {
        Controls=GetComponent<FirstPersonController>();
        capsule=GetComponent<CharacterController>();
    }

    private void Update()
    {
        if(Boat!=null)
        {
            // Carry the player by the exact rigid-platform delta first, then let the
            // normal FirstPersonController movement run later this frame.
            Vector3 relative=transform.position-lastPosition;
            Vector3 target=Boat.transform.position+Boat.transform.rotation*Quaternion.Inverse(lastRotation)*relative;
            if(Driving)target=Boat.SeatFor(this).position;
            if(capsule!=null && capsule.enabled)capsule.Move(target-transform.position);
            lastPosition=Boat.transform.position;
            lastRotation=Boat.transform.rotation;
        }

        if(Driving)
        {
            GroundedOnBoat=false;
            return;
        }

        BoatController under=FindBoatBelow(.72f);
        GroundedOnBoat=under!=null && under==Boat;

        // A tiny downward CharacterController move makes controller.isGrounded
        // reliable even when the raft itself moved vertically with a wave this frame.
        // This keeps walking acceleration, deceleration and jumping identical to land.
        if(GroundedOnBoat && capsule!=null && capsule.enabled)
            capsule.Move(Vector3.down*.025f);

        if(under==Boat)return;

        // Do not detach just because a jump temporarily leaves no collider directly
        // under the capsule. Stay attached while above the boat footprint so platform
        // translation/yaw continues naturally through the jump, then re-ground on
        // landing. Walking off the side still detaches immediately.
        if(Boat!=null && IsAboveBoatFootprint(Boat))return;

        Detach();
        if(under!=null && under.Board())
        {
            Boat=under;
            lastPosition=under.transform.position;
            lastRotation=under.transform.rotation;
            GroundedOnBoat=true;
        }
    }

    private BoatController FindBoatBelow(float distance)
    {
        Vector3 origin=transform.position+Vector3.up*.18f;
        RaycastHit[] hits=Physics.SphereCastAll(origin,.12f,Vector3.down,distance,~0,QueryTriggerInteraction.Ignore);
        BoatController best=null;
        float bestDistance=float.PositiveInfinity;
        foreach(var hit in hits)
        {
            if(hit.collider==null || hit.collider.transform.IsChildOf(transform))continue;
            BoatController candidate=hit.collider.GetComponentInParent<BoatController>();
            if(candidate==null || hit.distance>=bestDistance)continue;
            best=candidate;bestDistance=hit.distance;
        }
        return best;
    }

    private bool IsAboveBoatFootprint(BoatController boat)
    {
        if(boat==null || boat.Data==null)return false;
        Vector3 local=boat.transform.InverseTransformPoint(transform.position);
        Vector3 size=boat.Data.HullSize;
        if(boat.Data.ID=="raft")size=new Vector3(2.6749f,1.2542f,4.5634f);
        float halfX=size.x*.5f+.18f;
        float halfZ=size.z*.5f+.22f;
        // Wide enough vertically for a normal jump, but not enough to keep a player
        // attached after genuinely leaving/falling away from the boat.
        return Mathf.Abs(local.x)<=halfX && Mathf.Abs(local.z)<=halfZ && local.y>-1.1f && local.y<3.25f;
    }

    public bool BoardFromWater(BoatController boat)
    {
        if(Boat!=null || boat==null || Vector3.Distance(transform.position,boat.DeckExit.position)>4 || !boat.Board())return false;
        Boat=boat;
        capsule.enabled=false;
        transform.position=boat.DeckExit.position;
        capsule.enabled=true;
        Controls.ResetMotion();
        lastPosition=boat.transform.position;
        lastRotation=boat.transform.rotation;
        GroundedOnBoat=true;
        return true;
    }

    public void ToggleHelm()
    {
        if(Boat==null)return;
        if(Driving)
        {
            Boat.ReleaseHelm(this);
            Controls.ResetMotion();
            return;
        }
        if(!Boat.CanUseSeat(this))return;
        if(Boat.TakeHelm(this))
        {
            GetComponent<FishingSystem>()?.UnequipHands();
            Controls.ResetMotion();
            GroundedOnBoat=false;
        }
    }

    public void Detach()
    {
        if(Boat!=null)Boat.Leave(this);
        Boat=null;
        GroundedOnBoat=false;
    }

    private void OnDisable(){Detach();}
}

