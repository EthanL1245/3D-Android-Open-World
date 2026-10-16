using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Render-only perspective: gameplay always retains its original eye/aim transform.
[DefaultExecutionOrder(-10000)]
public sealed class FishingThirdPersonView : MonoBehaviour
{
    public bool IsThirdPerson { get; private set; }
    private Camera view;
    private float orbitYaw;
    private const float MinZoomDistance = 1.2f, MaxZoomDistance = 3.4f;
    private float zoomDistance = MaxZoomDistance;
    private FishingSystem fishing;
    private FishingRemotePlayer avatar;
    private bool rendering;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private Vector3 savedLineStart;
    private LineRenderer renderLine;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Renderer> hidden = new List<Renderer>();
    private readonly RaycastHit[] obstacles = new RaycastHit[64];

    public static FishingThirdPersonView For(FirstPersonController player)
    {
        if (player == null) return null;
        var component = player.GetComponent<FishingThirdPersonView>();
        return component != null ? component : player.gameObject.AddComponent<FishingThirdPersonView>();
    }

    private void Awake()
    {
        var player = GetComponent<FirstPersonController>();
        view = player != null ? player.ViewCamera : null;
        fishing = GetComponent<FishingSystem>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginSRP;
        RenderPipelineManager.endCameraRendering += EndSRP;
        Camera.onPreCull += BeginBuiltin;
        Camera.onPostRender += EndBuiltin;
    }

    public void Toggle()
    {
        if (view == null) return;
        RestoreRenderPose();
        IsThirdPerson = !IsThirdPerson;
        if (IsThirdPerson) orbitYaw = transform.eulerAngles.y;
        if (IsThirdPerson && avatar == null)
        {
            avatar = new GameObject("Local Third Person Angler").AddComponent<FishingRemotePlayer>();
            avatar.Initialize("", "local", false);
        }
        if (avatar != null)
        {
            if (IsThirdPerson) avatar.FollowLocal(FishingMultiplayer.For(GetComponent<FirstPersonController>()));
            avatar.gameObject.SetActive(IsThirdPerson);
        }
    }

    // A render interrupted by scene changes must never leave gameplay at the boom position.
    private void Update() => RestoreRenderPose();

    // Fingers spreading apart move closer; bringing them together zooms out.
    public void PinchZoom(float previousSeparation, float separation)
    {
        if (!IsThirdPerson || previousSeparation < 1f || separation < 1f) return;
        zoomDistance = Mathf.Clamp(zoomDistance * previousSeparation / separation,
            ClosestFullBodyDistance(), MaxZoomDistance);
    }

    public void Orbit(float yawDelta)
    {
        orbitYaw = Mathf.Repeat(orbitYaw + yawDelta, 360f);
    }

    // Forward/strafe controls follow the view only when movement is requested.
    // Looking around while standing or fishing never rotates the character.
    public void AlignForMovement()
    {
        if (IsThirdPerson) transform.rotation = Quaternion.Euler(0f, orbitYaw, 0f);
    }

    private float ClosestFullBodyDistance()
    {
        if (view == null || avatar == null) return MinZoomDistance;
        // Fit the full character vertically, with head/feet padding and body depth.
        float halfFov = Mathf.Clamp(view.fieldOfView, 10f, 150f) * .5f * Mathf.Deg2Rad;
        float fitDistance = avatar.BodyHeight * .5f * 1.2f / Mathf.Tan(halfFov) + .3f;
        return Mathf.Clamp(fitDistance, MinZoomDistance, MaxZoomDistance);
    }

    private void CameraPose(out Vector3 position, out Quaternion rotation)
    {
        rotation = Quaternion.AngleAxis(
            Mathf.DeltaAngle(transform.eulerAngles.y, orbitYaw), Vector3.up) * view.transform.rotation;
        float closest = ClosestFullBodyDistance();
        float framedDistance = Mathf.Clamp(zoomDistance, closest, MaxZoomDistance);
        float closeBlend = 1f - Mathf.InverseLerp(closest, MaxZoomDistance, framedDistance);
        Vector3 farPivot = view.transform.position + Vector3.up * .65f;
        // Lower the focus from above the head to the body's centre as we zoom in.
        Vector3 pivot = avatar != null ? Vector3.Lerp(farPivot, avatar.BodyCenter, closeBlend) : farPivot;
        Vector3 offset = rotation * new Vector3(.35f * framedDistance / MaxZoomDistance, 0f, -framedDistance);
        float distance = offset.magnitude;
        int count = Physics.SphereCastNonAlloc(pivot, .22f, offset / distance, obstacles,
            distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = obstacles[i].transform;
            if (hit == null || hit.IsChildOf(transform) ||
                (avatar != null && hit.IsChildOf(avatar.transform))) continue;
            distance = Mathf.Min(distance, Mathf.Max(0f, obstacles[i].distance - .08f));
        }
        position = pivot + offset.normalized * distance;
    }

