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
        float maxDistance = 10.5f)
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
            forward * 10.5f;

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
                17.25f,
                0.28f,
                9.50f
            ),
            baseMaterial
        );

        CreateCube(
            "Water",
            root.transform,
            new Vector3(
                0f,
                4.58f,
                0f
            ),
            new Vector3(
                15.65f,
                8.20f,
                7.75f
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
                4.58f,
                -4.10f
            ),
            new Vector3(
                16.45f,
                8.60f,
                0.08f
            ),
            glassMaterial
        );

        CreateCube(
            "BackGlass",
            root.transform,
            new Vector3(
                0f,
                4.58f,
                4.10f
            ),
            new Vector3(
                16.45f,
                8.60f,
                0.08f
            ),
            glassMaterial
        );

        CreateCube(
            "LeftGlass",
            root.transform,
            new Vector3(
                -8.225f,
                4.58f,
                0f
            ),
            new Vector3(
                0.08f,
                8.60f,
                8.20f
            ),
            glassMaterial
        );

        CreateCube(
            "RightGlass",
            root.transform,
            new Vector3(
                8.225f,
                4.58f,
                0f
            ),
            new Vector3(
                0.08f,
                8.60f,
                8.20f
            ),
            glassMaterial
        );

        CreateFrame(
            root.transform,
            new Vector3(
                8.225f,
                4.58f,
                4.10f
            )
        );

        CreateFrame(
            root.transform,
            new Vector3(
                -8.225f,
                4.58f,
                4.10f
            )
        );

        CreateFrame(
            root.transform,
            new Vector3(
                8.225f,
                4.58f,
                -4.10f
            )
        );

        CreateFrame(
            root.transform,
            new Vector3(
                -8.225f,
                4.58f,
                -4.10f
            )
        );

        if (!preview)
        {
            BoxCollider collider =
                root.AddComponent<BoxCollider>();

            collider.center =
                new Vector3(
                    0f,
                    4.58f,
                    0f
                );

            collider.size =
                new Vector3(
                    17.50f,
                    9.10f,
                    9.60f
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
                0.10f,
                8.88f,
                0.10f
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

    private int pathVariant;
    private float pathShapePhase;
    private float pathWobbleX;
    private float pathWobbleZ;
    private int heightHarmonic;
    private float secondaryHeightPhase;

    private float speedDriftPhase;
    private float speedDriftFrequency;
    private float speedDriftAmount;

    private struct BodyTrailSample
    {
        public float distance;
        public Vector3 positionWorld;
        public Vector3 forwardWorld;
    }

    private readonly List<BodyTrailSample> bodyTrail =
        new List<BodyTrailSample>(160);

    private float bodyTrailDistance;
    private Vector3 bodyTrailLastPosition;
    private Vector3 bodyTrailLastForward;
    private float bodyTrailLength = 0.92f;
    private bool bodyTrailInitialized;

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
                    0.78f,
                    0.90f
                );

            pathRadiusZ =
                UnityEngine.Random.Range(
                    0.24f,
                    0.32f
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
                    0.92f,
                    1.06f
                );

            pathRadiusZ =
                UnityEngine.Random.Range(
                    0.25f,
                    0.34f
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
                    0.32f,
                    0.42f
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
                    0.88f,
                    1.06f
                );

            pathRadiusZ =
                UnityEngine.Random.Range(
                    0.30f,
                    0.42f
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
                0.76f,
                1.24f
            );

        heightAmplitude =
            goatfishPresentation != null
                ? UnityEngine.Random.Range(
                    0.055f,
                    0.11f
                )
                : UnityEngine.Random.Range(
                    0.075f,
                    0.16f
                );

        heightPhase =
            UnityEngine.Random.Range(
                0f,
                Mathf.PI * 2f
            );

        pathVariant =
            UnityEngine.Random.Range(
                0,
                4
            );

        pathShapePhase =
            UnityEngine.Random.Range(
                0f,
                Mathf.PI * 2f
            );

        pathWobbleX =
            UnityEngine.Random.Range(
                0.06f,
                0.14f
            );

        pathWobbleZ =
            UnityEngine.Random.Range(
                0.06f,
                0.16f
            );

        heightHarmonic =
            UnityEngine.Random.value < 0.5f
                ? 1
                : 2;

        secondaryHeightPhase =
            UnityEngine.Random.Range(
                0f,
                Mathf.PI * 2f
            );

        speedDriftPhase =
            UnityEngine.Random.Range(
                0f,
                Mathf.PI * 2f
            );

        speedDriftFrequency =
            UnityEngine.Random.Range(
                0.16f,
                0.34f
            );

        speedDriftAmount =
            UnityEngine.Random.Range(
                0.05f,
                0.13f
            );

        tail =
            transform.Find("Tail");

        PlaceOnPath();

        ResetBodyTrail();
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

            // The body must TRAIL the head through a turn.
            // SignedHorizontalAngle describes where the HEAD is turning.
            // The body/tail bend is therefore the opposite sign so the rear
            // stays on the previous path instead of whipping into the turn.
            float normalizedBodyTurn =
                Mathf.Clamp(
                    -signedTurn /
                    80f,
                    -1f,
                    1f
                );

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

            float speedDrift =
                1f +
                Mathf.Sin(
                    Time.time *
                    speedDriftFrequency +
                    speedDriftPhase
                ) *
                speedDriftAmount;

            float desiredSpeed =
                cruiseSpeed *
                cornerSpeedFactor *
                speedDrift;

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

        UpdateBodyTrail();

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

    private void ResetBodyTrail()
    {
        bodyTrail.Clear();
        bodyTrailDistance = 0f;
        bodyTrailInitialized = false;

        if (!TryGetTrailHead(
                out Vector3 positionWorld,
                out Vector3 forwardWorld,
                out float lengthWorld))
        {
            return;
        }

        bodyTrailLength =
            Mathf.Clamp(
                lengthWorld,
                0.30f,
                1.60f
            );

        bodyTrailLastPosition =
            positionWorld;

        bodyTrailLastForward =
            forwardWorld;

        bodyTrail.Add(
            new BodyTrailSample
            {
                distance = 0f,
                positionWorld =
                    positionWorld,
                forwardWorld =
                    forwardWorld
            }
        );

        bodyTrailInitialized = true;

        PushBodyTrailToPresentation(
            forwardWorld
        );
    }

    private void UpdateBodyTrail()
    {
        if (tunaPresentation == null &&
            redSnapperPresentation == null)
        {
            return;
        }

        if (!TryGetTrailHead(
                out Vector3 positionWorld,
                out Vector3 forwardWorld,
                out float lengthWorld))
        {
            return;
        }

        bodyTrailLength =
            Mathf.Lerp(
                bodyTrailLength,
                Mathf.Clamp(
                    lengthWorld,
                    0.30f,
                    1.60f
                ),
                0.12f
            );

        if (!bodyTrailInitialized)
        {
            ResetBodyTrail();
            return;
        }

        float moved =
            Vector3.Distance(
                positionWorld,
                bodyTrailLastPosition
            );

        // A teleport/rebuild is not a piece of track.
        if (moved > 0.75f)
        {
            ResetBodyTrail();
            return;
        }

        // This is the key rope/train behavior: rotating in place does NOT lay
        // new track. The rear can only enter a curve after the head has
        // physically travelled into it.
        if (moved > 0.0015f)
        {
            bodyTrailDistance +=
                moved;

            bodyTrail.Add(
                new BodyTrailSample
                {
                    distance =
                        bodyTrailDistance,
                    positionWorld =
                        positionWorld,
                    forwardWorld =
                        forwardWorld
                }
            );

            bodyTrailLastPosition =
                positionWorld;

            bodyTrailLastForward =
                forwardWorld;

            float keepDistance =
                Mathf.Max(
                    1.25f,
                    bodyTrailLength *
                    1.65f
                );

            float oldest =
                bodyTrailDistance -
                keepDistance;

            while (bodyTrail.Count > 3 &&
                   bodyTrail[1].distance <
                   oldest)
            {
                bodyTrail.RemoveAt(0);
            }

            while (bodyTrail.Count > 180)
            {
                bodyTrail.RemoveAt(0);
            }
        }

        PushBodyTrailToPresentation(
            forwardWorld
        );
    }

    private bool TryGetTrailHead(
        out Vector3 positionWorld,
        out Vector3 forwardWorld,
        out float lengthWorld)
    {
        forwardWorld =
            transform.forward;

        if (forwardWorld.sqrMagnitude <
            0.0001f)
        {
            forwardWorld =
                Vector3.forward;
        }

        forwardWorld.Normalize();

        if (redSnapperPresentation != null &&
            redSnapperPresentation.TryGetHeadPositionWorld(
                out positionWorld))
        {
            lengthWorld =
                redSnapperPresentation
                    .GetBodyLengthWorld();

            return true;
        }

        if (tunaPresentation != null &&
            tunaPresentation.TryGetHeadPosition(
                out positionWorld))
        {
            lengthWorld =
                tunaPresentation
                    .GetBodyLengthWorld();

            return true;
        }

        positionWorld =
            transform.position;

        lengthWorld = 0.92f;

        return false;
    }

    private void PushBodyTrailToPresentation(
        Vector3 currentForwardWorld)
    {
        Vector3 forward25 =
            SampleBodyTrailForward(
                bodyTrailLength * 0.25f,
                currentForwardWorld
            );

        Vector3 forward50 =
            SampleBodyTrailForward(
                bodyTrailLength * 0.50f,
                forward25
            );

        Vector3 forward75 =
            SampleBodyTrailForward(
                bodyTrailLength * 0.75f,
                forward50
            );

        Vector3 forward100 =
            SampleBodyTrailForward(
                bodyTrailLength * 1.00f,
                forward75
            );

        if (tunaPresentation != null)
        {
            tunaPresentation.SetAquariumTrail(
                currentForwardWorld,
                forward25,
                forward50,
                forward75,
                forward100
            );
        }

        if (redSnapperPresentation != null)
        {
            redSnapperPresentation.SetAquariumTrail(
                currentForwardWorld,
                forward25,
                forward50,
                forward75,
                forward100
            );
        }
    }

    private Vector3 SampleBodyTrailForward(
        float distanceBehind,
        Vector3 fallback)
    {
        if (bodyTrail.Count == 0)
        {
            return fallback.normalized;
        }

        float targetDistance =
            bodyTrailDistance -
            Mathf.Max(
                0f,
                distanceBehind
            );

        if (targetDistance <=
            bodyTrail[0].distance)
        {
            return
                bodyTrail[0]
                    .forwardWorld
                    .normalized;
        }

        for (int i =
                bodyTrail.Count - 1;
             i > 0;
             i--)
        {
            BodyTrailSample newer =
                bodyTrail[i];

            BodyTrailSample older =
                bodyTrail[i - 1];

            if (older.distance <=
                    targetDistance &&
                newer.distance >=
                    targetDistance)
            {
                float span =
                    newer.distance -
                    older.distance;

                float t =
                    span > 0.00001f
                        ? (
                            targetDistance -
                            older.distance
                          ) /
                          span
                        : 0f;

                Vector3 result =
                    Vector3.Slerp(
                        older.forwardWorld,
                        newer.forwardWorld,
                        Mathf.Clamp01(t)
                    );

                if (result.sqrMagnitude >
                    0.0001f)
                {
                    return
                        result.normalized;
                }

                return
                    older.forwardWorld
                        .normalized;
            }
        }

        return
            bodyTrail[
                bodyTrail.Count - 1
            ]
            .forwardWorld
            .normalized;
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
        float x =
            Mathf.Cos(angle) *
            pathRadiusX;

        float z =
            Mathf.Sin(angle) *
            pathRadiusZ;

        // Four gentle loop families. They stay closed and tank-safe but avoid
        // making every fish orbit the same obvious ellipse.
        switch (pathVariant)
        {
            case 1:
                x +=
                    Mathf.Sin(
                        angle * 2f +
                        pathShapePhase
                    ) *
                    pathRadiusX *
                    pathWobbleX;

                z +=
                    Mathf.Sin(
                        angle * 3f +
                        pathShapePhase * 0.7f
                    ) *
                    pathRadiusZ *
                    pathWobbleZ;
                break;

            case 2:
                x +=
                    Mathf.Cos(
                        angle * 3f +
                        pathShapePhase
                    ) *
                    pathRadiusX *
                    pathWobbleX;

                z +=
                    Mathf.Sin(
                        angle * 2f +
                        pathShapePhase
                    ) *
                    pathRadiusZ *
                    pathWobbleZ;
                break;

            case 3:
                float breathingRadius =
                    0.90f +
                    Mathf.Sin(
                        angle * 2f +
                        pathShapePhase
                    ) *
                    0.10f;

                x *=
                    breathingRadius;

                z *=
                    1.04f -
                    (
                        breathingRadius -
                        0.90f
                    );
                break;
        }

        float y =
            baseHeight +
            Mathf.Sin(
                angle *
                heightHarmonic +
                heightPhase
            ) *
            heightAmplitude +
            Mathf.Sin(
                angle * 3f +
                secondaryHeightPhase
            ) *
            heightAmplitude *
            0.30f;

        return
            new Vector3(
                x,
                y,
                z
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
                -1.22f,
                1.22f
            );

        position.y =
            Mathf.Clamp(
                position.y,
                0.44f,
                1.53f
            );

        position.z =
            Mathf.Clamp(
                position.z,
                -0.52f,
                0.52f
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
