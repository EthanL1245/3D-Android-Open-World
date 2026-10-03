using UnityEngine;

// The WAV assets contain sample-level tip overlaps. Schedule the lead-in and
// seamless cycle on the audio clock so short repeats never depend on frame rate.
public sealed class FishingLoopAudio
{
    private readonly AudioSource leadIn;
    private readonly AudioSource cycle;
    public bool IsPlaying { get; private set; }

    public FishingLoopAudio(GameObject owner, string leadInPath, string cyclePath)
    {
        leadIn = CreateSource(owner, leadInPath, false);
        cycle = CreateSource(owner, cyclePath, true);
    }

    private static AudioSource CreateSource(GameObject owner, string path, bool loop)
    {
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
        double start = AudioSettings.dspTime + 0.02;
        double join = start + (double)leadIn.clip.samples / leadIn.clip.frequency;
        leadIn.PlayScheduled(start);
        cycle.PlayScheduled(join);
        IsPlaying = true;
    }

    public void Stop()
    {
        // Stop also cancels a scheduled cycle if the player releases during intro.
        if (leadIn != null) leadIn.Stop();
        if (cycle != null) cycle.Stop();
        IsPlaying = false;
    }
}
