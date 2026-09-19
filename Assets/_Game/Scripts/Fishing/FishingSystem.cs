using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FishingSystem : MonoBehaviour
{
    private enum FishingState
    {
        Idle,
        Casting,
        Waiting,
        Bite,
        Fighting
    }

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private OceanWater oceanWater;
    [SerializeField] private FishingInventory inventory;
    [SerializeField] private Canvas gameplayCanvas;

    [Header("Casting")]
    [SerializeField] private float preferredCastDistance = 16f;
    [SerializeField] private float minimumCastDistance = 5f;
    [SerializeField] private float maximumCastDistance = 28f;
    [SerializeField] private float castDuration = 0.72f;

    [Header("Bites")]
    [SerializeField] private float minimumBiteDelay = 2.5f;
    [SerializeField] private float maximumBiteDelay = 6.0f;
    [SerializeField] private float hookWindow = 1.35f;

    private FishingHUD hud;
    private FishingState state;

    private GameObject rodRoot;
    private Transform rodTip;
    private LineRenderer fishingLine;
    private GameObject bobber;

    private Transform heldFishAnchor;
    private GameObject heldFishVisual;
    private float heldFishFlopOffset;

    private bool rodEquipped = true;

    private Vector3 castPoint;
    private float stateTimer;
    private float fightTension;
    private float fightProgress;
    private float lineBreakTimer;
    private float fightTime;
    private float surgeAmount;
    private float surgeTimer;

    private int hookedSpeciesId;
    private float hookedWeightKg;

    private Terrain terrain;

    public FishingInventory Inventory => inventory;

    private void Start()
    {
        ResolveReferences();

        if (playerCamera == null ||
            oceanWater == null ||
            inventory == null ||
            gameplayCanvas == null)
        {
            Debug.LogWarning(
                "FishingSystem could not resolve all required references."
            );
            enabled = false;
            return;
        }

        terrain = Terrain.activeTerrain;

        CreateRodAndLine();
        CreateHeldFishAnchor();

        hud =
            gameplayCanvas
                .gameObject
                .GetComponent<FishingHUD>();

        if (hud == null)
        {
            hud =
                gameplayCanvas
                    .gameObject
                    .AddComponent<FishingHUD>();
        }

        hud.Initialize(this);

        inventory.Changed +=
            hud.RefreshInventory;

        EquipRod();
    }

    private void OnDestroy()
    {
        if (inventory != null &&
            hud != null)
        {
            inventory.Changed -=
                hud.RefreshInventory;
        }
    }

    private void Update()
    {
        if (hud == null)
            return;

        UpdateHeldFishAnimation();

        FishingActionButton action =
            hud.ActionInput;

        if (action != null &&
            action.ConsumePressed())
        {
            HandleActionPressed();
        }

        switch (state)
        {
            case FishingState.Waiting:
                UpdateWaiting();
                break;

            case FishingState.Bite:
                UpdateBite();
                break;

            case FishingState.Fighting:
                UpdateFight(
                    action != null &&
                    action.IsHeld
                );
                break;
        }

        UpdateLineAndBobber();
    }

    public void Configure(
        Camera camera,
        OceanWater water,
        FishingInventory fishingInventory,
        Canvas canvas)
    {
        playerCamera = camera;
        oceanWater = water;
        inventory = fishingInventory;
        gameplayCanvas = canvas;
    }

    public void EquipRod()
    {
        CancelFishing();

        rodEquipped = true;

        if (rodRoot != null)
            rodRoot.SetActive(true);

        ClearHeldFish();

        if (hud != null)
        {
            hud.SetRodSelected(true);
            hud.SetActionVisible(true);
            hud.SetActionLabel("CAST");
            hud.SetStatus(
                "Aim toward open water and cast."
            );
            hud.ShowFightMeters(false);
            hud.SetInventoryOpen(false);
        }
    }

    public void HoldFish(int inventoryIndex)
    {
        if (inventory == null ||
            inventoryIndex < 0 ||
            inventoryIndex >=
            inventory.Fish.Count)
        {
            return;
        }

        CancelFishing();

        rodEquipped = false;

        if (rodRoot != null)
            rodRoot.SetActive(false);

        ClearHeldFish();

        CaughtFishRecord record =
            inventory.Fish[inventoryIndex];

        heldFishVisual =
            FishVisualFactory.CreateFish(
                "Held_" +
                FishCatalog
                    .Get(record.speciesId)
                    .Name,
                heldFishAnchor,
                record.speciesId,
                Mathf.Lerp(
                    0.72f,
                    1.20f,
                    Mathf.InverseLerp(
                        0.2f,
                        8f,
                        record.weightKg
                    )
                )
            );

        heldFishVisual.transform.localPosition =
            Vector3.zero;

        heldFishVisual.transform.localRotation =
            Quaternion.Euler(
                4f,
                92f,
                8f
            );

        heldFishFlopOffset =
            Random.Range(
                0f,
                10f
            );

        if (hud != null)
        {
            hud.SetRodSelected(false);
            hud.SetActionVisible(false);
            hud.SetStatus(
                FishCatalog
                    .Get(record.speciesId)
                    .Name +
                "  " +
                record.weightKg
                    .ToString("0.00") +
                " kg"
            );
            hud.ShowFightMeters(false);
            hud.SetInventoryOpen(false);
        }
    }

    private void ResolveReferences()
    {
        if (playerCamera == null)
        {
            playerCamera =
                GetComponentInChildren<Camera>();

            if (playerCamera == null)
            {
                playerCamera =
                    FindFirstObjectByType<Camera>();
            }
        }

        if (oceanWater == null)
        {
            oceanWater =
                FindFirstObjectByType<OceanWater>();
        }

        if (inventory == null)
        {
            inventory =
                GetComponent<FishingInventory>();

            if (inventory == null)
            {
                inventory =
                    gameObject.AddComponent<FishingInventory>();
            }
        }

        if (gameplayCanvas == null)
        {
            gameplayCanvas =
                FindFirstObjectByType<Canvas>();
        }
    }

    private void HandleActionPressed()
    {
        if (!rodEquipped)
            return;

        switch (state)
        {
            case FishingState.Idle:
                TryCast();
                break;

            case FishingState.Bite:
                StartFight();
                break;
        }
    }

    private void TryCast()
    {
        if (!TryGetCastPoint(
                out Vector3 target))
        {
            hud.SetStatus(
                "Aim toward open water."
            );

            return;
        }

        StartCoroutine(
            CastRoutine(target)
        );
    }

    private IEnumerator CastRoutine(
        Vector3 target)
    {
        state = FishingState.Casting;
        hud.SetActionLabel("...");
        hud.SetStatus("Casting...");

        bobber.SetActive(true);
        fishingLine.enabled = true;

        Vector3 start =
            rodTip.position;

        float elapsed = 0f;

        while (elapsed < castDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / castDuration
                );

            Vector3 point =
                Vector3.Lerp(
                    start,
                    target,
                    t
                );

            point.y +=
                Mathf.Sin(
                    t * Mathf.PI
                ) *
                4.0f;

            bobber.transform.position =
                point;

            SetLinePositions();

            yield return null;
        }

        castPoint = target;
        SnapBobberToSurface();

        state = FishingState.Waiting;
        stateTimer =
            Random.Range(
                minimumBiteDelay,
                maximumBiteDelay
            );

        hud.SetActionLabel("WAIT");
        hud.SetStatus(
            "Waiting for a bite..."
        );
    }

    private void UpdateWaiting()
    {
        stateTimer -= Time.deltaTime;

        SnapBobberToSurface();

        if (stateTimer <= 0f)
        {
            hookedSpeciesId =
                FishCatalog.RollSpecies();

            hookedWeightKg =
                FishCatalog.RollWeight(
                    hookedSpeciesId
                );

            state =
                FishingState.Bite;

            stateTimer = hookWindow;

            hud.SetActionLabel("HOOK!");
            hud.SetStatus(
                "BITE! Tap HOOK!"
            );
        }
    }

    private void UpdateBite()
    {
        stateTimer -= Time.deltaTime;

        float surface =
            oceanWater.GetSurfaceHeight(
                castPoint
            );

        Vector3 position =
            castPoint;

        position.y =
            surface -
            0.22f +
            Mathf.Sin(
                Time.time * 17f
            ) *
            0.06f;

        bobber.transform.position =
            position;

        if (stateTimer <= 0f)
        {
            FailFishing(
                "Too slow - the fish got away."
            );
        }
    }

    private void StartFight()
    {
        state = FishingState.Fighting;

        fightTension = 0.30f;
        fightProgress = 0.08f;
        lineBreakTimer = 0f;
        fightTime = 0f;
        surgeAmount = 0f;
        surgeTimer =
            Random.Range(
                1.2f,
                2.3f
            );

        hud.SetActionLabel("REEL");
        hud.SetStatus(
            "Hold REEL, but keep tension out of the red!"
        );
        hud.ShowFightMeters(true);
        hud.SetFightMeters(
            fightTension,
            fightProgress
        );
    }

    private void UpdateFight(bool reeling)
    {
        FishSpeciesDefinition species =
            FishCatalog.Get(
                hookedSpeciesId
            );

        fightTime += Time.deltaTime;
        surgeTimer -= Time.deltaTime;

        if (surgeTimer <= 0f)
        {
            surgeAmount =
                Random.Range(
                    0.45f,
                    0.90f
                ) *
                Mathf.Lerp(
                    0.85f,
                    1.25f,
                    species.Difficulty
                );

            surgeTimer =
                Random.Range(
                    1.6f,
                    3.0f
                );
        }

        surgeAmount =
            Mathf.MoveTowards(
                surgeAmount,
                0f,
                Time.deltaTime * 0.55f
            );

        float naturalResistance =
            0.28f +
            Mathf.Sin(
                fightTime *
                2.2f +
                hookedSpeciesId
            ) *
            0.10f +
            species.Difficulty *
            0.22f +
            surgeAmount *
            0.30f;

        if (reeling)
        {
            fightTension +=
                Time.deltaTime *
                (
                    0.24f +
                    naturalResistance *
                    0.34f
                );

            float safeFactor =
                1f -
                Mathf.InverseLerp(
                    0.74f,
                    0.98f,
                    fightTension
                );

            fightProgress +=
                Time.deltaTime *
                Mathf.Lerp(
                    0.19f,
                    0.12f,
                    species.Difficulty
                ) *
                Mathf.Lerp(
                    0.40f,
                    1f,
                    safeFactor
                );
        }
        else
        {
            fightTension -=
                Time.deltaTime *
                Mathf.Lerp(
                    0.50f,
                    0.36f,
                    species.Difficulty
                );

            fightProgress -=
                Time.deltaTime *
                (
                    0.005f +
                    naturalResistance *
                    0.008f
                );
        }

        fightTension +=
            naturalResistance *
            0.035f *
            Time.deltaTime;

        fightTension =
            Mathf.Clamp01(
                fightTension
            );

        fightProgress =
            Mathf.Clamp01(
                fightProgress
            );

        if (fightTension >= 0.985f)
        {
            lineBreakTimer +=
                Time.deltaTime;
        }
        else
        {
            lineBreakTimer =
                Mathf.Max(
                    0f,
                    lineBreakTimer -
                    Time.deltaTime * 2f
                );
        }

        float surface =
            oceanWater.GetSurfaceHeight(
                castPoint
            );

        Vector3 side =
            playerCamera.transform.right *
            Mathf.Sin(
                fightTime *
                2.8f +
                hookedSpeciesId
            ) *
            (
                0.25f +
                surgeAmount * 0.55f
            );

        bobber.transform.position =
            castPoint +
            side +
            Vector3.up *
            (
                surface -
                castPoint.y -
                0.08f
            );

        hud.SetFightMeters(
            fightTension,
            fightProgress
        );

        hud.SetStatus(
            reeling
                ? "REELING - watch tension!"
                : "RESTING LINE - tension falling..."
        );

        if (lineBreakTimer >= 0.55f)
        {
            FailFishing(
                "SNAP! Too much tension."
            );

            return;
        }

        if (fightProgress >= 1f)
        {
            CatchFish();
        }
    }

    private void CatchFish()
    {
        int newIndex =
            inventory.AddFish(
                hookedSpeciesId,
                hookedWeightKg
            );

        CaughtFishRecord record =
            inventory.Fish[newIndex];

        ResetLine();

        state = FishingState.Idle;

        hud.ShowFightMeters(false);
        hud.ShowCatch(record);

        HoldFish(newIndex);
    }

    private void FailFishing(string message)
    {
        ResetLine();

        state = FishingState.Idle;

        hud.ShowFightMeters(false);
        hud.SetActionLabel("CAST");
        hud.SetStatus(message);
    }

    private void CancelFishing()
    {
        StopAllCoroutines();

        state = FishingState.Idle;

        ResetLine();

        if (hud != null)
        {
            hud.ShowFightMeters(false);
        }
    }

    private bool TryGetCastPoint(
        out Vector3 target)
    {
        target = Vector3.zero;

        Vector3 origin =
            playerCamera.transform.position;

        if (oceanWater.IsPointUnderwater(
                origin))
        {
            hud.SetStatus(
                "Surface before casting."
            );

            return false;
        }

        Vector3 forward =
            playerCamera.transform.forward;

        float baseLevel =
            oceanWater.BaseWaterLevel;

        float distance =
            preferredCastDistance;

        if (forward.y < -0.035f)
        {
            float t =
                (baseLevel - origin.y) /
                forward.y;

            if (t > 0f)
            {
                distance =
                    Mathf.Clamp(
                        t,
                        minimumCastDistance,
                        maximumCastDistance
                    );
            }
        }

        Vector3 horizontal =
            Vector3.ProjectOnPlane(
                forward,
                Vector3.up
            );

        if (horizontal.sqrMagnitude <
            0.001f)
        {
            horizontal =
                transform.forward;
        }

        horizontal.Normalize();

        Vector3 candidate =
            origin +
            horizontal * distance;

        float surface =
            oceanWater.GetSurfaceHeight(
                candidate
            );

        candidate.y =
            surface + 0.06f;

        if (!IsOpenWater(candidate))
            return false;

        target = candidate;
        return true;
    }

    private bool IsOpenWater(
        Vector3 worldPosition)
    {
        if (terrain == null)
            return true;

        TerrainData data =
            terrain.terrainData;

        Vector3 local =
            worldPosition -
            terrain.transform.position;

        if (local.x < 0f ||
            local.z < 0f ||
            local.x > data.size.x ||
            local.z > data.size.z)
        {
            return true;
        }

        float ground =
            terrain.SampleHeight(
                worldPosition
            ) +
            terrain.transform.position.y;

        float water =
            oceanWater.GetSurfaceHeight(
                worldPosition
            );

        return ground <
               water - 0.25f;
    }

    private void SnapBobberToSurface()
    {
        if (bobber == null ||
            !bobber.activeSelf)
        {
            return;
        }

        Vector3 position =
            castPoint;

        position.y =
            oceanWater.GetSurfaceHeight(
                castPoint
            ) +
            0.06f;

        position.y +=
            Mathf.Sin(
                Time.time * 2.5f
            ) *
            0.035f;

        bobber.transform.position =
            position;
    }

    private void UpdateLineAndBobber()
    {
        if (fishingLine == null ||
            !fishingLine.enabled ||
            bobber == null ||
            !bobber.activeSelf)
        {
            return;
        }

        SetLinePositions();
    }

    private void SetLinePositions()
    {
        fishingLine.SetPosition(
            0,
            rodTip.position
        );

        fishingLine.SetPosition(
            1,
            bobber.transform.position
        );
    }

    private void ResetLine()
    {
        if (fishingLine != null)
            fishingLine.enabled = false;

        if (bobber != null)
            bobber.SetActive(false);
    }

    private void CreateRodAndLine()
    {
        rodRoot =
            new GameObject(
                "FishingRodViewModel"
            );

        rodRoot.transform.SetParent(
            playerCamera.transform,
            false
        );

        rodRoot.transform.localPosition =
            new Vector3(
                0.44f,
                -0.43f,
                0.78f
            );

        rodRoot.transform.localRotation =
            Quaternion.Euler(
                67f,
                -6f,
                11f
            );

        Material rodMaterial =
            CreateMaterial(
                new Color(
                    0.25f,
                    0.16f,
                    0.07f
                ),
                0.22f
            );

        Material reelMaterial =
            CreateMaterial(
                new Color(
                    0.16f,
                    0.36f,
                    0.42f
                ),
                0.52f
            );

        GameObject shaft =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        shaft.name = "Rod";
        shaft.transform.SetParent(
            rodRoot.transform,
            false
        );

        shaft.transform.localPosition =
            new Vector3(
                0f,
                0.60f,
                0f
            );

        shaft.transform.localScale =
            new Vector3(
                0.018f,
                0.60f,
                0.018f
            );

        shaft.GetComponent<Renderer>()
            .sharedMaterial =
            rodMaterial;

        Destroy(
            shaft.GetComponent<Collider>()
        );

        GameObject reel =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        reel.name = "Reel";
        reel.transform.SetParent(
            rodRoot.transform,
            false
        );

        reel.transform.localPosition =
            new Vector3(
                0.06f,
                0.18f,
                0f
            );

        reel.transform.localRotation =
            Quaternion.Euler(
                90f,
                0f,
                0f
            );

        reel.transform.localScale =
            new Vector3(
                0.10f,
                0.035f,
                0.10f
            );

        reel.GetComponent<Renderer>()
            .sharedMaterial =
            reelMaterial;

        Destroy(
            reel.GetComponent<Collider>()
        );

        GameObject tip =
            new GameObject("RodTip");

        tip.transform.SetParent(
            rodRoot.transform,
            false
        );

        tip.transform.localPosition =
            new Vector3(
                0f,
                1.22f,
                0f
            );

        rodTip = tip.transform;

        fishingLine =
            rodRoot.AddComponent<LineRenderer>();

        fishingLine.useWorldSpace = true;
        fishingLine.positionCount = 2;
        fishingLine.startWidth = 0.008f;
        fishingLine.endWidth = 0.004f;

        Shader lineShader =
            Resources.Load<Shader>(
                "Fishing/FishingUnlit"
            );

        if (lineShader == null)
        {
            Debug.LogError(
                "Missing Resources/Fishing/FishingUnlit shader."
            );

            enabled = false;
            return;
        }

        Material lineMaterial =
            new Material(lineShader);

        if (lineMaterial.HasProperty("_BaseColor"))
        {
            lineMaterial.SetColor(
                "_BaseColor",
                new Color(
                    0.88f,
                    0.94f,
                    0.96f,
                    0.92f
                )
            );
        }
        else if (lineMaterial.HasProperty("_Color"))
        {
            lineMaterial.color =
                new Color(
                    0.88f,
                    0.94f,
                    0.96f,
                    0.92f
                );
        }

        fishingLine.material =
            lineMaterial;

        fishingLine.enabled = false;

        bobber =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        bobber.name = "FishingBobber";

        bobber.transform.localScale =
            Vector3.one * 0.16f;

        bobber.GetComponent<Renderer>()
            .sharedMaterial =
            CreateMaterial(
                new Color(
                    0.88f,
                    0.10f,
                    0.08f
                ),
                0.35f
            );

        Collider bobberCollider =
            bobber.GetComponent<Collider>();

        if (bobberCollider != null)
            Destroy(bobberCollider);

        bobber.SetActive(false);
    }

    private void CreateHeldFishAnchor()
    {
        GameObject anchor =
            new GameObject(
                "HeldFishAnchor"
            );

        anchor.transform.SetParent(
            playerCamera.transform,
            false
        );

        anchor.transform.localPosition =
            new Vector3(
                0.38f,
                -0.30f,
                0.86f
            );

        heldFishAnchor =
            anchor.transform;
    }

    private void UpdateHeldFishAnimation()
    {
        if (heldFishVisual == null)
            return;

        float time =
            Time.time * 5.6f +
            heldFishFlopOffset;

        heldFishVisual.transform.localPosition =
            new Vector3(
                0f,
                Mathf.Sin(time * 0.7f) *
                0.018f,
                0f
            );

        heldFishVisual.transform.localRotation =
            Quaternion.Euler(
                Mathf.Sin(time * 1.7f) *
                5f,
                92f +
                Mathf.Sin(time) *
                7f,
                8f +
                Mathf.Sin(time * 1.35f) *
                13f
            );

        Transform tail =
            heldFishVisual.transform.Find(
                "Tail"
            );

        if (tail != null)
        {
            tail.localRotation =
                Quaternion.Euler(
                    0f,
                    Mathf.Sin(
                        time * 2.2f
                    ) *
                    34f,
                    0f
                );
        }
    }

    private void ClearHeldFish()
    {
        if (heldFishVisual == null)
            return;

        Destroy(heldFishVisual);
        heldFishVisual = null;
    }

    private Material CreateMaterial(
        Color color,
        float smoothness)
    {
        Shader shader =
            Resources.Load<Shader>(
                "Fishing/FishingLit"
            );

        if (shader == null)
        {
            Debug.LogError(
                "Missing Resources/Fishing/FishingLit shader."
            );

            return null;
        }

        Material material =
            new Material(shader);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color
            );
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor(
                "_Color",
                color
            );
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat(
                "_Smoothness",
                smoothness
            );
        }

        return material;
    }
}
