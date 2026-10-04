using UnityEngine;

// Each boosted loop source has its own child object, isolating this gain from
// reel, ambience and other one-shots. AudioSource.volume itself is limited to 1.
public sealed class FishingAudioGain : MonoBehaviour
{
    public float Gain = 2f;
    private void OnAudioFilterRead(float[] data, int channels)
    {
        float gain = Gain;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;
    }
}
