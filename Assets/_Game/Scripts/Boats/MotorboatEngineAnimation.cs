using UnityEngine;

// Plays the imported FBX motor animation only while the motorboat is moving.
// Remote boats are kinematic, so their speed is measured from replicated poses.
[DisallowMultipleComponent]
public sealed class MotorboatEngineAnimation : MonoBehaviour
{
    public Animator MotorAnimator;
    public Transform FallbackPropeller;
    public float MovingThreshold = 0.18f;
    public float FallbackSpinDegreesPerSecond = 1440f;

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
        if(boat == null || boat.Data == null || boat.Data.ID != "sailboat")
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
        bool running = boat.enabled && speed > MovingThreshold;
        bool hasImportedAnimation = MotorAnimator != null && MotorAnimator.runtimeAnimatorController != null;
        if(hasImportedAnimation) MotorAnimator.speed = running ? 1f : 0f;
        else if(running && FallbackPropeller != null)
            FallbackPropeller.Rotate(Vector3.forward, FallbackSpinDegreesPerSecond * Time.deltaTime, Space.Self);
        lastPosition = transform.position;
        measured = true;
    }
}
