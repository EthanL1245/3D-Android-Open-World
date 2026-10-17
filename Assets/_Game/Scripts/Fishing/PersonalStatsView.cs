using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Personal stats use the shared shop frame and original supplied artwork.</summary>
public static class PersonalStatsView
{
    private static readonly Dictionary<string, Sprite> artwork = new Dictionary<string, Sprite>();
    private static readonly Color Cyan = new Color(.1f,.95f,1f);
    private static readonly Color Detail = new Color(.67f,.89f,.96f);
    public static Sprite Art(string name, bool locked = false)
    {
        string key = locked ? name + "_Locked" : name;
        if(artwork.TryGetValue(key,out Sprite sprite))return sprite;
        var source=Resources.Load<TextAsset>("Progression/"+name+".png");
        if(source==null)return null;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!texture.LoadImage(source.bytes,!locked)){Object.Destroy(texture);return null;}
        if(locked)
        {
            // Cache a genuinely desaturated version once; preserve the supplied alpha.
            // Unlocked cards and combat buttons continue to use the original artwork.
            Color32[] pixels=texture.GetPixels32();
            for(int i=0;i<pixels.Length;i++)
            {
                Color32 c=pixels[i];
                byte grey=(byte)Mathf.RoundToInt((c.r*.2126f+c.g*.7152f+c.b*.0722f)*.65f);
                pixels[i]=new Color32(grey,grey,grey,c.a);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
        }
        texture.wrapMode=TextureWrapMode.Clamp;
        // Keep original pixels; the card artwork includes transparent outer padding.
        sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
        artwork[key]=sprite;
        Resources.UnloadAsset(source);
        return sprite;
    }
    public static void Place(RectTransform rect,float x0,float y0,float x1,float y1,float inset=0)
    {
        rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);
        rect.offsetMin=new Vector2(inset,inset);rect.offsetMax=new Vector2(-inset,-inset);
    }
    private static GameObject Panel(Transform parent,string name,float x0,float y0,float x1,float y1,bool glass=true)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        Place(go.GetComponent<RectTransform>(),x0,y0,x1,y1,4);
        go.GetComponent<Image>().raycastTarget=false;
        if(glass)FishingHudTheme.Panel(go);else go.GetComponent<Image>().color=Color.clear;
        return go;
    }
    public static Text Label(Transform parent,string value,int size,float x0,float y0,float x1,float y1,Color color,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var go=new GameObject("Caption",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=size;
        t.resizeTextForBestFit=true;t.resizeTextMinSize=12;t.resizeTextMaxSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;
        Place(t.rectTransform,x0,y0,x1,y1,3);return t;
    }
    private static void Meter(Transform parent,float value,float x0,float y0,float x1,float y1)
    {
        var track=Panel(parent,"Meter",x0,y0,x1,y1);FishingHudTheme.Panel(track,3);
        var fill=Panel(track.transform,"Fill",0,0,Mathf.Clamp01(value),1,false);
        fill.GetComponent<Image>().color=Cyan;FishingHudTheme.Fill(fill.GetComponent<Image>());
        Place(fill.GetComponent<RectTransform>(),0,0,Mathf.Clamp01(value),1,0);
    }
    public static void AddMenuIcon(Button button)
    {
        var icon=Panel(button.transform,"PersonalStatsShield",.02f,.12f,.14f,.88f,false).GetComponent<Image>();
        icon.sprite=Art("LevelShield");icon.color=Color.white;icon.preserveAspect=true;
    }
    public static GameObject Build(Transform parent,AnglerProgression data)
    {
        if(data==null)data=new AnglerProgression();
        var root=Panel(parent,"PersonalStatsContent",.012f,.025f,.988f,1f,false);
        // CLOSE ends 80 canvas units below the modal top. Keep a compact 28-unit
        // header gap at every aspect ratio, then give the recovered height to the body.
        root.GetComponent<RectTransform>().offsetMax=new Vector2(-4f,-108f);
        var band=Panel(root.transform,"LevelAndExperience",0,.80f,1,1);
        var shield=Panel(band.transform,"LevelShield",.012f,.04f,.14f,.96f,false).GetComponent<Image>();
        shield.sprite=Art("LevelShield");shield.color=Color.white;shield.preserveAspect=true;
        Label(shield.transform,"LV\n"+data.level,30,.19f,.18f,.81f,.82f,Color.white,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold;
        Label(band.transform,"LEVEL "+data.level,32,.15f,.55f,.61f,.94f,Color.white).fontStyle=FontStyle.Bold;
        bool max=data.level>=AnglerProgression.MaxLevel;
        Label(band.transform,max?"MAX LEVEL":$"{data.experience:N0} / {AnglerProgression.RequiredXp(data.level):N0} XP",22,.15f,.30f,.61f,.59f,Cyan);
        Meter(band.transform,max?1f:(float)(data.experience/AnglerProgression.RequiredXp(data.level)),.15f,.12f,.61f,.29f);
        string next=data.level<80?"Next special ability\nLEVEL "+((data.level/20+1)*20):"ALL CURRENT ABILITIES UNLOCKED";
        Label(band.transform,next,26,.66f,.18f,.97f,.85f,Detail);
        var left=Panel(root.transform,"FishingAttributes",0,0,.505f,.79f);
        Label(left.transform,"FISHING ATTRIBUTES",29,.03f,.89f,.97f,.99f,Color.white).fontStyle=FontStyle.Bold;
        string[] descriptions={"Extra damage per reel hit","Boosts damage after the raw bonus","Retrieve line faster","Reduces tension gain","Bonus damage for your charged yank","Permanent larger-fish weight bonus"};
        for(int i=0;i<6;i++)
        {
            float top=.88f-i*.143f;
            var row=Panel(left.transform,"Stat_"+i,.02f,top-.137f,.98f,top);
            Label(row.transform,AnglerProgression.Names[i],25,.03f,.49f,.78f,.97f,Color.white).fontStyle=FontStyle.Bold;
            Label(row.transform,descriptions[i],17,.03f,.22f,.80f,.53f,Detail);
            Label(row.transform,"+"+data.Value(i)+(i==0?"":"%"),26,.80f,.25f,.98f,.91f,Cyan,TextAnchor.MiddleRight);
            Meter(row.transform,data.Value(i)/100f,.03f,.07f,.78f,.21f);
        }
        var right=Panel(root.transform,"SpecialAbilities",.51f,0,1,.79f);
        Label(right.transform,"SPECIAL ABILITIES",29,.03f,.90f,.97f,.99f,Color.white).fontStyle=FontStyle.Bold;
        Label(right.transform,"UNLOCK A NEW ABILITY EVERY 20 LEVELS",16,.03f,.85f,.97f,.91f,Detail);
        string[] descriptionsAbility={"Burns 2% of max HP per second for 5s. Once per fight.","Automatically deals 10% of max HP when hooking.","Reeling adds no tension for 5s. Once per fight.","Fish swims toward you for 4s. Once per fight."};
        // Six equal slots: only the first four have any objects or artwork.
        for(int i=0;i<4;i++)
        {
            float x=(i%2)*.5f, top=.84f-(i/2)*.278f;
            var card=Panel(right.transform,"Ability_"+i,x,top-.273f,x+.5f,top,false);
            bool unlocked=data.Unlocked(i);
            Color titleColor=unlocked?Color.white:new Color(.72f,.72f,.72f);
            Color detailColor=unlocked?Detail:new Color(.58f,.58f,.58f);
            Color accentColor=unlocked?Cyan:new Color(.66f,.66f,.66f);
            var art=card.GetComponent<Image>();art.sprite=Art(AnglerProgression.AbilityAssets[i],!unlocked);art.color=Color.white;
            Label(card.transform,AnglerProgression.AbilityNames[i],22,.31f,.67f,.95f,.87f,titleColor).fontStyle=FontStyle.Bold;
            Label(card.transform,"LEVEL "+((i+1)*20),16,.31f,.52f,.95f,.67f,accentColor);
            Label(card.transform,descriptionsAbility[i],18,.31f,.27f,.95f,.52f,detailColor);
            Label(card.transform,unlocked?"UNLOCKED":"LOCKED",16,.31f,.13f,.95f,.27f,accentColor);
        }
        return root;
    }
}
