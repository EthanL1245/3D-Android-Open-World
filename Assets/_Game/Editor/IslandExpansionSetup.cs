using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class IslandExpansionSetup
{
    [MenuItem("Tools/Setup Island Expansion")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();
        if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Save the island scene first.");
        if(Object.FindFirstObjectByType<FirstPersonController>()==null || Object.FindFirstObjectByType<OceanWater>()==null)
            throw new InvalidOperationException("Open the Suncrest / PrototypeWorld island scene first.");
        if(Object.FindFirstObjectByType<ReefZone>()==null)SuncrestReefSetup.Install();
        if(Object.FindFirstObjectByType<ReefZone>()==null)return;
        var config=Resources.Load<IslandExpansionConfig>("Islands/BrinebreakExpansion");
        if(config==null)throw new InvalidOperationException("BrinebreakExpansion.asset missing; pull the complete update.");
        var world=Object.FindFirstObjectByType<IslandExpansionWorld>();
        if(world==null){var go=new GameObject("Island Expansion");Undo.RegisterCreatedObjectUndo(go,"Add island expansion");world=go.AddComponent<IslandExpansionWorld>();}
        Undo.RecordObject(world,"Expansion settings");world.Config=config;EditorUtility.SetDirty(world);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Island expansion configured and saved. Press Play: Brinebreak Isle, the shared seabed and discovery trigger are generated before gameplay starts. Existing Suncrest assets are preserved.");
    }
}
