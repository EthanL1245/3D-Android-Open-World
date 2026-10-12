using UnityEngine;
using UnityEngine.UI;

public sealed class FishingMultiplayerPanel : MonoBehaviour
{
    private FishingMultiplayer session;
    private FirstPersonController player;
    private Text status;
    private InputField guestName,code;
    private Button open,join,leave,copy;
    private bool blocked;
    private Font font;
    public static void Build(RectTransform parent,FirstPersonController player)
    {
        var go=new GameObject("MultiplayerPanel",typeof(RectTransform),typeof(LayoutElement));
        go.transform.SetParent(parent,false);go.GetComponent<LayoutElement>().preferredHeight=650;
        var panel=go.AddComponent<FishingMultiplayerPanel>();panel.player=player;panel.session=FishingMultiplayer.For(player);panel.Create();
    }
    private void Create()
    {
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Label("PLAY TOGETHER",0,55,32);
        Label("Open a room and share its code. Friends join with the same game version.\nPlayer movement is shared; catches, boats and saves stay local.",60,80,23);
        status=Label("",145,65,25);
        guestName=Field("Your name",220);guestName.characterLimit=20;
        guestName.text=session!=null?session.PlayerName:"Angler";
        code=Field("Enter 8-character room code",300);code.characterLimit=8;code.contentType=InputField.ContentType.Alphanumeric;
        open=Action("OPEN SERVER",390,0,.49f,()=>{session.PlayerName=guestName.text;session.OpenServer();});
        join=Action("JOIN SERVER",390,.51f,1,()=>{session.PlayerName=guestName.text;session.JoinServer(code.text);});
        copy=Action("COPY ROOM CODE",480,0,.49f,()=>GUIUtility.systemCopyBuffer=session.Code);
        leave=Action("LEAVE SERVER",480,.51f,1,()=>session.LeaveServer());
        Label("Close this menu to walk around. Use the same island to meet up.\nLeaving as host closes the room for everyone.",570,75,23);
    }
    private void Update()
    {
        if(session==null)return;
        status.text=session.Status;
        open.interactable=join.interactable=!session.Busy && !session.Connected;
        leave.interactable=session.Connected || session.Busy;
        copy.interactable=session.Connected;
        guestName.interactable=code.interactable=!session.Connected && !session.Busy;
        bool typing=guestName.isFocused || code.isFocused;
        if(typing!=blocked){blocked=typing;if(player!=null)player.SetUIBlocked(typing);}
    }
    private void OnDisable()
    {
        if(blocked && player!=null && (ShopDimensionManager.Instance==null || !ShopDimensionManager.Instance.Traveling))player.SetUIBlocked(false);
        blocked=false;
    }
    private Text Label(string value,float y,float height,int size,Transform owner=null)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(owner!=null?owner:transform,false);
        var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.text=value;text.color=Color.white;text.raycastTarget=false;
        text.supportRichText=false;text.alignment=TextAnchor.MiddleCenter;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        Position(text.rectTransform,y,height,0,1);return text;
    }
    private InputField Field(string placeholder,float y)
    {
        var go=new GameObject("Input",typeof(RectTransform),typeof(Image),typeof(InputField));go.transform.SetParent(transform,false);
        Position(go.GetComponent<RectTransform>(),y,66,.1f,.9f);go.GetComponent<Image>().color=new Color(.08f,.15f,.18f);
        var input=go.GetComponent<InputField>();input.targetGraphic=go.GetComponent<Image>();
        var text=Label("",0,66,26,go.transform);input.textComponent=text;
        var hint=Label(placeholder,0,66,24,go.transform);hint.color=new Color(.65f,.75f,.8f);input.placeholder=hint;
        return input;
    }
    private Button Action(string caption,float y,float min,float max,UnityEngine.Events.UnityAction action)
    {
        var go=new GameObject(caption,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(transform,false);
        Position(go.GetComponent<RectTransform>(),y,72,min,max);go.GetComponent<Image>().color=new Color(.04f,.36f,.39f);
        var button=go.GetComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(action);
        Label(caption,0,72,27,go.transform);return button;
    }
    private static void Position(RectTransform rect,float y,float height,float min,float max)
    {
        rect.anchorMin=new Vector2(min,1);rect.anchorMax=new Vector2(max,1);rect.pivot=new Vector2(.5f,1);
        rect.offsetMin=new Vector2(8,-y-height);rect.offsetMax=new Vector2(-8,-y);
    }
}
