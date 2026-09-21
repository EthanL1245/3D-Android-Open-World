using UnityEngine;

/// <summary>One Unity world unit is one metre, regardless of imported rig units.</summary>
public static class FishWorldSize
{
    public static GameObject Create(string name, Transform parent, int species, float kg)
    {
        var fish = FishVisualFactory.CreateFish(name, parent, species, 1f);
        SetLength(fish, ShopCatalog.FishLength(species, kg));
        return fish;
    }

    public static void SetLength(GameObject fish, float metres)
    {
        if (fish == null || metres <= 0f || float.IsNaN(metres) || float.IsInfinity(metres))
            return;

        // Measure the same renderer bounds used by the prefab importers. Baking
        // skinned vertices here mixes FBX rig units with the normalized visual
        // hierarchy and caused the held fish and thumbnails to shrink severely.
        // Neutral world rotation keeps the longitudinal (+Z) measurement identical
        // for aquarium fish, camera-parented catches and sideways UI previews.
        var root = fish.transform;
        Quaternion rotation = root.rotation;
        try
        {
            root.rotation = Quaternion.identity;
            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in fish.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled ||
                    (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)))
                    continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            float length = bounds.size.z;
            if (found && length > 0.0001f && !float.IsNaN(length) && !float.IsInfinity(length))
                root.localScale *= metres / length;
        }
        finally
        {
            root.rotation = rotation;
        }
    }
}
