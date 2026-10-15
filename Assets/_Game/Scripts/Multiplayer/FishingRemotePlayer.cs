using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// A visual-only network avatar. The local player still owns movement, collisions and fishing.
public sealed class FishingRemotePlayer : MonoBehaviour
{
    private const string AvatarResource = "Multiplayer/FemaleRemoteAvatar";
    private const float WalkingThreshold = 0.12f;
    private const float DefaultHeight = 2.05f;

    public int World { get; private set; }

    private Vector3 target, previous;
    private Quaternion facing;
    private bool initialized, swimming;
    private float speed, lastPacket, walkClock, idleClock, walkBlend;
    private Transform label;

    private PlayableGraph animationGraph;
    private AnimationMixerPlayable animationMixer;
    private AnimationClipPlayable walkPlayable, idlePlayable;
    private AnimationClip walkClip, idleClip;

    public void Initialize(string displayName, string identity)
    {
        float labelHeight = CreateFemaleModel();

        var text = new GameObject("Name").AddComponent<TextMesh>();
        label = text.transform;
        label.SetParent(transform, false);
        label.localPosition = Vector3.up * labelHeight;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font != null)
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        text.text = displayName;
        text.anchor = TextAnchor.MiddleCenter;
        text.characterSize = .045f;
        text.fontSize = 48;
        text.color = Color.white;
        text.richText = false;
    }

    private float CreateFemaleModel()
    {
        FishingRemoteAvatarAsset asset = Resources.Load<FishingRemoteAvatarAsset>(AvatarResource);
        if (asset == null || asset.ModelPrefab == null)
        {
            Debug.LogError("Female multiplayer avatar is missing. In the Unity editor, run Tools/Open World/Rebuild Female Multiplayer Avatar before making a build.");
            return DefaultHeight;
        }

        GameObject visual = Instantiate(asset.ModelPrefab, transform, false);
        visual.name = "Female Character Model";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // The remote player is only a representation of another client's position.
        // Its FBX must not obstruct local players, boats, casts or triggers.
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = visual.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.runtimeAnimatorController = null;

        walkClip = asset.WalkClip;
        idleClip = asset.IdleClip != null ? asset.IdleClip : walkClip;

        if (walkClip != null && idleClip != null)
        {
            animationGraph = PlayableGraph.Create("Remote Female Character Walk");
            animationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            animationMixer = AnimationMixerPlayable.Create(animationGraph, 2);
            idlePlayable = AnimationClipPlayable.Create(animationGraph, idleClip);
            walkPlayable = AnimationClipPlayable.Create(animationGraph, walkClip);
            animationGraph.Connect(idlePlayable, 0, animationMixer, 0);
            animationGraph.Connect(walkPlayable, 0, animationMixer, 1);
            animationMixer.SetInputWeight(0, 1f);
            animationMixer.SetInputWeight(1, 0f);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(animationGraph, "Remote Avatar", animator);
            output.SetSourcePlayable(animationMixer);
            animationGraph.Play();
            animationGraph.Evaluate(0f);
        }
        else
        {
            Debug.LogError("Female multiplayer avatar was imported without a usable walking animation.");
        }

        // Respect original model proportions while matching the old avatar's height
        // and keeping its feet at the network player's ground position.
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!hasBounds || bounds.size.y < .001f)
            return DefaultHeight;

        float uniformScale = Mathf.Clamp(1.72f / bounds.size.y, .01f, 100f);
        visual.transform.localScale *= uniformScale;

        hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        visual.transform.position += Vector3.up * (transform.position.y - bounds.min.y);
        return Mathf.Clamp(bounds.max.y - bounds.min.y + .22f, 1.5f, 3f);
    }

    public void Receive(FishingMultiplayer.Peer peer)
    {
        World = peer.world;
        swimming = peer.swimming;
        target = new Vector3(peer.x, peer.y, peer.z);
        facing = Quaternion.Euler(0, peer.yaw, 0);
        lastPacket = Time.realtimeSinceStartup;

        if (!initialized || (target - transform.position).sqrMagnitude > 225f)
        {
            transform.SetPositionAndRotation(target, facing);
            previous = target;
            initialized = true;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (!initialized || dt <= 0f) return;

        float blend = 1f - Mathf.Exp(-12f * dt);
        transform.position = Vector3.Lerp(transform.position, target, blend);
        transform.rotation = Quaternion.Slerp(transform.rotation, facing, blend);
        float measured = Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).magnitude / dt;
        previous = transform.position;
        speed = Mathf.Lerp(speed, Time.realtimeSinceStartup - lastPacket > 1f ? 0f : measured, blend);

        if (animationGraph.IsValid())
        {
            bool moving = speed > WalkingThreshold && !swimming;
            float desiredBlend = moving ? Mathf.Clamp01(speed / .7f) : 0f;
            walkBlend = Mathf.MoveTowards(walkBlend, desiredBlend, dt * 7f);
            if (moving)
                walkClock += dt * Mathf.Clamp(speed / 2f, .7f, 2f);

            // Manually wrap the supplied FBX clip; this also works when the imported
            // animation's Loop Time checkbox was not enabled.
            walkPlayable.SetTime(walkClock % Mathf.Max(.01f, walkClip.length));
            if (idleClip == walkClip)
                idlePlayable.SetTime(0f); // No idle clip: hold the walk's starting pose.
            else
            {
                idleClock += dt;
                idlePlayable.SetTime(idleClock % Mathf.Max(.01f, idleClip.length));
            }

            animationMixer.SetInputWeight(0, 1f - walkBlend);
            animationMixer.SetInputWeight(1, walkBlend);
            animationGraph.Evaluate(0f);
        }

        if (label != null && Camera.main != null)
            label.rotation = Camera.main.transform.rotation;
    }

    private void OnDestroy()
    {
        if (animationGraph.IsValid())
            animationGraph.Destroy();
    }
}
