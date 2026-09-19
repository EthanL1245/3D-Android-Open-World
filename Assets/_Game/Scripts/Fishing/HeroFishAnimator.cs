using UnityEngine;

public class HeroFishAnimator : MonoBehaviour
{
    [SerializeField] private Transform spine1;
    [SerializeField] private Transform spine2;
    [SerializeField] private Transform spine3;
    [SerializeField] private Transform tail;
    [SerializeField] private Transform leftPectoral;
    [SerializeField] private Transform rightPectoral;

    [SerializeField] private float swimFrequency = 4.2f;
    [SerializeField] private float heldFrequency = 7.0f;

    private bool held;
    private float phase;

    private void Awake()
    {
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    public void Configure(
        Transform s1,
        Transform s2,
        Transform s3,
        Transform tailTransform,
        Transform leftFin,
        Transform rightFin)
    {
        spine1 = s1;
        spine2 = s2;
        spine3 = s3;
        tail = tailTransform;
        leftPectoral = leftFin;
        rightPectoral = rightFin;
    }

    public void SetHeld(bool value)
    {
        held = value;
    }

    private void Update()
    {
        float frequency =
            held ? heldFrequency : swimFrequency;

        float t =
            Time.time * frequency + phase;

        float strength =
            held ? 1.35f : 1f;

        if (spine1 != null)
        {
            spine1.localRotation =
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(t) * 3.5f * strength,
                    0f
                );
        }

        if (spine2 != null)
        {
            spine2.localRotation =
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(t - 0.55f) *
                    7.5f *
                    strength,
                    0f
                );
        }

        if (spine3 != null)
        {
            spine3.localRotation =
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(t - 1.05f) *
                    13.5f *
                    strength,
                    0f
                );
        }

        if (tail != null)
        {
            tail.localRotation =
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(t - 1.45f) *
                    24f *
                    strength,
                    0f
                );
        }

        float finFlutter =
            Mathf.Sin(t * 0.55f) *
            (held ? 10f : 5f);

        if (leftPectoral != null)
        {
            leftPectoral.localRotation =
                Quaternion.Euler(
                    12f + finFlutter,
                    -18f,
                    -28f
                );
        }

        if (rightPectoral != null)
        {
            rightPectoral.localRotation =
                Quaternion.Euler(
                    12f - finFlutter,
                    18f,
                    28f
                );
        }
    }
}
