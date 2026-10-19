using UnityEngine;

// Keeps the imported FBX motor animation intact; engine audio follows helm state,
// forward input and measured speed. Replicas use networked input and poses.
[DisallowMultipleComponent]
public sealed class MotorboatEngineAnimation : MonoBehaviour
{
    public Animator MotorAnimator;
    public Transform FallbackPropeller;
    public float MovingThreshold = 0.18f;
    public float FallbackSpinDegreesPerSecond = 1440f;

    private const float EngineTopSpeed = 27f;
    private const float CrossfadeRadians = Mathf.PI * 0.5f;
    private const float VolumeResponse = 4f;
    private const float SpeedResponse = 7f;
    private static readonly string[] ClipNames = {
        "Boats/MotorboatAudio/slow",
        "Boats/MotorboatAudio/moderate_speed",
        "Boats/MotorboatAudio/fast",
        "Boats/MotorboatAudio/idle"
    };

    private BoatController boat;
    private Rigidbody body;
    private Vector3 lastPosition;
    private bool measured;
    private bool clipsChecked;
    private float audioSpeed;
    private readonly AudioSource[] engineSources = new AudioSource[4];
    private AudioSource ignitionSource;
    private AudioClip startClip, stopClip;
    private bool hadDriver, engineRunning;

    private void Awake()
    {
        boat = GetComponent<BoatController>();
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        measured = false;
        audioSpeed = 0f;
        hadDriver = engineRunning = false;
        if(MotorAnimator != null) MotorAnimator.speed = 0f;
    }

    private void OnDisable()
    {
        hadDriver = engineRunning = false;
        if(ignitionSource != null) ignitionSource.Stop();
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
            var source = CreateSource();
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            engineSources[i] = source;
        }
        startClip = Resources.Load<AudioClip>("Boats/MotorboatAudio/start");
        stopClip = Resources.Load<AudioClip>("Boats/MotorboatAudio/stop");
        ignitionSource = CreateSource();
        ignitionSource.volume = 0.48f;
    }

    private AudioSource CreateSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.spatialize = false;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 12f;
        source.maxDistance = 110f;
        source.priority = 160;
        return source;
    }

    private void PlayIgnition(AudioClip clip)
    {
        ignitionSource.Stop();
        ignitionSource.clip = clip;
        if(clip != null) ignitionSource.Play();
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

        bool hasDriver = boat.Driver != null || boat.NetworkHasDriver;
        if(hasDriver && !hadDriver)
        {
            engineRunning = true;
            PlayIgnition(startClip);
        }
        // Leaving the helm lets the engine coast down before one shutdown sound.
        // Retaking the helm cancels that pending shutdown.
        if(engineRunning && !hasDriver && measuredSpeed <= 18f)
        {
            engineRunning = false;
            PlayIgnition(stopClip);
        }
        hadDriver = hasDriver;

        Vector2 input = boat.Driver != null
            ? (boat.Driver.Controls != null ? boat.Driver.Controls.BoatInput : Vector2.zero)
            : boat.NetworkFrontInput;
        bool idle = hasDriver && (input.y <= 0.05f || audioSpeed < 1f);
        // Broad equal-power blends centered on the 12 and 24 m/s band boundaries.
        // No throttle-based speed offset, and no pitch shifting of the recordings.
        float lowToMid = SmoothRange(8f, 16f, audioSpeed);
        float midToHigh = SmoothRange(21f, 27f, audioSpeed);
        float driveGain = engineRunning && !idle ? 1f : 0f;
        float idleGain = engineRunning && idle ? 1f : 0f;
        // Let the short start recording lead into the loops without a loud pileup.
        if(ignitionSource.isPlaying && ignitionSource.clip == startClip)
        {
            driveGain *= 0.3f;
            idleGain *= 0.3f;
        }
        float blend = 1f - Mathf.Exp(-VolumeResponse * dt);
        ApplyLayer(0, driveGain * Mathf.Cos(lowToMid * CrossfadeRadians), 1f, blend);
        ApplyLayer(1, driveGain * Mathf.Sin(lowToMid * CrossfadeRadians) * Mathf.Cos(midToHigh * CrossfadeRadians), 1f, blend);
        ApplyLayer(2, driveGain * Mathf.Sin(midToHigh * CrossfadeRadians), 1f, blend);
        ApplyLayer(3, idleGain, 1f, blend);
    }

    private void ApplyLayer(int index, float weight, float pitch, float blend)
    {
        var source = engineSources[index];
        if(source == null) return;
        // Raise only slow/moderate/fast; idle and ignition keep their levels.
        float target = weight * 0.48f * (index < 3 ? 1.61f : 1f);
        source.volume = Mathf.Lerp(source.volume, target, blend);
        source.pitch = Mathf.Lerp(source.pitch, pitch, blend);
        // Stop inaudible layers to reduce concurrent audio voices on Android.
        if(target > 0.005f && !source.isPlaying) source.Play();
        else if(target <= 0.005f && source.volume < 0.005f && source.isPlaying)
        {
            source.Stop();
            source.volume = 0f;
        }
    }
}
