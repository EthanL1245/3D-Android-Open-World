using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Plays the paddle animation authored in the user's Raft.blend only while the raft
/// is actively being driven forward. The clip itself is never rescaled, regenerated,
/// blended, or otherwise modified; this component only starts/stops and loops it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoatController), typeof(Rigidbody))]
public sealed class RaftPaddleAnimator : MonoBehaviour
{
    public Animator TargetAnimator;
    public AnimationClip PaddleClip;
    [Min(0f)] public float MinimumForwardSpeed = 0.04f;

    private BoatController boat;
    private Rigidbody body;
    private PlayableGraph graph;
    private AnimationClipPlayable clipPlayable;
    private bool graphReady;
    private bool rowing;

    private void Awake()
    {
        boat = GetComponent<BoatController>();
        body = GetComponent<Rigidbody>();
        BuildGraph();
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

    private void Update()
    {
        if (!graphReady || boat == null || body == null || PaddleClip == null) return;

        float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
        bool hasDriverInput = boat.Driver != null && boat.Driver.Controls != null &&
                              boat.Driver.Controls.BoatInput.sqrMagnitude > 0.0025f;
        bool shouldRow = hasDriverInput && forwardSpeed > MinimumForwardSpeed;

        if (shouldRow)
        {
            if (!rowing)
            {
                rowing = true;
                clipPlayable.SetTime(0d);
                graph.Evaluate(0f);
            }

            // Play at the exact authored speed. Loop manually so we do not have to
            // alter the imported AnimationClip's wrap/loop settings.
            clipPlayable.SetSpeed(1d);
            double length = PaddleClip.length;
            if (length > 0.0001d && clipPlayable.GetTime() >= length)
                clipPlayable.SetTime(clipPlayable.GetTime() % length);
        }
        else if (rowing)
        {
            // Stop immediately when forward propulsion stops and return the paddles
            // to the supplied clip's authored first-frame/rest pose.
            rowing = false;
            clipPlayable.SetSpeed(0d);
            clipPlayable.SetTime(0d);
            graph.Evaluate(0f);
        }
    }

    private void OnDisable()
    {
        if (!graphReady) return;
        clipPlayable.SetSpeed(0d);
        clipPlayable.SetTime(0d);
        graph.Evaluate(0f);
        rowing = false;
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
        graphReady = false;
    }
}
