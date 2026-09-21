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
            Time.time * 10.5f +
            phase;

        // Frantic caught-fish thrashing: small motion in the middle, much
        // stronger and less regular movement toward the rear and tail.
        if (midBone != null)
        {
            float midYaw =
                Mathf.Sin(t) * 8f +
                Mathf.Sin(
                    t * 1.87f +
                    0.8f
                ) * 3.5f;

            float midRoll =
                Mathf.Sin(
                    t * 0.73f +
                    1.1f
                ) * 3f;

            midBone.localRotation =
                midRest *
                Quaternion.Euler(
                    Mathf.Sin(
                        t * 1.31f
                    ) * 2f,
                    midYaw,
                    midRoll
                );
        }

        if (rearBone != null)
        {
            float rearYaw =
                Mathf.Sin(
                    t - 0.38f
                ) * 18f +
                Mathf.Sin(
                    t * 1.63f +
                    1.7f
                ) * 7f;

            float rearRoll =
                Mathf.Sin(
                    t * 0.91f +
                    0.5f
                ) * 6f;

            rearBone.localRotation =
                rearRest *
                Quaternion.Euler(
                    Mathf.Sin(
                        t * 1.42f +
                        0.3f
                    ) * 4f,
                    rearYaw,
                    rearRoll
                );
        }

        if (tailBone != null)
        {
            float tailYaw =
                Mathf.Sin(
                    t - 0.82f
                ) * 34f +
                Mathf.Sin(
                    t * 1.78f +
                    2.1f
                ) * 12f;

            float tailRoll =
                Mathf.Sin(
                    t * 1.12f +
                    1.4f
                ) * 9f;

            tailBone.localRotation =
                tailRest *
                Quaternion.Euler(
                    Mathf.Sin(
                        t * 1.55f +
                        0.6f
                    ) * 6f,
                    tailYaw,
                    tailRoll
                );
        }
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
