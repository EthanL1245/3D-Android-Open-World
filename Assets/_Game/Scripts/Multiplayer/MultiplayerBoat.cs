using System.Collections.Generic;
using UnityEngine;

// The owner simulates physics. Other clients keep a solid, boardable kinematic copy.
[DefaultExecutionOrder(-40)]
public sealed class MultiplayerBoat : MonoBehaviour
{
    private static readonly List<MultiplayerBoat> live = new List<MultiplayerBoat>();
    public string OwnerId, Key;
    public bool Replica;
    public BoatController Boat { get; private set; }
    public FishingMultiplayer Session;
    public string FrontId, RearId;
    public bool OtherGuestAboard;
    public bool CanBoard => !Boat.IsRowboat || Session==null || OwnerId==Session.LocalId || !OtherGuestAboard;
    private Vector3 target;
    private Quaternion rotation;
    private Rigidbody body;
    private bool received;
    private float lastSnapshot;

    public static MultiplayerBoat Find(string owner,string key)
    {
        foreach(var b in live)if(b!=null && b.OwnerId==owner && b.Key==key)return b;
        return null;
    }
    private void Awake(){Boat=GetComponent<BoatController>();body=GetComponent<Rigidbody>();live.Add(this);}
    public void Configure(FishingMultiplayer session,string owner,string key,bool replica)
    {
        Session=session;OwnerId=owner;Key=key;Replica=replica;
        Boat.Shared=this;
        if(replica){body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;}
    }
    public void Receive(FishingMultiplayer.BoatPose pose)
    {
        target=new Vector3(pose.x,pose.y,pose.z);rotation=Quaternion.Euler(pose.pitch,pose.yaw,pose.roll);
        if(!received || (transform.position-target).sqrMagnitude>225f)transform.SetPositionAndRotation(target,rotation);
        received=true;lastSnapshot=Time.realtimeSinceStartup;
    }
    public void MarkControlsReceived(){lastSnapshot=Time.realtimeSinceStartup;}
    private void Update()
    {
        if(Time.realtimeSinceStartup-lastSnapshot>1f)Boat.ClearNetworkInput();
        if(!Replica || !received)return;
        float blend=1f-Mathf.Exp(-14f*Time.unscaledDeltaTime);
        body.position=Vector3.Lerp(transform.position,target,blend);
        body.rotation=Quaternion.Slerp(transform.rotation,rotation,blend);
        // Passenger queries/movement run next in Update, before the next physics
        // tick. Keep the moving collider and displayed hull at the same pose.
        Physics.SyncTransforms();
    }
    public int DesiredSeat => Boat.IsRowboat && Session!=null && OwnerId!=Session.LocalId?2:1;
    public bool SeatAvailable(BoatPassenger passenger)
    {
        if(Session==null || !Session.Connected)return false;
        string occupant=DesiredSeat==2?RearId:FrontId;
        Transform seat=DesiredSeat==2?Boat.SecondRowingSeat:Boat.DriverSeat;
        return seat!=null && (string.IsNullOrEmpty(occupant)||occupant==Session.LocalId) &&
            Vector3.Distance(passenger.transform.position,seat.position)<2.5f;
    }
    public bool PassengerSeatAvailable(BoatPassenger passenger)
    {
        return Boat.IsMotorboat && Session!=null && Session.Connected &&
            !string.IsNullOrEmpty(FrontId) && FrontId!=Session.LocalId &&
            (string.IsNullOrEmpty(RearId) || RearId==Session.LocalId);
    }
    private void OnDestroy()
    {
        live.Remove(this);
        if(Boat!=null){Boat.Shared=null;Boat.NetworkOtherPassengers=0;Boat.ClearNetworkInput();}
        if(Session!=null){var p=Session.GetComponent<BoatPassenger>();if(p!=null && p.Boat==Boat)p.Detach();}
    }
}
