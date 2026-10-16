using System;
using System.Collections.Generic;
using UnityEngine;

// Presence and seat input ride along with the existing room snapshots; no second connection.
public sealed class FishingBoatMultiplayer : MonoBehaviour
{
    private FishingMultiplayer session;
    private BoatSystem system;
    private BoatPassenger passenger;
    private readonly Dictionary<string,MultiplayerBoat> replicas=new Dictionary<string,MultiplayerBoat>();
    private MultiplayerBoat owned;
    private int revision;
    private string requestOwner="",requestKey="";
    private int requestSeat=-1;
    private void Awake(){session=GetComponent<FishingMultiplayer>();system=GetComponent<BoatSystem>();passenger=GetComponent<BoatPassenger>();}
    public void Capture(FishingMultiplayer.Pose pose)
    {
        BoatController boat=system!=null?system.ActiveBoat:null;
        if(boat!=null && boat.Data!=null && pose.world==0)
        {
            var shared=boat.GetComponent<MultiplayerBoat>();
            if(shared==null)shared=boat.gameObject.AddComponent<MultiplayerBoat>();
            if(string.IsNullOrEmpty(shared.Key))shared.Key=Guid.NewGuid().ToString("N");
            if(session.Connected){shared.Configure(session,session.LocalId,shared.Key,false);owned=shared;}
            Vector3 p=boat.transform.position,e=boat.transform.eulerAngles;
            pose.boat=new FishingMultiplayer.BoatPose{key=shared.Key,kind=boat.Data.ID,x=p.x,y=p.y,z=p.z,pitch=e.x,yaw=e.y,roll=e.z};
        }
        var ride=passenger!=null && passenger.Boat!=null?passenger.Boat.Shared:null;
        if(session.Connected && pose.world==0 && ride!=null)
        {
            pose.rideOwner=ride.OwnerId;pose.rideKey=ride.Key;
            pose.rideSeat=passenger.Driving?(passenger.Boat.SecondRower==passenger?2:1):0;
            Vector3 local=ride.transform.InverseTransformPoint(transform.position);
            pose.rideX=Mathf.Clamp(local.x,-12,12);pose.rideY=Mathf.Clamp(local.y,-5,5);pose.rideZ=Mathf.Clamp(local.z,-12,12);
            pose.rideYaw=Mathf.DeltaAngle(ride.transform.eulerAngles.y,transform.eulerAngles.y);
            Vector2 input=passenger.Driving?passenger.Controls.BoatInput:Vector2.zero;
            pose.boatInputX=input.x;pose.boatInputY=input.y;
            Camera camera=passenger.Controls.ViewCamera;
            pose.boatLookYaw=camera!=null?camera.transform.eulerAngles.y:transform.eulerAngles.y;
        }
        if(requestOwner!=pose.rideOwner || requestKey!=pose.rideKey || requestSeat!=pose.rideSeat)
        {requestOwner=pose.rideOwner;requestKey=pose.rideKey;requestSeat=pose.rideSeat;revision++;}
        pose.rideRevision=revision;
    }
    public void Apply(FishingMultiplayer.Peer[] peers)
    {
        // Attach an existing local boat to the newly established session first.
        Capture(new FishingMultiplayer.Pose{world=ShopDimensionManager.Instance!=null?ShopDimensionManager.Instance.Destination:0});
        var present=new HashSet<string>();
        foreach(var peer in peers)
        {
            if(peer.id==session.LocalId || peer.boat==null || peer.world!=0)continue;
            present.Add(peer.id);
            if(replicas.TryGetValue(peer.id,out var old) && (old==null || old.Key!=peer.boat.key || old.Boat.Data.ID!=peer.boat.kind))
            {Remove(peer.id);old=null;}
            if(old==null)
            {
                BoatData data=system!=null && system.Catalog!=null?Array.Find(system.Catalog,d=>d!=null && d.ID==peer.boat.kind):null;
                if(data==null || data.Prefab==null)continue;
                var go=Instantiate(data.Prefab,new Vector3(peer.boat.x,peer.boat.y,peer.boat.z),Quaternion.Euler(peer.boat.pitch,peer.boat.yaw,peer.boat.roll));
                var controller=go.GetComponent<BoatController>();controller.Initialize(data,system.Water);
                old=go.AddComponent<MultiplayerBoat>();old.Configure(session,peer.id,peer.boat.key,true);replicas[peer.id]=old;
            }
            old.Receive(peer.boat);
        }
        var gone=new List<string>();foreach(var pair in replicas)if(!present.Contains(pair.Key))gone.Add(pair.Key);
        foreach(string id in gone)Remove(id);
        if(owned!=null)ApplyRiders(owned,peers);
        foreach(var b in replicas.Values)ApplyRiders(b,peers);
        foreach(var peer in peers)
        {
            if(peer.id!=session.LocalId || peer.rideRevision!=revision || passenger==null || passenger.Boat==null)continue;
            var current=passenger.Boat.Shared;if(current==null)continue;
            // Only reconcile an echo of our current request, never a stale response to an earlier click.
            if(peer.rideOwner!=requestOwner || peer.rideKey!=requestKey)passenger.Detach();
            else if(passenger.Driving && peer.rideSeat!=requestSeat){passenger.Boat.ReleaseHelm(passenger);passenger.Controls.ResetMotion();}
        }
    }
    private void ApplyRiders(MultiplayerBoat shared,FishingMultiplayer.Peer[] peers)
    {
        shared.MarkControlsReceived();
        var boat=shared.Boat;boat.ClearNetworkInput();boat.NetworkOtherPassengers=0;shared.FrontId=shared.RearId="";shared.OtherGuestAboard=false;
        foreach(var peer in peers)
        {
            if(peer.rideOwner!=shared.OwnerId || peer.rideKey!=shared.Key || peer.rideSeat<0)continue;
            if(peer.id!=session.LocalId){boat.NetworkOtherPassengers++;if(peer.id!=shared.OwnerId)shared.OtherGuestAboard=true;}
            if(peer.rideSeat==1)shared.FrontId=peer.id;
            if(peer.rideSeat==2)shared.RearId=peer.id;
            if(peer.id==session.LocalId)continue;
            if(peer.rideSeat==1){boat.NetworkFrontInput=new Vector2(peer.boatInputX,peer.boatInputY);boat.NetworkFrontYaw=peer.boatLookYaw;boat.NetworkHasDriver=true;}
            if(peer.rideSeat==2)boat.NetworkRearInput=new Vector2(peer.boatInputX,peer.boatInputY);
        }
    }
    public BoatController NearestBoardable()
    {
        BoatController nearest=null;float distance=4f;
        foreach(var b in replicas.Values)
        {
            if(b==null || !b.gameObject.activeInHierarchy || !b.CanBoard || b.Boat.PassengerCount>=b.Boat.Data.MaxCapacity)continue;
            float d=Vector3.Distance(transform.position,b.Boat.DeckExit.position);
            if(d<distance){distance=d;nearest=b.Boat;}
        }
        return nearest;
    }
    private void Update()
    {
        var travel=ShopDimensionManager.Instance;
        bool visible=session.Connected && (travel==null || (!travel.Traveling && travel.Destination==0));
        foreach(var b in replicas.Values)if(b!=null)b.gameObject.SetActive(visible);
    }
    private void Remove(string id)
    {
        if(replicas.TryGetValue(id,out var b) && b!=null){if(passenger!=null && passenger.Boat==b.Boat)passenger.Detach();b.gameObject.SetActive(false);Destroy(b.gameObject);}
        replicas.Remove(id);
    }
    public void Clear()
    {
        foreach(string id in new List<string>(replicas.Keys))Remove(id);
        if(owned!=null){owned.Boat.NetworkOtherPassengers=0;owned.Boat.ClearNetworkInput();owned.Boat.Shared=null;Destroy(owned);owned=null;}
        requestOwner=requestKey="";requestSeat=-1;revision=0;
    }
    private void OnDestroy(){Clear();}
}
