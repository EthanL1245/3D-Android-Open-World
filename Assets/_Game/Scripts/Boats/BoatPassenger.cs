using UnityEngine;

// Explicit platform displacement avoids parenting/scaling a CharacterController
// under a Rigidbody and keeps the existing movement/fishing controller intact.
[DefaultExecutionOrder(-20)]
public sealed class BoatPassenger : MonoBehaviour
{
    public FirstPersonController Controls {get;private set;}
    public BoatController Boat {get;private set;}
    public bool Driving => Boat!=null && Boat.Driver==this;
    private CharacterController capsule;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private void Awake(){Controls=GetComponent<FirstPersonController>();capsule=GetComponent<CharacterController>();}
    private void Update()
    {
        if(Boat!=null)
        {
            Vector3 target=Boat.transform.position+Boat.transform.rotation*Quaternion.Inverse(lastRotation)*(transform.position-lastPosition);
            if(Driving)target=Boat.DriverSeat.position;
            if(capsule.enabled)capsule.Move(target-transform.position);
            lastPosition=Boat.transform.position;lastRotation=Boat.transform.rotation;
        }
        if(Driving)return;
        BoatController under=null;
        if(Physics.Raycast(transform.position+Vector3.up*.15f,Vector3.down,out var hit,.55f,~0,QueryTriggerInteraction.Ignore))
            under=hit.collider.GetComponentInParent<BoatController>();
        if(under==Boat)return;
        Detach();
        if(under!=null && under.Board()){Boat=under;lastPosition=under.transform.position;lastRotation=under.transform.rotation;}
    }
    public bool BoardFromWater(BoatController boat)
    {
        if(Boat!=null || boat==null || Vector3.Distance(transform.position,boat.DeckExit.position)>4 || !boat.Board())return false;
        Boat=boat;capsule.enabled=false;transform.position=boat.DeckExit.position;capsule.enabled=true;Controls.ResetMotion();
        lastPosition=boat.transform.position;lastRotation=boat.transform.rotation;return true;
    }
    public void ToggleHelm()
    {
        if(Boat==null)return;
        if(Driving){Boat.ReleaseHelm(this);Controls.ResetMotion();return;}
        if(Vector3.Distance(transform.position,Boat.DriverSeat.position)>2.5f)return;
        if(Boat.TakeHelm(this)){GetComponent<FishingSystem>()?.UnequipHands();Controls.ResetMotion();}
    }
    public void Detach(){if(Boat!=null)Boat.Leave(this);Boat=null;}
    private void OnDisable(){Detach();}
}
