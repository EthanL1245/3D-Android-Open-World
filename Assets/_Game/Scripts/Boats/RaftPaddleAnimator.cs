using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Drives the raft paddles. If the original authored clip is assigned it is used.
/// The portable raft prefab no longer depends on Blender, so it otherwise animates
/// the preserved left/right paddle pivots directly. Releasing input freezes the
/// current pose and the next input resumes from that exact phase.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoatController), typeof(Rigidbody))]
public sealed class RaftPaddleAnimator : MonoBehaviour
{
    public Animator TargetAnimator;
    public AnimationClip PaddleClip;
    [Min(0f)] public float MinimumForwardSpeed = 0.04f;

    [Header("Portable paddle fallback")]
    [Min(0.05f)] public float ProceduralCyclesPerSecond = 0.72f;
    [Range(1f, 50f)] public float ProceduralStrokeDegrees = 24f;
    [Range(0f, 25f)] public float ProceduralFeatherDegrees = 7f;

    private BoatController boat;
    private Rigidbody body;
    private PlayableGraph graph;
    private AnimationClipPlayable clipPlayable;
    private bool graphReady;
    private bool rowing;

    private Transform leftPivot;
    private Transform rightPivot;
    private Quaternion leftBaseRotation;
    private Quaternion rightBaseRotation;
    private float proceduralPhase;
    private bool proceduralReady;

    private void Awake()
    {
        boat = GetComponent<BoatController>();
        body = GetComponent<Rigidbody>();

        if (TargetAnimator != null && PaddleClip != null)
            BuildGraph();

        if (!graphReady)
            BuildPortableFallback();
    }

    private void BuildGraph()
    {
        if (TargetAnimator == null || PaddleClip == null) return;

        TargetAnimator.applyRootMotion = false;
        graph = PlayableGraph.Create("Raft Authored Paddle Animation");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        clipPlayable = AnimationClipPlayable.Create(graph, PaddleClip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetSpeed(0d);
        clipPlayable.SetTime(0d);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Raft Paddle", TargetAnimator);
        output.SetSourcePlayable(clipPlayable);
        graph.Play();
        graph.Evaluate(0f);
        graphReady = true;
    }

    private void BuildPortableFallback()
    {
        Transform raft = transform.Find("ModelContainer/Uploaded Raft Model/Raft");
        if (raft == null) return;

        leftPivot = raft.Find("Empty.002");
        rightPivot = raft.Find("Empty.003");
        if (leftPivot == null || rightPivot == null) return;

        leftBaseRotation = leftPivot.localRotation;
        rightBaseRotation = rightPivot.localRotation;
        proceduralReady = true;
    }

    private void Update()
    {
        if (boat == null || body == null) return;

        float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
        bool hasDriverInput = boat.Driver != null && boat.Driver.Controls != null &&
                              boat.Driver.Controls.BoatInput.sqrMagnitude > 0.0025f;
        bool shouldRow = hasDriverInput && forwardSpeed > MinimumForwardSpeed;

        if (graphReady)
            UpdateAuthoredClip(shouldRow);
        else if (proceduralReady)
            UpdatePortablePaddles(shouldRow);
    }

    private void UpdateAuthoredClip(bool shouldRow)
    {
        if (PaddleClip == null) return;

        if (shouldRow)
        {
            rowing = true;
            clipPlayable.SetSpeed(1d);

            double length = PaddleClip.length;
            if (length > 0.0001d && clipPlayable.GetTime() >= length)
                clipPlayable.SetTime(clipPlayable.GetTime() % length);
        }
        else if (rowing)
        {
            rowing = false;
            clipPlayable.SetSpeed(0d);
            graph.Evaluate(0f);
        }
    }

    private void UpdatePortablePaddles(bool shouldRow)
    {
        if (!shouldRow)
        {
            rowing = false;
            return;
        }

        rowing = true;
        proceduralPhase = Mathf.Repeat(
            proceduralPhase + Time.deltaTime * ProceduralCyclesPerSecond * Mathf.PI * 2f,
            Mathf.PI * 2f);

        float stroke = Mathf.Sin(proceduralPhase) * ProceduralStrokeDegrees;
        float feather = Mathf.Cos(proceduralPhase) * ProceduralFeatherDegrees;

        leftPivot.localRotation = leftBaseRotation * Quaternion.Euler(stroke, 0f, feather);
        rightPivot.localRotation = rightBaseRotation * Quaternion.Euler(stroke, 0f, -feather);
    }

    private void OnDisable()
    {
        if (graphReady)
            clipPlayable.SetSpeed(0d);
        rowing = false;
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
        graphReady = false;
    }
}
