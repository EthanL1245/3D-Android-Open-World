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

    private enum FishTemperament
    {
        Calm,
        Irritated,
        Angry
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

    [Header("Line + Valid Water")]
    [SerializeField] private float maximumLineDistance = 36f;
    [SerializeField] private float minimumFishingDepth = 0.85f;
    [SerializeField] private float deepWaterSafetyRadius = 0.75f;

    [Header("Bites")]
    [SerializeField] private float minimumBiteDelay = 2.5f;
    [SerializeField] private float maximumBiteDelay = 6.0f;
    [SerializeField] private float hookWindow = 1.35f;

    private FishingHUD hud;
    private FishingState state;

    private GameObject rodRoot;
    private Transform rodTip;
    private FishingRodView rodView;
    private LineRenderer fishingLine;
    private GameObject bobber;

    private GameObject bobberIndicatorRoot;
    private RectTransform bobberIndicatorRect;
    private Image bobberIndicatorBackground;
    private Text bobberIndicatorText;

    private Transform heldFishAnchor;
    private GameObject heldFishVisual;
    private float heldFishFlopOffset;

    private bool rodEquipped = true;

    private Vector3 castPoint;
    private float stateTimer;
    private float fightTension;
    private float fishHealth;
    private float lineBreakTimer;
    private float fightTime;
    private float surgeAmount;
    private float surgeTimer;
    private float temperamentTimer;
    private bool fishUnconscious;
    private bool fishOnShore;

    private const float CatchDistance = 1.15f;

    private int hookedSpeciesId;
    private float hookedWeightKg;
    private FishTemperament hookedTemperament;

    private Vector3 fightTravelDirection;
    private float fightMovePhase;

    private static readonly float[] FightMoveAngles =
    {
        0f,
        32f,
        -32f,
        64f,
        -64f
    };

    private Terrain terrain;

    private const string YellowfinPreviewGrantKey =
        "OpenWorld.YellowfinTunaPreviewGrant.v2";

    private const string GoatfishPreviewGrantKey =
        "OpenWorld.GoatfishPreviewGrant.v1";

    private const string RedSnapperPreviewGrantKey =
        "OpenWorld.RedSnapperPreviewGrant.v1";

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

        minimumFishingDepth = 0.85f;
        deepWaterSafetyRadius = 0.75f;

        GrantYellowfinPreviewOnce();
        GrantGoatfishPreviewOnce();
        GrantRedSnapperPreviewOnce();

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

    private void OnDisable()
    {
        CancelFishing();
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

        if (state != FishingState.Idle &&
            state != FishingState.Casting &&
            playerCamera != null &&
            oceanWater.IsPointUnderwater(
                playerCamera.transform.position
            ))
        {
            FailFishing(
                "SNAP! The line broke when you went underwater."
            );

            return;
        }

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

        if (rodView != null)
        {
            bool fighting =
                rodEquipped &&
                state ==
                    FishingState.Fighting &&
                bobber != null &&
                bobber.activeSelf;

            if (fighting)
            {
                rodView.SetLinePull(
                    bobber.transform.position,
                    fightTension,
                    Time.deltaTime
                );
            }
            else
            {
                rodView.RelaxLinePull(
                    Time.deltaTime
                );
            }

            rodView.TickReel(
                fighting &&
                action != null &&
                action.IsHeld,
                Time.deltaTime
            );
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
                FishCatalog.GetVisualScale(
                    record.speciesId,
                    record.weightKg
                )
            );

        HeroFishAnimator heroAnimator =
            heldFishVisual.GetComponent<HeroFishAnimator>();

        if (heroAnimator != null)
        {
            heroAnimator.SetHeld(true);
        }

        YellowfinTunaPresentation tunaPresentation =
            heldFishVisual.GetComponent<YellowfinTunaPresentation>();

        if (tunaPresentation != null)
        {
            tunaPresentation.SetHeld(true);
        }

        GoatfishPresentation goatfishPresentation =
            heldFishVisual.GetComponent<GoatfishPresentation>();

        if (goatfishPresentation != null)
        {
            goatfishPresentation.SetHeld(true);
        }

        RedSnapperPresentation redSnapperPresentation =
            heldFishVisual.GetComponent<RedSnapperPresentation>();

        if (redSnapperPresentation != null)
        {
            redSnapperPresentation.SetHeld(true);
        }

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

    private void GrantYellowfinPreviewOnce()
    {
        if (inventory == null)
            return;

        if (PlayerPrefs.GetInt(
                YellowfinPreviewGrantKey,
                0) != 0)
        {
            return;
        }

        inventory.AddFish(
            FishCatalog.YellowfinTunaId,
            14.50f
        );

        PlayerPrefs.SetInt(
            YellowfinPreviewGrantKey,
            1
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Granted one temporary 14.50 kg Yellowfin Tuna preview catch. This one-time development grant will be removed after the imported fish is approved."
        );
    }

    private void GrantGoatfishPreviewOnce()
    {
        if (inventory == null)
            return;

        if (PlayerPrefs.GetInt(
                GoatfishPreviewGrantKey,
                0) != 0)
        {
            return;
        }

        inventory.AddFish(
            FishCatalog.YellowGoatfishId,
            1.25f
        );

        inventory.AddFish(
            FishCatalog.BlackSpotGoatfishId,
            1.10f
        );

        PlayerPrefs.SetInt(
            GoatfishPreviewGrantKey,
            1
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Granted one temporary Yellow Goatfish and one temporary Black Spot Goatfish for animation testing."
        );
    }

    private void GrantRedSnapperPreviewOnce()
    {
        if (inventory == null)
            return;

        if (PlayerPrefs.GetInt(
                RedSnapperPreviewGrantKey,
                0) != 0)
        {
            return;
        }

        inventory.AddFish(
            FishCatalog.RedSnapperId,
            1.75f
        );

        PlayerPrefs.SetInt(
            RedSnapperPreviewGrantKey,
            1
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Granted one temporary 1.75 kg Red Snapper for imported-model testing."
        );
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

        Vector3 start = rodTip.position;
        bool released = rodView == null;
        const float releaseProgress = 0.38f;
        float duration = Mathf.Max(0.1f, castDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            if (rodView != null) rodView.SetCastPose(t);
            if (!released && t < releaseProgress)
            {
                bobber.transform.position = rodTip.position;
                SetLinePositions();
                yield return null;
                continue;
            }
            if (!released)
            {
                start = rodTip.position;
                released = true;
            }
            float flightProgress = rodView != null
                ? Mathf.InverseLerp(releaseProgress, 1f, t)
                : t;

            Vector3 point =
                Vector3.Lerp(
                    start,
                    target,
                    flightProgress
                );

            point.y +=
                Mathf.Sin(
                    flightProgress * Mathf.PI
                ) *
                4.0f;

            bobber.transform.position =
                point;

            SetLinePositions();

            yield return null;
        }

        if (rodView != null) rodView.SetCastPose(1f);
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

            hookedTemperament =
                RollTemperament(
                    hookedSpeciesId,
                    hookedWeightKg
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

        fightTension = 0.24f;
        fishHealth = 1f;
        lineBreakTimer = 0f;
        fightTime = 0f;
        surgeAmount = 0f;
        fishUnconscious = false;
        fishOnShore = false;

        temperamentTimer =
            Random.Range(
                1.6f,
                3.4f
            );

        surgeTimer =
            Random.Range(
                1.2f,
                2.3f
            );

        fightMovePhase =
            Random.Range(
                0f,
                Mathf.PI * 2f
            );

        Vector3 away =
            bobber.transform.position -
            transform.position;

        away.y = 0f;

        if (away.sqrMagnitude <
            0.001f)
        {
            away =
                Vector3.ProjectOnPlane(
                    playerCamera.transform.forward,
                    Vector3.up
                );
        }

        fightTravelDirection =
            away.sqrMagnitude >
                0.001f
                ? away.normalized
                : Vector3.forward;

        hud.SetActionLabel("REEL");
        hud.SetStatus(
            GetTemperamentName() +
            " fish - wear down its health."
        );
        hud.ShowFightMeters(true);
        hud.SetFightMeters(
            fightTension,
            fishHealth
        );
    }

    private void UpdateFight(bool reeling)
    {
        FishSpeciesDefinition species =
            FishCatalog.Get(
                hookedSpeciesId
            );

        float effectiveDifficulty =
            GetEffectiveFightDifficulty(
                species,
                hookedWeightKg
            );

        fightTime += Time.deltaTime;

        if (!fishUnconscious)
        {
            temperamentTimer -=
                Time.deltaTime;

            if (temperamentTimer <= 0f)
            {
                hookedTemperament =
                    RollDifferentTemperament(
                        hookedSpeciesId,
                        hookedWeightKg,
                        hookedTemperament
                    );

                temperamentTimer =
                    Random.Range(
                        1.45f,
                        3.65f
                    );
            }

            surgeTimer -=
                Time.deltaTime;

            float temperamentTension =
                GetTemperamentTensionMultiplier();

            float temperamentResistance =
                GetTemperamentResistanceMultiplier();

            if (surgeTimer <= 0f)
            {
                surgeAmount =
                    Random.Range(
                        0.42f,
                        0.88f
                    ) *
                    Mathf.Lerp(
                        0.82f,
                        1.34f,
                        effectiveDifficulty
                    ) *
                    temperamentResistance;

                surgeTimer =
                    Random.Range(
                        1.5f,
                        2.9f
                    );
            }

            surgeAmount =
                Mathf.MoveTowards(
                    surgeAmount,
                    0f,
                    Time.deltaTime * 0.55f
                );

            float naturalResistance =
                (
                    0.23f +
                    Mathf.Sin(
                        fightTime *
                        2.2f +
                        hookedSpeciesId
                    ) *
                    0.10f +
                    effectiveDifficulty *
                    0.28f +
                    surgeAmount *
                    0.30f
                ) *
                temperamentResistance;

            if (reeling)
            {
                fightTension +=
                    Time.deltaTime *
                    (
                        0.18f +
                        naturalResistance *
                        0.34f
                    ) *
                    temperamentTension;

                float healthRate =
                    Mathf.Lerp(
                        0.18f,
                        0.085f,
                        effectiveDifficulty
                    );

                float moodHealthFactor =
                    hookedTemperament ==
                        FishTemperament.Calm
                        ? 1.12f
                        : hookedTemperament ==
                            FishTemperament.Angry
                            ? 0.78f
                            : 0.96f;

                fishHealth -=
                    Time.deltaTime *
                    healthRate *
                    moodHealthFactor;
            }
            else
            {
                fightTension -=
                    Time.deltaTime *
                    Mathf.Lerp(
                        0.52f,
                        0.32f,
                        effectiveDifficulty
                    );
            }

            fightTension +=
                naturalResistance *
                0.032f *
                Time.deltaTime;

            fishHealth =
                Mathf.Clamp01(
                    fishHealth
                );

            if (fishHealth <= 0f)
            {
                fishHealth = 0f;
                fishUnconscious = true;
                surgeAmount = 0f;
                lineBreakTimer = 0f;
                fightTension =
                    Mathf.Min(
                        fightTension,
                        0.18f
                    );

                hud.SetStatus(
                    "UNCONSCIOUS - reel it in."
                );
            }
            else
            {
                UpdateFightBobber(
                    effectiveDifficulty
                );
            }
        }
        else
        {
            surgeAmount = 0f;

            fightTension =
                Mathf.MoveTowards(
                    fightTension,
                    0.05f,
                    Time.deltaTime * 0.80f
                );

            if (reeling)
            {
                UpdateUnconsciousBobber();
            }
            else
            {
                SnapCurrentBobberToSurface();
            }
        }

        fightTension =
            Mathf.Clamp01(
                fightTension
            );

        float lineDistance =
            GetCurrentLineDistance();

        if (lineDistance >=
            maximumLineDistance)
        {
            fightTension = 1f;

            hud.SetFightMeters(
                fightTension,
                fishHealth
            );

            UpdateBobberIndicator();

            FailFishing(
                "SNAP! Maximum line distance reached."
            );

            return;
        }

        if (!fishUnconscious &&
            fightTension >= 0.985f)
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

        hud.SetFightMeters(
            fightTension,
            fishHealth
        );

        if (!fishUnconscious)
        {
            hud.SetStatus(
                reeling
                    ? GetTemperamentName() +
                      " - reducing fish health."
                    : GetTemperamentName() +
                      " - resting the line."
            );
        }
        else
        {
            hud.SetStatus(
                reeling
                    ? "UNCONSCIOUS - pulling it toward you."
                    : "UNCONSCIOUS - hold REEL to retrieve it."
            );
        }

        if (lineBreakTimer >= 0.55f)
        {
            FailFishing(
                "SNAP! Too much tension."
            );

            return;
        }

        if (fishUnconscious &&
            fishOnShore)
        {
            CatchFish(
                true
            );

            return;
        }

        if (fishUnconscious &&
            GetRemainingCatchDistance() <=
                0.01f)
        {
            CatchFish(
                false
            );
        }
    }

    private void CatchFish(
        bool shoreCatch)
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
        hud.ShowCatch(
            record,
            shoreCatch
                ? "LANDED ON SHORE!"
                : "CAUGHT!"
        );

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
        if (rodView != null) rodView.ResetMotion();
        if (hud != null && hud.ActionInput != null) hud.ActionInput.ResetInput();

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

        if (!IsValidFishingWater(
                candidate))
        {
            hud.SetStatus(
                "Cast into deeper open water."
            );

            return false;
        }

        target = candidate;
        return true;
    }

    private bool IsValidFishingWater(
        Vector3 worldPosition)
    {
        if (!HasFishingDepth(
                worldPosition,
                minimumFishingDepth))
        {
            return false;
        }

        float radius =
            Mathf.Max(
                0f,
                deepWaterSafetyRadius
            );

        if (radius <= 0f)
            return true;

        Vector3 right =
            Vector3.right *
            radius;

        Vector3 forward =
            Vector3.forward *
            radius;

        return
            HasFishingDepth(
                worldPosition + right,
                minimumFishingDepth * 0.85f
            ) &&
            HasFishingDepth(
                worldPosition - right,
                minimumFishingDepth * 0.85f
            ) &&
            HasFishingDepth(
                worldPosition + forward,
                minimumFishingDepth * 0.85f
            ) &&
            HasFishingDepth(
                worldPosition - forward,
                minimumFishingDepth * 0.85f
            );
    }

    private bool HasFishingDepth(
        Vector3 worldPosition,
        float requiredDepth)
    {
        if (terrain == null)
            return true;

        TerrainData data =
            terrain.terrainData;

        Vector3 local =
            worldPosition -
            terrain.transform.position;

        // Outside the finite terrain there is no shoreline/ground mesh under
        // the infinite ocean, so it is valid deep water.
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

        return
            water -
            ground >=
            Mathf.Max(
                0.25f,
                requiredDepth
            );
    }

    private float GetEffectiveFightDifficulty(
        FishSpeciesDefinition species,
        float weightKg)
    {
        float sizeDifficulty =
            Mathf.InverseLerp(
                species.MinWeightKg,
                species.MaxWeightKg,
                weightKg
            );

        return
            Mathf.Clamp01(
                species.Difficulty *
                0.74f +
                sizeDifficulty *
                0.26f
            );
    }

    private FishTemperament RollTemperament(
        int speciesId,
        float weightKg)
    {
        GetTemperamentWeights(
            speciesId,
            weightKg,
            out float calmWeight,
            out float irritatedWeight,
            out float angryWeight
        );

        float total =
            calmWeight +
            irritatedWeight +
            angryWeight;

        float roll =
            Random.value *
            total;

        if (roll <
            calmWeight)
        {
            return
                FishTemperament.Calm;
        }

        roll -=
            calmWeight;

        if (roll <
            irritatedWeight)
        {
            return
                FishTemperament.Irritated;
        }

        return
            FishTemperament.Angry;
    }

    private FishTemperament RollDifferentTemperament(
        int speciesId,
        float weightKg,
        FishTemperament current)
    {
        for (int i = 0;
             i < 5;
             i++)
        {
            FishTemperament next =
                RollTemperament(
                    speciesId,
                    weightKg
                );

            if (next != current)
            {
                return next;
            }
        }

        // Guarantee a visible mood change even after repeated weighted rolls.
        if (current ==
            FishTemperament.Calm)
        {
            return
                FishTemperament.Irritated;
        }

        if (current ==
            FishTemperament.Angry)
        {
            return
                FishTemperament.Irritated;
        }

        return
            Random.value < 0.5f
                ? FishTemperament.Calm
                : FishTemperament.Angry;
    }

    private void GetTemperamentWeights(
        int speciesId,
        float weightKg,
        out float calm,
        out float irritated,
        out float angry)
    {
        // Species signatures. These are intentionally different even before
        // size is considered, so each fish family has a recognizable fight.
        switch (speciesId)
        {
            case FishCatalog.RedSnapperId:
                calm = 0.22f;
                irritated = 0.48f;
                angry = 0.30f;
                break;

            case FishCatalog.YellowfinTunaId:
                calm = 0.08f;
                irritated = 0.28f;
                angry = 0.64f;
                break;

            case FishCatalog.YellowGoatfishId:
                calm = 0.64f;
                irritated = 0.33f;
                angry = 0.03f;
                break;

            case FishCatalog.BlackSpotGoatfishId:
                calm = 0.68f;
                irritated = 0.29f;
                angry = 0.03f;
                break;

            case 4: // Young Tuna
                calm = 0.16f;
                irritated = 0.38f;
                angry = 0.46f;
                break;

            case 3: // Yellowtail
                calm = 0.28f;
                irritated = 0.45f;
                angry = 0.27f;
                break;

            case 2: // Sea Bass
                calm = 0.42f;
                irritated = 0.43f;
                angry = 0.15f;
                break;

            default: // Blue Mackerel / fallback
                calm = 0.70f;
                irritated = 0.27f;
                angry = 0.03f;
                break;
        }

        FishSpeciesDefinition species =
            FishCatalog.Get(
                speciesId
            );

        float size =
            Mathf.InverseLerp(
                species.MinWeightKg,
                species.MaxWeightKg,
                weightKg
            );

        // Large individuals spend more of the fight angry, but every species
        // keeps a non-zero chance to switch through all three moods.
        float angryShift =
            size *
            Mathf.Lerp(
                0.08f,
                0.24f,
                species.Difficulty
            );

        calm =
            Mathf.Max(
                0.05f,
                calm -
                angryShift *
                0.72f
            );

        irritated =
            Mathf.Max(
                0.08f,
                irritated -
                angryShift *
                0.28f
            );

        angry =
            Mathf.Max(
                0.02f,
                angry +
                angryShift
            );
    }

    private float GetTemperamentTensionMultiplier()
    {
        switch (hookedTemperament)
        {
            case FishTemperament.Calm:
                return 0.55f;

            case FishTemperament.Angry:
                return 2.0f;

            default:
                return 1f;
        }
    }

    private float GetTemperamentResistanceMultiplier()
    {
        switch (hookedTemperament)
        {
            case FishTemperament.Calm:
                return 0.82f;

            case FishTemperament.Angry:
                return 1.28f;

            default:
                return 1f;
        }
    }

    private string GetTemperamentName()
    {
        if (fishUnconscious)
            return "UNCONSCIOUS";

        switch (hookedTemperament)
        {
            case FishTemperament.Calm:
                return "CALM";

            case FishTemperament.Angry:
                return "ANGRY";

            default:
                return "IRRITATED";
        }
    }

    private Color GetTemperamentColor()
    {
        if (fishUnconscious)
        {
            return
                new Color(
                    0.34f,
                    0.56f,
                    0.72f,
                    0.94f
                );
        }

        switch (hookedTemperament)
        {
            case FishTemperament.Calm:
                return
                    new Color(
                        0.18f,
                        0.78f,
                        0.30f,
                        0.92f
                    );

            case FishTemperament.Angry:
                return
                    new Color(
                        0.94f,
                        0.16f,
                        0.10f,
                        0.94f
                    );

            default:
                return
                    new Color(
                        0.96f,
                        0.72f,
                        0.10f,
                        0.94f
                    );
        }
    }

    private void UpdateFightBobber(
        float effectiveDifficulty)
    {
        if (bobber == null ||
            !bobber.activeSelf)
        {
            return;
        }

        Vector3 current =
            bobber.transform.position;

        Vector3 away =
            current -
            transform.position;

        away.y = 0f;

        if (away.sqrMagnitude <
            0.001f)
        {
            away =
                fightTravelDirection;
        }

        if (away.sqrMagnitude <
            0.001f)
        {
            away =
                Vector3.ProjectOnPlane(
                    playerCamera.transform.forward,
                    Vector3.up
                );
        }

        away =
            away.sqrMagnitude >
                0.001f
                ? away.normalized
                : Vector3.forward;

        Vector3 side =
            Vector3.Cross(
                Vector3.up,
                away
            ).normalized;

        float weave =
            Mathf.Sin(
                fightTime *
                Mathf.Lerp(
                    0.55f,
                    1.05f,
                    effectiveDifficulty
                ) +
                fightMovePhase
            );

        float surgeWeave =
            Mathf.Sin(
                fightTime * 1.7f +
                fightMovePhase * 0.63f
            ) *
            surgeAmount *
            0.22f;

        Vector3 desiredDirection =
            (
                away +
                side *
                (
                    weave * 0.58f +
                    surgeWeave
                )
            ).normalized;

        float temperamentPull =
            hookedTemperament ==
                FishTemperament.Angry
                ? 1.35f
                : hookedTemperament ==
                    FishTemperament.Calm
                    ? 0.62f
                    : 0.92f;

        float pullSpeed =
            Mathf.Lerp(
                0.38f,
                1.05f,
                effectiveDifficulty
            ) *
            temperamentPull *
            (
                1f +
                surgeAmount * 0.28f
            );

        if (TryMoveBobberInValidWater(
                current,
                desiredDirection,
                away,
                pullSpeed *
                Time.deltaTime,
                out Vector3 next))
        {
            bobber.transform.position =
                next;

            fightTravelDirection =
                desiredDirection;
        }
        else
        {
            current.y =
                oceanWater.GetSurfaceHeight(
                    current
                ) -
                0.08f;

            bobber.transform.position =
                current;
        }

        castPoint =
            bobber.transform.position;
    }

    private bool TryMoveBobberInValidWater(
        Vector3 current,
        Vector3 desiredDirection,
        Vector3 away,
        float distance,
        out Vector3 next)
    {
        for (int i = 0;
             i < FightMoveAngles.Length;
             i++)
        {
            Vector3 direction =
                Quaternion.AngleAxis(
                    FightMoveAngles[i],
                    Vector3.up
                ) *
                desiredDirection;

            direction.y = 0f;

            if (direction.sqrMagnitude <
                0.001f)
            {
                continue;
            }

            direction.Normalize();

            // Never choose a route that sends the hooked fish back through
            // the player. Sideways is allowed, but the overall motion must
            // continue away from the character.
            if (Vector3.Dot(
                    direction,
                    away) <
                0.12f)
            {
                continue;
            }

            Vector3 candidate =
                current +
                direction *
                Mathf.Max(
                    0f,
                    distance
                );

            candidate.y =
                oceanWater.GetSurfaceHeight(
                    candidate
                ) -
                0.08f;

            if (!IsValidFishingWater(
                    candidate))
            {
                continue;
            }

            next = candidate;
            return true;
        }

        next = current;
        return false;
    }

    private float GetCurrentLineDistance()
    {
        if (rodTip == null ||
            bobber == null)
        {
            return 0f;
        }

        return
            Vector3.Distance(
                rodTip.position,
                bobber.transform.position
            );
    }

    private float GetRemainingCatchDistance()
    {
        if (bobber == null)
            return 0f;

        Vector3 delta =
            bobber.transform.position -
            transform.position;

        delta.y = 0f;

        return
            Mathf.Max(
                0f,
                delta.magnitude -
                CatchDistance
            );
    }

    private void UpdateUnconsciousBobber()
    {
        if (bobber == null ||
            !bobber.activeSelf)
        {
            return;
        }

        Vector3 current =
            bobber.transform.position;

        Vector3 toPlayer =
            transform.position -
            current;

        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        Vector3 direction =
            toPlayer.normalized;

        Vector3 candidate =
            current +
            direction *
            (
                3.8f *
                Time.deltaTime
            );

        if (TryGetTerrainWaterDepth(
                candidate,
                out float ground,
                out float water,
                out float depth))
        {
            if (depth <= 0.08f)
            {
                candidate.y =
                    ground +
                    0.07f;

                bobber.transform.position =
                    candidate;

                castPoint =
                    candidate;

                fishOnShore = true;

                hud.SetStatus(
                    "UNCONSCIOUS - landed on shore!"
                );

                return;
            }

            candidate.y =
                water +
                0.03f;
        }
        else
        {
            candidate.y =
                oceanWater.GetSurfaceHeight(
                    candidate
                ) +
                0.03f;
        }

        bobber.transform.position =
            candidate;

        castPoint =
            candidate;
    }

    private void SnapCurrentBobberToSurface()
    {
        if (bobber == null ||
            !bobber.activeSelf ||
            fishOnShore)
        {
            return;
        }

        Vector3 position =
            bobber.transform.position;

        position.y =
            oceanWater.GetSurfaceHeight(
                position
            ) +
            0.03f;

        bobber.transform.position =
            position;

        castPoint =
            position;
    }

    private bool TryGetTerrainWaterDepth(
        Vector3 worldPosition,
        out float ground,
        out float water,
        out float depth)
    {
        water =
            oceanWater.GetSurfaceHeight(
                worldPosition
            );

        ground =
            water -
            1000f;

        depth = 1000f;

        if (terrain == null)
            return false;

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
            return false;
        }

        ground =
            terrain.SampleHeight(
                worldPosition
            ) +
            terrain.transform.position.y;

        depth =
            water -
            ground;

        return true;
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
            if (bobberIndicatorRoot != null)
            {
                bobberIndicatorRoot.SetActive(
                    false
                );
            }

            return;
        }

        SetLinePositions();
        UpdateBobberIndicator();
    }

    private void CreateBobberIndicator()
    {
        if (bobberIndicatorRoot != null)
        {
            Destroy(
                bobberIndicatorRoot
            );
        }

        bobberIndicatorRoot =
            new GameObject(
                "BobberStatusIndicator",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        bobberIndicatorRoot.transform.SetParent(
            gameplayCanvas.transform,
            false
        );

        bobberIndicatorRect =
            bobberIndicatorRoot
                .GetComponent<RectTransform>();

        bobberIndicatorRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        bobberIndicatorRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        bobberIndicatorRect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );

        // Fixed pixel size: distance from the bobber never changes readability.
        bobberIndicatorRect.sizeDelta =
            new Vector2(
                360f,
                92f
            );

        bobberIndicatorBackground =
            bobberIndicatorRoot
                .GetComponent<Image>();

        bobberIndicatorBackground.color =
            new Color(
                0.08f,
                0.13f,
                0.16f,
                0.94f
            );

        bobberIndicatorBackground.raycastTarget =
            false;

        GameObject textObject =
            new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );

        textObject.transform.SetParent(
            bobberIndicatorRoot.transform,
            false
        );

        bobberIndicatorText =
            textObject.GetComponent<Text>();

        bobberIndicatorText.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        bobberIndicatorText.fontSize = 24;
        bobberIndicatorText.alignment =
            TextAnchor.MiddleCenter;

        bobberIndicatorText.color =
            Color.white;

        bobberIndicatorText.raycastTarget =
            false;

        RectTransform textRect =
            bobberIndicatorText
                .rectTransform;

        textRect.anchorMin =
            Vector2.zero;

        textRect.anchorMax =
            Vector2.one;

        textRect.offsetMin =
            new Vector2(
                10f,
                6f
            );

        textRect.offsetMax =
            new Vector2(
                -10f,
                -6f
            );

        bobberIndicatorRoot.SetActive(
            false
        );
    }

    private void UpdateBobberIndicator()
    {
        if (bobberIndicatorRoot == null ||
            bobberIndicatorRect == null ||
            bobberIndicatorText == null ||
            bobberIndicatorBackground == null ||
            bobber == null ||
            !bobber.activeSelf ||
            state ==
                FishingState.Casting ||
            state ==
                FishingState.Idle)
        {
            if (bobberIndicatorRoot != null)
            {
                bobberIndicatorRoot.SetActive(
                    false
                );
            }

            return;
        }

        Vector3 screenPoint =
            playerCamera.WorldToScreenPoint(
                bobber.transform.position +
                Vector3.up * 0.52f
            );

        if (screenPoint.z <= 0f)
        {
            bobberIndicatorRoot.SetActive(
                false
            );

            return;
        }

        RectTransform canvasRect =
            gameplayCanvas.transform
                as RectTransform;

        Camera uiCamera =
            gameplayCanvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : gameplayCanvas.worldCamera;

        if (canvasRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                uiCamera,
                out Vector2 localPoint))
        {
            bobberIndicatorRoot.SetActive(
                false
            );

            return;
        }

        bobberIndicatorRoot.SetActive(
            true
        );

        bobberIndicatorRect.anchoredPosition =
            localPoint +
            new Vector2(
                0f,
                42f
            );

        float lineDistance =
            GetCurrentLineDistance();

        float catchDistance =
            GetRemainingCatchDistance();

        string distanceText =
            "DIST " +
            catchDistance.ToString("0.0") +
            " m   LINE " +
            lineDistance.ToString("0.0") +
            "/" +
            maximumLineDistance.ToString("0") +
            " m";

        if (state ==
                FishingState.Bite ||
            state ==
                FishingState.Fighting)
        {
            bobberIndicatorBackground.color =
                GetTemperamentColor();

            bobberIndicatorText.text =
                GetTemperamentName() +
                "\n" +
                distanceText;
        }
        else
        {
            bobberIndicatorBackground.color =
                new Color(
                    0.08f,
                    0.16f,
                    0.20f,
                    0.94f
                );

            bobberIndicatorText.text =
                distanceText;
        }
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

        if (bobberIndicatorRoot != null)
            bobberIndicatorRoot.SetActive(false);

        if (rodView != null)
        {
            rodView.RelaxLinePull(
                Time.deltaTime
            );
        }
    }

    // Keep old scenes playable before the one-click asset installation has run.
    private void CreatePlaceholderRod()
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

    }

    private void CreateRodAndLine()
    {
        GameObject prefab = Resources.Load<GameObject>("Fishing/FishingRodReel");
        FishingRodView prefabView = prefab != null ? prefab.GetComponent<FishingRodView>() : null;
        if (prefabView != null && prefabView.RodTip != null)
        {
            rodRoot = Instantiate(prefab, playerCamera.transform, false);
            rodRoot.name = "FishingRodViewModel";
            rodRoot.transform.localPosition = new Vector3(0.28f, -0.23f, 0.48f);
            rodRoot.transform.localRotation = Quaternion.Euler(67f, -6f, 11f);
            rodView = rodRoot.GetComponent<FishingRodView>();
            rodTip = rodView.RodTip;
            rodView.InitializePose();
        }
        else
        {
            Debug.LogWarning("Rod/reel prefab not installed. Run Tools > Open World > Install Fishing Rod + Animated Reel (One Click).");
            CreatePlaceholderRod();
        }

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

        CreateBobberIndicator();
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

