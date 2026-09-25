using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using EnCare;
using EnCare.Editor;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// One-click editor tool that builds the full EnCare Cleanup office scene.
/// Run via: EnCare ▶ Build Cleanup Scene
/// </summary>
public static class CleanupSceneBuilder
{
    // ─── Paths ────────────────────────────────────────────────────────────────
    const string k_ScenePath  = "Assets/Scenes/CleanupScene.unity";
    const string k_PrefabRoot = "Assets/EnCare/Prefabs";
    const string k_TrashRoot  = "Assets/EnCare/Prefabs/Trash";
    const string k_BackyardTrashRoot = "Assets/EnCare/Prefabs/BackyardTrash";
    const string k_MatRoot    = "Assets/EnCare/Materials";
    const string k_XRPrefabRig      = "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string k_XRPrefabTemplate = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";

    // ─── Entry Point ──────────────────────────────────────────────────────────
    [MenuItem("EnCare/\u2699  Build Cleanup Scene", false, 0)]
    public static void BuildScene()
    {
        if (!EditorUtility.DisplayDialog("Build Cleanup Scene",
            "Creates Assets/Scenes/CleanupScene.unity.\n" +
            "Save your current scene before proceeding.\n\nBuild?",
            "Build It", "Cancel")) return;

        // Directories
        EnsureDir(k_PrefabRoot);
        EnsureDir(k_TrashRoot);
        EnsureDir(k_MatRoot);

        // Fresh empty scene (prefabs will be temporarily created here then saved)
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Assets (saved to disk) ────────────────────────────────────────────
        Material     outlineMat   = GetOrCreateOutlineMaterial();
        GameObject[] trashPrefabs = CreateTrashPrefabs(outlineMat);
        GameObject   basketPrefab = CreateBasketPrefab();

        // ── Scene content ─────────────────────────────────────────────────────
        SetupLighting();
        CreateOfficeEnvironment();
        GameObject xrOrigin = PlaceXROrigin();

        var spawnParent = new GameObject("TrashSpawnPoints");
        Transform[] spawnPoints = CreateSpawnPoints(spawnParent.transform);

        // CleanupManager
        var managerGO = new GameObject("CleanupManager");
        var mission   = managerGO.AddComponent<CleanupMission>();
        var spawner   = managerGO.AddComponent<RandomTrashSpawner>();
        var grabDist  = managerGO.AddComponent<GrabDistanceSettings>();
        ConfigureMission(mission, spawner);
        ConfigureSpawner(spawner, trashPrefabs, spawnPoints, new Color(0.1f, 1f, 0.8f, 1f));

        // Event bridge
        var receiverGO = new GameObject("CleanupEventReceiver");
        var receiver   = receiverGO.AddComponent<CleanupEventReceiver>();

        // Handover controller — only RestartLevel() is used; no Timeline
        var handoverGO = new GameObject("HandoverController");
        var handover   = handoverGO.AddComponent<HandoverCutscene>();

        // UI panels
        GameObject successPanel = CreateSuccessPanel();
        GameObject failurePanel = CreateFailurePanel(handover);

        // Wire receiver
        var rcvSO = new SerializedObject(receiver);
        rcvSO.FindProperty("successPanel").objectReferenceValue = successPanel;
        rcvSO.FindProperty("failurePanel").objectReferenceValue = failurePanel;
        rcvSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire mission events → receiver
        UnityEventTools.AddVoidPersistentListener(mission.onCompleted, receiver.OnSuccess);
        UnityEventTools.AddVoidPersistentListener(mission.onFailed,    receiver.OnFailure);
        EditorUtility.SetDirty(mission);

        // Wrist HUD (automatically parented to Left Controller)
        CreateWristHUD(mission, xrOrigin);

        // Controller Help Tooltips (Gaze-activated for Left & Right Controllers)
        SetupControllerHelpUIs(xrOrigin);

        // Basket in scene (mission wired post-instantiation)
        PlaceBasketInScene(basketPrefab, mission);

        // ── Save ──────────────────────────────────────────────────────────────
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(activeScene, k_ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[EnCare] CleanupScene built → " + k_ScenePath);
        EditorUtility.DisplayDialog("\u2705 CleanupScene Created",
            "Saved to Assets/Scenes/CleanupScene.unity\n\n" +
            "VR Setup Complete:\n" +
            "• Standard VR XR Origin (XR Rig) + XRInteractionManager configured\n" +
            "• WristHUD automatically attached to Left Controller\n" +
            "• Stationed basket & grabbable e-waste ready\n\n" +
            "Remaining steps:\n" +
            "1. Add scene to Build Profiles (File → Build Profiles)\n" +
            "2. Put on your VR headset and hit Play!", "Got it!");
    }

    // ─── Outline Material ────────────────────────────────────────────────────
    static Material GetOrCreateOutlineMaterial()
    {
        const string path = "Assets/EnCare/Materials/TrashOutline_Mat.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("EnCare/TrashOutlineURP");
        if (shader == null)
        {
            Debug.LogWarning("[EnCare] TrashOutlineURP not found yet — using URP Lit as fallback. Re-run after Unity compiles the shader.");
            shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        }

        var mat = new Material(shader);
        if (mat.HasProperty("_OutlineColor"))
            mat.SetColor("_OutlineColor", new Color(0.35f * 3f, 0.86f * 3f, 0.78f * 3f, 1f)); // HDR cyan
        if (mat.HasProperty("_OutlineWidth"))
            mat.SetFloat("_OutlineWidth", 0.006f);

        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Material GetOrCreateBackyardOutlineMaterial(Color glowColor)
    {
        const string path = "Assets/EnCare/Materials/TrashOutline_Backyard_Mat.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            if (existing.HasProperty("_OutlineColor"))
                existing.SetColor("_OutlineColor", new Color(glowColor.r * 3f, glowColor.g * 3f, glowColor.b * 3f, 1f));
            return existing;
        }

        var shader = Shader.Find("EnCare/TrashOutlineURP");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        }

        var mat = new Material(shader);
        if (mat.HasProperty("_OutlineColor"))
            mat.SetColor("_OutlineColor", new Color(glowColor.r * 3f, glowColor.g * 3f, glowColor.b * 3f, 1f));
        if (mat.HasProperty("_OutlineWidth"))
            mat.SetFloat("_OutlineWidth", 0.006f);

        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ─── Trash Prefabs (Office E-Waste) ──────────────────────────────────────
    static GameObject[] CreateTrashPrefabs(Material outlineMat)
    {
        var defs = new (string name, string display, Vector3 scale, Color color)[]
        {
            ("Trash_CircuitBoard", "Circuit Board", new Vector3(0.20f, 0.02f, 0.15f), new Color(0.10f, 0.40f, 0.10f)),
            ("Trash_Monitor",      "Old Monitor",   new Vector3(0.35f, 0.25f, 0.05f), new Color(0.20f, 0.20f, 0.20f)),
            ("Trash_Keyboard",     "Keyboard",      new Vector3(0.38f, 0.02f, 0.13f), new Color(0.70f, 0.70f, 0.70f)),
            ("Trash_HardDrive",    "Hard Drive",    new Vector3(0.10f, 0.02f, 0.15f), new Color(0.40f, 0.40f, 0.50f)),
            ("Trash_Phone",        "Old Phone",     new Vector3(0.07f, 0.01f, 0.14f), new Color(0.15f, 0.15f, 0.15f)),
        };

        var prefabs = new GameObject[defs.Length];
        Color cyanGlow = new Color(0.1f, 1f, 0.8f, 1f);
        for (int i = 0; i < defs.Length; i++)
            prefabs[i] = CreateSingleTrashPrefab(k_TrashRoot, defs[i].name, defs[i].display,
                                                  defs[i].scale, defs[i].color, outlineMat, cyanGlow);
        return prefabs;
    }

    // ─── Trash Prefabs (Backyard Clinical Waste) ────────────────────────────
    static GameObject[] CreateClinicalTrashPrefabs(Material outlineMat, Color glowColor)
    {
        EnsureDir(k_BackyardTrashRoot);
        var defs = new (string name, string display, Vector3 scale, Color color)[]
        {
            ("Trash_BiohazardBin",     "Biohazard Sharps Bin", new Vector3(0.18f, 0.24f, 0.14f), new Color(0.96f, 0.78f, 0.08f)), // Yellow biohazard bin
            ("Trash_MedicineBottle",   "Medicine Bottle",      new Vector3(0.08f, 0.15f, 0.08f), new Color(0.78f, 0.42f, 0.10f)), // Amber pill container
            ("Trash_MedicalSyringe",   "Medical Syringe",      new Vector3(0.04f, 0.04f, 0.20f), new Color(0.90f, 0.94f, 0.98f)), // Syringe
            ("Trash_IVFluidBag",       "IV Fluid Bag",         new Vector3(0.16f, 0.22f, 0.04f), new Color(0.85f, 0.92f, 0.95f)), // IV drip bag
            ("Trash_FirstAidPack",     "First Aid Gauze Box",  new Vector3(0.20f, 0.08f, 0.15f), new Color(0.95f, 0.95f, 0.95f)), // Medical box
            ("Trash_ClinicalWasteBag", "Clinical Waste Bag",   new Vector3(0.24f, 0.24f, 0.24f), new Color(0.98f, 0.88f, 0.15f)), // Yellow hazard bag
        };

        var prefabs = new GameObject[defs.Length];
        for (int i = 0; i < defs.Length; i++)
            prefabs[i] = CreateSingleTrashPrefab(k_BackyardTrashRoot, defs[i].name, defs[i].display,
                                                  defs[i].scale, defs[i].color, outlineMat, glowColor);
        return prefabs;
    }

    static GameObject CreateSingleTrashPrefab(string rootFolder, string prefabName, string displayName,
                                               Vector3 modelScale, Color modelColor, Material outlineMat, Color glowColor)
    {
        string path = $"{rootFolder}/{prefabName}.prefab";

        var root = new GameObject(prefabName);

        // Rigidbody
        var rb = root.AddComponent<Rigidbody>();
        rb.mass                   = 0.5f;
        rb.useGravity             = true;
        rb.isKinematic            = false;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Collider on root (padded slightly for forgiving VR hand grab)
        var col  = root.AddComponent<BoxCollider>();
        col.size = new Vector3(
            Mathf.Max(modelScale.x, 0.10f),
            Mathf.Max(modelScale.y, 0.05f),
            Mathf.Max(modelScale.z, 0.10f)
        );

        // XR Grab Interactable
        var grab = root.AddComponent<XRGrabInteractable>();
        grab.selectMode       = InteractableSelectMode.Single;
        grab.movementType     = XRGrabInteractable.MovementType.VelocityTracking;
        grab.trackPosition    = true;
        grab.trackRotation    = true;
        grab.useDynamicAttach = true;

        // TrashItem
        var item   = root.AddComponent<TrashItem>();
        var itemSO = new SerializedObject(item);
        itemSO.FindProperty("displayName").stringValue = displayName;

        // Model child (primitive placeholder — easily replaced with custom 3D model later)
        var modelGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        modelGO.name = "Model";
        modelGO.transform.SetParent(root.transform, false);
        modelGO.transform.localScale = modelScale;
        Object.DestroyImmediate(modelGO.GetComponent<BoxCollider>()); // physics on root
        var shader   = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var modelMat = new Material(shader) { color = modelColor };
        modelGO.GetComponent<MeshRenderer>().sharedMaterial = modelMat;

        // GripPoint — where the controller attaches
        var gripGO = new GameObject("GripPoint");
        gripGO.transform.SetParent(root.transform, false);
        grab.attachTransform = gripGO.transform;

        // DepositPoint — near item centre, used by GarbageCollector
        var depositGO = new GameObject("DepositPoint");
        depositGO.transform.SetParent(root.transform, false);
        depositGO.transform.localPosition = Vector3.zero;

        itemSO.FindProperty("depositPoint").objectReferenceValue = depositGO.transform;
        itemSO.ApplyModifiedPropertiesWithoutUndo();

        // TrashGlow — inverted-hull outline and pulse
        var glow   = root.AddComponent<TrashGlow>();
        var glowSO = new SerializedObject(glow);
        glowSO.FindProperty("outlineMaterial").objectReferenceValue = outlineMat;
        var renderers = glowSO.FindProperty("sourceRenderers");
        renderers.arraySize = 1;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = modelGO.GetComponent<MeshRenderer>();
        glowSO.FindProperty("glowColor").colorValue          = glowColor;
        glowSO.FindProperty("intensity").floatValue          = 3f;
        glowSO.FindProperty("width").floatValue              = 0.006f;
        glowSO.FindProperty("smoothOutlineNormals").boolValue = false;
        glowSO.ApplyModifiedPropertiesWithoutUndo();

        // Save & clean up temp scene object
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    public static GameObject CreateBasketPrefab()
    {
        return BasketPrefabBuilder.RebuildBasketPrefab();
    }

    // ─── Lighting ────────────────────────────────────────────────────────────
    static void SetupLighting()
    {
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.38f, 0.38f, 0.45f);

        var lightGO = new GameObject("Directional Light");
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light     = lightGO.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1.1f;
        light.color     = new Color(1f, 0.97f, 0.92f);
        light.shadows   = LightShadows.Soft;
    }

    // ─── Office Environment (placeholder primitives) ──────────────────────────
    static void CreateOfficeEnvironment()
    {
        var env = new GameObject("OfficeEnvironment [placeholder — replace with real assets]");

        var shader   = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var floorMat = new Material(shader) { color = new Color(0.62f, 0.60f, 0.66f) };
        var wallMat  = new Material(shader) { color = new Color(0.88f, 0.87f, 0.90f) };
        var deskMat  = new Material(shader) { color = new Color(0.56f, 0.42f, 0.26f) };

        // Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(env.transform);
        floor.transform.localScale = new Vector3(3f, 1f, 3f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        // Ceiling (visual only — no collider)
        var ceil = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ceil.name = "Ceiling";
        ceil.transform.SetParent(env.transform);
        ceil.transform.position = new Vector3(0f, 3f, 0f);
        ceil.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
        ceil.transform.localScale = new Vector3(3f, 1f, 3f);
        ceil.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        Object.DestroyImmediate(ceil.GetComponent<MeshCollider>());

        // Walls
        AddWallCube(env.transform, "Wall_Back",  new Vector3( 0f,   1.5f, -5f), new Vector3(10f, 3f, 0.2f), wallMat);
        AddWallCube(env.transform, "Wall_Front", new Vector3( 0f,   1.5f,  5f), new Vector3(10f, 3f, 0.2f), wallMat);
        AddWallCube(env.transform, "Wall_Left",  new Vector3(-5f,   1.5f,  0f), new Vector3(0.2f, 3f, 10f), wallMat);
        AddWallCube(env.transform, "Wall_Right", new Vector3( 5f,   1.5f,  0f), new Vector3(0.2f, 3f, 10f), wallMat);

        // Desks at standard 0.75 m height
        AddDesk(env.transform, new Vector3(-2f, 0f, -2f), deskMat);
        AddDesk(env.transform, new Vector3(-2f, 0f,  1f), deskMat);
        AddDesk(env.transform, new Vector3( 2f, 0f, -2f), deskMat);
    }

    static void AddWallCube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.position   = pos;
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void AddDesk(Transform parent, Vector3 origin, Material mat)
    {
        const float deskTop   = 0.75f;  // height of desk surface in world Y
        const float deskThick = 0.05f;
        const float legH      = deskTop - deskThick; // 0.70 m

        // Surface
        var surf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surf.name = "Desk_Surface";
        surf.transform.SetParent(parent, false);
        surf.transform.position   = origin + new Vector3(0f, deskTop - deskThick * 0.5f, 0f);
        surf.transform.localScale = new Vector3(1.4f, deskThick, 0.7f);
        surf.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Legs (visual only)
        float[] xs = { -0.65f,  0.65f, -0.65f,  0.65f };
        float[] zs = { -0.32f, -0.32f,  0.32f,  0.32f };
        for (int i = 0; i < 4; i++)
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = "Desk_Leg";
            leg.transform.SetParent(parent, false);
            leg.transform.position   = origin + new Vector3(xs[i], legH * 0.5f, zs[i]);
            leg.transform.localScale = new Vector3(0.05f, legH, 0.05f);
            leg.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(leg.GetComponent<BoxCollider>());
        }
    }

    // ─── XR Origin (Standard VR Rig) ─────────────────────────────────────────
    static GameObject PlaceXROrigin()
    {
        // 1. Ensure XRInteractionManager exists in the scene (required for VR interactors & grab interactables)
        if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
        {
            var mgrGO = new GameObject("XR Interaction Manager");
            mgrGO.AddComponent<XRInteractionManager>();
            Debug.Log("[EnCare] Added XRInteractionManager to scene.");
        }

        // 2. Load the official VR XR Origin (XR Rig)
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_XRPrefabRig)
                  ?? AssetDatabase.LoadAssetAtPath<GameObject>(k_XRPrefabTemplate);

        if (prefab == null)
        {
            Debug.LogWarning("[EnCare] XR Origin prefab not found. Please add XR Origin to the scene manually.");
            var fallback = new GameObject("XR Origin [MISSING — add manually]");
            var camGO    = new GameObject("Main Camera");
            camGO.transform.SetParent(fallback.transform);
            camGO.AddComponent<Camera>().tag = "MainCamera";
            return fallback;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        return instance;
    }

    // ─── Spawn Points ─────────────────────────────────────────────────────────
    static Transform[] CreateSpawnPoints(Transform parent)
    {
        // 15 positions spread across desk surfaces (y≈0.85) and floor (y≈0.05)
        var positions = new Vector3[]
        {
            new Vector3(-2.5f, 0.85f, -2.2f),  // Desk_1 left
            new Vector3(-1.6f, 0.85f, -1.8f),  // Desk_1 right
            new Vector3(-2.0f, 0.85f, -2.0f),  // Desk_1 centre
            new Vector3(-2.5f, 0.85f,  0.8f),  // Desk_2 left
            new Vector3(-1.6f, 0.85f,  1.2f),  // Desk_2 right
            new Vector3(-2.0f, 0.85f,  1.0f),  // Desk_2 centre
            new Vector3( 1.5f, 0.85f, -2.2f),  // Desk_3 left
            new Vector3( 2.5f, 0.85f, -1.8f),  // Desk_3 right
            new Vector3( 2.0f, 0.85f, -2.0f),  // Desk_3 centre
            new Vector3( 0.0f, 0.05f,  0.0f),  // Floor centre
            new Vector3(-1.0f, 0.05f,  2.5f),  // Floor back-left
            new Vector3( 1.0f, 0.05f, -0.5f),  // Floor mid-right
            new Vector3( 3.5f, 0.05f,  1.0f),  // Floor far-right
            new Vector3(-3.5f, 0.05f, -1.0f),  // Floor far-left
            new Vector3( 0.0f, 0.05f,  2.0f),  // Floor doorway
        };

        var points = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var go = new GameObject($"Spawn_{(i + 1):00}");
            go.transform.SetParent(parent, false);
            go.transform.position = positions[i];
            points[i] = go.transform;
        }
        return points;
    }

