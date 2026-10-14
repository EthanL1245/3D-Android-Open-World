using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoatController : MonoBehaviour
{
    public BoatData Data;
    public Transform DriverSeat;
    public Transform DeckExit;
    public Transform SecondRowingSeat;
    public BoatPassenger SecondRower {get;private set;}
    public bool IsRowboat => Data!=null && Data.ID=="rowboat";
    public int ActivePaddlers => (HasInput(Driver)?1:0)+(IsRowboat && HasInput(SecondRower)?1:0);
    public float RowingSpeed => Data==null?0f:Data.Speed*(IsRowboat?Mathf.Max(1,ActivePaddlers):1);
    private static bool HasInput(BoatPassenger p) => p!=null && p.Controls!=null && p.Controls.BoatInput.sqrMagnitude>.0025f;
    public Transform SeatFor(BoatPassenger p) => p==SecondRower?SecondRowingSeat:DriverSeat;
    public bool CanUseSeat(BoatPassenger p) => p!=null && (p.Driving ||
        (Driver==null && Vector3.Distance(p.transform.position,DriverSeat.position)<2.5f) ||
        (IsRowboat && SecondRower==null && SecondRowingSeat!=null && Vector3.Distance(p.transform.position,SecondRowingSeat.position)<2.5f));
    public BoatPassenger Driver {get;private set;}
    public int PassengerCount {get;private set;}

    private Rigidbody body;
    private OceanWater water;
    private Camera driverView;

    // Four water samples steer the Rigidbody's pitch/roll without new meshes.
    private const float RaftTiltLimit = 13f;
    private const float RowboatTiltLimit = 11f;
    private const float SailboatTiltLimit = 8f;
    private const float YachtTiltLimit = 5f;
    private const float RockingSpring = 26f;
    private const float RockingDamping = 8f;

    public void Initialize(BoatData data,OceanWater ocean)
    {
        Data=data;water=ocean;body=GetComponent<Rigidbody>();
        body.mass=300;body.useGravity=false;body.linearDamping=.65f;body.angularDamping=3;
        // Permit wave-induced pitching/rolling. Yaw is still controlled by steering.
        body.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
        body.interpolation=RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
    }

    public bool Board()
    {
        if(Data==null || PassengerCount>=Data.MaxCapacity)return false;
        PassengerCount++;
        return true;
    }

    public void Leave(BoatPassenger passenger)
    {
        ReleaseHelm(passenger);
        PassengerCount=Mathf.Max(0,PassengerCount-1);
    }

    public bool TakeHelm(BoatPassenger passenger)
    {
        if(passenger==null || passenger.Boat!=this)return false;
        if(Driver==passenger || SecondRower==passenger)return true;
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
        if(SecondRower==passenger)SecondRower=null;
        if(Driver!=passenger)return;
        // The remaining rower takes the front seat so solo always uses the front pair.
        Driver=SecondRower;SecondRower=null;
        driverView=Driver!=null?Driver.GetComponentInChildren<Camera>():null;
        if(Driver!=null)Driver.SnapToRowingSeat();
    }

    private const float RowboatFreeboard = 0.50f;

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

        // Angular spring/damping affects pitch and roll but not steering yaw.
        Vector3 tiltError=Vector3.ProjectOnPlane(
            Vector3.Cross(body.rotation*Vector3.up,desiredUp),Vector3.up);
        Vector3 tiltVelocity=Vector3.ProjectOnPlane(body.angularVelocity,Vector3.up);
        Vector3 acceleration=tiltError*RockingSpring-tiltVelocity*RockingDamping;
        body.AddTorque(Vector3.ClampMagnitude(acceleration,8f),ForceMode.Acceleration);

        // Reuse wave rocking's four probes, accounting for lowered sides of a
        // pitched/rolled hull, then cover curvature with four corner probes.
        highestWaveAtHull=0f;
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

    private void FixedUpdate()
    {
        if(body==null || water==null || Data==null)return;

        // Horizontal axes keep thrust and collision checks stable while tilted.
        Vector3 forward=Vector3.ProjectOnPlane(body.rotation*Vector3.forward,Vector3.up);
        if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;
        forward.Normalize();
        Vector3 right=Vector3.Cross(Vector3.up,forward);
        float highestWaveAtHull;
        ApplyWaveRocking(forward,right,out highestWaveAtHull);
        float centerSurface=water.GetSurfaceHeight(body.position);
        float height=IsRowboat
            ? Mathf.Max(centerSurface,highestWaveAtHull)+RowboatFreeboard
            : centerSurface;

        if(IsRowboat)
        {
            // Keep the hollow floor above crests without teleporting upward or
            // resetting horizontal momentum. The spring allows brief hull dips.
            float rise=height-body.position.y;
            float buoyancy=rise*105f-body.linearVelocity.y*18f;
            body.AddForce(Vector3.up*Mathf.Clamp(buoyancy,-30f,48f),ForceMode.Acceleration);
        }
        else body.AddForce(Vector3.up*((height-body.position.y)*18f-body.linearVelocity.y*7f),ForceMode.Acceleration);

        // Boats should track cleanly rather than skating sideways across the water.
        Vector3 planarVelocity=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up);
        Vector3 side=right*Vector3.Dot(planarVelocity,right);
        body.AddForce(-side*9f,ForceMode.Acceleration);

        Vector2 input=Driver!=null?Driver.Controls.BoatInput:Vector2.zero;
        float inputAmount=Mathf.Clamp01(input.magnitude);
        // Only the driver steers. A rear rower can propel the current heading while
        // the driver rests, but never contributes merely by being aboard.
        bool rearPaddling=IsRowboat && HasInput(SecondRower);
        float propulsion=rearPaddling?1f:inputAmount;
        float speed=RowingSpeed;
        Vector3 desiredDirection=Vector3.zero;
        float headingError=0f;
        // Clearance and steering predictions must ignore hull pitch/roll.
        Quaternion futureHeading=Quaternion.LookRotation(forward,Vector3.up);

        if(Driver!=null && inputAmount>.05f)
        {
            if(driverView==null)driverView=Driver.GetComponentInChildren<Camera>();
            Transform look=driverView!=null?driverView.transform:Driver.transform;

            Vector3 lookForward=Vector3.ProjectOnPlane(look.forward,Vector3.up);
            Vector3 lookRight=Vector3.ProjectOnPlane(look.right,Vector3.up);
            if(lookForward.sqrMagnitude<.0001f)lookForward=forward;
            if(lookRight.sqrMagnitude<.0001f)lookRight=right;
            lookForward.Normalize();lookRight.Normalize();

            // Same mental model as walking: joystick direction is interpreted in
            // camera space. The hull then turns toward that travel direction instead
            // of instantly strafing or using the joystick X axis as a rudder.
            desiredDirection=lookForward*input.y+lookRight*input.x;
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
        if(BoatClearance.ApproachingShallowWater(Data,future,futureHeading,water))
        {
            body.AddForce(-planarVelocity*7f,ForceMode.Acceleration);
            body.AddTorque(Vector3.up*(-body.angularVelocity.y*5f),ForceMode.Acceleration);
            return;
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
            else if(forwardSpeed<Data.Speed)
                body.AddForce(forward*inputAmount*Data.Speed*.8f*thrustScale,ForceMode.Acceleration);
        }
    }
}


