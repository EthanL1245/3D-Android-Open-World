using UnityEngine;

// Keeps the imported FBX motor animation intact and layers three spatial engine
// recordings by measured boat speed. Replicas derive speed from networked poses.
[DisallowMultipleComponent]
public sealed class MotorboatEngineAnimation : MonoBehaviour
{
    public Animator MotorAnimator;
    public Transform FallbackPropeller;
    public float MovingThreshold = 0.18f;
    public float FallbackSpinDegreesPerSecond = 1440f;

    private const float EngineTopSpeed = 27f;
    private const float CrossfadeRadians = Mathf.PI * 0.5f;
    private const float VolumeResponse = 9f;
    private const float SpeedResponse = 7f;
    private static readonly string[] ClipNames = {
        "Boats/MotorboatAudio/slow",
        "Boats/MotorboatAudio/moderate_speed",
        "Boats/MotorboatAudio/fast"
    };

    private BoatController boat;
    private Rigidbody body;
    private Vector3 lastPosition;
    private bool measured;
    private bool clipsChecked;
    private float audioSpeed;
    private readonly AudioSource[] engineSources = new AudioSource[3];

    private void Awake()
    {
        boat = GetComponent<BoatController>();
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        measured = false;
        audioSpeed = 0f;
        if(MotorAnimator != null) MotorAnimator.speed = 0f;
    }

    private void OnDisable()
    {
        for(int i = 0; i < engineSources.Length; i++)
        {
            var source = engineSources[i];
            if(source != null) { source.Stop(); source.volume = 0f; }
        }
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

        // Placement previews disable BoatController and must stay silent.
        bool running = boat.enabled;
        bool moving = running && speed > MovingThreshold;
        bool hasImportedAnimation = MotorAnimator != null && MotorAnimator.runtimeAnimatorController != null;
        if(hasImportedAnimation) MotorAnimator.speed = moving ? 1f : 0f;
        else if(moving && FallbackPropeller != null)
            FallbackPropeller.Rotate(Vector3.forward, FallbackSpinDegreesPerSecond * Time.deltaTime, Space.Self);

        UpdateEngineAudio(speed, running);
        lastPosition = transform.position;
        measured = true;
    }

    private void PrepareAudio()
    {
        if(clipsChecked) return;
        clipsChecked = true;
        for(int i = 0; i < engineSources.Length; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>(ClipNames[i]);
            if(clip == null)
            {
                Debug.LogWarning("Motorboat engine audio clip missing from Resources: " + ClipNames[i], this);
                continue;
            }
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 1f;
            source.spatialize = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 12f;
            source.maxDistance = 110f;
            source.priority = 160;
            source.volume = 0f;
            engineSources[i] = source;
        }
    }

    private static float SmoothRange(float a, float b, float value)
    {
        float t = Mathf.Clamp01((value - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private void UpdateEngineAudio(float measuredSpeed, bool running)
    {
        if(!running) { OnDisable(); return; }
        PrepareAudio();

        float dt = Time.deltaTime;
        if(dt <= 0f) return;

        // Smooth replicated position jitter, without changing any boat physics.
        float speedBlend = 1f - Mathf.Exp(-SpeedResponse * dt);
        audioSpeed = Mathf.Lerp(audioSpeed, Mathf.Clamp(measuredSpeed, 0f, EngineTopSpeed), speedBlend);

        float throttle = boat.Driver != null && boat.Driver.Controls != null
            ? boat.Driver.Controls.BoatInput.magnitude : boat.NetworkFrontInput.magnitude;
        // A small immediate rev response precedes the three-second speed ramp.
        float audibleSpeed = Mathf.Min(EngineTopSpeed, audioSpeed + 1.4f * Mathf.Clamp01(throttle));
        float lowToMid = SmoothRange(2f, 13f, audibleSpeed);
        float midToHigh = SmoothRange(15f, EngineTopSpeed, audibleSpeed);
        float[] weights = {
            Mathf.Cos(lowToMid * CrossfadeRadians),
            Mathf.Sin(lowToMid * CrossfadeRadians) * Mathf.Cos(midToHigh * CrossfadeRadians),
            Mathf.Sin(midToHigh * CrossfadeRadians)
        };
        float pitchBoost = 0.045f * Mathf.Clamp01(throttle);
        float[] pitches = {
            Mathf.Lerp(0.95f, 1.09f, lowToMid) + pitchBoost,
            Mathf.Lerp(0.94f, 1.10f, midToHigh) + pitchBoost,
            Mathf.Lerp(0.96f, 1.13f, midToHigh) + pitchBoost
        };

        float volumeBlend = 1f - Mathf.Exp(-VolumeResponse * dt);
        for(int i = 0; i < engineSources.Length; i++)
        {
            var source = engineSources[i];
            if(source == null) continue;
            float target = weights[i] * 0.48f;
            source.volume = Mathf.Lerp(source.volume, target, volumeBlend);
            source.pitch = Mathf.Lerp(source.pitch, pitches[i], volumeBlend);
            // Do not decode/voice silent loops on Android or for distant boats.
            if(target > 0.005f && !source.isPlaying) source.Play();
            else if(target <= 0.005f && source.volume < 0.005f && source.isPlaying)
            {
                source.Stop();
                source.volume = 0f;
            }
        }
    }
}
