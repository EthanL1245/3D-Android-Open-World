using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FishingHUD : MonoBehaviour
{
    private FishingSystem system;

    private GameObject root;
    private GameObject actionButtonObject;
    private FishingActionButton actionInput;
    private Text actionLabel;

    private Text statusText;
    private GameObject fightPanel;
    private Image tensionFill;
    private Image progressFill;
    private Text tensionLabel;
    private Text progressLabel;

    private GameObject inventoryPanel;
    private RectTransform inventoryList;
    private Text emptyInventoryText;

    private Image rodSlotImage;
    private Outline rodSlotOutline;

    private GameObject catchPanel;
    private Text catchText;
    private float catchPanelTimer;

    private Font font;

    public FishingActionButton ActionInput => actionInput;

    public void Initialize(FishingSystem owner)
    {
        system = owner;

        if (root != null)
            return;

        font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        BuildUi();
        RefreshInventory();
    }

    private void Update()
    {
        if (catchPanelTimer <= 0f)
            return;

        catchPanelTimer -= Time.deltaTime;

        if (catchPanelTimer <= 0f &&
            catchPanel != null)
        {
            catchPanel.SetActive(false);
        }
    }

    public void SetRodSelected(bool selected)
    {
        if (rodSlotOutline != null)
        {
            rodSlotOutline.effectColor =
                selected
                    ? new Color(
                        0.24f,
                        0.88f,
                        1f,
                        0.95f
                    )
                    : new Color(
                        0.22f,
                        0.28f,
                        0.31f,
                        0.70f
                    );

            rodSlotOutline.effectDistance =
                selected
                    ? new Vector2(4f, -4f)
                    : new Vector2(2f, -2f);
        }

        if (rodSlotImage != null)
        {
            rodSlotImage.color =
                selected
                    ? new Color(
                        0.08f,
                        0.19f,
                        0.24f,
                        0.88f
                    )
                    : new Color(
                        0.05f,
                        0.07f,
                        0.09f,
                        0.72f
                    );
        }
    }

    public void SetActionVisible(bool visible)
    {
        if (actionButtonObject != null)
            actionButtonObject.SetActive(visible);
    }

    public void SetActionLabel(string text)
    {
        if (actionLabel != null)
            actionLabel.text = text;
    }

    public void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    public void ShowFightMeters(bool visible)
    {
        if (fightPanel != null)
            fightPanel.SetActive(visible);
    }

    public void SetFightMeters(
        float tension,
        float health)
    {
        tension =
            Mathf.Clamp01(tension);

        health =
            Mathf.Clamp01(health);

        if (tensionFill != null)
        {
            SetBarWidth(
                tensionFill.rectTransform,
                tension
            );

            tensionFill.color =
                Color.Lerp(
                    new Color(
                        0.22f,
                        0.78f,
                        0.42f
                    ),
                    new Color(
                        0.95f,
                        0.20f,
                        0.12f
                    ),
                    Mathf.InverseLerp(
                        0.55f,
                        0.95f,
                        tension
                    )
                );
        }

        if (progressFill != null)
        {
            SetBarWidth(
                progressFill.rectTransform,
                health
            );
        }

        if (tensionLabel != null)
        {
            tensionLabel.text =
                "TENSION " +
                Mathf.RoundToInt(
                    tension * 100f
                ) +
                "%";
        }

        if (progressLabel != null)
        {
            progressLabel.text =
                "HEALTH " +
                Mathf.RoundToInt(
                    health * 100f
                ) +
                "%";
        }
    }

    public void ShowCatch(
        CaughtFishRecord record,
        string heading = "CAUGHT!")
    {
        if (catchPanel == null ||
            catchText == null)
        {
            return;
        }

        FishSpeciesDefinition species =
            FishCatalog.Get(record.speciesId);

        catchText.text =
            heading +
            "\n" +
            species.Name +
            "\n" +
            record.weightKg.ToString("0.00") +
            " kg";

        catchPanel.SetActive(true);
        catchPanelTimer = 3.5f;
    }

    public void RefreshInventory()
    {
        if(GetComponent<ShopWorldHUD>()!=null) return;
        if (inventoryList == null ||
            system == null ||
            system.Inventory == null)
        {
            return;
        }

        for (int i =
                inventoryList.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                inventoryList
                    .GetChild(i)
                    .gameObject
            );
        }

        IReadOnlyList<CaughtFishRecord> fish =
            system.Inventory.Fish;

        if (emptyInventoryText != null)
        {
            emptyInventoryText.gameObject
                .SetActive(
                    fish.Count == 0
                );
        }

        int visibleCount = fish.Count;
        inventoryList.sizeDelta=new Vector2(0f,Mathf.Max(1f,visibleCount*64f));

        for (int row = 0;
             row < visibleCount;
             row++)
        {
            int inventoryIndex =
                fish.Count - 1 - row;

            CaughtFishRecord record =
                fish[inventoryIndex];

            FishSpeciesDefinition species =
                FishCatalog.Get(
                    record.speciesId
                );

            GameObject rowObject =
                CreatePanel(
                    "FishRow_" + inventoryIndex,
                    inventoryList,
                    new Color(
                        0.08f,
                        0.12f,
                        0.14f,
                        0.92f
                    )
                );

            RectTransform rowRect =
                rowObject.GetComponent<RectTransform>();

            rowRect.anchorMin =
                new Vector2(0f, 1f);

            rowRect.anchorMax =
                new Vector2(1f, 1f);

            rowRect.pivot =
                new Vector2(0.5f, 1f);

            rowRect.sizeDelta =
                new Vector2(0f, 58f);

            rowRect.anchoredPosition =
                new Vector2(
                    0f,
                    -row * 64f
                );

            Button button =
                rowObject.AddComponent<Button>();

            int capturedIndex =
                inventoryIndex;

            button.onClick.AddListener(
                () =>
                    system.HoldFish(
                        capturedIndex
                    )
            );

            Text label =
                CreateText(
                    "Label",
                    rowObject.transform,
                    species.Name +
                    "   " +
                    record.weightKg
                        .ToString("0.00") +
                    " kg",
                    24,
                    TextAnchor.MiddleLeft
                );

            RectTransform labelRect =
                label.rectTransform;

            labelRect.anchorMin =
                new Vector2(0f, 0f);

            labelRect.anchorMax =
                new Vector2(1f, 1f);

            labelRect.offsetMin =
                new Vector2(18f, 0f);

            labelRect.offsetMax =
                new Vector2(-12f, 0f);
        }
    }

    public void SetMenuCovered(bool covered)
    {
        if(root!=null) root.SetActive(!covered);
        if(actionInput!=null) actionInput.ResetInput();
    }
    public void SetInventoryOpen(bool open)
    {
        if(open)
        {
            ShopWorldHUD shop=FindFirstObjectByType<ShopWorldHUD>();
            if(shop!=null) { shop.Open("bag"); return; }
        }
        if (inventoryPanel == null)
            return;

        if (open)
            RefreshInventory();

        inventoryPanel.SetActive(open);
    }

    private void BuildUi()
    {
        root =
            new GameObject(
                "FishingHUDRoot",
                typeof(RectTransform)
            );

        root.transform.SetParent(
            transform,
            false
        );

        RectTransform rootRect =
            root.GetComponent<RectTransform>();

        StretchFullScreen(rootRect);

        BuildHotbar();
        BuildActionButton();
        BuildStatus();
        BuildFightPanel();
        BuildInventory();
        BuildCatchPanel();

        root.transform.SetAsLastSibling();
    }

    private void BuildHotbar()
    {
        GameObject hotbar =
            new GameObject(
                "Hotbar",
                typeof(RectTransform)
            );

        hotbar.transform.SetParent(
            root.transform,
            false
        );

        RectTransform hotbarRect =
            hotbar.GetComponent<RectTransform>();

        hotbarRect.anchorMin =
            new Vector2(0.5f, 0f);

        hotbarRect.anchorMax =
            new Vector2(0.5f, 0f);

        hotbarRect.pivot =
            new Vector2(0.5f, 0f);

        hotbarRect.sizeDelta =
            new Vector2(470f, 92f);

        hotbarRect.anchoredPosition =
            new Vector2(0f, 24f);

        for (int i = 0; i < 5; i++)
        {
            GameObject slot =
                CreatePanel(
                    "Slot_" + (i + 1),
                    hotbar.transform,
                    new Color(
                        0.05f,
                        0.07f,
                        0.09f,
                        0.72f
                    )
                );

            RectTransform slotRect =
                slot.GetComponent<RectTransform>();

            slotRect.anchorMin =
                new Vector2(0f, 0.5f);

            slotRect.anchorMax =
                new Vector2(0f, 0.5f);

            slotRect.pivot =
                new Vector2(0f, 0.5f);

            slotRect.sizeDelta =
                new Vector2(82f, 82f);

            slotRect.anchoredPosition =
                new Vector2(
                    i * 94f,
                    0f
                );

            Outline outline =
                slot.AddComponent<Outline>();

            outline.effectColor =
                new Color(
                    0.22f,
                    0.28f,
                    0.31f,
                    0.70f
                );

            outline.effectDistance =
                new Vector2(2f, -2f);

            Text number =
                CreateText(
                    "Number",
                    slot.transform,
                    (i + 1).ToString(),
                    16,
                    TextAnchor.UpperLeft
                );

            number.color =
                new Color(
                    0.68f,
                    0.75f,
                    0.78f,
                    0.92f
                );

            RectTransform numberRect =
                number.rectTransform;

            numberRect.anchorMin =
                new Vector2(0f, 0f);

            numberRect.anchorMax =
                new Vector2(1f, 1f);

            numberRect.offsetMin =
                new Vector2(7f, 3f);

            numberRect.offsetMax =
                new Vector2(-4f, -3f);

            if (i == 0)
            {
                rodSlotImage =
                    slot.GetComponent<Image>();

                rodSlotOutline = outline;

                Button button =
                    slot.AddComponent<Button>();

                button.onClick.AddListener(
                    system.EquipRod
                );

                CreateRodIcon(slot.transform);
            }
        }
    }

    private void CreateRodIcon(Transform parent)
    {
        GameObject rod =
            CreatePanel(
                "RodIcon",
                parent,
                new Color(
                    0.73f,
                    0.57f,
                    0.32f,
                    1f
                )
            );

        RectTransform rodRect =
            rod.GetComponent<RectTransform>();

        rodRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rodRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rodRect.sizeDelta =
            new Vector2(8f, 58f);

        rodRect.anchoredPosition =
            new Vector2(6f, 0f);

        rodRect.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -28f
            );

        GameObject reel =
            CreatePanel(
                "ReelIcon",
                parent,
                new Color(
                    0.32f,
                    0.75f,
                    0.86f,
                    1f
                )
            );

        RectTransform reelRect =
            reel.GetComponent<RectTransform>();

        reelRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        reelRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        reelRect.sizeDelta =
            new Vector2(20f, 20f);

        reelRect.anchoredPosition =
            new Vector2(-6f, -8f);
    }

    private void BuildActionButton()
    {
        actionButtonObject =
            CreatePanel(
                "FishingActionButton",
                root.transform,
                new Color(
                    0.05f,
                    0.30f,
                    0.42f,
                    0.88f
                )
            );

        RectTransform rect =
            actionButtonObject
                .GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(1f, 0f);

        rect.anchorMax =
            new Vector2(1f, 0f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.sizeDelta =
            new Vector2(170f, 170f);

        rect.anchoredPosition =
            new Vector2(
                -180f,
                400f
            );

        Outline outline =
            actionButtonObject
                .AddComponent<Outline>();

        outline.effectColor =
            new Color(
                0.36f,
                0.90f,
                1f,
                0.85f
            );

        outline.effectDistance =
            new Vector2(4f, -4f);

        actionInput =
            actionButtonObject
                .AddComponent<FishingActionButton>();

        actionLabel =
            CreateText(
                "Label",
                actionButtonObject.transform,
                "CAST",
                34,
                TextAnchor.MiddleCenter
            );

        StretchFullScreen(
            actionLabel.rectTransform
        );
    }

    private void BuildStatus()
    {
        statusText =
            CreateText(
                "FishingStatus",
                root.transform,
                string.Empty,
                27,
                TextAnchor.MiddleCenter
            );

        RectTransform rect =
            statusText.rectTransform;

        rect.anchorMin =
            new Vector2(0.5f, 1f);

        rect.anchorMax =
            new Vector2(0.5f, 1f);

        rect.pivot =
            new Vector2(0.5f, 1f);

        rect.sizeDelta =
            new Vector2(700f, 70f);

        rect.anchoredPosition =
            new Vector2(0f, -24f);

        statusText.color =
            new Color(
                0.93f,
                0.98f,
                1f,
                0.96f
            );
    }

    private void BuildFightPanel()
    {
        fightPanel =
            CreatePanel(
                "FightPanel",
                root.transform,
                new Color(
                    0.02f,
                    0.05f,
                    0.07f,
                    0.78f
                )
            );

        RectTransform panelRect =
            fightPanel.GetComponent<RectTransform>();

        panelRect.anchorMin =
            new Vector2(0.5f, 1f);

        panelRect.anchorMax =
            new Vector2(0.5f, 1f);

        panelRect.pivot =
            new Vector2(0.5f, 1f);

        panelRect.sizeDelta =
            new Vector2(650f, 130f);

        panelRect.anchoredPosition =
            new Vector2(0f, -92f);

        CreateBar(
            fightPanel.transform,
            "TENSION",
            new Vector2(0f, -18f),
            new Color(
                0.22f,
                0.78f,
                0.42f
            ),
            out tensionFill,
            out tensionLabel
        );

        CreateBar(
            fightPanel.transform,
            "HEALTH",
            new Vector2(0f, -74f),
            new Color(
                0.22f,
                0.65f,
                0.96f
            ),
            out progressFill,
            out progressLabel
        );

        fightPanel.SetActive(false);
    }

    private void CreateBar(
        Transform parent,
        string label,
        Vector2 position,
        Color fillColor,
        out Image fill,
        out Text labelText)
    {
        labelText =
            CreateText(
                label + "Label",
                parent,
                label + " 0%",
                18,
                TextAnchor.MiddleLeft
            );

        Text text = labelText;

        RectTransform textRect =
            text.rectTransform;

        textRect.anchorMin =
            new Vector2(0f, 1f);

        textRect.anchorMax =
            new Vector2(0f, 1f);

        textRect.pivot =
            new Vector2(0f, 1f);

        textRect.sizeDelta =
            new Vector2(110f, 34f);

        textRect.anchoredPosition =
            new Vector2(
                18f,
                position.y
            );

        GameObject background =
            CreatePanel(
                label + "Background",
                parent,
                new Color(
                    0.14f,
                    0.16f,
                    0.17f,
                    0.96f
                )
            );

        RectTransform bgRect =
            background.GetComponent<RectTransform>();

        bgRect.anchorMin =
            new Vector2(0f, 1f);

        bgRect.anchorMax =
            new Vector2(0f, 1f);

        bgRect.pivot =
            new Vector2(0f, 1f);

        bgRect.sizeDelta =
            new Vector2(490f, 28f);

        bgRect.anchoredPosition =
            new Vector2(
                135f,
                position.y - 3f
            );

        GameObject fillObject =
            CreatePanel(
                label + "Fill",
                background.transform,
                fillColor
            );

        fill =
            fillObject.GetComponent<Image>();

        fill.type = Image.Type.Simple;
        fill.raycastTarget = false;

        RectTransform fillRect =
            fill.rectTransform;

        fillRect.anchorMin =
            new Vector2(0f, 0f);

        fillRect.anchorMax =
            new Vector2(0f, 1f);

        fillRect.pivot =
            new Vector2(0f, 0.5f);

        fillRect.offsetMin =
            Vector2.zero;

        fillRect.offsetMax =
            Vector2.zero;
    }

    private static void SetBarWidth(
        RectTransform rect,
        float amount)
    {
        Vector2 min =
            rect.anchorMin;

        Vector2 max =
            rect.anchorMax;

        min.x = 0f;
        max.x = Mathf.Clamp01(amount);

        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void BuildInventory()
    {
        GameObject inventoryButton =
            CreatePanel(
                "InventoryButton",
                root.transform,
                new Color(
                    0.07f,
                    0.10f,
                    0.12f,
                    0.88f
                )
            );

        RectTransform buttonRect =
            inventoryButton
                .GetComponent<RectTransform>();

        buttonRect.anchorMin =
            new Vector2(1f, 1f);

        buttonRect.anchorMax =
            new Vector2(1f, 1f);

        buttonRect.pivot =
            new Vector2(1f, 1f);

        buttonRect.sizeDelta =
            new Vector2(150f, 58f);

        buttonRect.anchoredPosition =
            new Vector2(-28f, -28f);

        Button button =
            inventoryButton.AddComponent<Button>();

        button.onClick.AddListener(
            () =>
                SetInventoryOpen(
                    !inventoryPanel.activeSelf
                )
        );

        if(GetComponent<ShopWorldHUD>()!=null) inventoryButton.SetActive(false);

        Text inventoryButtonText =
            CreateText(
                "Label",
                inventoryButton.transform,
                "FISH",
                24,
                TextAnchor.MiddleCenter
            );

        StretchFullScreen(
            inventoryButtonText.rectTransform
        );

        inventoryPanel =
            CreatePanel(
                "InventoryPanel",
                root.transform,
                new Color(
                    0.025f,
                    0.045f,
                    0.055f,
                    0.96f
                )
            );

        RectTransform panelRect =
            inventoryPanel
                .GetComponent<RectTransform>();

        panelRect.anchorMin =
            new Vector2(1f, 0.5f);

        panelRect.anchorMax =
            new Vector2(1f, 0.5f);

        panelRect.pivot =
            new Vector2(1f, 0.5f);

        panelRect.sizeDelta =
            new Vector2(520f, 720f);

        panelRect.anchoredPosition =
            new Vector2(-28f, 0f);

        Text title =
            CreateText(
                "Title",
                inventoryPanel.transform,
                "CAUGHT FISH",
                34,
                TextAnchor.MiddleLeft
            );

        RectTransform titleRect =
            title.rectTransform;

        titleRect.anchorMin =
            new Vector2(0f, 1f);

        titleRect.anchorMax =
            new Vector2(1f, 1f);

        titleRect.pivot =
            new Vector2(0.5f, 1f);

        titleRect.sizeDelta =
            new Vector2(0f, 70f);

        titleRect.offsetMin =
            new Vector2(22f, -70f);

        titleRect.offsetMax =
            new Vector2(-90f, 0f);

        GameObject closeObject =
            CreatePanel(
                "Close",
                inventoryPanel.transform,
                new Color(
                    0.30f,
                    0.10f,
                    0.10f,
                    0.92f
                )
            );

        RectTransform closeRect =
            closeObject
                .GetComponent<RectTransform>();

        closeRect.anchorMin =
            new Vector2(1f, 1f);

        closeRect.anchorMax =
            new Vector2(1f, 1f);

        closeRect.pivot =
            new Vector2(1f, 1f);

        closeRect.sizeDelta =
            new Vector2(64f, 52f);

        closeRect.anchoredPosition =
            new Vector2(-14f, -14f);

        Button closeButton =
            closeObject.AddComponent<Button>();

        closeButton.onClick.AddListener(
            () =>
                SetInventoryOpen(false)
        );

        Text closeText =
            CreateText(
                "Label",
                closeObject.transform,
                "X",
                26,
                TextAnchor.MiddleCenter
            );

        StretchFullScreen(
            closeText.rectTransform
        );

        GameObject viewport = new GameObject("FishViewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
        viewport.transform.SetParent(inventoryPanel.transform,false);
        RectTransform viewportRect=viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin=Vector2.zero; viewportRect.anchorMax=Vector2.one;
        viewportRect.offsetMin=new Vector2(20f,24f); viewportRect.offsetMax=new Vector2(-20f,-88f);
        viewport.GetComponent<Image>().color=new Color(0,0,0,0.01f);
        GameObject listObject=new GameObject("FishList",typeof(RectTransform));
        listObject.transform.SetParent(viewport.transform,false);
        inventoryList=listObject.GetComponent<RectTransform>();
        inventoryList.anchorMin=new Vector2(0,1); inventoryList.anchorMax=Vector2.one;
        inventoryList.pivot=new Vector2(0.5f,1); inventoryList.sizeDelta=Vector2.zero;
        ScrollRect scroll=viewport.GetComponent<ScrollRect>();
        scroll.viewport=viewportRect; scroll.content=inventoryList; scroll.horizontal=false;
        scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35f;

        emptyInventoryText =
            CreateText(
                "Empty",
                inventoryPanel.transform,
                "No catches yet.\nEquip the rod and cast into the ocean.",
                24,
                TextAnchor.MiddleCenter
            );

        RectTransform emptyRect =
            emptyInventoryText.rectTransform;

        emptyRect.anchorMin =
            new Vector2(0f, 0f);

        emptyRect.anchorMax =
            new Vector2(1f, 1f);

        emptyRect.offsetMin =
            new Vector2(30f, 30f);

        emptyRect.offsetMax =
            new Vector2(-30f, -90f);

        emptyInventoryText.color =
            new Color(
                0.64f,
                0.73f,
                0.76f,
                0.94f
            );

        inventoryPanel.SetActive(false);
    }

    private void BuildCatchPanel()
    {
        catchPanel =
            CreatePanel(
                "CatchPanel",
                root.transform,
                new Color(
                    0.02f,
                    0.16f,
                    0.20f,
                    0.94f
                )
            );

        RectTransform rect =
            catchPanel.GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.sizeDelta =
            new Vector2(460f, 210f);

        rect.anchoredPosition =
            new Vector2(0f, 90f);

        catchText =
            CreateText(
                "CatchText",
                catchPanel.transform,
                string.Empty,
                34,
                TextAnchor.MiddleCenter
            );

        StretchFullScreen(
            catchText.rectTransform
        );

        catchPanel.SetActive(false);
    }

    private GameObject CreatePanel(
        string name,
        Transform parent,
        Color color)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        gameObject.transform.SetParent(
            parent,
            false
        );

        Image image =
            gameObject.GetComponent<Image>();

        image.color = color;

        return gameObject;
    }

    private Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );

        gameObject.transform.SetParent(
            parent,
            false
        );

        Text text =
            gameObject.GetComponent<Text>();

        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        return text;
    }

    private static void StretchFullScreen(
        RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
