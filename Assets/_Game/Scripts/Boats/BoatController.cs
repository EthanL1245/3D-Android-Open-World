using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoatController : MonoBehaviour
{
    public BoatData Data;
    public Transform DriverSeat;
    public Transform DeckExit;
    public Transform SecondRowingSeat;
    public BoatPassenger SeatedGuest {get;private set;}
    public Transform GuestSeat => IsMotorboat ? MotorboatDrivingSeat.For(this).PassengerAnchor : null;
    public bool HasHelmOccupant => Driver!=null || (Shared!=null && !string.IsNullOrEmpty(Shared.FrontId));
    public bool CanSit(BoatPassenger p) => IsMotorboat && p!=null && p.Boat==this && !p.Driving &&
        (SeatedGuest==p || (SeatedGuest==null && HasHelmOccupant &&
        (Shared==null || Shared.PassengerSeatAvailable(p)) &&
        Vector3.Distance(p.transform.position,GuestSeat.position)<2.5f));
    public bool TakePassengerSeat(BoatPassenger p)
    {
        if(!CanSit(p))return false;
        SeatedGuest=p;return true;
    }
    public BoatPassenger SecondRower {get;private set;}
    public bool IsRowboat => Data!=null && Data.ID=="rowboat";
    // The existing sailboat inventory ID is retained so old purchases and saves work.
    public bool IsMotorboat => Data!=null && Data.ID=="sailboat";
    public MultiplayerBoat Shared;
    public Vector2 NetworkFrontInput, NetworkRearInput;
    public float NetworkFrontYaw;
    public bool NetworkHasDriver;
    public int NetworkOtherPassengers;
    public bool FrontPaddling => HasInput(Driver) || NetworkFrontInput.sqrMagnitude>.0025f;
    public bool RearPaddling => IsRowboat && (HasInput(SecondRower) || NetworkRearInput.sqrMagnitude>.0025f);
    public int ActivePaddlers => (FrontPaddling?1:0)+(RearPaddling?1:0);
    public void ClearNetworkInput(){NetworkFrontInput=NetworkRearInput=Vector2.zero;NetworkHasDriver=false;}
    public float RowingSpeed => Data==null?0f:Data.Speed*(IsRowboat?Mathf.Max(1,ActivePaddlers):1);
    private static bool HasInput(BoatPassenger p) => p!=null && p.Controls!=null && p.Controls.BoatInput.sqrMagnitude>.0025f;
    public Transform SeatFor(BoatPassenger p) => p==SeatedGuest?GuestSeat:p==SecondRower?SecondRowingSeat:DriverSeat;
    public bool CanUseSeat(BoatPassenger p) => p!=null && (p.Seated || CanSit(p) || (Shared!=null ? Shared.SeatAvailable(p) : (
        (Driver==null && Vector3.Distance(p.transform.position,DriverSeat.position)<2.5f) ||
        (IsRowboat && SecondRower==null && SecondRowingSeat!=null && Vector3.Distance(p.transform.position,SecondRowingSeat.position)<2.5f))));
    public BoatPassenger Driver {get;private set;}
    private int localPassengerCount;
    public int PassengerCount => localPassengerCount + NetworkOtherPassengers;

    private Rigidbody body;
    private OceanWater water;
    private Camera driverView;

    // Four water samples steer the Rigidbody's pitch/roll without new meshes.
    private const float RaftTiltLimit = 13f;
    private const float RowboatTiltLimit = 11f;
    private const float SailboatTiltLimit = 8f;
    private const float YachtTiltLimit = 5f;
    private const float RockingSpring = 26f;
    private const float MotorboatAccelerationTime = 3f;
    private const float MotorboatBankLimit = 15f;
    private const float MotorboatBowRiseLimit = 10f;
    private const float RockingDamping = 8f;

    // No ocean wave has a physical wall. Constrain the vertical response, and
    // preserve the last real forward motion through successive deep-water crests.
    private const float MaxCrestLift = 0.65f;
    private const float BuoyancyRiseSpeed = 5.5f;
    private const float BuoyancyFallSpeed = 5.0f;
    private const float MaxOpenWaterDeceleration = 2.5f;
    private Collider[] waveSafetyContacts = new Collider[48];
    private int solidNavigationMask = Physics.DefaultRaycastLayers;
    private float filteredBuoyancyHeight;
    private float lastDrivenForwardSpeed;
    private bool buoyancyStarted;
    private bool wasPropelling;

    public void Initialize(BoatData data,OceanWater ocean)
    {
        Data=data;water=ocean;body=GetComponent<Rigidbody>();
        body.mass=300;body.useGravity=false;body.linearDamping=.65f;body.angularDamping=3;
        // Permit wave-induced pitching/rolling. Yaw is still controlled by steering.
        body.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
        body.interpolation=RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;

        // Exclude the dedicated query-water layer without changing world collision
        // rules. Rocks, terrain, docks and other boats remain physical obstacles.
        int waterLayer=LayerMask.NameToLayer("Water");
        solidNavigationMask=Physics.DefaultRaycastLayers;
        if(IsMotorboat && waterLayer>=0)
        {
            body.excludeLayers |= 1<<waterLayer;
            solidNavigationMask &= ~(1<<waterLayer);
        }
        // Reserve space for our own compound hull PLUS external obstacles once.
        // The old 48 slots could all be occupied by the newly baked cockpit hull.
        int hullCount=GetComponentsInChildren<Collider>().Length;
        if(IsMotorboat && waveSafetyContacts.Length<hullCount+64)
            waveSafetyContacts=new Collider[Mathf.Min(8192,Mathf.NextPowerOfTwo(hullCount+64))];

        // Water is visual/non-solid. If an older scene has an accidental ocean
        // collider, do not let the hull collide with it during wave crossings.
        if(water!=null)
        {
            var oceanColliders=water.GetComponentsInChildren<Collider>();
            var hullColliders=GetComponentsInChildren<Collider>();
            foreach(var oceanCollider in oceanColliders)
                foreach(var hullCollider in hullColliders)
                    if(oceanCollider!=hullCollider)
                        Physics.IgnoreCollision(oceanCollider,hullCollider,true);
        }

        buoyancyStarted=false;
        wasPropelling=false;
        lastDrivenForwardSpeed=0f;
        MotorboatWake.Attach(this, ocean);
        OceanContactFoam.RegisterExclusion(transform);
    }

    public bool Board()
    {
        if(Data==null || PassengerCount>=Data.MaxCapacity || (Shared!=null && !Shared.CanBoard))return false;
        localPassengerCount++;
        return true;
    }

    public void Leave(BoatPassenger passenger)
    {
        ReleaseHelm(passenger);
        localPassengerCount=Mathf.Max(0,localPassengerCount-1);
    }

    public bool TakeHelm(BoatPassenger passenger)
    {
        if(passenger==null || passenger.Boat!=this)return false;
        if(Driver==passenger || SecondRower==passenger)return true;
        if(Shared!=null)
        {
            if(!Shared.SeatAvailable(passenger))return false;
            if(Shared.DesiredSeat==2){SecondRower=passenger;return true;}
            Driver=passenger;driverView=passenger.GetComponentInChildren<Camera>();return true;
        }
        if(Driver!=null)
        {
            if(!IsRowboat || SecondRower!=null || SecondRowingSeat==null)return false;
            SecondRower=passenger;
            return true;
        }
        Driver=passenger;
        driverView=passenger.GetComponentInChildren<Camera>();
        return true;
    }

    public void ReleaseHelm(BoatPassenger passenger)
    {
        if(SeatedGuest==passenger)SeatedGuest=null;
        if(SecondRower==passenger)SecondRower=null;
        if(Driver!=passenger)return;
        if(Shared!=null){Driver=null;driverView=null;return;}
        // The remaining rower takes the front seat so solo always uses the front pair.
        Driver=SecondRower;SecondRower=null;
        driverView=Driver!=null?Driver.GetComponentInChildren<Camera>():null;
        if(Driver!=null)Driver.SnapToRowingSeat();
    }

    private const float RowboatFreeboard = 0.36f;

    // The interior floor must clear the highest wave under the actual hull.
    public static float FloatingHeight(BoatData data,OceanWater ocean,Vector3 position,Quaternion rotation)
    {
        float highest=ocean.GetSurfaceHeight(position);
        if(data==null || data.ID!="rowboat")return highest;
        float halfLength=Mathf.Max(.35f,data.HullSize.z*.42f);
        float halfWidth=Mathf.Max(.35f,data.HullSize.x*.42f);
        for(int x=-1;x<=1;x++)
        for(int z=-1;z<=1;z++)
        {
            Vector3 offset=rotation*new Vector3(x*halfWidth,0f,z*halfLength);
            highest=Mathf.Max(highest,ocean.GetSurfaceHeight(position+offset)-offset.y);
        }
        return highest+RowboatFreeboard;
    }

    private float ApplyWaveRocking(Vector3 forward,Vector3 right,out float highestWaveAtHull)
    {
        // Use the same wave simulation as vertical buoyancy. Probes remain
        // horizontal rather than following the hull's current tilt.
        bool raft=Data.ID=="raft";
        float width=raft?2.6749f:Mathf.Max(.5f,Data.HullSize.x);
        float length=raft?4.5634f:Mathf.Max(.5f,Data.HullSize.z);
        float halfWidth=Mathf.Max(.35f,width*.42f);
        float halfLength=Mathf.Max(.35f,length*.42f);
        Vector3 center=body.position;
        float bow=water.GetSurfaceHeight(center+forward*halfLength);
        float stern=water.GetSurfaceHeight(center-forward*halfLength);
        float starboard=water.GetSurfaceHeight(center+right*halfWidth);
        float port=water.GetSurfaceHeight(center-right*halfWidth);

        float forwardSlope=(bow-stern)/(2f*halfLength);
        float rightSlope=(starboard-port)/(2f*halfWidth);
        Vector3 desiredUp=(Vector3.up-forward*forwardSlope-right*rightSlope).normalized;
        float maxTilt=raft?RaftTiltLimit:IsRowboat?RowboatTiltLimit:
                      Data.ID=="yacht"?YachtTiltLimit:SailboatTiltLimit;
        desiredUp=Vector3.RotateTowards(Vector3.up,desiredUp,maxTilt*Mathf.Deg2Rad,0f);
        if(IsMotorboat)
        {
            // Keep the same four-point water response, but add a planing bow rise
            // and turn bank. Limit the final pitch/roll individually so waves
            // cannot make a fast motorboat exceed the requested presentation.
            float planarSpeed=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude;
            float speedFraction=Mathf.Clamp01(planarSpeed/Mathf.Max(.01f,Data.Speed));
            float bowRise=speedFraction*MotorboatBowRiseLimit;
            float yawFraction=Mathf.Clamp(body.angularVelocity.y*Mathf.Rad2Deg/32f,-1f,1f);
            float bank=-yawFraction*MotorboatBankLimit*Mathf.Clamp01(planarSpeed/5f);
            float wavePitch=-Mathf.Asin(Mathf.Clamp(Vector3.Dot(desiredUp,forward),-1f,1f))*Mathf.Rad2Deg;
            float waveRoll=-Mathf.Asin(Mathf.Clamp(Vector3.Dot(desiredUp,right),-1f,1f))*Mathf.Rad2Deg;
            float finalPitch=Mathf.Clamp(wavePitch+bowRise,-MotorboatBowRiseLimit,MotorboatBowRiseLimit);
            float finalRoll=Mathf.Clamp(waveRoll+bank,-MotorboatBankLimit,MotorboatBankLimit);
            desiredUp=Quaternion.AngleAxis(finalRoll,forward)*
                      Quaternion.AngleAxis(-finalPitch,right)*Vector3.up;
        }

        // Angular spring/damping affects pitch and roll but not steering yaw.
        Vector3 tiltError=Vector3.ProjectOnPlane(
            Vector3.Cross(body.rotation*Vector3.up,desiredUp),Vector3.up);
        Vector3 tiltVelocity=Vector3.ProjectOnPlane(body.angularVelocity,Vector3.up);
        Vector3 acceleration=tiltError*RockingSpring-tiltVelocity*RockingDamping;
        body.AddTorque(Vector3.ClampMagnitude(acceleration,8f),ForceMode.Acceleration);

        // Reuse wave rocking's four probes, accounting for lowered sides of a
        // pitched/rolled hull, then cover curvature with four corner probes.
        highestWaveAtHull=float.NegativeInfinity;
        if(IsRowboat)
        {
            Vector3 pitchOffset=body.rotation*Vector3.forward*halfLength;
            Vector3 rollOffset=body.rotation*Vector3.right*halfWidth;
            highestWaveAtHull=Mathf.Max(Mathf.Max(bow-pitchOffset.y,stern+pitchOffset.y),
                                        Mathf.Max(starboard-rollOffset.y,port+rollOffset.y));
            for(int x=-1;x<=1;x+=2)
            for(int z=-1;z<=1;z+=2)
            {
                Vector3 offset=body.rotation*new Vector3(x*halfWidth,0f,z*halfLength);
                highestWaveAtHull=Mathf.Max(highestWaveAtHull,
                    water.GetSurfaceHeight(center+offset)-offset.y);
            }
        }
        return (bow+stern+starboard+port)*.25f;
    }

    // Only restore speed after a sudden loss when we can prove the water ahead
    // has no shoreline or real obstacle. Real collisions must still stop boats.
    private bool HasClearOpenWaterAhead(Vector3 forward)
    {
        float hullWidth=Data.ID=="raft"?2.6749f:Mathf.Max(0.5f,Data.HullSize.x);
        float hullLength=Data.ID=="raft"?4.5634f:Mathf.Max(0.5f,Data.HullSize.z);
        Quaternion yaw=Quaternion.LookRotation(forward,Vector3.up);
        Vector3 centre=body.position+forward*(hullLength*.34f);
        float verticalExtent=Mathf.Abs(body.position.y-water.BaseWaterLevel)*.5f+1.6f;
        centre.y=(body.position.y+water.BaseWaterLevel)*.5f;
        Vector3 half=new Vector3(hullWidth*.5f+.25f,verticalExtent,hullLength*.18f+.65f);
        int hits;
        while(true)
        {
            hits=Physics.OverlapBoxNonAlloc(centre,half,waveSafetyContacts,yaw,
                                          solidNavigationMask,QueryTriggerInteraction.Ignore);
            if(hits<waveSafetyContacts.Length)break;
            // Grow only on saturation, retain the buffer, and rerun the SAME query.
            // Never treat incomplete results as proof that the path is clear.
            if(waveSafetyContacts.Length>=8192)return false;
            System.Array.Resize(ref waveSafetyContacts,Mathf.Min(8192,waveSafetyContacts.Length*2));
        }
        for(int i=0;i<hits;i++)
        {
            Collider c=waveSafetyContacts[i];
            if(c==null || c.transform.IsChildOf(transform))continue;
            if(c is TerrainCollider)continue; // Depth is checked separately.
            var rider=c.GetComponentInParent<BoatPassenger>();
            if(rider!=null && rider.Boat==this)continue;
            if(c.GetComponentInParent<OceanWater>()!=null)continue;
            return false;
        }
        return true;
    }

    private void PreserveDeepWaterMomentum(Vector3 forward,Vector3 plannedDirection,
                                           float propulsion,float targetSpeed,
                                           float headingError,bool shallowAhead)
    {
        float current=Vector3.Dot(body.linearVelocity,forward);
        // The player must still be actively propelling approximately forward.
        // Releasing the input, braking, or deliberately turning remains natural.
        bool driving=propulsion>.05f && plannedDirection.sqrMagnitude>.0001f
                     && Mathf.Abs(headingError)<30f && !shallowAhead;
        if(driving && wasPropelling && lastDrivenForwardSpeed>0.9f)
        {
            float expected=Mathf.Min(lastDrivenForwardSpeed,targetSpeed)
                           -MaxOpenWaterDeceleration*Time.fixedDeltaTime;
            if(expected>0.7f && current<expected && HasClearOpenWaterAhead(forward))
            {
                // Never touch vertical velocity (splashing/downward motion) or
                // the sideways component. Only suppress implausible instant
                // forward-speed loss in genuinely obstacle-free deep water.
                body.linearVelocity+=forward*(expected-current);
                current=expected;
            }
        }
        wasPropelling=driving;
        lastDrivenForwardSpeed=driving?Mathf.Max(0f,current):0f;
    }

    private void FixedUpdate()
    {
        if(body==null || water==null || Data==null || (Shared!=null && Shared.Replica))return;

        // Horizontal axes keep thrust and collision checks stable while tilted.
        Vector3 forward=Vector3.ProjectOnPlane(body.rotation*Vector3.forward,Vector3.up);
        if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;
        forward.Normalize();
        Vector3 right=Vector3.Cross(Vector3.up,forward);
        float highestWaveAtHull;
        ApplyWaveRocking(forward,right,out highestWaveAtHull);
        float centerSurface=water.GetSurfaceHeight(body.position);

        // Large offshore crests can rise far above the hull's centre. Trying to
        // lift the ENTIRE rowboat above the highest bow crest makes it shoot up,
        // crash down and feel stuck against the next wave. The bow may instead
        // dip through the water briefly while the body follows the central swell.
        float crestLift=IsRowboat
            ? Mathf.Clamp(highestWaveAtHull-centerSurface,0f,MaxCrestLift)
            : 0f;
        float targetHeight=centerSurface+(IsRowboat?RowboatFreeboard+crestLift:0f);
        if(!buoyancyStarted)
        {
            filteredBuoyancyHeight=targetHeight;
            buoyancyStarted=true;
        }
        float change=targetHeight-filteredBuoyancyHeight;
        float rate=change>=0f?BuoyancyRiseSpeed:BuoyancyFallSpeed;
        filteredBuoyancyHeight+=Mathf.Clamp(change,-rate*Time.fixedDeltaTime,
                                            rate*Time.fixedDeltaTime);

        // Vertical force is ONLY vertical: waves never directly brake X/Z.
        float difference=filteredBuoyancyHeight-body.position.y;
        float lift=IsRowboat
            ? difference*28f-body.linearVelocity.y*11f
            : difference*18f-body.linearVelocity.y*8f;
        body.AddForce(Vector3.up*Mathf.Clamp(lift,-18f,24f),ForceMode.Acceleration);

        // Boats should track cleanly rather than skating sideways across the water.
        Vector3 planarVelocity=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up);
        Vector3 side=right*Vector3.Dot(planarVelocity,right);
        body.AddForce(-side*9f,ForceMode.Acceleration);

        Vector2 input=Driver!=null?Driver.Controls.BoatInput:NetworkFrontInput;
        float inputAmount=Mathf.Clamp01(input.magnitude);
        // Only the driver steers. A rear rower can propel the current heading while
        // the driver rests, but never contributes merely by being aboard.
        bool rearPaddling=RearPaddling;
        float propulsion=rearPaddling?1f:inputAmount;
        float speed=RowingSpeed;
        Vector3 desiredDirection=Vector3.zero;
        float headingError=0f;
        // Clearance and steering predictions must ignore hull pitch/roll.
        Quaternion futureHeading=Quaternion.LookRotation(forward,Vector3.up);

        if((Driver!=null || NetworkHasDriver) && inputAmount>.05f)
        {
            if(driverView==null && Driver!=null)driverView=Driver.GetComponentInChildren<Camera>();
            Quaternion look=Driver!=null?(driverView!=null?driverView.transform.rotation:Driver.transform.rotation):Quaternion.Euler(0f,NetworkFrontYaw,0f);

            Vector3 lookForward=Vector3.ProjectOnPlane(look*Vector3.forward,Vector3.up);
            Vector3 lookRight=Vector3.ProjectOnPlane(look*Vector3.right,Vector3.up);
            if(lookForward.sqrMagnitude<.0001f)lookForward=forward;
            if(lookRight.sqrMagnitude<.0001f)lookRight=right;
            lookForward.Normalize();lookRight.Normalize();

            // Keep first-person motorboat control and all other boats' existing
            // camera-relative steering unchanged. In third-person motorboat mode
            // the camera is merely orbiting/recentering behind the bow: steering
            // against a fixed player-camera yaw makes a held left/right stick stop
            // turning once the hull reaches that world-space heading.
            // Resolve only that mode's joystick against the moving hull, so a
            // held sideways input keeps requesting a turn as the hull rotates.
            bool motorboatThirdPerson = IsMotorboat && Driver != null &&
                Driver.Controls != null && Driver.Controls.IsThirdPersonView;
            desiredDirection = motorboatThirdPerson
                ? forward * input.y + right * input.x
                : lookForward * input.y + lookRight * input.x;
            desiredDirection=Vector3.ProjectOnPlane(desiredDirection,Vector3.up);
            if(desiredDirection.sqrMagnitude>.0001f)
            {
                desiredDirection.Normalize();
                headingError=Vector3.SignedAngle(forward,desiredDirection,Vector3.up);

                // Deliberately slower than first-person turning. The desired yaw
                // speed tops out at 32 degrees/sec and the torque eases into it.
                float desiredYawSpeed=Mathf.Clamp(headingError*1.25f,-32f,32f);
                float currentYawSpeed=body.angularVelocity.y*Mathf.Rad2Deg;
                float yawAcceleration=Mathf.Clamp((desiredYawSpeed-currentYawSpeed)*Mathf.Deg2Rad*5.2f,-3.0f,3.0f);
                body.AddTorque(Vector3.up*yawAcceleration,ForceMode.Acceleration);

                float predictedTurn=Mathf.Clamp(headingError,-18f,18f);
                futureHeading=Quaternion.AngleAxis(predictedTurn,Vector3.up)*futureHeading;
            }
        }

        if(rearPaddling && inputAmount<=.05f)desiredDirection=forward;
        Vector3 predictedForward=futureHeading*Vector3.forward;
        Vector3 future=body.position+planarVelocity*.8f+predictedForward*(propulsion*1.5f);
        // The full placement OverlapBox can falsely flag offshore colliders or
        // map edges as walls. Runtime pre-braking checks only real shallow terrain.
        // Actual rocks, docks and the shore retain their Rigidbody collisions.
        bool shallowAhead=BoatClearance.ApproachingShallowWater(Data,future,futureHeading,water);
        if(shallowAhead)
        {
            wasPropelling=false;
            lastDrivenForwardSpeed=0f;
            body.AddForce(-planarVelocity*7f,ForceMode.Acceleration);
            body.AddTorque(Vector3.up*(-body.angularVelocity.y*5f),ForceMode.Acceleration);
            return;
        }

        // Let an unattended motorboat glide farther after Leave Helm. Cancel
        // only 0.15 of the existing 0.65 forward damping (about 30% more range).
        // Keep vertical/side damping and the shallow-water braking above intact.
        if(IsMotorboat && Driver==null && !NetworkHasDriver && propulsion<=.05f)
        {
            float coastingSpeed=Mathf.Max(0f,Vector3.Dot(planarVelocity,forward));
            float dragRelief=Mathf.Min(.15f,body.linearDamping);
            body.AddForce(forward*(coastingSpeed*dragRelief),ForceMode.Acceleration);
        }

        if(propulsion>.05f && desiredDirection.sqrMagnitude>.0001f)
        {
            float forwardSpeed=Vector3.Dot(planarVelocity,forward);
            float alignment=Mathf.Clamp01(1f-Mathf.Abs(headingError)/120f);
            float thrustScale=Mathf.Lerp(.22f,1f,alignment);

            // Turn first, then smoothly build forward motion as the bow lines up
            // with the camera-relative joystick direction.
            if(IsRowboat)
            {
                // Closed-loop thrust reaches the stated speed despite drag. A second
                // active rower doubles the target (including on partial stick input).
                float target=speed*propulsion*thrustScale;
                float acceleration=(target-forwardSpeed)*2.5f+forwardSpeed*body.linearDamping;
                body.AddForce(forward*acceleration,ForceMode.Acceleration);
            }
            else if(IsMotorboat)
            {
                // Constant 9 m/s^2 ramp at full throttle: zero to 27 m/s in
                // three seconds on open water. Counter linear damping without
                // teleporting the Rigidbody, retaining collisions and buoyancy.
                float target=speed*propulsion*thrustScale;
                float allowedDelta=speed/MotorboatAccelerationTime*Time.fixedDeltaTime;
                float nextSpeed=Mathf.MoveTowards(forwardSpeed,target,allowedDelta);
                float acceleration=(nextSpeed-forwardSpeed)/Time.fixedDeltaTime+
                                   forwardSpeed*body.linearDamping;
                body.AddForce(forward*acceleration,ForceMode.Acceleration);
            }
            else if(forwardSpeed<Data.Speed)
                body.AddForce(forward*inputAmount*Data.Speed*.8f*thrustScale,ForceMode.Acceleration);
        }

        // A downward impact into the next large swell must not erase the
        // forward speed the player was already carrying across open water.
        // This is guarded by a non-alloc solid-obstacle check and seabed depth.
        float targetForwardSpeed=speed*propulsion*
            Mathf.Lerp(.22f,1f,Mathf.Clamp01(1f-Mathf.Abs(headingError)/120f));
        PreserveDeepWaterMomentum(forward,desiredDirection,propulsion,
                                  targetForwardSpeed,headingError,shallowAhead);
    }
}



