using System.Collections.Generic;
using UnityEngine;

public static class FishVisualFactory
{
    private static readonly Dictionary<int, Material> BodyMaterials =
        new Dictionary<int, Material>();

    private static readonly Dictionary<int, Material> AccentMaterials =
        new Dictionary<int, Material>();

    private static Mesh tailMesh;

    public static GameObject CreateFish(
        string name,
        Transform parent,
        int speciesId,
        float scale)
    {
        FishSpeciesDefinition species =
            FishCatalog.Get(speciesId);

        GameObject root =
            new GameObject(name);

        if (parent != null)
        {
            root.transform.SetParent(
                parent,
                false
            );
        }

        root.transform.localScale =
            Vector3.one * scale;

        GameObject body =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        body.name = "Body";
        body.transform.SetParent(
            root.transform,
            false
        );

        body.transform.localScale =
            new Vector3(
                0.48f,
                0.28f,
                0.82f
            );

        Renderer bodyRenderer =
            body.GetComponent<Renderer>();

        bodyRenderer.sharedMaterial =
            GetBodyMaterial(speciesId);

        Collider bodyCollider =
            body.GetComponent<Collider>();

        if (bodyCollider != null)
            Object.Destroy(bodyCollider);

        GameObject tail =
            new GameObject(
                "Tail",
                typeof(MeshFilter),
                typeof(MeshRenderer)
            );

        tail.transform.SetParent(
            root.transform,
            false
        );

        tail.transform.localPosition =
            new Vector3(
                0f,
                0f,
                -0.48f
            );

        tail.GetComponent<MeshFilter>()
            .sharedMesh = GetTailMesh();

        tail.GetComponent<MeshRenderer>()
            .sharedMaterial =
            GetAccentMaterial(speciesId);

        GameObject fin =
            new GameObject(
                "DorsalFin",
                typeof(MeshFilter),
                typeof(MeshRenderer)
            );

        fin.transform.SetParent(
            root.transform,
            false
        );

        fin.transform.localPosition =
            new Vector3(
                0f,
                0.18f,
                -0.02f
            );

        fin.transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                90f
            );

        fin.transform.localScale =
            new Vector3(
                0.52f,
                0.52f,
                0.52f
            );

        fin.GetComponent<MeshFilter>()
            .sharedMesh = GetTailMesh();

        fin.GetComponent<MeshRenderer>()
            .sharedMaterial =
            GetAccentMaterial(speciesId);

        return root;
    }

    private static Material GetBodyMaterial(int speciesId)
    {
        if (BodyMaterials.TryGetValue(
                speciesId,
                out Material material))
        {
            return material;
        }

        FishSpeciesDefinition species =
            FishCatalog.Get(speciesId);

        material =
            CreateLitMaterial(
                species.BodyColor
            );

        BodyMaterials[speciesId] =
            material;

        return material;
    }

    private static Material GetAccentMaterial(int speciesId)
    {
        if (AccentMaterials.TryGetValue(
                speciesId,
                out Material material))
        {
            return material;
        }

        FishSpeciesDefinition species =
            FishCatalog.Get(speciesId);

        material =
            CreateLitMaterial(
                species.AccentColor
            );

        AccentMaterials[speciesId] =
            material;

        return material;
    }

    private static Material CreateLitMaterial(Color color)
    {
        Shader shader =
            Resources.Load<Shader>(
                "Fishing/FishingLit"
            );

        if (shader == null)
        {
            Debug.LogError(
                "Missing Resources/Fishing/FishingLit shader."
            );

            return null;
        }

        Material material =
            new Material(shader);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color
            );
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor(
                "_Color",
                color
            );
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat(
                "_Smoothness",
                0.34f
            );
        }

        material.enableInstancing = true;

        return material;
    }

    private static Mesh GetTailMesh()
    {
        if (tailMesh != null)
            return tailMesh;

        tailMesh =
            new Mesh
            {
                name = "RuntimeFishTail"
            };

        Vector3[] vertices =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(-0.28f, 0.31f, -0.22f),
            new Vector3(0.28f, 0.31f, -0.22f),
            new Vector3(-0.28f, -0.31f, -0.22f),
            new Vector3(0.28f, -0.31f, -0.22f)
        };

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 1,
            0, 4, 3,
            0, 3, 4
        };

        tailMesh.vertices = vertices;
        tailMesh.triangles = triangles;
        tailMesh.RecalculateNormals();
        tailMesh.RecalculateBounds();

        return tailMesh;
    }
}
