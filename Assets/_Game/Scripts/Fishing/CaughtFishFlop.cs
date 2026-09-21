using UnityEngine;

/// <summary>
/// Adds extra caught-fish motion only to the rear half of compatible
/// bone-rigged fish. The mouth/front remain stable while the body and tail
/// provide most of the visible flop.
/// </summary>
public sealed class CaughtFishFlop : MonoBehaviour
{
    private Transform midBone;
    private Transform rearBone;
    private Transform tailBone;

    private Quaternion midRest;
    private Quaternion rearRest;
    private Quaternion tailRest;

    private float phase;
    private bool initialized;

    public void Initialize()
    {
        Transform[] bones =
            GetComponentsInChildren<Transform>(
                true
            );

        midBone =
            FindBone(
                bones,
                "Bone.002"
            );

        rearBone =
            FindBone(
                bones,
                "Bone.003"
            );

        tailBone =
            FindBone(
                bones,
                "Bone.004"
            );

        // Some rigs end at Bone.003.
        if (tailBone == null)
        {
            tailBone = rearBone;
            rearBone = midBone;
            midBone =
                FindBone(
                    bones,
                    "Bone.001"
                );
        }

        if (midBone != null)
            midRest = midBone.localRotation;

        if (rearBone != null)
            rearRest = rearBone.localRotation;

        if (tailBone != null)
            tailRest = tailBone.localRotation;

        phase =
            Random.Range(
                0f,
                Mathf.PI * 2f
            );

        initialized = true;
    }

    private void Awake()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        if (!initialized)
            Initialize();

        float t =
            Time.time * 9.2f +
            phase;

        // A caught fish should KICK, not smoothly wave. SharpWave creates
        // fast direction changes, while the phase delay sends the kick from
        // mid-body through the rear and finally into the tail.
        float burst =
            Mathf.Lerp(
                0.62f,
                1.12f,
                Mathf.SmoothStep(
                    0f,
                    1f,
                    0.5f +
                    0.5f *
                    Mathf.Sin(
                        t * 0.31f +
                        phase
                    )
                )
            );

        float midBeat =
            SharpWave(
                t * 1.05f
            );

        float rearBeat =
            SharpWave(
                t * 1.05f -
                0.48f
            );

        float tailBeat =
            SharpWave(
                t * 1.05f -
                0.98f
            );

        float twitch =
            SharpWave(
                t * 1.93f +
                1.4f
            );

        if (midBone != null)
        {
            midBone.localRotation =
                midRest *
                Quaternion.Euler(
                    twitch *
                    2.5f *
                    burst,
                    (
                        midBeat * 11f +
                        twitch * 3f
                    ) *
                    burst,
                    Mathf.Sin(
                        t * 0.77f
                    ) *
                    3f *
                    burst
                );
        }

        if (rearBone != null)
        {
            rearBone.localRotation =
                rearRest *
                Quaternion.Euler(
                    twitch *
                    5f *
                    burst,
                    (
                        rearBeat * 27f +
                        twitch * 8f
                    ) *
                    burst,
                    Mathf.Sin(
                        t * 0.91f +
                        0.8f
                    ) *
                    7f *
                    burst
                );
        }

        if (tailBone != null)
        {
            tailBone.localRotation =
                tailRest *
                Quaternion.Euler(
                    twitch *
                    8f *
                    burst,
                    (
                        tailBeat * 50f +
                        twitch * 15f
                    ) *
                    burst,
                    Mathf.Sin(
                        t * 1.14f +
                        1.6f
                    ) *
                    11f *
                    burst
                );
        }
    }

    private static float SharpWave(
        float value)
    {
        float wave =
            Mathf.Sin(
                value
            );

        return
            Mathf.Sign(
                wave
            ) *
            Mathf.Pow(
                Mathf.Abs(
                    wave
                ),
                0.48f
            );
    }

    private static Transform FindBone(
        Transform[] bones,
        string boneName)
    {
        for (int i = 0;
             i < bones.Length;
             i++)
        {
            Transform bone =
                bones[i];

            if (bone != null &&
                bone.name ==
                    boneName)
            {
                return bone;
            }
        }

        return null;
    }
}
