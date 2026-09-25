using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoatController : MonoBehaviour
{
    public BoatData Data;
    public Transform DriverSeat;
    public Transform DeckExit;
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
        if(Driver==passenger)
        {
            Driver=null;
            driverView=null;
        }
        PassengerCount=Mathf.Max(0,PassengerCount-1);
    }

    public bool TakeHelm(BoatPassenger passenger)
    {
        if(Driver!=null && Driver!=passenger)return false;
        Driver=passenger;
        driverView=passenger!=null?passenger.GetComponentInChildren<Camera>():null;
        return true;
    }

    public void ReleaseHelm(BoatPassenger passenger)
    {
        if(Driver!=passenger)return;
        Driver=null;
        driverView=null;
    }

    private void FixedUpdate()
    {
        if(body==null || water==null || Data==null)return;

        float height=water.GetSurfaceHeight(transform.position);
        body.AddForce(Vector3.up*((height-body.position.y)*18f-body.linearVelocity.y*7f),ForceMode.Acceleration);

        // Boats should track cleanly rather than skating sideways across the water.
        Vector3 planarVelocity=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up);
        Vector3 side=transform.right*Vector3.Dot(planarVelocity,transform.right);
        body.AddForce(-side*9f,ForceMode.Acceleration);

        Vector2 input=Driver!=null?Driver.Controls.BoatInput:Vector2.zero;
        float inputAmount=Mathf.Clamp01(input.magnitude);
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

        Vector3 predictedForward=futureHeading*Vector3.forward;
        Vector3 future=body.position+planarVelocity*.8f+predictedForward*(inputAmount*1.5f);
        if(!BoatClearance.Valid(Data,future,futureHeading,water,transform,Driver!=null?Driver.transform:null))
        {
            body.AddForce(-planarVelocity*12f,ForceMode.Acceleration);
            body.AddTorque(-body.angularVelocity*12f,ForceMode.Acceleration);
            return;
        }

        if(inputAmount>.05f && desiredDirection.sqrMagnitude>.0001f)
        {
            float forwardSpeed=Vector3.Dot(planarVelocity,transform.forward);
            float alignment=Mathf.Clamp01(1f-Mathf.Abs(headingError)/120f);
            float thrustScale=Mathf.Lerp(.22f,1f,alignment);

            // Turn first, then smoothly build forward motion as the bow lines up
            // with the camera-relative joystick direction.
            if(forwardSpeed<Data.Speed)
                body.AddForce(transform.forward*inputAmount*Data.Speed*.8f*thrustScale,ForceMode.Acceleration);
        }
    }
}
