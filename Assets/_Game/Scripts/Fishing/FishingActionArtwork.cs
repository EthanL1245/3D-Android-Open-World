using UnityEngine;

// Original transparent uploads, cached once; presentation only.
public static class FishingActionArtwork
{
    private static readonly Sprite[] sprites = new Sprite[2];
    private static readonly bool[] attempted = new bool[2];

    public static Sprite Get(bool reel)
    {
        int index = reel ? 1 : 0;
        if (attempted[index]) return sprites[index];
        attempted[index] = true;
        string path = reel ? "Fishing/ReelActionIcon.png" : "Fishing/CastActionIcon.png";
        TextAsset source = Resources.Load<TextAsset>(path);
        if (source == null)
        {
            Debug.LogError("Missing fishing action artwork: " + path);
            return null;
        }
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
        bool loaded = texture.LoadImage(source.bytes,false);
        Resources.UnloadAsset(source);
        if (!loaded)
        {
            Object.Destroy(texture);
            Debug.LogError("Could not decode fishing action artwork: " + path);
            return null;
        }
        texture.name = reel ? "ReelActionIcon" : "CastActionIcon";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        sprites[index] = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),
            new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);
        texture.Apply(false,true);
        return sprites[index];
    }
}
