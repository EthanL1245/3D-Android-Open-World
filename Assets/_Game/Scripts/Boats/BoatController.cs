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

    public void Initialize(BoatData data,OceanWater ocean)
    {
        Data=data;water=ocean;body=GetComponent<Rigidbody>();
        body.mass=300;body.useGravity=false;body.linearDamping=.65f;body.angularDamping=3;
        body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
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

    public static float FloatingHeight(BoatData data,OceanWater ocean,Vector3 position,Quaternion rotation)
    {
        float surface=ocean.GetSurfaceHeight(position);
        if(data==null || data.ID!="rowboat")return surface;
        // The authored inner floor is at -0.247 m; clear the highest wave across
        // the entire hull rather than allowing bow/stern crests through the floor.
        for(int x=-1;x<=1;x++)for(int z=-4;z<=4;z++)
            surface=Mathf.Max(surface,ocean.GetSurfaceHeight(position+rotation*new Vector3(x*1.2f,0,z*.96f)));
        return surface+.38f;
    }

    private void FixedUpdate()
    {
        if(body==null || water==null || Data==null)return;

        float height=FloatingHeight(Data,water,body.position,body.rotation);
        if(IsRowboat)
        {
            // A rising crest must not flood the shallow interior while the spring
            // catches up. Descent stays damped, and horizontal momentum is preserved.
            float minimum=height-.06f;
            if(body.position.y<minimum)
            {
                var p=body.position;p.y=minimum;body.position=p;
                var velocity=body.linearVelocity;velocity.y=Mathf.Max(0,velocity.y);body.linearVelocity=velocity;
            }
            body.AddForce(Vector3.up*((height-body.position.y)*80f-body.linearVelocity.y*18f),ForceMode.Acceleration);
        }
        else body.AddForce(Vector3.up*((height-body.position.y)*18f-body.linearVelocity.y*7f),ForceMode.Acceleration);

        // Boats should track cleanly rather than skating sideways across the water.
        Vector3 planarVelocity=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up);
        Vector3 side=transform.right*Vector3.Dot(planarVelocity,transform.right);
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
        Quaternion futureHeading=body.rotation;

        if(Driver!=null && inputAmount>.05f)
        {
            if(driverView==null)driverView=Driver.GetComponentInChildren<Camera>();
            Transform look=driverView!=null?driverView.transform:Driver.transform;

            Vector3 lookForward=Vector3.ProjectOnPlane(look.forward,Vector3.up);
            Vector3 lookRight=Vector3.ProjectOnPlane(look.right,Vector3.up);
            if(lookForward.sqrMagnitude<.0001f)lookForward=transform.forward;
            if(lookRight.sqrMagnitude<.0001f)lookRight=transform.right;
            lookForward.Normalize();lookRight.Normalize();

            // Same mental model as walking: joystick direction is interpreted in
            // camera space. The hull then turns toward that travel direction instead
            // of instantly strafing or using the joystick X axis as a rudder.
            desiredDirection=lookForward*input.y+lookRight*input.x;
            desiredDirection=Vector3.ProjectOnPlane(desiredDirection,Vector3.up);
            if(desiredDirection.sqrMagnitude>.0001f)
            {
                desiredDirection.Normalize();
                headingError=Vector3.SignedAngle(transform.forward,desiredDirection,Vector3.up);

                // Deliberately slower than first-person turning. The desired yaw
                // speed tops out at 32 degrees/sec and the torque eases into it.
                float desiredYawSpeed=Mathf.Clamp(headingError*1.25f,-32f,32f);
                float currentYawSpeed=body.angularVelocity.y*Mathf.Rad2Deg;
                float yawAcceleration=Mathf.Clamp((desiredYawSpeed-currentYawSpeed)*Mathf.Deg2Rad*5.2f,-3.0f,3.0f);
                body.AddTorque(Vector3.up*yawAcceleration,ForceMode.Acceleration);

                float predictedTurn=Mathf.Clamp(headingError,-18f,18f);
                futureHeading=Quaternion.AngleAxis(predictedTurn,Vector3.up)*body.rotation;
            }
        }

        if(rearPaddling && inputAmount<=.05f)desiredDirection=transform.forward;
        Vector3 predictedForward=futureHeading*Vector3.forward;
        Vector3 future=body.position+planarVelocity*.8f+predictedForward*(propulsion*1.5f);
        if(!BoatClearance.Valid(Data,future,futureHeading,water,transform,Driver!=null?Driver.transform:null))
        {
            body.AddForce(-planarVelocity*12f,ForceMode.Acceleration);
            body.AddTorque(-body.angularVelocity*12f,ForceMode.Acceleration);
            return;
        }

        if(propulsion>.05f && desiredDirection.sqrMagnitude>.0001f)
        {
            float forwardSpeed=Vector3.Dot(planarVelocity,transform.forward);
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
                body.AddForce(transform.forward*acceleration,ForceMode.Acceleration);
            }
            else if(forwardSpeed<Data.Speed)
                body.AddForce(transform.forward*inputAmount*Data.Speed*.8f*thrustScale,ForceMode.Acceleration);
        }
    }
}


