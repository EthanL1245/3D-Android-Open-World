using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FishingPrototypeSetup
{
    [MenuItem("Tools/Open World/Setup Fishing Prototype")]
    public static void SetupFishingPrototype()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Fishing Prototype",
                "Exit Play Mode before running the setup.",
                "OK"
            );

            return;
        }

        GameObject player =
            GameObject.Find("Player");

        if (player == null)
        {
            EditorUtility.DisplayDialog(
                "Fishing Prototype",
                "Could not find the Player GameObject.",
                "OK"
            );

            return;
        }

        Camera camera =
            player.GetComponentInChildren<Camera>();

        OceanWater ocean =
            Object.FindFirstObjectByType<OceanWater>();

        Canvas canvas =
            Object.FindFirstObjectByType<Canvas>();

        if (camera == null ||
            ocean == null ||
            canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Fishing Prototype",
                "Fishing setup needs PlayerCamera, OceanWater, and the gameplay Canvas.",
                "OK"
            );

            return;
        }

        FishingInventory inventory =
            player.GetComponent<FishingInventory>();

        if (inventory == null)
        {
            inventory =
                Undo.AddComponent<FishingInventory>(
                    player
                );
        }

        FishingSystem system =
            player.GetComponent<FishingSystem>();

        if (system == null)
        {
            system =
                Undo.AddComponent<FishingSystem>(
                    player
                );
        }

        system.Configure(
            camera,
            ocean,
            inventory,
            canvas
        );

        GameObject fishingWorld =
            GameObject.Find("FishingWorld");

        if (fishingWorld == null)
        {
            fishingWorld =
                new GameObject(
                    "FishingWorld"
                );

            Undo.RegisterCreatedObjectUndo(
                fishingWorld,
                "Create Fishing World"
            );
        }

        AmbientFishManager manager =
            fishingWorld
                .GetComponent<AmbientFishManager>();

        if (manager == null)
        {
            manager =
                Undo.AddComponent<AmbientFishManager>(
                    fishingWorld
                );
        }

        manager.Configure(
            ocean,
            player.transform
        );

        EditorUtility.SetDirty(inventory);
        EditorUtility.SetDirty(system);
        EditorUtility.SetDirty(manager);

        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Selection.activeGameObject =
            player;

        EditorUtility.DisplayDialog(
            "Fishing Prototype Ready",
            "Fishing is wired up. At runtime the game will create the hotbar, rod, cast/reel button, line and bobber, persistent caught-fish inventory, held fish view model, and ambient swimming fish.\n\nPress Play and walk to the ocean. Slot 1 equips the rod. Cast toward open water, wait for a bite, tap HOOK, then hold/release REEL to manage tension.",
            "OK"
        );
    }
}
