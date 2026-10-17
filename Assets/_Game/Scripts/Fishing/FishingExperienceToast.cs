using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A non-interactive, top-right XP toast under MENU / TRAVEL. It animates the
/// actual saved catch-XP award through every crossed level threshold, including
/// any XP left over after leveling up. No separate progression is maintained.
/// </summary>
[DisallowMultipleComponent]
public sealed class FishingExperienceToast : MonoBehaviour
{
    private const float Width = 690f;
    private const float Height = 138f;
    private const float RightMargin = 36f;
    // MENU / TRAVEL is 92 units high and 32 units below the screen top.
    private const float TopOffset = 140f;
    private const float SlideSeconds = .38f;
    private const float HoldSeconds = 1.25f;

    private static readonly Color Cyan = new Color(.02f, .94f, 1f, 1f);
    private static readonly Color Gold = new Color(1f, .84f, .38f, 1f);

    private struct Award
    {
        public int amount, startLevel, endLevel;
        public double startXp, endXp;
    }

    private enum Phase { Hidden, Entering, Filling, Holding, Exiting }

    private readonly Queue<Award> waiting = new Queue<Award>();
    private ShopProgress progress;
    private RectTransform popup;
    private RectTransform meterFill;
    private RectTransform badge;
    private Text levelInBadge, levelTitle, xpValue, earnedValue;
    private Award current;
    private Phase phase = Phase.Hidden;
    private float elapsed;
    private float fillSeconds;
    private float badgePulse;
    private int drawnLevel = -1;

    private void Awake()
    {
        progress = GetComponent<ShopProgress>();
        if (progress != null) progress.FishingXpAwarded += OnFishingXpAwarded;
    }

    private void OnDestroy()
    {
        if (progress != null) progress.FishingXpAwarded -= OnFishingXpAwarded;
    }

    private void OnFishingXpAwarded(int amount, int startLevel, double startXp, int endLevel, double endXp)
    {
        if (amount <= 0 || (startLevel == endLevel && Math.Abs(startXp - endXp) < .001d)) return;
        waiting.Enqueue(new Award
        {
            amount = amount,
            startLevel = startLevel,
            startXp = startXp,
            endLevel = endLevel,
            endXp = endXp
        });
    }

    private void Update()
    {
        if (popup == null)
        {
            if (waiting.Count == 0 || !BuildIfReady()) return;
        }

        float dt = Mathf.Min(.1f, Time.unscaledDeltaTime);
        if (phase == Phase.Hidden && waiting.Count > 0) NextAward();

        switch (phase)
        {
            case Phase.Entering:
                elapsed += dt;
                SetSlide(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SlideSeconds)));
                if (elapsed >= SlideSeconds) { phase = Phase.Filling; elapsed = 0f; }
                break;

            case Phase.Filling:
                elapsed += dt;
                float fraction = Mathf.Clamp01(elapsed / fillSeconds);
                ShowEarned(current.amount * Mathf.SmoothStep(0f, 1f, fraction));
                if (fraction >= 1f)
                {
                    // Use the saved final snapshot to avoid accumulated float error.
                    ShowLevel(current.endLevel, current.endXp);
                    phase = Phase.Holding;
                    elapsed = 0f;
                }
                break;

            case Phase.Holding:
                elapsed += dt;
                if (elapsed >= HoldSeconds) { phase = Phase.Exiting; elapsed = 0f; }
                break;

