using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FishingHUD : MonoBehaviour
{
    private FishingSystem system;

    private GameObject root;
    private GameObject hotbarObject,dragPanel,skillPanel;
    private RectTransform skillFill,dragRect,joystickRect;
    private Text skillLabel,dragTitle;
    private Slider dragSlider;
    private readonly Vector3[] joystickCorners=new Vector3[4];
    private FishingBurstDamageRuntime dragOwner;
    public bool FishingUiVisible=>root!=null && root.activeInHierarchy;
    public bool CombatInputVisible=>root!=null && root.activeInHierarchy && dragPanel!=null && dragPanel.activeInHierarchy;
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

    private GameObject castPanel;
    private CastPowerGauge castGauge;
    private GameObject cancelCast;
    private FirstPersonController castPlayer;
    private Text castCaption;
    private readonly Text[] damageLabels=new Text[8];
    private readonly float[] damageLife=new float[8];
    private int nextDamage;
    private Font font;

    public void SetCastAvailable(bool available)
    {
        if(actionButtonObject==null)return;
        actionButtonObject.GetComponent<Image>().color=available?new Color(.04f,.55f,.58f,.95f):new Color(.10f,.17f,.19f,.85f);
        actionButtonObject.GetComponent<Outline>().effectColor=available?new Color(.4f,1f,.86f):new Color(.25f,.32f,.34f,.65f);
    }
    public void SetActionInteractable(bool value)
    {if(actionInput!=null)actionInput.SetInteractable(value);}
    public void SetCastPower(bool visible,float power,float distance,bool[] validSamples=null,bool rangeAvailable=true)
    {
        if(castPanel==null && visible)
        {
            castPanel=new GameObject("QuarterCircleCastGauge",typeof(RectTransform),typeof(CastPowerGauge));
            castPanel.transform.SetParent(actionButtonObject.transform,false);
            var r=castPanel.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(470,470);
            castGauge=castPanel.GetComponent<CastPowerGauge>();castGauge.raycastTarget=false;
            var captionBox=CreatePanel("GaugeCaption",castPanel.transform,new Color(.02f,.07f,.09f,.96f));
            captionBox.GetComponent<Image>().raycastTarget=false;
            var c=captionBox.GetComponent<RectTransform>();c.anchorMin=c.anchorMax=new Vector2(.5f,.5f);c.sizeDelta=new Vector2(330,56);c.anchoredPosition=new Vector2(-95,268);
            castCaption=CreateText("CastDistance",captionBox.transform,"",29,TextAnchor.MiddleCenter);StretchFullScreen(castCaption.rectTransform);
        }
        if(castPanel==null)return;castPanel.SetActive(visible);
        if(!visible)return;
        castGauge.Set(power,validSamples,rangeAvailable);
        castCaption.text=$"{distance:0.0} m"+(!rangeAvailable || !FishingRules.IsCastPowerAvailable(validSamples,power)?" • BLOCKED":"");
    }
    public void SetCastCancel(bool visible,System.Action callback)
    {
        if(system==null)return;
        if(castPlayer==null)castPlayer=system.GetComponent<FirstPersonController>();
        if(castPlayer==null)return;
        var jump=castPlayer.JumpControl;
        if(cancelCast==null && visible && jump!=null)
        {
            cancelCast=CreatePanel("CancelCast",jump.parent,new Color(.72f,.09f,.07f,.96f));
            var r=cancelCast.GetComponent<RectTransform>();r.anchorMin=jump.anchorMin;r.anchorMax=jump.anchorMax;r.pivot=jump.pivot;r.sizeDelta=jump.sizeDelta;r.anchoredPosition=castPlayer.JumpRestPosition;
            var text=CreateText("Cancel",cancelCast.transform,"CANCEL\nCAST",26,TextAnchor.MiddleCenter);StretchFullScreen(text.rectTransform);
            cancelCast.AddComponent<Button>();
        }
        if(visible && cancelCast!=null && jump!=null)
        {
            var r=cancelCast.GetComponent<RectTransform>();r.anchorMin=jump.anchorMin;r.anchorMax=jump.anchorMax;r.pivot=jump.pivot;r.sizeDelta=jump.sizeDelta;r.anchoredPosition=castPlayer.JumpRestPosition;
        }
        castPlayer.SetCastMode(visible);
        if(cancelCast!=null)
        {
            cancelCast.SetActive(visible);var b=cancelCast.GetComponent<Button>();b.onClick.RemoveAllListeners();
            if(callback!=null)b.onClick.AddListener(()=>callback());
        }
    }
    public void ShowDamage(int amount,Vector3 screenPoint)
    {
        int i=nextDamage++%damageLabels.Length;
        if(damageLabels[i]==null)
        {
            damageLabels[i]=CreateText("ReelDamage",root.transform,"",32,TextAnchor.MiddleCenter);
            var r=damageLabels[i].rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(160,60);
        }
        if(screenPoint.z<=0)screenPoint=new Vector3(Screen.width*.5f,Screen.height*.5f,1);
        var canvas=root.GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root.GetComponent<RectTransform>(),screenPoint,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var local);
        damageLabels[i].rectTransform.anchoredPosition=local+new Vector2(Random.Range(-35f,35f),25);
        damageLabels[i].text="−"+amount;damageLabels[i].color=new Color(1,.85f,.35f,1);damageLabels[i].gameObject.SetActive(true);damageLife[i]=.9f;
    }
    private void UpdateDamage()
    {
        for(int i=0;i<damageLabels.Length;i++)if(damageLife[i]>0 && damageLabels[i]!=null)
        {
            damageLife[i]-=Time.deltaTime;
            damageLabels[i].rectTransform.anchoredPosition+=Vector2.up*(Time.deltaTime*75f);
            var color=damageLabels[i].color;color.a=Mathf.Clamp01(damageLife[i]/.4f);damageLabels[i].color=color;
            if(damageLife[i]<=0)damageLabels[i].gameObject.SetActive(false);
        }
    }

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
        UpdateDamage();
        if (catchPanelTimer <= 0f)
            return;

        catchPanelTimer -= Time.deltaTime;

        if (catchPanelTimer <= 0f &&
            catchPanel != null)
        {
            catchPanel.SetActive(false);
        }
    }

    public void SetDragFightUI(bool fighting,bool live,int mode,float charge)
    {
        if(root==null)return;
        if(fighting && dragPanel==null)BuildDragUI();
        if(hotbarObject!=null)hotbarObject.SetActive(!fighting);
        if(dragPanel==null)return;
        dragPanel.SetActive(fighting);
        skillPanel.SetActive(fighting);
        if(!fighting)return;
        PositionDragAboveJoystick();
        string[] names={"LOW","MEDIUM","HIGH"};
        mode=Mathf.Clamp(mode,0,2);
        dragTitle.text="DRAG • "+names[mode];
        dragSlider.interactable=live;
        dragSlider.SetValueWithoutNotify(mode);
        skillFill.anchorMax=new Vector2(Mathf.Clamp01(charge),1f);
        skillLabel.text=!live?"FISH SUBDUED • REEL IT IN":charge>=1f?"SKILL READY • SWIPE ANY DIRECTION • 10× HIT":
            "SKILL "+Mathf.FloorToInt(charge*100f)+"% • "+(mode==FishingDragRules.High?"CHARGING":"USE HIGH DRAG TO CHARGE");
        skillFill.GetComponent<Image>().color=charge>=1f?new Color(1f,.76f,.23f,1):new Color(.06f,.57f,.53f,1);
    }

    private void BuildDragUI()
    {
        dragOwner=system!=null?system.GetComponent<FishingBurstDamageRuntime>():null;
        dragPanel=CreatePanel("FishingDrag",root.transform,new Color(.025f,.075f,.095f,.95f));
        dragPanel.GetComponent<Image>().raycastTarget=false;
        dragRect=dragPanel.GetComponent<RectTransform>();
        dragRect.anchorMin=dragRect.anchorMax=new Vector2(.5f,.5f);
        dragRect.pivot=new Vector2(.5f,0);dragRect.sizeDelta=new Vector2(340,104);
        dragRect.localScale=Vector3.one*1.3f;
        dragTitle=CreateText("DragTitle",dragPanel.transform,"DRAG • MEDIUM",23,TextAnchor.MiddleCenter);
        var titleRect=dragTitle.rectTransform;titleRect.anchorMin=new Vector2(0,.65f);titleRect.anchorMax=Vector2.one;titleRect.offsetMin=titleRect.offsetMax=Vector2.zero;
        // A single drag target with a handle and exactly three discrete values.
        var sliderObject=CreatePanel("DragSlider",dragPanel.transform,Color.clear);
        var sliderRect=sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin=sliderRect.anchorMax=Vector2.zero;sliderRect.pivot=Vector2.zero;
        sliderRect.anchoredPosition=new Vector2(16,26);sliderRect.sizeDelta=new Vector2(308,40);
        var rail=CreatePanel("Rail",sliderObject.transform,new Color(.18f,.30f,.34f,1));
        rail.GetComponent<Image>().raycastTarget=false;
        var railRect=rail.GetComponent<RectTransform>();railRect.anchorMin=new Vector2(0,.5f);railRect.anchorMax=new Vector2(1,.5f);
        railRect.offsetMin=new Vector2(16,-4);railRect.offsetMax=new Vector2(-16,4);
        string[] names={"LOW","MEDIUM","HIGH"};
        for(int i=0;i<3;i++)
        {
            var tick=CreatePanel("Notch"+i,sliderObject.transform,new Color(.55f,.78f,.77f,1));
            tick.GetComponent<Image>().raycastTarget=false;
            var tickRect=tick.GetComponent<RectTransform>();tickRect.anchorMin=tickRect.anchorMax=new Vector2(0,.5f);
            tickRect.sizeDelta=new Vector2(4,18);tickRect.anchoredPosition=new Vector2(16+i*138,0);
            var label=CreateText("NotchLabel"+i,dragPanel.transform,names[i],18,TextAnchor.MiddleCenter);
            var labelRect=label.rectTransform;labelRect.anchorMin=labelRect.anchorMax=Vector2.zero;
            labelRect.sizeDelta=new Vector2(90,22);labelRect.anchoredPosition=new Vector2(Mathf.Clamp(32+i*138,45,295),13);
        }
        var handleArea=new GameObject("HandleArea",typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform,false);
        var areaRect=handleArea.GetComponent<RectTransform>();StretchFullScreen(areaRect);
        areaRect.offsetMin=new Vector2(16,0);areaRect.offsetMax=new Vector2(-16,0);
        var handle=CreatePanel("Handle",handleArea.transform,new Color(.08f,.78f,.66f,1));
        var handleRect=handle.GetComponent<RectTransform>();handleRect.sizeDelta=new Vector2(30,-4);
        dragSlider=sliderObject.AddComponent<Slider>();
        dragSlider.direction=Slider.Direction.LeftToRight;
        dragSlider.minValue=FishingDragRules.Low;dragSlider.maxValue=FishingDragRules.High;
        dragSlider.wholeNumbers=true;dragSlider.handleRect=handleRect;
        dragSlider.targetGraphic=handle.GetComponent<Image>();
        dragSlider.navigation=new Navigation{mode=Navigation.Mode.None};
        dragSlider.SetValueWithoutNotify(FishingDragRules.Medium);
        dragSlider.onValueChanged.AddListener(value=>
        {
            if(dragOwner==null && system!=null)dragOwner=system.GetComponent<FishingBurstDamageRuntime>();
            if(dragOwner!=null)
            {
                int previous = dragOwner.SelectedDrag;
                dragOwner.SelectDrag(Mathf.RoundToInt(value));
                int notches = Mathf.Abs(dragOwner.SelectedDrag - previous);
                if(notches > 0 && system != null) system.PlayDragNotchSound(notches);
            }
        });
        skillPanel=CreatePanel("FishingSkill",root.transform,new Color(.025f,.075f,.095f,.96f));
        skillPanel.GetComponent<Image>().raycastTarget=false;
        var sr=skillPanel.GetComponent<RectTransform>();sr.anchorMin=sr.anchorMax=new Vector2(.5f,0);sr.pivot=new Vector2(.5f,0);
        sr.sizeDelta=new Vector2(610,120);sr.anchoredPosition=new Vector2(0,24);
        var track=CreatePanel("SkillTrack",skillPanel.transform,new Color(.09f,.16f,.19f,1));track.GetComponent<Image>().raycastTarget=false;
        var tr=track.GetComponent<RectTransform>();tr.anchorMin=new Vector2(0,0);tr.anchorMax=new Vector2(1,.42f);tr.offsetMin=new Vector2(14,14);tr.offsetMax=new Vector2(-14,0);
        var fill=CreatePanel("SkillFill",track.transform,new Color(.06f,.57f,.53f,1));fill.GetComponent<Image>().raycastTarget=false;
        skillFill=fill.GetComponent<RectTransform>();skillFill.anchorMin=Vector2.zero;skillFill.anchorMax=new Vector2(0,1);skillFill.offsetMin=skillFill.offsetMax=Vector2.zero;
        skillLabel=CreateText("SkillLabel",skillPanel.transform,"",23,TextAnchor.MiddleCenter);
        var lr=skillLabel.rectTransform;lr.anchorMin=new Vector2(0,.45f);lr.anchorMax=Vector2.one;lr.offsetMin=new Vector2(10,0);lr.offsetMax=new Vector2(-10,-5);
    }

    private void PositionDragAboveJoystick()
    {
        if(joystickRect==null)
        {
            var joystick=FindFirstObjectByType<MobileJoystick>();
            if(joystick!=null)joystickRect=joystick.GetComponent<RectTransform>();
        }
        var rootRect=root.GetComponent<RectTransform>();
        Vector2 position=new Vector2(rootRect.rect.xMin+190,rootRect.rect.yMin+320);
        if(joystickRect!=null)
        {
            joystickRect.GetWorldCorners(joystickCorners);
            var source=joystickRect.GetComponentInParent<Canvas>();
            var target=root.GetComponentInParent<Canvas>();
            Camera sourceCamera=source!=null && source.renderMode!=RenderMode.ScreenSpaceOverlay?source.worldCamera:null;
            Camera targetCamera=target!=null && target.renderMode!=RenderMode.ScreenSpaceOverlay?target.worldCamera:null;
            Vector2 screen=RectTransformUtility.WorldToScreenPoint(sourceCamera,(joystickCorners[1]+joystickCorners[2])*.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect,screen,targetCamera,out position);
            position.y+=18f;
        }
        position.x=Mathf.Clamp(position.x,rootRect.rect.xMin+229,rootRect.rect.xMax-229);
        position.y=Mathf.Clamp(position.y,rootRect.rect.yMin+18,rootRect.rect.yMax-154);
        dragRect.localPosition=new Vector3(position.x,position.y,0);
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
        float health, int maxHealth=100)
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
            progressLabel.text = $"HP {Mathf.RoundToInt(health*maxHealth)}/{maxHealth}";
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
            FishCatalog.FormatWeight(record.weightKg) +
            " / " + ShopCatalog.FishLength(record.speciesId,record.weightKg).ToString("0.00") + " m";

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

        hotbarObject=hotbar;
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
            new Vector2(610f, 120f);

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
                new Vector2(110f, 110f);

            slotRect.anchoredPosition =
                new Vector2(
                    i * 122f,
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

            if(i==1)system.GetComponent<BoatSystem>()?.BindSlot(slot);
            if (i == 0)
            {
                rodSlotImage =
                    slot.GetComponent<Image>();

                rodSlotOutline = outline;

                Button button =
                    slot.AddComponent<Button>();

                button.onClick.AddListener(
                    system.ToggleRod
                );

                CreateRodIcon(slot.transform);
            }
        }
    }

    private void CreateRodIcon(Transform parent)
    {
        var icon=new GameObject("AuthoredRodIcon",typeof(RectTransform),typeof(RawImage));
        icon.transform.SetParent(parent,false);
        var image=icon.GetComponent<RawImage>();image.raycastTarget=false;
        image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;
        image.rectTransform.offsetMin=new Vector2(5,5);image.rectTransform.offsetMax=new Vector2(-5,-5);
        var preview=icon.AddComponent<ShopPreview>();preview.Attach(image,0,0,"RodAssembly");
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
                -225f,
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
            new Vector2(160f, 34f);

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
            new Vector2(435f, 28f);

        bgRect.anchoredPosition =
            new Vector2(
                190f,
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




