using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FishTankSaveRecord
{
    public string id;
    public float x;
    public float y;
    public float z;
    public float rotationY;

    public List<CaughtFishRecord> fish =
        new List<CaughtFishRecord>();
}

[Serializable]
internal class AquariumSaveData
{
    public int unplacedTanks;

    public List<FishTankSaveRecord> tanks =
        new List<FishTankSaveRecord>();
}

public class AquariumSystem : MonoBehaviour
{
    private const string SaveKey =
        "OpenWorld.Aquariums.v1";

    public const int TankPrice = 250;
    public const int TankCapacity = 8;

    [SerializeField] private Transform player;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FishingInventory inventory;
    [SerializeField] private EconomySystem economy;

    private AquariumSaveData saveData =
        new AquariumSaveData();

    private readonly List<PlacedFishTank> spawnedTanks =
        new List<PlacedFishTank>();

    private GameObject placementPreview;
    private Vector3 placementPosition;
    private float placementRotationY;
    private bool isPlacing;

    private Terrain terrain;

    private static Material frameMaterial;
    private static Material glassMaterial;
    private static Material waterMaterial;
    private static Material baseMaterial;

    public event Action Changed;

    public int UnplacedTankCount =>
        saveData != null
            ? saveData.unplacedTanks
            : 0;

    public bool IsPlacing => isPlacing;

    private void Awake()
    {
        terrain = Terrain.activeTerrain;

        Load();
    }

    private void Start()
    {
        ResolveReferences();
        SpawnSavedTanks();
    }

    private void Update()
    {
        if (isPlacing)
        {
            UpdatePlacementPreview();
        }
    }

    public void Configure(
        Transform playerTransform,
        Camera camera,
        FishingInventory fishingInventory,
        EconomySystem economySystem)
    {
        player = playerTransform;
        playerCamera = camera;
        inventory = fishingInventory;
        economy = economySystem;
    }

    public bool BuyTank()
    {
        if (economy == null)
            return false;

        if (!economy.TrySpendCoins(TankPrice))
            return false;

        saveData.unplacedTanks++;
        Save();

        return true;
    }

    public bool BeginPlacement()
    {
        if (saveData.unplacedTanks <= 0 ||
            player == null)
        {
            return false;
        }

        isPlacing = true;

        if (placementPreview == null)
        {
            placementPreview =
                CreateTankShell(
                    "FishTankPreview",
                    true
                );
        }

        placementPreview.SetActive(true);
        UpdatePlacementPreview();

        Changed?.Invoke();

        return true;
    }

    public void CancelPlacement()
    {
        isPlacing = false;

        if (placementPreview != null)
            placementPreview.SetActive(false);

        Changed?.Invoke();
    }

    public bool ConfirmPlacement()
    {
        if (!isPlacing ||
            saveData.unplacedTanks <= 0)
        {
            return false;
        }

        FishTankSaveRecord record =
            new FishTankSaveRecord
            {
                id =
                    Guid.NewGuid()
                        .ToString("N"),
                x = placementPosition.x,
                y = placementPosition.y,
                z = placementPosition.z,
                rotationY =
                    placementRotationY,
                fish =
                    new List<CaughtFishRecord>()
            };

        saveData.tanks.Add(record);
        saveData.unplacedTanks--;

        SpawnTank(record);
        Save();

        isPlacing = false;

        if (placementPreview != null)
            placementPreview.SetActive(false);

        Changed?.Invoke();

        return true;
    }

    public PlacedFishTank GetNearestTank(
        float maxDistance = 3.2f)
    {
        if (player == null)
            return null;

        PlacedFishTank nearest = null;
        float best =
            maxDistance * maxDistance;

        foreach (PlacedFishTank tank in spawnedTanks)
        {
            if (tank == null)
                continue;

            Vector3 delta =
                tank.transform.position -
                player.position;

            delta.y = 0f;

            float distance =
                delta.sqrMagnitude;

            if (distance <= best)
            {
                best = distance;
                nearest = tank;
            }
        }

        return nearest;
    }

