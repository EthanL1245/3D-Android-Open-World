using System.Collections.Generic;
using UnityEngine;

public static class FishVisualFactory
{
    private static readonly Dictionary<int, Material> BodyMaterials =
        new Dictionary<int, Material>();

    private static readonly Dictionary<int, Material> AccentMaterials =
        new Dictionary<int, Material>();

    private static Mesh tailMesh;
    private static GameObject heroYellowtailPrefab;
    private static GameObject redSnapperPrefab;
    private static GameObject yellowfinTunaPrefab;
    private static GameObject yellowGoatfishPrefab;
    private static GameObject blackSpotGoatfishPrefab;
    private static GameObject mackerelPrefab;

    private const int HeroYellowtailSpeciesId = 3;

    public static GameObject CreateFish(
        string name,
        Transform parent,
        int speciesId,
        float scale)
    {
        return CreateFishInternal(
            name,
            parent,
            speciesId,
            scale,
            true
        );
    }

    public static GameObject CreateAmbientFish(
        string name,
        Transform parent,
        int speciesId,
        float scale)
    {
        return CreateFishInternal(
            name,
            parent,
            speciesId,
            scale,
            true
        );
    }

    private static GameObject CreateFishInternal(
        string name,
        Transform parent,
        int speciesId,
        float scale,
        bool allowHero)
    {
        speciesId=FishCatalog.CanonicalId(speciesId);
        FishSpeciesDefinition species =
            FishCatalog.Get(speciesId);

        if (allowHero && speciesId == 2)
        {
            var prefab = Resources.Load<GameObject>("Fishing/SeaBass");
            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab, parent, false);
                instance.name = name; instance.transform.localScale = Vector3.one * scale;
                return instance;
            }
        }


        if (allowHero &&
            speciesId == 0)
        {
            GameObject mackerel =
                CreateMackerel(
                    name,
                    parent,
                    scale
                );

            if (mackerel != null)
                return mackerel;
        }

        if (allowHero &&
            speciesId ==
            HeroYellowtailSpeciesId)
        {
            GameObject hero =
                CreateHeroYellowtail(
                    name,
                    parent,
                    scale
                );

            if (hero != null)
                return hero;
        }

        if (allowHero &&
            speciesId ==
            FishCatalog.RedSnapperId)
        {
            GameObject snapper =
                CreateRedSnapper(
                    name,
                    parent,
                    scale
                );

            if (snapper != null)
                return snapper;
        }

        if (allowHero &&
            speciesId ==
            FishCatalog.YellowfinTunaId)
        {
            GameObject tuna =
                CreateYellowfinTuna(
                    name,
                    parent,
                    scale
                );

            if (tuna != null)
                return tuna;
        }

        if (allowHero &&
            (
                speciesId ==
                FishCatalog.YellowGoatfishId ||
                speciesId ==
                FishCatalog.BlackSpotGoatfishId
            ))
        {
            GameObject goatfish =
                CreateGoatfish(
                    name,
                    parent,
                    speciesId,
                    scale
                );

            if (goatfish != null)
                return goatfish;
        }

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

    private static GameObject CreateMackerel(
        string name,
        Transform parent,
        float scale)
    {
        if (mackerelPrefab == null)
        {
            mackerelPrefab =
                Resources.Load<GameObject>(
                    "Fishing/Mackerel"
                );
        }

        if (mackerelPrefab == null)
            return null;

        GameObject instance =
            Object.Instantiate(
                mackerelPrefab
            );

        instance.name = name;

        if (parent != null)
        {
            instance.transform.SetParent(
                parent,
                false
            );
        }

        instance.transform.localScale =
            Vector3.one * scale;

        return instance;
    }

    private static GameObject CreateHeroYellowtail(
        string name,
        Transform parent,
        float scale)
    {
        if (heroYellowtailPrefab == null)
        {
            heroYellowtailPrefab =
                Resources.Load<GameObject>(
                    "Fishing/HeroYellowtail"
                );
        }

        if (heroYellowtailPrefab == null)
            return null;

        GameObject instance =
            Object.Instantiate(
                heroYellowtailPrefab
            );

        instance.name = name;

        if (parent != null)
        {
            instance.transform.SetParent(
                parent,
                false
            );
        }

        instance.transform.localScale =
            Vector3.one * scale;

        return instance;
    }

    private static GameObject CreateRedSnapper(
        string name,
        Transform parent,
        float scale)
    {
        if (redSnapperPrefab == null)
        {
            redSnapperPrefab =
                Resources.Load<GameObject>(
                    "Fishing/RedSnapper"
                );
        }

        if (redSnapperPrefab == null)
            return null;

        GameObject instance =
            Object.Instantiate(
                redSnapperPrefab
            );

        instance.name = name;

        if (parent != null)
        {
            instance.transform.SetParent(
                parent,
                false
            );
        }

        instance.transform.localScale =
            Vector3.one * scale;

        return instance;
    }

    private static GameObject CreateYellowfinTuna(
        string name,
        Transform parent,
        float scale)
    {
        if (yellowfinTunaPrefab == null)
        {
            yellowfinTunaPrefab =
                Resources.Load<GameObject>(
                    "Fishing/YellowfinTuna"
                );
        }

        if (yellowfinTunaPrefab == null)
            return null;

        GameObject instance =
            Object.Instantiate(
                yellowfinTunaPrefab
            );

        instance.name = name;

        if (parent != null)
        {
            instance.transform.SetParent(
                parent,
                false
            );
        }

        instance.transform.localScale =
            Vector3.one * scale;

        return instance;
    }

    private static GameObject CreateGoatfish(
        string name,
        Transform parent,
        int speciesId,
        float scale)
    {
        GameObject prefab = null;

        if (speciesId ==
            FishCatalog.YellowGoatfishId)
        {
            if (yellowGoatfishPrefab == null)
            {
                yellowGoatfishPrefab =
                    Resources.Load<GameObject>(
                        "Fishing/YellowGoatfish"
                    );
            }

            prefab =
                yellowGoatfishPrefab;
        }
        else if (speciesId ==
                 FishCatalog.BlackSpotGoatfishId)
        {
            if (blackSpotGoatfishPrefab == null)
            {
                blackSpotGoatfishPrefab =
                    Resources.Load<GameObject>(
                        "Fishing/BlackSpotGoatfish"
                    );
            }

            prefab =
                blackSpotGoatfishPrefab;
        }

        if (prefab == null)
            return null;

        GameObject instance =
            Object.Instantiate(
                prefab
            );

        instance.name = name;

        if (parent != null)
        {
            instance.transform.SetParent(
                parent,
                false
            );
        }

        instance.transform.localScale =
            Vector3.one * scale;

        return instance;
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
