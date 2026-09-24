using UnityEditor;
using UnityEngine;

public static class TunaModelSetup
{
    [MenuItem("Tools/Open World/Install Updated Tuna + Fishing (One Click)")]
    public static void Install()
    {
        if(EditorApplication.isPlaying){Debug.LogWarning("Exit Play Mode before installing tuna models.");return;}
        InstallModels();
        Debug.Log("Updated tuna installed with authored textures, full swim clips and animated mouth anchors. Fishing, HP and index updates are ready for Play Mode.");
    }
    public static void InstallModels()
    {
        SeaBassImporter.InstallModel("YellowfinTuna","Assets/Resources/Fishing/YellowfinTuna.prefab");
        SeaBassImporter.InstallModel("BigeyeTuna","Assets/Resources/Fishing/BigeyeTuna.prefab");
        AssetDatabase.SaveAssets();
    }
}