    public FishTankSaveRecord GetTankRecord(
        string tankId)
    {
        if (saveData == null ||
            saveData.tanks == null)
        {
            return null;
        }

        for (int i = 0;
             i < saveData.tanks.Count;
             i++)
        {
            if (saveData.tanks[i].id ==
                tankId)
            {
                return saveData.tanks[i];
            }
        }

        return null;
    }

    public bool AddFishToTank(
        string tankId,
        int inventoryIndex)
    {
        FishTankSaveRecord tank =
            GetTankRecord(tankId);

        if (tank == null ||
            inventory == null ||
            tank.fish.Count >= TankCapacity)
        {
            return false;
        }

        CaughtFishRecord record =
            inventory.RemoveFishAt(
                inventoryIndex
            );

        if (record == null)
            return false;

        tank.fish.Add(record);

        Save();
        RefreshTank(tankId);

        return true;
    }

    public bool RemoveFishFromTank(
        string tankId,
        int tankFishIndex)
    {
        FishTankSaveRecord tank =
            GetTankRecord(tankId);

        if (tank == null ||
            inventory == null ||
            tankFishIndex < 0 ||
            tankFishIndex >=
            tank.fish.Count)
        {
            return false;
        }

        CaughtFishRecord record =
            tank.fish[tankFishIndex];

        tank.fish.RemoveAt(
            tankFishIndex
        );

        inventory.AddExistingFish(record);

        Save();
        RefreshTank(tankId);

        return true;
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            FirstPersonController controller =
                FindFirstObjectByType<FirstPersonController>();

            if (controller != null)
                player = controller.transform;
        }

        if (playerCamera == null)
        {
            playerCamera =
                FindFirstObjectByType<Camera>();
        }

        if (inventory == null &&
            player != null)
        {
            inventory =
                player.GetComponent<FishingInventory>();
        }

