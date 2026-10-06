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
    [Range(0f, 25f)] public float ProceduralFeatherDegrees = 12f;
    [Range(0f, 40f)] public float ProceduralDipDegrees = 20f;

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
    private float proceduralBlend;
    private float leftStrokeCenter;
    private float rightStrokeCenter;
    private float leftSide;
    private float rightSide;

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
        CalibratePaddle(leftPivot, out leftSide, out leftStrokeCenter);
        CalibratePaddle(rightPivot, out rightSide, out rightStrokeCenter);
        proceduralReady = true;
    }

    private void CalibratePaddle(Transform pivot, out float side, out float strokeCenter)
    {
        side = transform.InverseTransformPoint(pivot.position).x < 0f ? -1f : 1f;
        strokeCenter = 0f;
        MeshFilter filter = pivot.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return;

        // Locate the outboard blade in boat space, including the right oar's
        // negative scale. Its imported resting pose points toward the bow;
        // centre the working stroke outboard without changing the mesh/pivot.
        Vector3 pivotInBoat = transform.InverseTransformPoint(pivot.position);
        Vector3 blade = Vector3.zero;
        float furthestOutboard = 0f;
        foreach (Vector3 vertex in filter.sharedMesh.vertices)
        {
            Vector3 offset = transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)) - pivotInBoat;
            if (offset.x * side <= furthestOutboard) continue;
            furthestOutboard = offset.x * side;
            blade = offset;
        }
        blade.y = 0f;
        if (blade.sqrMagnitude > 0.0001f)
            strokeCenter = Vector3.SignedAngle(blade, Vector3.right * side, Vector3.up);
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
        proceduralBlend = Mathf.MoveTowards(proceduralBlend, 1f, Time.deltaTime * 4f);
        proceduralPhase = Mathf.Repeat(
            proceduralPhase + Time.deltaTime * ProceduralCyclesPerSecond * Mathf.PI * 2f,
            Mathf.PI * 2f);

        float stroke = Mathf.Sin(proceduralPhase) * ProceduralStrokeDegrees;
        float dip = ProceduralDipDegrees + Mathf.Cos(proceduralPhase) * ProceduralFeatherDegrees;

        ApplyPortablePose(leftPivot, leftBaseRotation, leftSide, leftStrokeCenter, stroke, dip);
        ApplyPortablePose(rightPivot, rightBaseRotation, rightSide, rightStrokeCenter, stroke, dip);
    }

    private void ApplyPortablePose(Transform pivot, Quaternion rest, float side, float center, float stroke, float dip)
    {
        // Pre-multiply in the pivot PARENT frame, not the mirrored/tilted
        // imported paddle frame. Both blades pull backward together, dip on
        // the power stroke and lift on the forward recovery stroke.
        Vector3 up = pivot.parent.InverseTransformDirection(transform.up);
        Vector3 forward = pivot.parent.InverseTransformDirection(transform.forward);
        Quaternion sweep = Quaternion.AngleAxis((center + side * stroke) * proceduralBlend, up);
        Quaternion lift = Quaternion.AngleAxis(-side * dip * proceduralBlend, forward);
        pivot.localRotation = lift * sweep * rest;
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
