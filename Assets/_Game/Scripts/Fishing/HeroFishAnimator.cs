using UnityEngine;

public class HeroFishAnimator : MonoBehaviour
{
    [SerializeField] private Transform midBody;
    [SerializeField] private Transform rearBody;
    [SerializeField] private Transform peduncle;
    [SerializeField] private Transform tailBone;
    [SerializeField] private Transform leftPectoral;
    [SerializeField] private Transform rightPectoral;

    [SerializeField] private float swimFrequency = 3.9f;
    [SerializeField] private float heldFrequency = 6.4f;

    private Quaternion midBase;
    private Quaternion rearBase;
    private Quaternion peduncleBase;
    private Quaternion tailBase;
    private Quaternion leftFinBase;
    private Quaternion rightFinBase;

    private bool held;
    private float phase;

    private void Awake()
    {
        phase =
            Random.Range(
                0f,
                Mathf.PI * 2f
            );

        CaptureBaseRotations();
    }

    public void Configure(
        Transform mid,
        Transform rear,
        Transform peduncleTransform,
        Transform tail,
        Transform leftFin,
        Transform rightFin)
    {
        midBody = mid;
        rearBody = rear;
        peduncle = peduncleTransform;
        tailBone = tail;
        leftPectoral = leftFin;
        rightPectoral = rightFin;

        CaptureBaseRotations();
    }

    public void SetHeld(bool value)
    {
        held = value;
    }

    private void CaptureBaseRotations()
    {
        if (midBody != null)
            midBase = midBody.localRotation;

        if (rearBody != null)
            rearBase = rearBody.localRotation;

        if (peduncle != null)
            peduncleBase = peduncle.localRotation;

        if (tailBone != null)
            tailBase = tailBone.localRotation;

        if (leftPectoral != null)
            leftFinBase = leftPectoral.localRotation;

        if (rightPectoral != null)
            rightFinBase = rightPectoral.localRotation;
    }

    private void Update()
    {
        float frequency =
            held
                ? heldFrequency
                : swimFrequency;

        float t =
            Time.time *
            frequency +
            phase;

        float strength =
            held ? 1.28f : 1f;

        if (midBody != null)
        {
            midBody.localRotation =
                midBase *
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(t) *
                    2.5f *
                    strength,
                    0f
                );
        }

        if (rearBody != null)
        {
            rearBody.localRotation =
                rearBase *
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(t - 0.45f) *
                    5.8f *
                    strength,
                    0f
                );
        }

        if (peduncle != null)
        {
            peduncle.localRotation =
                peduncleBase *
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(t - 0.92f) *
                    10.5f *
                    strength,
                    0f
                );
        }

        if (tailBone != null)
        {
            tailBone.localRotation =
                tailBase *
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(t - 1.22f) *
                    18.5f *
                    strength,
                    0f
                );
        }

        float finFlutter =
            Mathf.Sin(
                t * 0.58f
            ) *
            (held ? 8f : 4f);

        if (leftPectoral != null)
        {
            leftPectoral.localRotation =
                leftFinBase *
                Quaternion.Euler(
                    0f,
                    0f,
                    finFlutter
                );
        }

        if (rightPectoral != null)
        {
            rightPectoral.localRotation =
                rightFinBase *
                Quaternion.Euler(
                    0f,
                    0f,
                    -finFlutter
                );
        }
    }
}