        if (economy == null &&
            player != null)
        {
            economy =
                player.GetComponent<EconomySystem>();
        }
    }

    private void SpawnSavedTanks()
    {
        if (saveData == null ||
            saveData.tanks == null)
        {
            return;
        }

        foreach (FishTankSaveRecord record
                 in saveData.tanks)
        {
            SpawnTank(record);
        }
    }

    private void SpawnTank(
        FishTankSaveRecord record)
    {
        GameObject root =
            CreateTankShell(
                "FishTank_" + record.id,
                false
            );

        root.transform.position =
            new Vector3(
                record.x,
                record.y,
                record.z
            );

        root.transform.rotation =
            Quaternion.Euler(
                0f,
                record.rotationY,
                0f
            );

        PlacedFishTank tank =
            root.AddComponent<PlacedFishTank>();

        tank.Initialize(
            this,
            record.id
        );

        spawnedTanks.Add(tank);
    }

    private void RefreshTank(string tankId)
    {
        foreach (PlacedFishTank tank in spawnedTanks)
        {
            if (tank != null &&
                tank.TankId == tankId)
            {
                tank.RefreshFish();
                break;
            }
        }
    }

    private void UpdatePlacementPreview()
    {
        if (player == null ||
            placementPreview == null)
        {
            return;
        }

        Vector3 forward =
            player.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude <
            0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        Vector3 candidate =
            player.position +
            forward * 3.2f;

        float groundY =
            FindGroundHeight(
                candidate
            );

        placementPosition =
            new Vector3(
                candidate.x,
                groundY,
                candidate.z
            );

        placementRotationY =
            player.eulerAngles.y;

        placementPreview.transform.position =
            placementPosition;

        placementPreview.transform.rotation =
            Quaternion.Euler(
                0f,
                placementRotationY,
                0f
            );
    }

    private float FindGroundHeight(
        Vector3 candidate)
    {
        Vector3 origin =
            candidate +
            Vector3.up * 8f;

        if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                20f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        if (terrain != null)
        {
            return terrain.SampleHeight(
                       candidate
                   ) +
                   terrain.transform.position.y;
        }

        return candidate.y;
    }

    private GameObject CreateTankShell(
        string objectName,
        bool preview)
    {
        EnsureMaterials();

        GameObject root =
            new GameObject(objectName);

        CreateCube(
            "Base",
            root.transform,
            new Vector3(
                0f,
                0.12f,
                0f
            ),
            new Vector3(
                3.0f,
                0.24f,
                1.65f
            ),
            baseMaterial
        );

        CreateCube(
            "Water",
            root.transform,
            new Vector3(
                0f,
                0.92f,
                0f
            ),
            new Vector3(
                2.72f,
                1.30f,
                1.35f
            ),
            preview
                ? glassMaterial
                : waterMaterial
        );

        CreateCube(
            "FrontGlass",
            root.transform,
            new Vector3(
                0f,
                0.93f,
                -0.72f
            ),
            new Vector3(
                2.86f,
                1.52f,
                0.05f
            ),
            glassMaterial
        );

        CreateCube(
            "BackGlass",
            root.transform,
            new Vector3(
                0f,
                0.93f,
                0.72f
            ),
            new Vector3(
                2.86f,
                1.52f,
                0.05f
            ),
            glassMaterial
        );

        CreateCube(
            "LeftGlass",
            root.transform,
            new Vector3(
                -1.43f,
                0.93f,
                0f
            ),
            new Vector3(
                0.05f,
                1.52f,
                1.42f
            ),
            glassMaterial
        );

        CreateCube(
            "RightGlass",
            root.transform,
            new Vector3(
                1.43f,
                0.93f,
                0f
            ),
            new Vector3(
                0.05f,
                1.52f,
                1.42f
            ),
            glassMaterial
        );

        CreateFrame(
            root.transform,
            new Vector3(
                1.43f,
                0.93f,
                0.72f
            )
        );

        CreateFrame(
            root.transform,
            new Vector3(
                -1.43f,
                0.93f,
                0.72f
            )
        );

        CreateFrame(
            root.transform,
            new Vector3(
                1.43f,
                0.93f,
                -0.72f
            )
        );

        CreateFrame(
            root.transform,
            new Vector3(
                -1.43f,
                0.93f,
                -0.72f
            )
        );

        if (!preview)
        {
            BoxCollider collider =
                root.AddComponent<BoxCollider>();

            collider.center =
                new Vector3(
                    0f,
                    0.88f,
                    0f
                );

            collider.size =
                new Vector3(
                    3.05f,
                    1.75f,
                    1.70f
                );
        }

        return root;
    }

    private void CreateFrame(
        Transform parent,
        Vector3 localPosition)
    {
        CreateCube(
            "Frame",
            parent,
            localPosition,
            new Vector3(
                0.07f,
                1.74f,
                0.07f
            ),
            frameMaterial
        );
    }

    private static GameObject CreateCube(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject cube =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        cube.name = name;

        cube.transform.SetParent(
            parent,
            false
        );

        cube.transform.localPosition =
            localPosition;

        cube.transform.localScale =
            localScale;

        Renderer renderer =
            cube.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = material;

        Collider collider =
            cube.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        return cube;
    }

    private static void EnsureMaterials()
    {
        if (frameMaterial != null)
            return;

        Shader lit =
            Resources.Load<Shader>(
                "Fishing/FishingLit"
            );

        Shader unlit =
            Resources.Load<Shader>(
                "Fishing/FishingUnlit"
            );

        if (lit == null ||
            unlit == null)
        {
            Debug.LogError(
                "Aquarium shaders are missing."
            );

            return;
        }

        frameMaterial =
            new Material(lit);

        frameMaterial.SetColor(
            "_BaseColor",
            new Color(
                0.08f,
                0.10f,
                0.12f,
                1f
            )
        );

        baseMaterial =
            new Material(lit);

        baseMaterial.SetColor(
            "_BaseColor",
            new Color(
                0.18f,
                0.12f,
                0.07f,
                1f
            )
        );

        glassMaterial =
            new Material(unlit);

        glassMaterial.SetColor(
            "_BaseColor",
            new Color(
                0.55f,
                0.88f,
                0.96f,
                0.16f
            )
        );

        waterMaterial =
            new Material(unlit);

        waterMaterial.SetColor(
            "_BaseColor",
            new Color(
                0.06f,
                0.40f,
                0.60f,
                0.14f
            )
        );
    }

    private void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            saveData =
                new AquariumSaveData();

            return;
        }

        string json =
            PlayerPrefs.GetString(
                SaveKey,
                string.Empty
            );

        if (string.IsNullOrWhiteSpace(json))
        {
            saveData =
                new AquariumSaveData();

            return;
        }

        try
        {
            AquariumSaveData data =
                JsonUtility.FromJson<AquariumSaveData>(
                    json
                );

            saveData =
                data ??
                new AquariumSaveData();

            if (saveData.tanks == null)
            {
                saveData.tanks =
                    new List<FishTankSaveRecord>();
            }

            foreach (FishTankSaveRecord tank
                     in saveData.tanks)
            {
                if (tank.fish == null)
                {
                    tank.fish =
                        new List<CaughtFishRecord>();
                }
            }
        }
        catch
        {
            saveData =
                new AquariumSaveData();
        }
    }

    private void Save()
    {
        string json =
            JsonUtility.ToJson(
                saveData
            );

        PlayerPrefs.SetString(
            SaveKey,
            json
        );

        PlayerPrefs.Save();

        Changed?.Invoke();
    }
}

