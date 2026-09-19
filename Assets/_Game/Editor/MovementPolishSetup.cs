using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MovementPolishSetup
{
    private const string UiRoot =
        "Assets/_Game/UI";

    private const string GeneratedFolder =
        UiRoot + "/Generated";

    private const string CircleSpritePath =
        GeneratedFolder +
        "/ControlCircle.png";

    private const string JumpSpritePath =
        GeneratedFolder +
        "/JumpButton.png";

    private const string SprintSpritePath =
        GeneratedFolder +
        "/SprintIndicator.png";

    [MenuItem("Tools/Open World/Setup Movement Polish")]
    public static void SetupMovementPolish()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Movement Polish",
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
                "Movement Polish",
                "Could not find a GameObject named Player.",
                "OK"
            );
            return;
        }

        FirstPersonController controller =
            player.GetComponent<FirstPersonController>();

        if (controller == null)
        {
            EditorUtility.DisplayDialog(
                "Movement Polish",
                "Player does not have FirstPersonController.",
                "OK"
            );
            return;
        }

        Canvas canvas =
            Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Movement Polish",
                "Could not find the gameplay Canvas.",
                "OK"
            );
            return;
        }

        EnsureFolders();

        Sprite circleSprite =
            CreateOrUpdateCircleSprite();

        Sprite jumpSprite =
            CreateOrUpdateJumpSprite();

        Sprite sprintSprite =
            CreateOrUpdateSprintSprite();

        GameObject sprintIndicator =
            StyleJoystick(
                canvas,
                circleSprite,
                sprintSprite
            );

        MobileActionButton jumpButton =
            CreateOrUpdateJumpButton(
                canvas,
                jumpSprite
            );

        controller.SetJumpButton(
            jumpButton
        );

        SerializedObject controllerSettings =
            new SerializedObject(controller);

        controllerSettings
            .FindProperty("mobileSprintMagnitude")
            .floatValue = 0.98f;

        controllerSettings
            .FindProperty("mobileSprintForward")
            .floatValue = 0.88f;

        controllerSettings
            .FindProperty("mobileSprintHoldTime")
            .floatValue = 0.18f;

        controllerSettings
            .FindProperty("mobileSprintReleaseMagnitude")
            .floatValue = 0.82f;

        controllerSettings
            .FindProperty("mobileSprintReleaseForward")
            .floatValue = 0.68f;

        controllerSettings.ApplyModifiedProperties();

        EditorUtility.SetDirty(
            controller
        );

        EnsureTouchLayering(
            canvas,
            jumpButton
        );

        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject =
            jumpButton.gameObject;

        EditorUtility.DisplayDialog(
            "Movement Polish Ready",
            "Added the mobile Jump/Swim-Up button, circular control visuals, and wired the player.\n\nMobile sprint is automatic when the joystick is pushed strongly forward. On PC, use Space to jump and Left Shift to sprint.",
            "OK"
        );
    }

    private static void EnsureFolders()
    {
        EnsureFolder(
            "Assets/_Game",
            "UI"
        );

        EnsureFolder(
            UiRoot,
            "Generated"
        );
    }

    private static void EnsureFolder(
        string parent,
        string name)
    {
        string path =
            parent + "/" + name;

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(
                parent,
                name
            );
        }
    }

    private static Sprite CreateOrUpdateCircleSprite()
    {
        const int size = 128;

        Texture2D texture =
            new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false
            );

        Color[] pixels =
            new Color[size * size];

        Vector2 center =
            new Vector2(
                (size - 1) * 0.5f,
                (size - 1) * 0.5f
            );

        float radius =
            size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance =
                    Vector2.Distance(
                        new Vector2(x, y),
                        center
                    );

                float edge =
                    Mathf.InverseLerp(
                        radius - 2.5f,
                        radius + 0.5f,
                        distance
                    );

                float alpha =
                    1f -
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        edge
                    );

                pixels[y * size + x] =
                    new Color(
                        1f,
                        1f,
                        1f,
                        alpha
                    );
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        WriteSpritePng(
            texture,
            CircleSpritePath
        );

        Object.DestroyImmediate(
            texture
        );

        return AssetDatabase
            .LoadAssetAtPath<Sprite>(
                CircleSpritePath
            );
    }

    private static Sprite CreateOrUpdateJumpSprite()
    {
        const int size = 160;

        Texture2D texture =
            new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false
            );

        Color[] pixels =
            new Color[size * size];

        Vector2 center =
            new Vector2(
                (size - 1) * 0.5f,
                (size - 1) * 0.5f
            );

        float radius =
            size * 0.47f;

        Color circle =
            new Color(
                0.04f,
                0.11f,
                0.16f,
                0.62f
            );

        Color arrow =
            new Color(
                0.88f,
                0.96f,
                1f,
                0.96f
            );

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p =
                    new Vector2(
                        x,
                        y
                    );

                float distance =
                    Vector2.Distance(
                        p,
                        center
                    );

                float edge =
                    Mathf.InverseLerp(
                        radius - 3f,
                        radius + 0.5f,
                        distance
                    );

                float edgeAlpha =
                    1f -
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        edge
                    );

                Color color =
                    circle;

                color.a *= edgeAlpha;

                float nx =
                    (x - center.x) /
                    radius;

                float ny =
                    (y - center.y) /
                    radius;

                bool stem =
                    Mathf.Abs(nx) < 0.10f &&
                    ny > -0.34f &&
                    ny < 0.18f;

                bool arrowHead =
                    ny >= 0.05f &&
                    ny <= 0.45f &&
                    Mathf.Abs(nx) <
                    Mathf.Lerp(
                        0.34f,
                        0.02f,
                        Mathf.InverseLerp(
                            0.05f,
                            0.45f,
                            ny
                        )
                    );

                if ((stem || arrowHead) &&
                    edgeAlpha > 0.5f)
                {
                    color = arrow;
                }

                pixels[y * size + x] =
                    color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        WriteSpritePng(
            texture,
            JumpSpritePath
        );

        Object.DestroyImmediate(
            texture
        );

        return AssetDatabase
            .LoadAssetAtPath<Sprite>(
                JumpSpritePath
            );
    }

    private static Sprite CreateOrUpdateSprintSprite()
    {
        const int width = 160;
        const int height = 100;

        Texture2D texture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false
            );

        Color[] pixels =
            new Color[width * height];

        Color arrow =
            new Color(
                0.70f,
                0.96f,
                1f,
                1f
            );

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx =
                    (x - width * 0.5f) /
                    (width * 0.5f);

                float ny =
                    (y - height * 0.5f) /
                    (height * 0.5f);

                bool lowerChevron =
                    ny > -0.55f &&
                    ny < 0.05f &&
                    Mathf.Abs(
                        Mathf.Abs(nx) -
                        (ny + 0.55f) * 0.58f
                    ) < 0.10f;

                bool upperChevron =
                    ny > -0.05f &&
                    ny < 0.55f &&
                    Mathf.Abs(
                        Mathf.Abs(nx) -
                        (ny + 0.05f) * 0.58f
                    ) < 0.10f;

                pixels[y * width + x] =
                    (lowerChevron || upperChevron)
                        ? arrow
                        : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        WriteSpritePng(
            texture,
            SprintSpritePath
        );

        Object.DestroyImmediate(texture);

        return AssetDatabase
            .LoadAssetAtPath<Sprite>(
                SprintSpritePath
            );
    }

    private static void WriteSpritePng(
        Texture2D texture,
        string assetPath)
    {
        string absolutePath =
            Path.GetFullPath(
                assetPath
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                absolutePath
            )
        );

        File.WriteAllBytes(
            absolutePath,
            texture.EncodeToPNG()
        );

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions
                .ForceUpdate
        );

        TextureImporter importer =
            AssetImporter
                .GetAtPath(assetPath)
            as TextureImporter;

        if (importer == null)
            return;

        importer.textureType =
            TextureImporterType.Sprite;

        importer.spriteImportMode =
            SpriteImportMode.Single;

        importer.alphaIsTransparency =
            true;

        importer.mipmapEnabled =
            false;

        importer.filterMode =
            FilterMode.Bilinear;

        importer.SaveAndReimport();
    }

    private static GameObject StyleJoystick(
        Canvas canvas,
        Sprite circleSprite,
        Sprite sprintSprite)
    {
        MobileJoystick joystick =
            Object.FindFirstObjectByType<MobileJoystick>();

        if (joystick == null ||
            circleSprite == null)
        {
            return null;
        }

        RectTransform backgroundRect =
            joystick.transform
                as RectTransform;

        if (backgroundRect != null)
        {
            backgroundRect.anchorMin =
                new Vector2(0f, 0f);

            backgroundRect.anchorMax =
                new Vector2(0f, 0f);

            backgroundRect.pivot =
                new Vector2(0.5f, 0.5f);

            backgroundRect.sizeDelta =
                new Vector2(350f, 350f);

            backgroundRect.anchoredPosition =
                new Vector2(250f, 250f);
        }

        Image background =
            joystick.GetComponent<Image>();

        if (background != null)
        {
            Undo.RecordObject(
                background,
                "Style Joystick"
            );

            background.sprite =
                circleSprite;

            background.color =
                new Color(
                    0.05f,
                    0.10f,
                    0.14f,
                    0.42f
                );

            background.type =
                Image.Type.Simple;

            background.raycastTarget =
                true;

            EditorUtility.SetDirty(
                background
            );
        }

        Transform handleTransform =
            joystick.transform.Find(
                "JoystickHandle"
            );

        if (handleTransform == null &&
            joystick.transform.childCount > 0)
        {
            handleTransform =
                joystick.transform.GetChild(0);
        }

        if (handleTransform == null)
            return null;

        Image handle =
            handleTransform
                .GetComponent<Image>();

        if (handle == null)
            return null;

        RectTransform handleRect =
            handleTransform as RectTransform;

        if (handleRect != null)
        {
            handleRect.sizeDelta =
                new Vector2(120f, 120f);

            handleRect.anchoredPosition =
                Vector2.zero;
        }

        Undo.RecordObject(
            handle,
            "Style Joystick Handle"
        );

        handle.sprite =
            circleSprite;

        handle.color =
            new Color(
                0.76f,
                0.91f,
                1f,
                0.88f
            );

        handle.type =
            Image.Type.Simple;

        handle.raycastTarget =
            false;

        EditorUtility.SetDirty(handle);

        GameObject sprintIndicator =
            CreateOrUpdateSprintIndicator(
                canvas,
                sprintSprite
            );

        joystick.ConfigureVisuals(
            background,
            handle,
            sprintIndicator
        );

        EditorUtility.SetDirty(
            joystick
        );

        return sprintIndicator;
    }

    private static GameObject CreateOrUpdateSprintIndicator(
        Canvas canvas,
        Sprite sprintSprite)
    {
        Transform existing =
            canvas.transform.Find(
                "SprintIndicator"
            );

        GameObject indicator;

        if (existing == null)
        {
            indicator =
                new GameObject(
                    "SprintIndicator",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );

            Undo.RegisterCreatedObjectUndo(
                indicator,
                "Create Sprint Indicator"
            );

            indicator.transform.SetParent(
                canvas.transform,
                false
            );
        }
        else
        {
            indicator = existing.gameObject;

            if (indicator.GetComponent<Image>() == null)
            {
                Undo.AddComponent<Image>(
                    indicator
                );
            }
        }

        RectTransform rect =
            indicator.GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0f, 0f);

        rect.anchorMax =
            new Vector2(0f, 0f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.sizeDelta =
            new Vector2(120f, 80f);

        rect.anchoredPosition =
            new Vector2(250f, 465f);

        Image image =
            indicator.GetComponent<Image>();

        image.sprite = sprintSprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        indicator.SetActive(false);

        return indicator;
    }

    private static MobileActionButton CreateOrUpdateJumpButton(
        Canvas canvas,
        Sprite jumpSprite)
    {
        Transform existing =
            canvas.transform.Find(
                "JumpButton"
            );

        GameObject buttonObject;

        if (existing == null)
        {
            buttonObject =
                new GameObject(
                    "JumpButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(MobileActionButton)
                );

            Undo.RegisterCreatedObjectUndo(
                buttonObject,
                "Create Jump Button"
            );

            buttonObject.transform.SetParent(
                canvas.transform,
                false
            );
        }
        else
        {
            buttonObject =
                existing.gameObject;

            if (buttonObject
                    .GetComponent<Image>() ==
                null)
            {
                Undo.AddComponent<Image>(
                    buttonObject
                );
            }

            if (buttonObject
                    .GetComponent<MobileActionButton>() ==
                null)
            {
                Undo.AddComponent<MobileActionButton>(
                    buttonObject
                );
            }
        }

        RectTransform rect =
            buttonObject
                .GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(
                1f,
                0f
            );

        rect.anchorMax =
            new Vector2(
                1f,
                0f
            );

        rect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );

        rect.sizeDelta =
            new Vector2(
                170f,
                170f
            );

        rect.anchoredPosition =
            new Vector2(
                -180f,
                190f
            );

        Image image =
            buttonObject
                .GetComponent<Image>();

        image.sprite =
            jumpSprite;

        image.color =
            Color.white;

        image.raycastTarget =
            true;

        image.preserveAspect =
            true;

        buttonObject.transform
            .SetAsLastSibling();

        EditorUtility.SetDirty(
            buttonObject
        );

        return buttonObject
            .GetComponent<MobileActionButton>();
    }

    private static void EnsureTouchLayering(
        Canvas canvas,
        MobileActionButton jumpButton)
    {
        TouchLookArea touchLook =
            Object.FindFirstObjectByType<TouchLookArea>();

        if (touchLook != null)
        {
            RectTransform rect =
                touchLook.transform
                    as RectTransform;

            if (rect != null)
            {
                rect.anchorMin =
                    Vector2.zero;

                rect.anchorMax =
                    Vector2.one;

                rect.offsetMin =
                    Vector2.zero;

                rect.offsetMax =
                    Vector2.zero;
            }

            touchLook.transform
                .SetAsFirstSibling();
        }

        MobileJoystick joystick =
            Object.FindFirstObjectByType<MobileJoystick>();

        if (joystick != null)
        {
            joystick.transform
                .SetAsLastSibling();
        }

        if (jumpButton != null)
        {
            jumpButton.transform
                .SetAsLastSibling();
        }
    }
}
