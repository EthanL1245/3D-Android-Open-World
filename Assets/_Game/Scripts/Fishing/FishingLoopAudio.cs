using UnityEngine;

// The WAV assets contain sample-level tip overlaps. Schedule the lead-in and
// seamless cycle on the audio clock so short repeats never depend on frame rate.
public sealed class FishingLoopAudio
{
    private readonly AudioSource leadIn;
    private readonly AudioSource cycle;
    private float playbackPitch = 1f;
    private double startTime, joinTime, pitchUpdatedAt, leadSecondsRemaining;
    public bool IsPlaying { get; private set; }

    public FishingLoopAudio(GameObject owner, string leadInPath, string cyclePath, float gain = 1f)
    {
        leadIn = CreateSource(owner, leadInPath, false, gain);
        cycle = CreateSource(owner, cyclePath, true, gain);
    }

    private static AudioSource CreateSource(GameObject owner, string path, bool loop, float gain)
    {
        if (gain != 1f)
        {
            var audioObject = new GameObject("FishingLoopGain");
            audioObject.transform.SetParent(owner.transform, false);
            owner = audioObject;
            owner.AddComponent<FishingAudioGain>().Gain = gain;
        }
        AudioSource source = owner.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = loop;
        source.clip = Resources.Load<AudioClip>(path);
        return source;
    }

    public void Play()
    {
        Stop();
        if (leadIn.clip == null || cycle.clip == null) return;
        // Both sources use uncompressed, preloaded PCM; allow one small scheduling
        // lead, then join exactly at the sample boundary without frame polling.
        startTime = AudioSettings.dspTime + 0.02;
        pitchUpdatedAt = startTime;
        leadSecondsRemaining = (double)leadIn.clip.samples / leadIn.clip.frequency;
        joinTime = startTime + leadSecondsRemaining / playbackPitch;
        leadIn.pitch = cycle.pitch = playbackPitch;
        leadIn.PlayScheduled(startTime);
        cycle.PlayScheduled(joinTime);
        IsPlaying = true;
    }

    public void SetPitch(float pitch)
    {
        pitch = Mathf.Clamp(pitch, .25f, 3f);
        if (Mathf.Abs(pitch - playbackPitch) < .001f) return;
        double now = AudioSettings.dspTime;
        // Move the pending join with the intro's remaining audio time. Once the
        // cycle starts, native PCM looping retains its baked overlap at any pitch.
        if (IsPlaying && now < joinTime)
        {
            leadSecondsRemaining = System.Math.Max(0d, leadSecondsRemaining -
                System.Math.Max(0d, now - pitchUpdatedAt) * playbackPitch);
            pitchUpdatedAt = System.Math.Max(now, startTime);
            joinTime = pitchUpdatedAt + leadSecondsRemaining / pitch;
            cycle.SetScheduledStartTime(joinTime);
        }
        playbackPitch = pitch;
        leadIn.pitch = cycle.pitch = pitch;
    }

    public void Stop()
    {
        // Stop also cancels a scheduled cycle if the player releases during intro.
        if (leadIn != null) leadIn.Stop();
        if (cycle != null) cycle.Stop();
        IsPlaying = false;
    }
}