public class PlacedFishTank : MonoBehaviour
{
    private AquariumSystem owner;
    private string tankId;
    private Transform fishRoot;

    public string TankId => tankId;

    public void Initialize(
        AquariumSystem aquarium,
        string id)
    {
        owner = aquarium;
        tankId = id;

        GameObject fishRootObject =
            new GameObject("TankFish");

        fishRootObject.transform.SetParent(
            transform,
            false
        );

        fishRoot =
            fishRootObject.transform;

        RefreshFish();
    }

    public void RefreshFish()
    {
        if (fishRoot == null ||
            owner == null)
        {
            return;
        }

        for (int i =
                fishRoot.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                fishRoot
                    .GetChild(i)
                    .gameObject
            );
        }

        FishTankSaveRecord record =
            owner.GetTankRecord(
                tankId
            );

        if (record == null)
            return;

        for (int i = 0;
             i < record.fish.Count;
             i++)
        {
            CaughtFishRecord fish =
                record.fish[i];

            GameObject visual =
                FishVisualFactory.CreateFish(
                    "TankFish_" + i,
                    fishRoot,
                    fish.speciesId,
                    FishCatalog.GetVisualScale(
                        fish.speciesId,
                        fish.weightKg
                    ) *
                    0.48f
                );

            TankFishAgent agent =
                visual.AddComponent<TankFishAgent>();

            agent.Configure(
                i * 1.73f,
                fish.speciesId
            );
        }
    }
}

public class TankFishAgent : MonoBehaviour
{
    private Transform tail;

    private HeroFishAnimator heroAnimator;
    private YellowfinTunaPresentation tunaPresentation;
    private GoatfishPresentation goatfishPresentation;
    private RedSnapperPresentation redSnapperPresentation;

    private float cruiseSpeed;
    private float currentSpeed;
    private float turnSpeedDeg;
    private float pitchSpeedDeg;
    private float phase;
    private int speciesId;

    private float pathAngle;
    private float pathDirection;
    private float pathRadiusX;
    private float pathRadiusZ;
    private float baseHeight;
    private float heightAmplitude;
    private float heightPhase;