            case Phase.Exiting:
                elapsed += dt;
                SetSlide(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SlideSeconds)));
                if (elapsed >= SlideSeconds)
                {
                    phase = Phase.Hidden;
                    popup.gameObject.SetActive(false);
                    if (waiting.Count > 0) NextAward();
                }
                break;
        }

        if (badge != null)
        {
            badgePulse = Mathf.MoveTowards(badgePulse, 0f, dt * 2.5f);
            badge.localScale = Vector3.one * (1f + .09f * badgePulse);
        }
    }

    private void NextAward()
    {
        current = waiting.Dequeue();
        popup.gameObject.SetActive(true);
        popup.SetAsLastSibling(); // Above the normal HUD, below any subsequently opened modal.
        drawnLevel = -1;
        badgePulse = 0f;
        badge.localScale = Vector3.one;
        earnedValue.text = "+" + current.amount.ToString("N0") + " XP";
        earnedValue.color = current.endLevel > current.startLevel ? Gold : Cyan;
        ShowLevel(current.startLevel, current.startXp);
        // Each crossed level needs enough time to be recognizable.
        fillSeconds = Mathf.Min(5f, 1.3f + Mathf.Max(0, current.endLevel - current.startLevel) * .50f);
        elapsed = 0f;
        phase = Phase.Entering;
        SetSlide(0f);
    }

    private void ShowEarned(double awardedSoFar)
    {
        int level = current.startLevel;
        double xp = current.startXp + awardedSoFar;
        // Process level boundaries individually, rather than interpolating two
        // percentage values (which would incorrectly run backwards on level-up).
        while (level < current.endLevel && level < AnglerProgression.MaxLevel)
        {
            double required = AnglerProgression.RequiredXp(level);
            if (xp + .00001d < required) break;
            xp -= required;
            level++;
        }
        if (level >= AnglerProgression.MaxLevel) xp = 0d;
        ShowLevel(level, xp);
    }

    private void ShowLevel(int level, double xp)
    {
        bool max = level >= AnglerProgression.MaxLevel;
        if (drawnLevel != level)
        {
            if (drawnLevel >= 0 && level > drawnLevel) badgePulse = 1f;
            drawnLevel = level;
            levelInBadge.text = "LV\n" + level;
            levelTitle.text = max ? "MAX LEVEL" : "LEVEL " + level;
        }
        double required = AnglerProgression.RequiredXp(level);
        double fraction = max ? 1d : Math.Max(0d, Math.Min(1d, xp / required));
        xpValue.text = max ? "MAX LEVEL" : xp.ToString("N0") + " / " + required.ToString("N0") + " XP";
        meterFill.anchorMax = new Vector2((float)fraction, 1f);
        meterFill.offsetMin = Vector2.zero;
        meterFill.offsetMax = Vector2.zero;
    }

    private void SetSlide(float visible)
    {
        if (popup == null) return;
        float outside = popup.rect.width + 45f;
        popup.anchoredPosition = new Vector2(
            Mathf.Lerp(outside, -RightMargin, visible), -TopOffset);
    }

    private bool BuildIfReady()
    {
        // ShopWorldHUD creates TideglassHUD in Start, so wait for its real canvas.
        ShopWorldHUD hud = FindFirstObjectByType<ShopWorldHUD>();
        if (hud == null) return false;
        Transform root = hud.transform.Find("TideglassHUD");
        if (root == null || !(root is RectTransform)) return false;

        GameObject box = new GameObject("CaughtFishExperiencePopup",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        box.transform.SetParent(root, false);
        popup = box.GetComponent<RectTransform>();
        popup.anchorMin = popup.anchorMax = popup.pivot = Vector2.one;
        float available = ((RectTransform)root).rect.width;
        popup.sizeDelta = new Vector2(available > 0f ? Mathf.Min(Width, Mathf.Max(420f, available - 72f)) : Width, Height);

        Image background = box.GetComponent<Image>();
        FishingHudTheme.Panel(box);
        background.raycastTarget = false;
        CanvasGroup group = box.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // The existing LEVEL shield is the same artwork used in Personal Stats.
        GameObject shieldObject = ImageAt(box.transform, "LevelShield", .012f, .035f, .205f, .965f);
        Image shieldImage = shieldObject.GetComponent<Image>();
        shieldImage.sprite = PersonalStatsView.Art("LevelShield");
        shieldImage.preserveAspect = true;
        shieldImage.color = Color.white;
        badge = shieldObject.GetComponent<RectTransform>();
        levelInBadge = TextAt(shieldObject.transform, "LevelNumber", "LV\n1", 30,
            .26f, .23f, .74f, .80f, Color.white, TextAnchor.MiddleCenter, true);

        levelTitle = TextAt(box.transform, "LevelTitle", "LEVEL 1", 32,
            .235f, .60f, .70f, .94f, Color.white, TextAnchor.MiddleLeft, true);
        xpValue = TextAt(box.transform, "ExperienceAmount", "0 / 250 XP", 23,
            .235f, .34f, .82f, .60f, Cyan, TextAnchor.MiddleLeft);
        earnedValue = TextAt(box.transform, "CatchXpAward", "", 23,
            .71f, .64f, .965f, .94f, Cyan, TextAnchor.MiddleRight, true);

        GameObject track = ImageAt(box.transform, "ExperienceTrack", .235f, .13f, .965f, .305f);
        FishingHudTheme.Panel(track, 3);
        Image trackImage = track.GetComponent<Image>();
        trackImage.raycastTarget = false;
        GameObject filled = ImageAt(track.transform, "AnimatedExperienceFill", 0f, 0f, 0f, 1f);
        Image fillImage = filled.GetComponent<Image>();
        FishingHudTheme.Fill(fillImage);
        fillImage.color = Cyan;
        fillImage.raycastTarget = false;
        meterFill = filled.GetComponent<RectTransform>();

        popup.gameObject.SetActive(false);
        SetSlide(0f);
        return true;
    }

    private static GameObject ImageAt(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        PersonalStatsView.Place(go.GetComponent<RectTransform>(), x0, y0, x1, y1);
        go.GetComponent<Image>().raycastTarget = false;
        return go;
    }

    private static Text TextAt(Transform parent, string name, string value, int fontSize,
        float x0, float y0, float x1, float y1, Color color,
        TextAnchor alignment, bool bold = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        PersonalStatsView.Place(go.GetComponent<RectTransform>(), x0, y0, x1, y1);
        Text label = go.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 14;
        label.resizeTextMaxSize = fontSize;
        label.text = value;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        if (bold) label.fontStyle = FontStyle.Bold;
        return label;
    }
}
