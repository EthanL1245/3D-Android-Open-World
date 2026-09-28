using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Decode source images into persistent native assets, independent of TextureImporter artifacts.</summary>
public static class AuthoredTextureAsset
{
    public static Texture2D Load(string sourcePath,string assetPath)
    {
        string fullPath=Path.GetFullPath(sourcePath);
        if(!File.Exists(fullPath))throw new FileNotFoundException("Authored texture source is missing. Pull the complete update.",fullPath);
        var decoded=new Texture2D(2,2,TextureFormat.RGBA32,true,false);
        try
        {
            if(!ImageConversion.LoadImage(decoded,File.ReadAllBytes(fullPath),false) || decoded.width<2 || decoded.height<2)
                throw new InvalidDataException("Could not decode authored texture: "+sourcePath);
            decoded.name=Path.GetFileNameWithoutExtension(sourcePath);
            decoded.wrapMode=TextureWrapMode.Repeat;decoded.filterMode=FilterMode.Trilinear;
            decoded.anisoLevel=4;decoded.Apply(true,false);
            var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if(saved==null){AssetDatabase.CreateAsset(decoded,assetPath);saved=decoded;decoded=null;}
            else EditorUtility.CopySerialized(decoded,saved);
            EditorUtility.SetDirty(saved);AssetDatabase.SaveAssets();
            saved=AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if(saved==null || saved.width<2 || saved.height<2)
                throw new InvalidOperationException("Could not save authored texture asset: "+assetPath);
            return saved;
        }
        finally {if(decoded!=null)UnityEngine.Object.DestroyImmediate(decoded);}
    }
}
