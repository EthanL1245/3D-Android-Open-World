using UnityEngine;

// Plays the imported FBX motor animation only while the motorboat is moving.
// Remote boats are kinematic, so their speed is measured from replicated poses.
[DisallowMultipleComponent]
public sealed class MotorboatEngineAnimation : MonoBehaviour
{
    public Animator MotorAnimator;
    public float MovingThreshold = 0.18f;

    private BoatController boat;
    private Rigidbody body;
    private Vector3 lastPosition;
    private bool measured;

    private void Awake()
    {
        boat = GetComponent<BoatController>();
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        measured = false;
        if(MotorAnimator != null) MotorAnimator.speed = 0f;
    }

    private void LateUpdate()
    {
        if(boat == null || boat.Data == null || boat.Data.ID != "sailboat" || MotorAnimator == null)
            return;

        float speed = 0f;
        if(measured && Time.deltaTime > 0.0001f)
        {
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            speed = delta.magnitude / Time.deltaTime;
        }
        if(body != null && !body.isKinematic)
        {
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            speed = velocity.magnitude;
        }

        // Placement ghosts disable BoatController, and should never animate.
        MotorAnimator.speed = boat.enabled && speed > MovingThreshold ? 1f : 0f;
        lastPosition = transform.position;
        measured = true;
    }
}
