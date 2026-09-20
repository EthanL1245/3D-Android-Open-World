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
                i * 1.73f
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

    private float cruiseSpeed;
    private float phase;

    // Smooth closed swimming lane.
    private float pathAngle;
    private float pathDirection;
    private float pathRadiusX;
    private float pathRadiusZ;
    private float pathWarp;
    private float pathWarpPhase;

    private float baseHeight;
    private float heightAmplitude;
    private float heightPhase;

    private Vector3 previousForward;

    public void Configure(float offset)
    {
        phase = offset;

        heroAnimator =
            GetComponent<HeroFishAnimator>();

        tunaPresentation =
            GetComponent<YellowfinTunaPresentation>();

        goatfishPresentation =
            GetComponent<GoatfishPresentation>();

        bool isDetailedFish =
            heroAnimator != null ||
            tunaPresentation != null ||
            goatfishPresentation != null;

        if (tunaPresentation != null)
        {
            cruiseSpeed =
                UnityEngine.Random.Range(
                    0.48f,
                    0.58f
                );
        }
        else if (goatfishPresentation != null)
        {
            // Goatfish were previously animating too quickly
            // relative to their actual forward travel.
            cruiseSpeed =
                UnityEngine.Random.Range(
                    0.43f,
                    0.53f
                );
        }
        else if (heroAnimator != null)
        {
            cruiseSpeed =
                UnityEngine.Random.Range(
                    0.42f,
                    0.51f
                );
        }
        else
        {
            cruiseSpeed =
                UnityEngine.Random.Range(
                    0.30f,
                    0.42f
                );
        }

        pathRadiusX =
            isDetailedFish
                ? UnityEngine.Random.Range(
                    0.82f,
                    0.94f
                )
                : UnityEngine.Random.Range(
                    0.76f,
                    0.92f
                );

        pathRadiusZ =
            isDetailedFish
                ? UnityEngine.Random.Range(
                    0.27f,
                    0.35f
                )
                : UnityEngine.Random.Range(
                    0.24f,
                    0.34f
                );

        pathWarp =
            UnityEngine.Random.Range(
                0.018f,
                0.050f
            );

        pathWarpPhase =
            UnityEngine.Random.Range(
                0f,
                Mathf.PI * 2f
            );

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
                0.70f,
                1.08f
            );

        heightAmplitude =
            UnityEngine.Random.Range(
                0.035f,
                0.085f
            );

        heightPhase =
            UnityEngine.Random.Range(
                0f,
                Mathf.PI * 2f
            );

        tail =
            transform.Find("Tail");

        PlaceOnPath();

        if (goatfishPresentation != null)
        {
            goatfishPresentation.SetAquariumLocomotion(
                cruiseSpeed
            );
        }

        if (tunaPresentation != null)
        {
            tunaPresentation.SetAquariumLocomotion(
                cruiseSpeed
            );
        }
    }

    private void Update()
    {
        float deltaTime =
            Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        // Advance by physical distance, not an arbitrary angular rate.
        // This keeps speed approximately constant around the whole oval.
        Vector3 derivative =
            EvaluatePathDerivative(
                pathAngle
            );

        float distancePerRadian =
            Mathf.Max(
                0.08f,
                derivative.magnitude
            );

        float angularDelta =
            cruiseSpeed /
            distancePerRadian *
            deltaTime *
            pathDirection;

        pathAngle +=
            angularDelta;

        WrapPathAngle();

        Vector3 oldPosition =
            transform.localPosition;

        Vector3 newPosition =
            EvaluatePath(
                pathAngle
            );

        Vector3 velocity =
            newPosition -
            oldPosition;

        Vector3 forward;

        if (velocity.sqrMagnitude >
            0.0000001f)
        {
            forward =
                velocity.normalized;
        }
        else
        {
            forward =
                EvaluatePathDerivative(
                    pathAngle
                ) *
                pathDirection;

            if (forward.sqrMagnitude <
                0.0001f)
            {
                forward =
                    previousForward.sqrMagnitude >
                    0.0001f
                        ? previousForward
                        : Vector3.forward;
            }

            forward.Normalize();
        }

        // This is the key rule for every detailed fish:
        // the head/root points along the same vector the fish actually travels.
        float signedTurn =
            previousForward.sqrMagnitude >
                0.0001f
                ? SignedHorizontalAngle(
                    previousForward,
                    forward
                )
                : 0f;

        float turnRate =
            deltaTime > 0.0001f
                ? signedTurn /
                  deltaTime
                : 0f;

        float bank =
            Mathf.Clamp(
                -turnRate / 110f,
                -1f,
                1f
            ) *
            3.5f;

        Quaternion heading =
            Quaternion.LookRotation(
                forward,
                Vector3.up
            );

        transform.localPosition =
            newPosition;

        transform.localRotation =
            heading *
            Quaternion.Euler(
                0f,
                0f,
                bank
            );

        previousForward =
            forward;

        if (tunaPresentation != null)
        {
            // Tail should lag outside the turn while the head stays on tangent.
            tunaPresentation.SetAquariumTurn(
                Mathf.Clamp(
                    -turnRate /
                    100f,
                    -1f,
                    1f
                )
            );

            tunaPresentation.SetAquariumLocomotion(
                cruiseSpeed
            );
        }

        if (goatfishPresentation != null)
        {
            goatfishPresentation.SetAquariumLocomotion(
                cruiseSpeed
            );
        }

        if (tail != null)
        {
            tail.localRotation =
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(
                        Time.time *
                        cruiseSpeed *
                        18f +
                        phase
                    ) *
                    27f,
                    0f
                );
        }
    }

    private void PlaceOnPath()
    {
        Vector3 position =
            EvaluatePath(
                pathAngle
            );

        Vector3 tangent =
            EvaluatePathDerivative(
                pathAngle
            ) *
            pathDirection;

        if (tangent.sqrMagnitude <
            0.0001f)
        {
            tangent =
                Vector3.forward;
        }

        tangent.Normalize();

        transform.localPosition =
            position;

        transform.localRotation =
            Quaternion.LookRotation(
                tangent,
                Vector3.up
            );

        previousForward =
            tangent;
    }

    private Vector3 EvaluatePath(
        float angle)
    {
        float x =
            Mathf.Cos(angle) *
            pathRadiusX;

        float z =
            Mathf.Sin(angle) *
            pathRadiusZ +
            Mathf.Sin(
                angle * 2f +
                pathWarpPhase
            ) *
            pathWarp;

        float y =
            baseHeight +
            Mathf.Sin(
                angle * 0.72f +
                heightPhase
            ) *
            heightAmplitude;

        return
            new Vector3(
                x,
                y,
                z
            );
    }

    private Vector3 EvaluatePathDerivative(
        float angle)
    {
        // Analytic derivative of EvaluatePath with respect to angle.
        float dx =
            -Mathf.Sin(angle) *
            pathRadiusX;

        float dz =
            Mathf.Cos(angle) *
            pathRadiusZ +
            Mathf.Cos(
                angle * 2f +
                pathWarpPhase
            ) *
            pathWarp *
            2f;

        float dy =
            Mathf.Cos(
                angle * 0.72f +
                heightPhase
            ) *
            heightAmplitude *
            0.72f;

        return
            new Vector3(
                dx,
                dy,
                dz
            );
    }

    private void WrapPathAngle()
    {
        float full =
            Mathf.PI * 2f;

        while (pathAngle >= full)
            pathAngle -= full;

        while (pathAngle < 0f)
            pathAngle += full;
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
}