    public void Configure(
        float offset,
        int configuredSpeciesId)
    {
        phase = offset;
        speciesId = configuredSpeciesId;

        heroAnimator =
            GetComponent<HeroFishAnimator>();

        tunaPresentation =
            GetComponent<YellowfinTunaPresentation>();

        goatfishPresentation =
            GetComponent<GoatfishPresentation>();

        redSnapperPresentation =
            GetComponent<RedSnapperPresentation>();

        if (goatfishPresentation != null)
        {
            if (speciesId ==
                FishCatalog.BlackSpotGoatfishId)
            {
                // Black Spot Goatfish and Red Snapper intentionally share
                // the same aquarium travel-speed range.
                cruiseSpeed =
                    UnityEngine.Random.Range(
                        0.92f,
                        1.02f
                    );
            }
            else
            {
                // Yellow Goatfish: restore the normal authored-animation
                // matched cruise speed (remove the temporary 0.5x test).
                cruiseSpeed =
                    goatfishPresentation
                        .GetRecommendedCruiseSpeed() *
                    UnityEngine.Random.Range(
                        0.78f,
                        0.84f
                    );
            }

            turnSpeedDeg =
                UnityEngine.Random.Range(
                    138f,
                    165f
                );

            pitchSpeedDeg = 72f;

            pathRadiusX =
                UnityEngine.Random.Range(
                    0.68f,
                    0.77f
                );

            pathRadiusZ =
                UnityEngine.Random.Range(
                    0.19f,
                    0.26f
                );
        }
        else if (redSnapperPresentation != null)
        {
            // Exactly the same aquarium speed band as Black Spot Goatfish.
            cruiseSpeed =
                UnityEngine.Random.Range(
                    0.92f,
                    1.02f
                );

            turnSpeedDeg =
                UnityEngine.Random.Range(
                    165f,
                    195f
                );

            pitchSpeedDeg = 62f;

            pathRadiusX =
                UnityEngine.Random.Range(
                    0.70f,
                    0.80f
                );

            pathRadiusZ =
                UnityEngine.Random.Range(
                    0.20f,
                    0.27f
                );
        }
        else if (tunaPresentation != null)
        {
            cruiseSpeed =
                UnityEngine.Random.Range(
                    0.40f,
                    0.50f
                );

            turnSpeedDeg =
                UnityEngine.Random.Range(
                    108f,
                    132f
                );

            pitchSpeedDeg = 55f;

            pathRadiusX =
                UnityEngine.Random.Range(
                    0.82f,
                    0.94f
                );

            pathRadiusZ =
                UnityEngine.Random.Range(
                    0.26f,
                    0.34f
                );
        }
        else
        {
            // Preserve the Yellowtail/generic behavior that already looked good.
            cruiseSpeed =
                UnityEngine.Random.Range(
                    0.26f,
                    0.38f
                );

            turnSpeedDeg =
                UnityEngine.Random.Range(
                    90f,
                    120f
                );

            pitchSpeedDeg = 48f;

            pathRadiusX =
                UnityEngine.Random.Range(
                    0.78f,
                    0.94f
                );

            pathRadiusZ =
                UnityEngine.Random.Range(
                    0.24f,
                    0.34f
                );
        }

        currentSpeed =
            cruiseSpeed * 0.92f;

        pathDirection =
            UnityEngine.Random.value < 0.5f
                ? -1f
                : 1f;

        pathAngle =
            UnityEngine.Random.Range(
                0f,
                Mathf.PI * 2f
            );

        baseHeight =
            UnityEngine.Random.Range(
                0.72f,
                1.08f
            );

        heightAmplitude =
            goatfishPresentation != null
                ? UnityEngine.Random.Range(
                    0.025f,
                    0.065f
                )
                : UnityEngine.Random.Range(
                    0.045f,
                    0.10f
                );

        heightPhase =
            UnityEngine.Random.Range(
                0f,
                Mathf.PI * 2f
            );

        tail =
            transform.Find("Tail");

        PlaceOnPath();

        SyncPresentationSpeed();
    }

