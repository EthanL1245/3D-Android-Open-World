using UnityEditor;
using UnityEngine;

// Compatibility menu for the old texture-only importer. Never restore the old
// cube geometry over the authored dock or stack another visual on top of it.
public static class SuncrestMarinaVisualImporter
{
    [MenuItem("Tools/Open World/Import Suncrest Marina Dock Visuals (One Click)")]
    public static void ImportOneClick()=>FishingContentUpdateSetup.Install();

    [MenuItem("Tools/Open World/Reapply Imported Suncrest Marina Dock Visuals")]
    private static void Reapply()=>FishingContentUpdateSetup.Install();

    public static bool TryApplyExisting(bool interactive)
    {
        FishingContentUpdateSetup.EnsureInstalled();
        return Resources.Load<GameObject>("Boats/AuthoredMarinaDock")!=null;
    }
}