    // ─── Mission / Spawner configuration ─────────────────────────────────────
    static void ConfigureMission(CleanupMission mission, RandomTrashSpawner spawner)
    {
        var so = new SerializedObject(mission);
        so.FindProperty("targetCount").intValue         = 10;
        so.FindProperty("timeLimitSeconds").floatValue  = 120f;
        so.FindProperty("spawner").objectReferenceValue = spawner;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigureSpawner(RandomTrashSpawner spawner, GameObject[] trashPrefabs, Transform[] spawnPoints, Color glowColor)
    {
        var so   = new SerializedObject(spawner);
        var pool = so.FindProperty("prefabPool");
        pool.arraySize = trashPrefabs.Length;
        for (int i = 0; i < trashPrefabs.Length; i++)
            pool.GetArrayElementAtIndex(i).objectReferenceValue = trashPrefabs[i].GetComponent<TrashItem>();

        var pts = so.FindProperty("spawnPoints");
        pts.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++)
            pts.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];

        so.FindProperty("spawnCount").intValue         = 10;
        so.FindProperty("overrideGlowColor").boolValue = true;
        so.FindProperty("levelGlowColor").colorValue   = glowColor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void PlaceBasketInScene(GameObject basketPrefab, CleanupMission mission)
    {
        var basket = (GameObject)PrefabUtility.InstantiatePrefab(basketPrefab);
        basket.transform.position = new Vector3(0f, 0.13f, 0.5f); // sits on the floor

        var collector = basket.GetComponentInChildren<GarbageCollector>();
        if (collector != null)
        {
            var so = new SerializedObject(collector);
            so.FindProperty("mission").objectReferenceValue = mission;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ─── UI: Success Panel ───────────────────────────────────────────────────
    static GameObject CreateSuccessPanel()
    {
        var panel = CreateWorldCanvas("SuccessPanel", new Vector3(0f, 1.6f, 2f), new Vector2(600f, 380f));
        panel.AddComponent<LazyFollowView>();
        panel.SetActive(false);

        AddPanelBg(panel, new Color(0.04f, 0.18f, 0.08f, 0.95f)); // dark green

        MakeTMP(panel.transform, "Title",    "Mission Complete!",
            new Vector2(0, 90),   new Vector2(560, 130), 52, new Color(0.3f, 1f, 0.55f));
        MakeTMP(panel.transform, "Body",     "All e-waste collected.\nWell done!",
            new Vector2(0, -20),  new Vector2(540, 100), 30, Color.white);
        MakeTMP(panel.transform, "Hint",     "Remove your headset to continue.",
            new Vector2(0, -140), new Vector2(540,  50), 20, new Color(0.7f, 0.9f, 0.8f));

        return panel;
    }

    // ─── UI: Failure Panel ───────────────────────────────────────────────────
    static GameObject CreateFailurePanel(HandoverCutscene handover)
    {
        var panel = CreateWorldCanvas("FailurePanel", new Vector3(0f, 1.6f, 2f), new Vector2(600f, 380f));
        panel.AddComponent<LazyFollowView>();
        panel.SetActive(false);

        AddPanelBg(panel, new Color(0.18f, 0.04f, 0.04f, 0.95f)); // dark red

        MakeTMP(panel.transform, "Title", "Time's Up!",
            new Vector2(0, 90),  new Vector2(560, 120), 54, new Color(1f, 0.4f, 0.4f));
        MakeTMP(panel.transform, "Body",  "You ran out of time.\nTry to be quicker!",
            new Vector2(0, -20), new Vector2(540, 90),  30, Color.white);

        // Retry button
        var btnGO  = new GameObject("RetryButton");
        btnGO.transform.SetParent(panel.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.85f, 0.20f, 0.20f, 1f);
        var btnRT  = btnGO.GetComponent<RectTransform>();
        btnRT.anchoredPosition = new Vector2(0, -145);
        btnRT.sizeDelta        = new Vector2(220, 65);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        MakeTMP(btnGO.transform, "Label", "Try Again", Vector2.zero, new Vector2(200, 55), 30, Color.white);

        if (handover != null)
            UnityEventTools.AddVoidPersistentListener(btn.onClick, handover.RestartLevel);

        return panel;
    }

    // ─── UI: Wrist HUD ───────────────────────────────────────────────────────
    static void CreateWristHUD(CleanupMission mission, GameObject xrOrigin)
    {
        // 300x220 at 0.0003 scale = 9cm x 6.6cm on the wrist
        var canvasGO = CreateWorldCanvas("WristHUD", new Vector3(-0.5f, 1.2f, 0f), new Vector2(300f, 220f));
        canvasGO.transform.localScale = Vector3.one * 0.0003f;

        // Auto-parent to Left Controller if available in the scene
        if (xrOrigin != null)
        {
            Transform leftCtrl = null;
            foreach (var t in xrOrigin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Left Controller" || t.name == "LeftHand Controller")
                {
                    leftCtrl = t;
                    break;
                }
            }

            if (leftCtrl != null)
            {
                canvasGO.transform.SetParent(leftCtrl, false);
                canvasGO.transform.localPosition = new Vector3(0f, 0.05f, -0.05f);
                canvasGO.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
                canvasGO.transform.localScale    = Vector3.one * 0.0003f;
                Debug.Log("[EnCare] WristHUD automatically attached to Left Controller.");
            }
        }

        // Remove GraphicRaycaster — display only, no VR pointer interaction needed
        Object.DestroyImmediate(canvasGO.GetComponent<GraphicRaycaster>());

        // Static visual background
        AddPanelBg(canvasGO, new Color(0.05f, 0.05f, 0.18f, 0.88f));

        // Progress bar background (static)
        var bgBar  = new GameObject("ProgressBg");
        bgBar.transform.SetParent(canvasGO.transform, false);
        var bgImg  = bgBar.AddComponent<Image>();
        bgImg.color        = new Color(0.15f, 0.15f, 0.25f, 1f);
        bgImg.raycastTarget = false;
        var bgRT   = bgBar.GetComponent<RectTransform>();
        bgRT.anchoredPosition = new Vector2(0f, -100f);
        bgRT.sizeDelta        = new Vector2(260f, 14f);

        // Sub-canvas for dynamic elements to prevent rebuilding the static background canvas
        var dynamicGO = new GameObject("DynamicContent");
        dynamicGO.transform.SetParent(canvasGO.transform, false);
        dynamicGO.AddComponent<Canvas>();
        var dynamicRT = dynamicGO.GetComponent<RectTransform>();
        dynamicRT.anchorMin = Vector2.zero;
        dynamicRT.anchorMax = Vector2.one;
        dynamicRT.sizeDelta = Vector2.zero;

        var countGO  = MakeTMP(dynamicGO.transform, "CountText",  "Trash Collected\n0/10",  new Vector2(0,  65), new Vector2(280, 80), 16, Color.white);
        var timerGO  = MakeTMP(dynamicGO.transform, "TimerText",  "Time Remaining\n02:00",  new Vector2(0, -15), new Vector2(280, 60), 16, Color.white);
        var statusGO = MakeTMP(dynamicGO.transform, "StatusText", "Grab an item to start", new Vector2(0, -78), new Vector2(280, 40), 12, new Color(0.7f, 0.9f, 0.8f));

        // Progress fill (dynamic)
        var fillGO  = new GameObject("ProgressFill");
        fillGO.transform.SetParent(dynamicGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color        = new Color(0.1f, 0.9f, 0.7f, 1f);
        fillImg.type         = Image.Type.Filled;
        fillImg.fillMethod   = Image.FillMethod.Horizontal;
        fillImg.fillAmount   = 0f;
        fillImg.raycastTarget = false;
        var fillRT  = fillGO.GetComponent<RectTransform>();
        fillRT.pivot            = new Vector2(0f, 0.5f);
        fillRT.anchoredPosition = new Vector2(-130f, -100f);
        fillRT.sizeDelta        = new Vector2(260f, 14f);

        // CleanupHUD component
        var hud   = canvasGO.AddComponent<CleanupHUD>();
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("mission").objectReferenceValue      = mission;
        hudSO.FindProperty("countText").objectReferenceValue    = countGO.GetComponent<TMP_Text>();
        hudSO.FindProperty("timerText").objectReferenceValue    = timerGO.GetComponent<TMP_Text>();
        hudSO.FindProperty("statusText").objectReferenceValue   = statusGO.GetComponent<TMP_Text>();
        hudSO.FindProperty("progressFill").objectReferenceValue = fillImg;
        hudSO.ApplyModifiedPropertiesWithoutUndo();
    }

    // ─── UI: Controller Help Tooltips ────────────────────────────────────────
    static void SetupControllerHelpUIs(GameObject xrOrigin)
    {
        if (xrOrigin == null) return;

        Transform leftCtrl = null;
        Transform rightCtrl = null;
        foreach (var t in xrOrigin.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Left Controller" || t.name == "LeftHand Controller")
                leftCtrl = t;
            else if (t.name == "Right Controller" || t.name == "RightHand Controller")
                rightCtrl = t;
        }

        if (leftCtrl != null)
        {
            CreateControllerHelpUI(leftCtrl, ControllerHelpUI.ControllerHand.Left);
        }

        if (rightCtrl != null)
        {
            CreateControllerHelpUI(rightCtrl, ControllerHelpUI.ControllerHand.Right);
        }
    }

    public static GameObject CreateControllerHelpUI(Transform controllerTransform, ControllerHelpUI.ControllerHand hand)
    {
        if (controllerTransform == null) return null;
        var existing = controllerTransform.GetComponentInChildren<ControllerHelpUI>(true);
        if (existing != null) return existing.gameObject;

        string name = hand == ControllerHelpUI.ControllerHand.Left ? "LeftControllerHelpUI" : "RightControllerHelpUI";
        var canvasGO = new GameObject(name);
        canvasGO.transform.SetParent(controllerTransform, false);

        canvasGO.transform.localPosition = new Vector3(0f, 0.08f, 0.03f);
        canvasGO.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        canvasGO.transform.localScale    = Vector3.one * 0.00035f;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>();
        var canvasGroup = canvasGO.AddComponent<CanvasGroup>();

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(340f, 220f);

        AddPanelBg(canvasGO, new Color(0.04f, 0.08f, 0.16f, 0.90f));

        string title = hand == ControllerHelpUI.ControllerHand.Left ? "Left Hand" : "Right Hand";
        string body = hand == ControllerHelpUI.ControllerHand.Left
            ? "🕹️ <b>Thumbstick:</b> Walk / Move\n✊ <b>Grip:</b> Grab Trash & Basket\n👆 <b>Trigger:</b> Select / Interact"
            : "🕹️ <b>Thumbstick:</b> Snap Turn / Teleport\n✊ <b>Grip:</b> Grab Trash & Basket\n🗑️ <b>Basket:</b> Release inside to score";

        var titleGO = MakeTMP(canvasGO.transform, "TitleText", title,
            new Vector2(0f, 75f), new Vector2(300f, 45f), 18, new Color(0.2f, 0.9f, 1f));

        var bodyGO = MakeTMP(canvasGO.transform, "BodyText", body,
            new Vector2(0f, -15f), new Vector2(300f, 130f), 13, Color.white);

        var helpComp = canvasGO.AddComponent<ControllerHelpUI>();
        var so = new SerializedObject(helpComp);
        so.FindProperty("m_Hand").enumValueIndex = (int)hand;
        so.FindProperty("m_TitleText").stringValue = title;
        so.FindProperty("m_InstructionsText").stringValue = body;
        so.FindProperty("m_CanvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("m_TitleTMP").objectReferenceValue = titleGO.GetComponent<TMP_Text>();
        so.FindProperty("m_BodyTMP").objectReferenceValue = bodyGO.GetComponent<TMP_Text>();
        so.ApplyModifiedPropertiesWithoutUndo();

        return canvasGO;
    }

    // ─── Backyard Level Setup ──────────────────────────────────────────────
    [MenuItem("EnCare/\U0001F3E1 Setup Backyard Level (Lvl_Backyard)", false, 1)]
    public static void SetupBackyardLevel()
    {
        const string backyardScenePath = "Assets/Scenes/Lvl_Backyard.unity";
        var scene = EditorSceneManager.OpenScene(backyardScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("Error", "Could not open " + backyardScenePath, "OK");
            return;
        }

        InitializeSceneForEnCare(isBackyard: true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("\u2705 Backyard Level Initialized",
            "Assets/Scenes/Lvl_Backyard.unity has been fully configured for EnCare VR:\n\n" +
            "• XRInteractionManager + VR Camera resolution\n" +
            "• 15 Backyard Trash Spawn Points\n" +
            "• CleanupMission (10 items, 120s timer) + RandomTrashSpawner\n" +
            "• GrabDistanceSettings (Configurable grab distance)\n" +
            "• WristHUD on Left Controller\n" +
            "• Gaze-activated Controller Help Tooltips on both hands\n" +
            "• Pass/Fail Panels with smooth LazyFollowView\n" +
            "• Stationed & Grabbable Basket with GarbageCollector\n\n" +
            "You are ready to enter Play mode in VR!", "Awesome!");
    }

    [MenuItem("EnCare/\u2699 Initialize EnCare in Active Scene", false, 2)]
    public static void InitializeActiveScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        InitializeSceneForEnCare(isBackyard: activeScene.name.ToLower().Contains("backyard"));
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("\u2705 Active Scene Initialized",
            "Configured EnCare VR gameplay systems in " + activeScene.name, "Got it!");
    }

    static void InitializeSceneForEnCare(bool isBackyard)
    {
        // 1. Ensure Directories
        EnsureDir(k_PrefabRoot);
        EnsureDir(k_TrashRoot);
        EnsureDir(k_MatRoot);

        // 2. Prefabs & Materials
        Material outlineMat;
        GameObject[] trashPrefabs;
        Color levelGlowColor;

        if (isBackyard)
        {
            EnsureDir(k_BackyardTrashRoot);
            levelGlowColor = new Color(0.2f, 1.0f, 0.35f, 1f); // Vibrant Clinical Neon Green
            outlineMat = GetOrCreateBackyardOutlineMaterial(levelGlowColor);
            trashPrefabs = CreateClinicalTrashPrefabs(outlineMat, levelGlowColor);
        }
        else
        {
            levelGlowColor = new Color(0.1f, 1f, 0.8f, 1f); // Office Cyan
            outlineMat = GetOrCreateOutlineMaterial();
            trashPrefabs = CreateTrashPrefabs(outlineMat);
        }

        GameObject basketPrefab = null;
        if (!isBackyard)
        {
            basketPrefab = CreateBasketPrefab();
        }

        // 3. Ensure XR Interaction Manager
        if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
        {
            var mgrGO = new GameObject("XR Interaction Manager");
            mgrGO.AddComponent<XRInteractionManager>();
            Debug.Log("[EnCare] Added XRInteractionManager to scene.");
        }

        // 4. Find or place XR Origin
        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)")
                           ?? GameObject.Find("XR Origin")
                           ?? GameObject.Find("Complete XR Origin Set Up Variant");
        if (xrOrigin == null)
        {
            xrOrigin = PlaceXROrigin();
        }

        // 5. Clean up standalone non-VR Main Camera if XR Origin has its own camera
        var standaloneCams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var cam in standaloneCams)
        {
            if (cam.transform.parent == null && cam.gameObject.name == "Main Camera")
            {
                // Disable standalone camera so XR Origin's camera renders the VR view
                cam.gameObject.SetActive(false);
                Debug.Log("[EnCare] Disabled standalone camera in scene root to avoid VR camera conflicts.");
            }
        }

        // 6. Spawn Points (15 backyard or standard points)
        var existingSpawnParent = GameObject.Find("TrashSpawnPoints");
        if (existingSpawnParent != null) Object.DestroyImmediate(existingSpawnParent);

        var spawnParent = new GameObject("TrashSpawnPoints");
        Transform[] spawnPoints;
        if (isBackyard)
        {
            spawnPoints = CreateBackyardSpawnPoints(spawnParent.transform);
        }
        else
        {
            spawnPoints = CreateSpawnPoints(spawnParent.transform);
        }

        // 7. CleanupManager
        var existingManager = GameObject.Find("CleanupManager");
        if (existingManager != null) Object.DestroyImmediate(existingManager);

        var managerGO = new GameObject("CleanupManager");
        var mission = managerGO.AddComponent<CleanupMission>();
        var spawner = managerGO.AddComponent<RandomTrashSpawner>();
        var grabDist = managerGO.AddComponent<GrabDistanceSettings>();
        ConfigureMission(mission, spawner);
        ConfigureSpawner(spawner, trashPrefabs, spawnPoints, levelGlowColor);

        // 8. Event Bridge
        var existingReceiver = GameObject.Find("CleanupEventReceiver");
        if (existingReceiver != null) Object.DestroyImmediate(existingReceiver);

        var receiverGO = new GameObject("CleanupEventReceiver");
        var receiver = receiverGO.AddComponent<CleanupEventReceiver>();

        // 9. Handover / Restart Controller
        var existingHandover = GameObject.Find("HandoverController");
        if (existingHandover != null) Object.DestroyImmediate(existingHandover);

        var handoverGO = new GameObject("HandoverController");
        var handover = handoverGO.AddComponent<HandoverCutscene>();

        // 10. Pass/Fail UI Panels
        var existingSuccess = GameObject.Find("SuccessPanel");
        if (existingSuccess != null) Object.DestroyImmediate(existingSuccess);

        var existingFailure = GameObject.Find("FailurePanel");
        if (existingFailure != null) Object.DestroyImmediate(existingFailure);

        GameObject successPanel = CreateSuccessPanel();
        GameObject failurePanel = CreateFailurePanel(handover);

        // Wire receiver
        var rcvSO = new SerializedObject(receiver);
        rcvSO.FindProperty("successPanel").objectReferenceValue = successPanel;
        rcvSO.FindProperty("failurePanel").objectReferenceValue = failurePanel;
        rcvSO.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddVoidPersistentListener(mission.onCompleted, receiver.OnSuccess);
        UnityEventTools.AddVoidPersistentListener(mission.onFailed, receiver.OnFailure);
        EditorUtility.SetDirty(mission);

        // 11. Wrist HUD on Left Controller
        var existingHUD = GameObject.Find("WristHUD");
        if (existingHUD != null) Object.DestroyImmediate(existingHUD);
        CreateWristHUD(mission, xrOrigin);

        // 12. Controller Help UIs
        SetupControllerHelpUIs(xrOrigin);

        // 13. Basket in Scene (Only for standard/office scene — completely skipped for backyard where user creates biohazard bag by hand)
        if (!isBackyard && basketPrefab != null)
        {
            var existingBasket = GameObject.Find("Basket");
            if (existingBasket != null) Object.DestroyImmediate(existingBasket);
            PlaceBasketAtPosition(basketPrefab, mission, new Vector3(0f, 0.13f, 0.5f));
        }

        Debug.Log("[EnCare] Setup complete for scene: " + EditorSceneManager.GetActiveScene().name);
    }

    static Transform[] CreateBackyardSpawnPoints(Transform parent)
    {
        // 15 positions placed across tables, deck, grass, pathway, and garden
        var positions = new Vector3[]
        {
            new Vector3(10.5f, 0.45f, -9.5f),   // Patio deck coffee table
            new Vector3( 7.5f, 0.20f, -11.0f),  // Near porch stairs
            new Vector3(12.0f, 0.15f, -7.0f),   // Patio lounge corner
            new Vector3( 5.0f, 0.10f, -5.0f),   // Upper lawn
            new Vector3( 2.0f, 0.10f, -11.5f),  // Main stone walkway
            new Vector3( 0.0f, 0.10f,  0.0f),   // Central lawn area
            new Vector3(-4.0f, 0.10f, -14.0f),  // Lower path
            new Vector3(-8.0f, 0.10f, -5.0f),   // Near garden fence
            new Vector3(-12.0f, 0.40f, -8.0f),  // Garden bench / planter
            new Vector3(-15.0f, 0.10f,  4.0f),  // Far backyard corner
            new Vector3( 8.0f, 0.60f, -3.0f),   // Outdoor BBQ counter
            new Vector3(-2.0f, 0.15f,  8.0f),   // Planter garden bed
            new Vector3( 4.0f, 0.10f,  7.5f),   // Deck pool edge
            new Vector3(-10.0f, 0.10f, 11.0f),  // Gazebo / shed front
            new Vector3(14.0f, 0.10f, -14.5f),  // Side gate entrance
        };

        var points = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var go = new GameObject($"Spawn_Backyard_{(i + 1):00}");
            go.transform.SetParent(parent, false);
            go.transform.position = positions[i];
            points[i] = go.transform;
        }
        return points;
    }

    static void PlaceBasketAtPosition(GameObject basketPrefab, CleanupMission mission, Vector3 pos)
    {
        var basket = (GameObject)PrefabUtility.InstantiatePrefab(basketPrefab);
        basket.transform.position = pos;

        var collector = basket.GetComponentInChildren<GarbageCollector>();
        if (collector != null)
        {
            var so = new SerializedObject(collector);
            so.FindProperty("mission").objectReferenceValue = mission;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ─── UI Helpers ──────────────────────────────────────────────────────────
    static GameObject CreateWorldCanvas(string name, Vector3 worldPos, Vector2 sizeDelta)
    {
        var go     = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        go.GetComponent<RectTransform>().sizeDelta = sizeDelta;
        go.transform.position   = worldPos;
        go.transform.localScale = Vector3.one * 0.002f;
        return go;
    }

    static void AddPanelBg(GameObject canvas, Color color)
    {
        var bg  = new GameObject("Background");
        bg.transform.SetParent(canvas.transform, false);
        var img = bg.AddComponent<Image>();
        img.color        = color;
        img.raycastTarget = false;
        var rt  = bg.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
    }

    static GameObject MakeTMP(Transform parent, string name, string text,
                               Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = fontSize;
        tmp.color         = color;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return go;
    }

    // ─── Utilities ───────────────────────────────────────────────────────────
    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
        string folder = Path.GetFileName(path);
        EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}

