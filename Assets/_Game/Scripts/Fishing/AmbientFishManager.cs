using System.Collections.Generic;
using UnityEngine;

public class AmbientFishManager : MonoBehaviour
{
    [SerializeField] private OceanWater oceanWater;
    [SerializeField] private Transform player;
    [SerializeField] private int fishCount = 18;
    [SerializeField] private float minRadius = 8f;
    [SerializeField] private float maxRadius = 36f;
    [SerializeField] private float minDepth = 1.2f;
    [SerializeField] private float maxDepth = 5.5f;

    private readonly List<AmbientFishAgent> fish =
        new List<AmbientFishAgent>();

    private Terrain terrain;

    private void Start()
    {
        terrain = Terrain.activeTerrain;

        if (oceanWater == null)
        {
            oceanWater =
                FindFirstObjectByType<OceanWater>();
        }

        if (player == null)
        {
            FirstPersonController controller =
                FindFirstObjectByType<FirstPersonController>();

            if (controller != null)
                player = controller.transform;
        }

        SpawnFish();
    }

    public void Configure(
        OceanWater water,
        Transform playerTransform)
    {
        oceanWater = water;
        player = playerTransform;
    }

    public bool TryGetSwimPoint(
        out Vector3 point)
    {
        point = Vector3.zero;

        if (oceanWater == null ||
            player == null)
        {
            return false;
        }

        for (int attempt = 0;
             attempt < 24;
             attempt++)
        {
            Vector2 direction =
                Random.insideUnitCircle;

            if (direction.sqrMagnitude <
                0.05f)
            {
                continue;
            }

            direction.Normalize();

            float radius =
                Random.Range(
                    minRadius,
                    maxRadius
                );

            Vector3 candidate =
                player.position +
                new Vector3(
                    direction.x,
                    0f,
                    direction.y
                ) *
                radius;

            float surface =
                oceanWater.GetSurfaceHeight(
                    candidate
                );

            float floor =
                GetTerrainHeight(candidate);

            float availableDepth =
                surface - floor;

            if (availableDepth < 1.8f)
                continue;

            float depth =
                Mathf.Min(
                    Random.Range(
                        minDepth,
                        maxDepth
                    ),
                    availableDepth - 0.55f
                );

            candidate.y =
                surface - depth;

            point = candidate;
            return true;
        }

        return false;
    }

    public bool IsTooFar(Vector3 position)
    {
        if (player == null)
            return false;

        Vector2 delta =
            new Vector2(
                position.x - player.position.x,
                position.z - player.position.z
            );

        return delta.sqrMagnitude >
               (maxRadius + 18f) *
               (maxRadius + 18f);
    }

    private void SpawnFish()
    {
        for (int i = 0;
             i < fishCount;
             i++)
        {
            int speciesId =
                Random.Range(
                    0,
                    FishCatalog.Count
                );

            GameObject visual =
                FishVisualFactory.CreateAmbientFish(
                    "AmbientFish_" + i,
                    transform,
                    speciesId,
                    Random.Range(
                        0.65f,
                        1.15f
                    )
                );

            AmbientFishAgent agent =
                visual.AddComponent<AmbientFishAgent>();

            agent.Configure(
                this,
                Random.Range(
                    1.15f,
                    2.35f
                ),
                Random.Range(
                    0f,
                    100f
                )
            );

            fish.Add(agent);
        }
    }

    private float GetTerrainHeight(
        Vector3 worldPosition)
    {
        if (terrain == null)
            return -1000f;

        Vector3 local =
            worldPosition -
            terrain.transform.position;

        TerrainData data =
            terrain.terrainData;

        if (local.x < 0f ||
            local.z < 0f ||
            local.x > data.size.x ||
            local.z > data.size.z)
        {
            return -1000f;
        }

        return terrain.SampleHeight(
                   worldPosition
               ) +
               terrain.transform.position.y;
    }
}

public class AmbientFishAgent : MonoBehaviour
{
    private AmbientFishManager manager;
    private Transform tail;

    private Vector3 target;
    private bool hasTarget;
    private float speed;
    private float animationOffset;
    private float retargetCooldown;

    public void Configure(
        AmbientFishManager owner,
        float swimSpeed,
        float offset)
    {
        manager = owner;
        speed = swimSpeed;
        animationOffset = offset;

        tail = transform.Find("Tail");

        FindNewTarget(true);
    }

    private void Update()
    {
        if (manager == null)
            return;

        if (!gameObject.activeSelf)
            return;

        retargetCooldown -=
            Time.deltaTime;

        if (!hasTarget ||
            manager.IsTooFar(
                transform.position
            ) ||
            Vector3.Distance(
                transform.position,
                target
            ) < 1.2f)
        {
            FindNewTarget(
                manager.IsTooFar(
                    transform.position
                )
            );
        }

        if (!hasTarget)
            return;

        Vector3 direction =
            target - transform.position;

        if (direction.sqrMagnitude <
            0.01f)
        {
            return;
        }

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                1f -
                Mathf.Exp(
                    -3.2f *
                    Time.deltaTime
                )
            );

        transform.position +=
            transform.forward *
            speed *
            Time.deltaTime;

        if (tail != null)
        {
            tail.localRotation =
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(
                        Time.time *
                        speed *
                        7f +
                        animationOffset
                    ) *
                    28f,
                    0f
                );
        }
    }

    private void FindNewTarget(bool teleport)
    {
        if (retargetCooldown > 0f &&
            !teleport)
        {
            return;
        }

        retargetCooldown = 0.35f;

        if (!manager.TryGetSwimPoint(
                out Vector3 newTarget))
        {
            hasTarget = false;
            return;
        }

        target = newTarget;
        hasTarget = true;

        if (teleport)
        {
            transform.position =
                newTarget;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    Random.Range(
                        0f,
                        360f
                    ),
                    0f
                );
        }
    }
}
