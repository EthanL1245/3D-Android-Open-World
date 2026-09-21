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
            Time.time * 7.2f +
            phase;

        // Intentionally little motion near the head/front and progressively
        // stronger motion toward the tail.
        if (midBone != null)
        {
            midBone.localRotation =
                midRest *
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(t) * 4.5f,
                    Mathf.Sin(
                        t * 0.58f
                    ) * 1.5f
                );
        }

        if (rearBone != null)
        {
            rearBone.localRotation =
                rearRest *
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(
                        t - 0.42f
                    ) * 10f,
                    Mathf.Sin(
                        t * 0.72f +
                        0.7f
                    ) * 3f
                );
        }

        if (tailBone != null)
        {
            tailBone.localRotation =
                tailRest *
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(
                        t - 0.86f
                    ) * 22f,
                    Mathf.Sin(
                        t * 0.88f +
                        1.2f
                    ) * 5f
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