    private void BeginSRP(ScriptableRenderContext context, Camera camera) => BeginRender(camera);
    private void EndSRP(ScriptableRenderContext context, Camera camera) { if (camera == view) RestoreRenderPose(); }
    private void BeginBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) BeginRender(camera); }
    private void EndBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null && camera == view) RestoreRenderPose(); }

    private void BeginRender(Camera camera)
    {
        if (!IsThirdPerson || camera != view || rendering) return;
        // Read the final rod flex before moving the eye for rendering. This includes
        // current tension, fish modes, reel load, lure flex and release/relaxation.
        if (avatar != null && fishing != null) avatar.CopyLocalRodBend(fishing.PresentationRod);
        CameraPose(out Vector3 position, out Quaternion rotation);
        savedPosition = view.transform.localPosition;
        savedRotation = view.transform.localRotation;
        rendering = true;
        // Hide only the first-person presentation, keeping its simulation running.
        renderLine = fishing != null ? fishing.PresentationLine : null;
        view.GetComponentsInChildren<Renderer>(true, renderers);
        hidden.Clear();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == renderLine || renderer.forceRenderingOff) continue;
            renderer.forceRenderingOff = true;
            hidden.Add(renderer);
        }
        if (renderLine != null && renderLine.positionCount > 0)
        {
            savedLineStart = renderLine.GetPosition(0);
            if (avatar != null && avatar.RodTip != null)
                renderLine.SetPosition(0, avatar.RodTip.position);
        }
        view.transform.SetPositionAndRotation(position, rotation);
    }

    private void RestoreRenderPose()
    {
        if (!rendering) return;
        if (view != null)
        {
            view.transform.localPosition = savedPosition;
            view.transform.localRotation = savedRotation;
        }
        foreach (Renderer renderer in hidden)
            if (renderer != null) renderer.forceRenderingOff = false;
        hidden.Clear();
        if (renderLine != null && renderLine.positionCount > 0) renderLine.SetPosition(0, savedLineStart);
        renderLine = null;
        rendering = false;
    }

    // World-attached HUD labels must use the displayed perspective, not the aiming eye.
    public static Vector3 WorldToScreenPoint(Camera camera, Vector3 point)
    {
        var perspective = camera != null ? camera.GetComponentInParent<FishingThirdPersonView>() : null;
        if (perspective == null || !perspective.IsThirdPerson || perspective.rendering)
            return camera != null ? camera.WorldToScreenPoint(point) : Vector3.zero;
        perspective.CameraPose(out Vector3 position, out Quaternion rotation);
        Vector3 local = Quaternion.Inverse(rotation) * (point - position);
        Vector4 clip = camera.projectionMatrix * new Vector4(local.x, local.y, -local.z, 1f);
        if (Mathf.Abs(clip.w) < .00001f) return new Vector3(0f, 0f, local.z);
        Rect pixels = camera.pixelRect;
        return new Vector3(pixels.x + (clip.x / clip.w + 1f) * .5f * pixels.width,
            pixels.y + (clip.y / clip.w + 1f) * .5f * pixels.height, local.z);
    }

    private void OnDisable()
    {
        RestoreRenderPose();
        RenderPipelineManager.beginCameraRendering -= BeginSRP;
        RenderPipelineManager.endCameraRendering -= EndSRP;
        Camera.onPreCull -= BeginBuiltin;
        Camera.onPostRender -= EndBuiltin;
        IsThirdPerson = false;
        if (avatar != null) avatar.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        RestoreRenderPose();
        if (avatar != null) Destroy(avatar.gameObject);
    }
}
