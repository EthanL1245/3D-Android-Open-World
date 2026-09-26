using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the starter raft's PHYSICS footprint on the stationary raft body instead of
/// the animated oars. This never changes the authored mesh, materials, UVs or visuals.
/// It is intentionally idempotent so re-importing Raft.blend cannot make the side
/// collision wide again.
/// </summary>
public static class RaftGameplayTuningV22
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const float TargetWidth = 2.60f;
    private const float TargetLength = 4.48f;

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Apply Raft Gameplay Tuning (v22)")]
    private static void Force()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft gameplay tuning","Exit Play Mode first.","OK");
            return;
        }
        Apply(true);
    }

    private static bool Apply(bool showDialog)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return false;
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab==null)return false;

        GameObject root=PrefabUtility.LoadPrefabContents(PrefabPath);
        bool changed=false;
        try
        {
            foreach(BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if(box==null)continue;
                string n=box.gameObject.name;

                if(string.Equals(n,"Walkable deck",StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(n,"Hull collider",StringComparison.OrdinalIgnoreCase))
                {
                    Vector3 size=box.size;
                    float sx=Mathf.Max(.0001f,Mathf.Abs(box.transform.lossyScale.x));
                    float sz=Mathf.Max(.0001f,Mathf.Abs(box.transform.lossyScale.z));
                    float desiredLocalX=TargetWidth/sx;
                    float desiredLocalZ=TargetLength/sz;
                    if(Mathf.Abs(size.x-desiredLocalX)>.0005f || Mathf.Abs(size.z-desiredLocalZ)>.0005f)
                    {
                        size.x=desiredLocalX;
                        size.z=desiredLocalZ;
                        box.size=size;
                        EditorUtility.SetDirty(box);
                        changed=true;
                    }
                }
                else if(string.Equals(n,"Low gunwale",StringComparison.OrdinalIgnoreCase))
                {
                    // These were generic invisible side bumpers from the placeholder
                    // boat. On the authored raft they sit farther out than the real
                    // stationary body and are what makes left/right contact feel wide.
                    // The deck/hull colliders still provide the actual boat collision.
                    if(box.enabled)
                    {
                        box.enabled=false;
                        EditorUtility.SetDirty(box);
                        changed=true;
                    }
                }
            }

            if(changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[RAFT V22] SUCCESS — physical deck/hull width set to 2.60 m, length 4.48 m, and obsolete wide side-bumper colliders disabled. Visual raft/oars were untouched.");
            }
            else
            {
                Debug.Log("[RAFT V22] Raft physics footprint already tuned; no prefab change needed.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if(showDialog)
            EditorUtility.DisplayDialog("Raft gameplay tuning","Raft collision now follows the stationary raft body, not the oars. Visuals/materials were not changed.","OK");
        return true;
    }
}
