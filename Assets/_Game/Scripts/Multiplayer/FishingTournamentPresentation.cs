using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Tournament presentation shares the shop backdrop, glass controls and index fish studio.
public sealed partial class FishingTournament
{
    private Button closeButton;
    private Image headerTrophy;
    private FishIndexLivePreview resultPreview;
    private Text resultSubtitle,championName,championScore,championReward,catchPlayer,catchSpecies,catchWeight;
    private GameObject resultRoot;
    private readonly List<Text[]> resultRows=new List<Text[]>();
    private static readonly Dictionary<string,Sprite> artworkCache=new Dictionary<string,Sprite>();
    private static readonly Color Gold=new Color(1f,.85f,.35f);
    private static readonly Color Ice=new Color(.55f,.9f,1f);

    private void LayoutModal(bool results)
    {
        var rect=modal.GetComponent<RectTransform>();
        rect.sizeDelta=results?new Vector2(1320,850):new Vector2(1040,760);
        title.rectTransform.anchoredPosition=results?new Vector2(-40,366):new Vector2(-25,315);
        title.rectTransform.sizeDelta=results?new Vector2(1000,62):new Vector2(730,70);
        title.alignment=TextAnchor.MiddleLeft;
        title.fontSize=results?36:29;
        closeButton.GetComponent<RectTransform>().anchoredPosition=results?new Vector2(562,366):new Vector2(438,315);
        closeButton.GetComponent<RectTransform>().sizeDelta=new Vector2(130,56);
        var closeLabel=closeButton.GetComponentInChildren<Text>();closeLabel.rectTransform.sizeDelta=new Vector2(120,50);closeLabel.fontSize=22;
        headerTrophy.rectTransform.anchoredPosition=results?new Vector2(-596,366):new Vector2(-456,315);
        noticeText.rectTransform.anchoredPosition=new Vector2(0,results?-400:-325);
        noticeText.rectTransform.sizeDelta=new Vector2(results?1240:940,38);
        FitModal();
    }
    private void LateUpdate(){if(modal!=null && modal.activeSelf)FitModal();}
    private void FitModal()
    {
        var available=canvas.GetComponent<RectTransform>().rect.size;
        var size=modal.GetComponent<RectTransform>().sizeDelta;
        float scale=Mathf.Min(1f,Mathf.Min((available.x-48)/size.x,(available.y-48)/size.y));
        modal.transform.localScale=Vector3.one*Mathf.Max(.1f,scale);
    }
    private static Image Artwork(Transform parent,string key,Vector2 at,Vector2 size)
    {
        if(!artworkCache.TryGetValue(key,out var sprite))
        {
            var texture=Resources.Load<Texture2D>("Tournament/"+key);
            if(texture!=null)
            {
                // Rects omit transparent upload margins; source PNGs are unchanged.
                Rect crop;
                switch(key)
                {
                    case "Champion":crop=new Rect(0,197,2048,296);break;
                    case "Coins":crop=new Rect(271,53,951,961);break;
                    case "Trophy":crop=new Rect(157,86,940,1087);break;
                    case "Place1":crop=new Rect(134,202,986,906);break;
                    case "Place2":crop=new Rect(127,130,1001,992);break;
                    default:crop=new Rect(107,155,1040,980);break;
                }
                sprite=Sprite.Create(texture,crop,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                artworkCache[key]=sprite;
            }
        }
        var go=Panel(key,parent,at,size,Color.white);var image=go.GetComponent<Image>();
        image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
        return image;
    }
    private static GameObject Glass(Transform parent,string name,Vector2 at,Vector2 size)
    {
        var go=Panel(name,parent,at,size,Color.white);FishingHudTheme.Panel(go);return go;
    }
    private static Text ResultText(Transform parent,string value,int font,Vector2 at,Vector2 size,Color color,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var text=Label(parent,value,font,at,size);text.color=color;text.alignment=align;
        text.fontStyle=FontStyle.Bold;text.resizeTextForBestFit=true;text.resizeTextMinSize=16;text.resizeTextMaxSize=font;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;return text;
    }
    private void BuildResults()
    {
        resultRoot=new GameObject("TournamentResults",typeof(RectTransform));resultRoot.transform.SetParent(modal.transform,false);
        modalControls.Add(resultRoot);
        resultSubtitle=ResultText(resultRoot.transform,"",19,new Vector2(-5,321),new Vector2(1230,30),Ice);
        var banner=Artwork(resultRoot.transform,"Champion",new Vector2(0,207),new Vector2(1260,182));
        championName=ResultText(banner.transform,"",40,new Vector2(-6,-4),new Vector2(470,60),Color.white);
        championScore=ResultText(banner.transform,"",22,new Vector2(-6,-58),new Vector2(470,37),Ice);
        Artwork(banner.transform,"Coins",new Vector2(320,-15),new Vector2(86,88));
        championReward=ResultText(banner.transform,"",32,new Vector2(493,-15),new Vector2(242,76),Gold);

        var board=Glass(resultRoot.transform,"Final leaderboard",new Vector2(0,-43),new Vector2(1260,288));
        ResultText(board.transform,"FINAL LEADERBOARD",25,new Vector2(-10,116),new Vector2(1200,40),Color.white);
        ResultText(board.transform,"PLAYER",18,new Vector2(-330,76),new Vector2(400,30),Ice);
        ResultText(board.transform,"FISH CAUGHT",18,new Vector2(60,76),new Vector2(190,30),Ice,TextAnchor.MiddleCenter);
        ResultText(board.transform,"POINTS",18,new Vector2(292,76),new Vector2(205,30),Ice,TextAnchor.MiddleCenter);
        ResultText(board.transform,"REWARD",18,new Vector2(510,76),new Vector2(190,30),Ice,TextAnchor.MiddleCenter);
        var viewport=new GameObject("Leaderboard viewport",typeof(RectTransform),typeof(RectMask2D));viewport.transform.SetParent(board.transform,false);
        var vr=viewport.GetComponent<RectTransform>();vr.sizeDelta=new Vector2(1234,190);vr.anchoredPosition=new Vector2(0,-36);
        var content=new GameObject("Rows",typeof(RectTransform));content.transform.SetParent(viewport.transform,false);
        var cr=content.GetComponent<RectTransform>();cr.anchorMin=new Vector2(0,1);cr.anchorMax=Vector2.one;cr.pivot=new Vector2(.5f,1);
        var joined=new List<Entry>();foreach(var entry in current.entries??new Entry[0])if(entry.status=="joined")joined.Add(entry);
        int count=Mathf.Max(3,joined.Count);cr.sizeDelta=new Vector2(0,count*63);
        var scroll=viewport.AddComponent<ScrollRect>();scroll.viewport=vr;scroll.content=cr;scroll.horizontal=false;scroll.vertical=count>3;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
        for(int i=0;i<count;i++)
        {
            var row=Glass(content.transform,"Place "+(i+1),new Vector2(0,-31-i*63),new Vector2(1226,59));
            var rr=row.GetComponent<RectTransform>();rr.anchorMin=rr.anchorMax=new Vector2(.5f,1);
            row.GetComponent<Image>().color=i==0?new Color(1,.85f,.4f):i==2?new Color(.9f,.72f,.56f):Color.white;
            if(i<3)Artwork(row.transform,"Place"+(i+1),new Vector2(-568,0),new Vector2(55,53));
            else ResultText(row.transform,(i+1).ToString(),25,new Vector2(-568,0),new Vector2(55,50),Ice,TextAnchor.MiddleCenter);
            resultRows.Add(new[]{
                ResultText(row.transform,"",25,new Vector2(-315,0),new Vector2(420,49),Color.white),
                ResultText(row.transform,"",26,new Vector2(60,0),new Vector2(170,49),Color.white,TextAnchor.MiddleCenter),
                ResultText(row.transform,"",29,new Vector2(292,0),new Vector2(205,49),i==0?Gold:Color.white,TextAnchor.MiddleCenter),
                ResultText(row.transform,"",25,new Vector2(502,0),new Vector2(152,49),Gold,TextAnchor.MiddleCenter)});
            if(i<joined.Count && joined[i].id==current.winnerId)Artwork(row.transform,"Coins",new Vector2(590,0),new Vector2(34,38));
        }
        var biggest=Glass(resultRoot.transform,"Biggest catch",new Vector2(0,-290),new Vector2(1260,184));
        ResultText(biggest.transform,"BIGGEST CATCH",25,new Vector2(-377,62),new Vector2(450,36),Gold);
        ResultText(biggest.transform,"PLAYER",18,new Vector2(30,46),new Vector2(250,30),Ice);
        ResultText(biggest.transform,"SPECIES",18,new Vector2(300,46),new Vector2(250,30),Ice);
        ResultText(biggest.transform,"WEIGHT",18,new Vector2(515,46),new Vector2(180,30),Ice);
        catchPlayer=ResultText(biggest.transform,"",27,new Vector2(30,-10),new Vector2(250,72),Color.white);
        catchSpecies=ResultText(biggest.transform,"",26,new Vector2(300,-10),new Vector2(250,72),Color.white);
        catchWeight=ResultText(biggest.transform,"",33,new Vector2(515,-10),new Vector2(180,72),Gold);
        if(current.biggestKg>0)
        {
            var fish=new GameObject("Swimming biggest catch",typeof(RectTransform),typeof(RawImage));fish.transform.SetParent(biggest.transform,false);
            var raw=fish.GetComponent<RawImage>();raw.raycastTarget=false;raw.rectTransform.sizeDelta=new Vector2(440,132);raw.rectTransform.anchoredPosition=new Vector2(-350,-23);
            resultPreview=fish.AddComponent<FishIndexLivePreview>();resultPreview.Show(raw,current.species,440f/132f);
        }
        else ResultText(biggest.transform,"No qualifying catch",23,new Vector2(-350,-15),new Vector2(450,70),Ice,TextAnchor.MiddleCenter);
    }
    private void RefreshResults()
    {
        if(resultRoot==null)return;
        resultSubtitle.text=(current.speciesName??"RANDOM SPECIES")+"  /  "+current.islandName+"  /  6 MINUTE MATCH";
        bool won=!string.IsNullOrEmpty(current.winnerId);
        var winner=current.entries==null?null:System.Array.Find(current.entries,e=>e.id==current.winnerId);
        championName.text=won?current.winnerName:"NO WINNER";
        championScore.text=won && winner!=null?winner.points.ToString("N0")+" POINTS  ("+winner.catches+" FISH CAUGHT)":current.reason;
        championReward.text=won?"+"+current.pool.ToString("N0")+"\nCOINS!":"ENTRY FEES\nREFUNDED";
        int i=0;foreach(var row in resultRows)foreach(var cell in row)cell.text="";
        foreach(var entry in current.entries??new Entry[0])
        {
            if(entry.status!="joined" || i>=resultRows.Count)continue;
            var row=resultRows[i++];row[0].text=entry.name;row[1].text=entry.catches.ToString();row[2].text=entry.points.ToString("N0");
            row[3].text=entry.id==current.winnerId?current.pool.ToString("N0"):"—";
        }
        catchPlayer.text=current.biggestKg>0?current.biggestName:"—";
        catchSpecies.text=current.biggestKg>0?current.speciesName:"—";
        catchWeight.text=current.biggestKg>0?current.biggestKg.ToString("0.00")+" kg":"—";
    }
    private void ClearResults()
    {
        if(resultPreview!=null)resultPreview.Clear();resultPreview=null;
        resultRows.Clear();resultRoot=null;
    }
}