    private void Update()
    {
        float deltaTime =
            Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        Vector3 snapperHeadBefore =
            Vector3.zero;

        bool snapperHasHead =
            redSnapperPresentation != null &&
            redSnapperPresentation.TryGetHeadPosition(
                transform.parent,
                out snapperHeadBefore
            );

        float effectiveRadius =
            Mathf.Sqrt(
                (
                    pathRadiusX *
                    pathRadiusX +
                    pathRadiusZ *
                    pathRadiusZ
                ) *
                0.5f
            );

        float angularSpeed =
            currentSpeed /
            Mathf.Max(
                0.42f,
                effectiveRadius
            );

        if (redSnapperPresentation != null &&
            snapperHasHead)
        {
            // The RED SNAPPER PATH IS GUIDED BY THE HEAD, not its body
            // center. This prevents the body pivot from dragging the head
            // sideways around turns.
            pathAngle =
                EstimatePathAngle(
                    snapperHeadBefore
                );
        }
        else if (goatfishPresentation != null)
        {
            // Goatfish use a current-position-anchored guide.
            pathAngle =
                EstimatePathAngle(
                    transform.localPosition
                );
        }
        else
        {
            pathAngle +=
                pathDirection *
                angularSpeed *
                deltaTime;

            WrapPathAngle();
        }

        float lookAhead;

        if (goatfishPresentation != null)
        {
            lookAhead = 0.44f;
        }
        else if (redSnapperPresentation != null)
        {
            lookAhead = 0.34f;
        }
        else if (tunaPresentation != null)
        {
            lookAhead = 0.34f;
        }
        else
        {
            lookAhead = 0.30f;
        }

        Vector3 target =
            EvaluatePath(
                pathAngle +
                pathDirection *
                lookAhead
            );

        Vector3 steeringOrigin =
            redSnapperPresentation != null &&
            snapperHasHead
                ? snapperHeadBefore
                : transform.localPosition;

        Vector3 toTarget =
            target -
            steeringOrigin;

        if (toTarget.sqrMagnitude >
            0.0001f)
        {
            Vector3 desiredDirection =
                toTarget.normalized;

            Vector3 currentForward =
                transform.localRotation *
                Vector3.forward;

            float signedTurn =
                SignedHorizontalAngle(
                    currentForward,
                    desiredDirection
                );

            float absoluteTurn =
                Mathf.Abs(
                    signedTurn
                );

            // Body curvature follows the same signed turn request as the
            // head/root. The root still owns movement; these values only make
            // the body trail the curve instead of remaining rigid.
            float normalizedBodyTurn =
                Mathf.Clamp(
                    signedTurn /
                    72f,
                    -1f,
                    1f
                );

            if (tunaPresentation != null)
            {
                tunaPresentation.SetAquariumTurn(
                    normalizedBodyTurn
                );
            }

            if (redSnapperPresentation != null)
            {
                redSnapperPresentation.SetAquariumTurn(
                    normalizedBodyTurn
                );
            }

            float minimumCornerFactor =
                goatfishPresentation != null
                    ? 0.86f
                    : 0.78f;

            float cornerSpeedFactor =
                Mathf.Lerp(
                    1f,
                    minimumCornerFactor,
                    Mathf.InverseLerp(
                        18f,
                        85f,
                        absoluteTurn
                    )
                );

            float desiredSpeed =
                cruiseSpeed *
                cornerSpeedFactor;

            currentSpeed =
                Mathf.MoveTowards(
                    currentSpeed,
                    desiredSpeed,
                    deltaTime *
                    cruiseSpeed *
                    3.2f
                );

            Vector3 flatDesired =
                Vector3.ProjectOnPlane(
                    desiredDirection,
                    Vector3.up
                );

            Vector3 flatForward =
                Vector3.ProjectOnPlane(
                    currentForward,
                    Vector3.up
                );

            if (flatDesired.sqrMagnitude >
                    0.0001f &&
                flatForward.sqrMagnitude >
                    0.0001f)
            {
                flatDesired.Normalize();
                flatForward.Normalize();

                Vector3 newFlatForward =
                    Vector3.RotateTowards(
                        flatForward,
                        flatDesired,
                        turnSpeedDeg *
                        Mathf.Deg2Rad *
                        deltaTime,
                        0f
                    );

                float desiredPitch =
                    Mathf.Asin(
                        Mathf.Clamp(
                            desiredDirection.y,
                            -0.30f,
                            0.30f
                        )
                    ) *
                    Mathf.Rad2Deg;

                float currentPitch =
                    NormalizeAngle(
                        transform.localEulerAngles.x
                    );

                float newPitch =
                    Mathf.MoveTowardsAngle(
                        currentPitch,
                        -desiredPitch,
                        pitchSpeedDeg *
                        deltaTime
                    );

                transform.localRotation =
                    Quaternion.LookRotation(
                        newFlatForward,
                        Vector3.up
                    ) *
                    Quaternion.Euler(
                        newPitch,
                        0f,
                        0f
                    );
            }

            // Head-led propulsion.
            Vector3 forward =
                transform.localRotation *
                Vector3.forward;

            forward.Normalize();

            if (redSnapperPresentation != null &&
                snapperHasHead)
            {
                // Rotation above happens on the GameObject root, whose pivot
                // is near the fish body. Counter that pivot sweep first, then
                // move the HEAD exactly forward. This makes the head the
                // locomotion anchor: it cannot strafe sideways during a turn.
                if (redSnapperPresentation.TryGetHeadPosition(
                        transform.parent,
                        out Vector3 headAfterRotation
                    ))
                {
                    Vector3 desiredHeadPosition =
                        snapperHeadBefore +
                        forward *
                        currentSpeed *
                        deltaTime;

                    transform.localPosition +=
                        desiredHeadPosition -
                        headAfterRotation;
                }
                else
                {
                    transform.localPosition +=
                        forward *
                        currentSpeed *
                        deltaTime;
                }
            }
            else
            {
                transform.localPosition +=
                    forward *
                    currentSpeed *
                    deltaTime;
            }

            if (goatfishPresentation == null &&
                redSnapperPresentation == null)
            {
                SoftContainInsideTank();
            }
        }

        SyncPresentationSpeed();

        if (tail != null)
        {
            tail.localRotation =
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(
                        Time.time *
                        cruiseSpeed *
                        22f +
                        phase
                    ) *
                    30f,
                    0f
                );
        }
    }

    private void SyncPresentationSpeed()
    {
        if (goatfishPresentation != null)
        {
            goatfishPresentation.SetAquariumLocomotion(
                currentSpeed
            );
        }

        if (redSnapperPresentation != null)
        {
            redSnapperPresentation.SetAquariumLocomotion(
                currentSpeed
            );
        }

        if (tunaPresentation != null)
        {
            tunaPresentation.SetAquariumLocomotion(
                currentSpeed
            );
        }
    }

    private void PlaceOnPath()
    {
        transform.localPosition =
            EvaluatePath(
                pathAngle
            );

        Vector3 tangent =
            EvaluatePath(
                pathAngle +
                pathDirection *
                0.035f
            ) -
            transform.localPosition;

        tangent =
            Vector3.ProjectOnPlane(
                tangent,
                Vector3.up
            );

        if (tangent.sqrMagnitude <
            0.0001f)
        {
            tangent =
                Vector3.forward;
        }

        transform.localRotation =
            Quaternion.LookRotation(
                tangent.normalized,
                Vector3.up
            );
    }

    private Vector3 EvaluatePath(
        float angle)
    {
        float y =
            baseHeight +
            Mathf.Sin(
                angle * 0.72f +
                heightPhase
            ) *
            heightAmplitude;

        return
            new Vector3(
                Mathf.Cos(angle) *
                pathRadiusX,
                y,
                Mathf.Sin(angle) *
                pathRadiusZ
            );
    }

    private float EstimatePathAngle(
        Vector3 localPosition)
    {
        float normalizedX =
            localPosition.x /
            Mathf.Max(
                0.001f,
                pathRadiusX
            );

        float normalizedZ =
            localPosition.z /
            Mathf.Max(
                0.001f,
                pathRadiusZ
            );

        float angle =
            Mathf.Atan2(
                normalizedZ,
                normalizedX
            );

        if (angle < 0f)
        {
            angle +=
                Mathf.PI * 2f;
        }

        return angle;
    }

    private void WrapPathAngle()
    {
        float fullCircle =
            Mathf.PI * 2f;

        if (pathAngle >= fullCircle)
        {
            pathAngle -= fullCircle;
        }
        else if (pathAngle < 0f)
        {
            pathAngle += fullCircle;
        }
    }

    private void SoftContainInsideTank()
    {
        Vector3 position =
            transform.localPosition;

        position.x =
            Mathf.Clamp(
                position.x,
                -1.03f,
                1.03f
            );

        position.y =
            Mathf.Clamp(
                position.y,
                0.43f,
                1.33f
            );

        position.z =
            Mathf.Clamp(
                position.z,
                -0.41f,
                0.41f
            );

        transform.localPosition =
            position;
    }

    private static float SignedHorizontalAngle(
        Vector3 from,
        Vector3 to)
    {
        Vector3 flatFrom =
            Vector3.ProjectOnPlane(
                from,
                Vector3.up
            );

        Vector3 flatTo =
            Vector3.ProjectOnPlane(
                to,
                Vector3.up
            );

        if (flatFrom.sqrMagnitude <
                0.0001f ||
            flatTo.sqrMagnitude <
                0.0001f)
        {
            return 0f;
        }

        return Vector3.SignedAngle(
            flatFrom,
            flatTo,
            Vector3.up
        );
    }

    private static float NormalizeAngle(
        float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}
