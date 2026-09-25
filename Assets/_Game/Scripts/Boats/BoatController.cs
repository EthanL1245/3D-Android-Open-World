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
    public void Initialize(BoatData data,OceanWater ocean)
    {
        Data=data;water=ocean;body=GetComponent<Rigidbody>();
        body.mass=300;body.useGravity=false;body.linearDamping=.65f;body.angularDamping=3;
        body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
        body.interpolation=RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
    }
    public bool Board(){if(Data==null || PassengerCount>=Data.MaxCapacity)return false;PassengerCount++;return true;}
    public void Leave(BoatPassenger passenger){if(Driver==passenger)Driver=null;PassengerCount=Mathf.Max(0,PassengerCount-1);}
    public bool TakeHelm(BoatPassenger passenger)
    {if(Driver!=null && Driver!=passenger)return false;Driver=passenger;return true;}
    public void ReleaseHelm(BoatPassenger passenger){if(Driver==passenger)Driver=null;}
    private void FixedUpdate()
    {
        if(body==null || water==null || Data==null)return;
        float height=water.GetSurfaceHeight(transform.position);
        body.AddForce(Vector3.up*((height-body.position.y)*18-body.linearVelocity.y*7),ForceMode.Acceleration);
        Vector3 side=transform.right*Vector3.Dot(body.linearVelocity,transform.right);
        body.AddForce(-side*9,ForceMode.Acceleration);
        Vector2 input=Driver!=null?Driver.Controls.BoatInput:Vector2.zero;
        Vector3 future=body.position+Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up)*.8f+transform.forward*input.y;
        Quaternion futureHeading=body.rotation*Quaternion.Euler(0,(body.angularVelocity.y*Mathf.Rad2Deg+input.x*15)*.5f,0);
        if(!BoatClearance.Valid(Data,future,futureHeading,water,transform,Driver!=null?Driver.transform:null))
        {
            body.AddForce(-Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up)*12,ForceMode.Acceleration);
            body.AddTorque(-body.angularVelocity*12,ForceMode.Acceleration);
            input=Vector2.zero;
        }
        float speed=Vector3.Dot(body.linearVelocity,transform.forward);
        if(Mathf.Abs(speed)<Data.Speed || Mathf.Sign(input.y)!=Mathf.Sign(speed))
            body.AddForce(transform.forward*input.y*Data.Speed*.8f,ForceMode.Acceleration);
        body.AddTorque(Vector3.up*input.x*2.8f,ForceMode.Acceleration);
    }
}
