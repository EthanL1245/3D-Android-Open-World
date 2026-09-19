using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public class OceanWater : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer waterRenderer;
    [SerializeField] private Transform followTarget;

    [Header("Infinite Ocean")]
    [SerializeField] private float followSnapDistance = 20f;

    [Header("Large Waves")]
    [SerializeField] private float waveAmplitude1 = 0.45f;
    [SerializeField] private float waveLength1 = 20f;
    [SerializeField] private float waveSpeed1 = 1.15f;
    [SerializeField] private Vector2 waveDirection1 = new Vector2(1f, 0.35f);

    [Header("Medium Waves")]
    [SerializeField] private float waveAmplitude2 = 0.22f;
    [SerializeField] private float waveLength2 = 9f;
    [SerializeField] private float waveSpeed2 = 1.75f;
    [SerializeField] private Vector2 waveDirection2 = new Vector2(-0.4f, 1f);

    [Header("Small Waves")]
    [SerializeField] private float waveAmplitude3 = 0.08f;
    [SerializeField] private float waveLength3 = 4f;
    [SerializeField] private float waveSpeed3 = 2.4f;
    [SerializeField] private Vector2 waveDirection3 = new Vector2(0.8f, -0.65f);

    private MaterialPropertyBlock propertyBlock;

    public float BaseWaterLevel => transform.position.y;

    private void Awake()
    {
        EnsureRenderer();
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        EnsureRenderer();

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void LateUpdate()
    {
        FollowTarget();
        ApplyShaderProperties();
    }

    public void Configure(MeshRenderer renderer, Transform target)
    {
        waterRenderer = renderer;
        followTarget = target;
    }

    public float GetSurfaceHeight(Vector3 worldPosition)
    {
        return BaseWaterLevel + EvaluateWaves(worldPosition.x, worldPosition.z, Time.time);
    }

    public bool IsPointUnderwater(Vector3 worldPosition)
    {
        return worldPosition.y < GetSurfaceHeight(worldPosition);
    }

    private void EnsureRenderer()
    {
        if (waterRenderer == null)
        {
            waterRenderer = GetComponent<MeshRenderer>();
        }
    }

    private void FollowTarget()
    {
        if (followTarget == null || followSnapDistance <= 0f)
            return;

        Vector3 current = transform.position;

        float snappedX =
            Mathf.Round(followTarget.position.x / followSnapDistance) *
            followSnapDistance;

        float snappedZ =
            Mathf.Round(followTarget.position.z / followSnapDistance) *
            followSnapDistance;

        transform.position = new Vector3(snappedX, current.y, snappedZ);
    }

    private void ApplyShaderProperties()
    {
        if (waterRenderer == null)
            return;

        waterRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetFloat("_OceanTime", Time.time);

        SetWaveProperties(
            propertyBlock,
            1,
            waveAmplitude1,
            waveLength1,
            waveSpeed1,
            waveDirection1
        );

        SetWaveProperties(
            propertyBlock,
            2,
            waveAmplitude2,
            waveLength2,
            waveSpeed2,
            waveDirection2
        );

        SetWaveProperties(
            propertyBlock,
            3,
            waveAmplitude3,
            waveLength3,
            waveSpeed3,
            waveDirection3
        );

        waterRenderer.SetPropertyBlock(propertyBlock);
    }

    private static void SetWaveProperties(
        MaterialPropertyBlock block,
        int index,
        float amplitude,
        float wavelength,
        float speed,
        Vector2 direction)
    {
        Vector2 normalizedDirection =
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;

        block.SetFloat("_WaveAmplitude" + index, amplitude);
        block.SetFloat("_WaveLength" + index, Mathf.Max(0.1f, wavelength));
        block.SetFloat("_WaveSpeed" + index, speed);
        block.SetVector(
            "_WaveDirection" + index,
            new Vector4(
                normalizedDirection.x,
                normalizedDirection.y,
                0f,
                0f
            )
        );
    }

    private float EvaluateWaves(float x, float z, float time)
    {
        Vector2 position = new Vector2(x, z);

        return
            EvaluateWave(
                position,
                time,
                waveAmplitude1,
                waveLength1,
                waveSpeed1,
                waveDirection1
            ) +
            EvaluateWave(
                position,
                time,
                waveAmplitude2,
                waveLength2,
                waveSpeed2,
                waveDirection2
            ) +
            EvaluateWave(
                position,
                time,
                waveAmplitude3,
                waveLength3,
                waveSpeed3,
                waveDirection3
            );
    }

    private static float EvaluateWave(
        Vector2 position,
        float time,
        float amplitude,
        float wavelength,
        float speed,
        Vector2 direction)
    {
        if (amplitude == 0f)
            return 0f;

        Vector2 dir =
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;

        float frequency =
            (Mathf.PI * 2f) / Mathf.Max(0.1f, wavelength);

        float phase =
            Vector2.Dot(position, dir) * frequency +
            time * speed;

        return Mathf.Sin(phase) * amplitude;
    }
}
