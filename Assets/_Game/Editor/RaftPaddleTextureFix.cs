using UnityEditor;
using UnityEngine;

/// <summary>
/// Safety shim for the old renderer-level paddle texture repair.
///
/// IMPORTANT: the current authored raft imports the deck and BOTH oars inside one
/// merged MeshRenderer. The old implementation could mistake that merged renderer
/// for a paddle after it saw an oar-named material, then assign the oar texture to
/// every material slot on the renderer. That is the exact path that repainted the
/// raft/deck with Oar Texture.jpg.
///
/// Merged-mesh oar texturing is now handled by the dedicated raft recovery pass at
/// submesh/triangle level. This legacy class intentionally performs NO automatic
/// asset-postprocess or InitializeOnLoad work so it can never repaint the raft again.
/// </summary>
public static class RaftPaddleTextureFix
{
    [MenuItem("Tools/Open World/Repair Raft Paddle Texture")]
    private static void ExplainRetiredRepair()
    {
        EditorUtility.DisplayDialog(
            "Raft Paddle Repair",
            "This old renderer-level repair is disabled because the authored raft and oars share one merged renderer. Use the current raft material recovery pass instead; it textures only the dedicated oar submesh.",
            "OK");
        Debug.Log("[RAFT SAFETY] Legacy RaftPaddleTextureFix is retired; it will not assign Oar Texture.jpg to the merged raft renderer.");
    }
}
