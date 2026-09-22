using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using EnCare;
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
        ConfigureMission(mission, spawner);
        ConfigureSpawner(spawner, trashPrefabs, spawnPoints);

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

    // ─── Trash Prefabs ───────────────────────────────────────────────────────
    static GameObject[] CreateTrashPrefabs(Material outlineMat)
    {
        // (prefabName, displayName, modelScale, modelColor)
        var defs = new (string name, string display, Vector3 scale, Color color)[]
        {
            ("Trash_CircuitBoard", "Circuit Board", new Vector3(0.20f, 0.02f, 0.15f), new Color(0.10f, 0.40f, 0.10f)),
            ("Trash_Monitor",      "Old Monitor",   new Vector3(0.35f, 0.25f, 0.05f), new Color(0.20f, 0.20f, 0.20f)),
            ("Trash_Keyboard",     "Keyboard",      new Vector3(0.38f, 0.02f, 0.13f), new Color(0.70f, 0.70f, 0.70f)),
            ("Trash_HardDrive",    "Hard Drive",    new Vector3(0.10f, 0.02f, 0.15f), new Color(0.40f, 0.40f, 0.50f)),
            ("Trash_Phone",        "Old Phone",     new Vector3(0.07f, 0.01f, 0.14f), new Color(0.15f, 0.15f, 0.15f)),
        };

        var prefabs = new GameObject[defs.Length];
        for (int i = 0; i < defs.Length; i++)
            prefabs[i] = CreateSingleTrashPrefab(defs[i].name, defs[i].display,
                                                  defs[i].scale, defs[i].color, outlineMat);
        return prefabs;
    }

    static GameObject CreateSingleTrashPrefab(string prefabName, string displayName,
                                               Vector3 modelScale, Color modelColor, Material outlineMat)
    {
        string path = $"{k_TrashRoot}/{prefabName}.prefab";

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

        // Model child (primitive — replace with real mesh later)
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

        // TrashGlow — inverted-hull outline
        var glow   = root.AddComponent<TrashGlow>();
        var glowSO = new SerializedObject(glow);
        glowSO.FindProperty("outlineMaterial").objectReferenceValue = outlineMat;
        var renderers = glowSO.FindProperty("sourceRenderers");
        renderers.arraySize = 1;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = modelGO.GetComponent<MeshRenderer>();
        glowSO.FindProperty("glowColor").colorValue          = new Color(0.1f, 1f, 0.8f, 1f);
        glowSO.FindProperty("intensity").floatValue          = 3f;
        glowSO.FindProperty("width").floatValue              = 0.006f;
        glowSO.FindProperty("smoothOutlineNormals").boolValue = false; // primitives don't support it
        glowSO.ApplyModifiedPropertiesWithoutUndo();

        // Save & clean up temp scene object
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ─── Basket Prefab ───────────────────────────────────────────────────────
    [MenuItem("EnCare/Update Basket Prefab", false, 1)]
    public static void UpdateBasketPrefabMenuItem()
    {
        EnsureDir(k_PrefabRoot);
        CreateBasketPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EnCare] Updated Assets/EnCare/Prefabs/Basket.prefab with Rigidbody and XRGrabInteractable.");
    }

    public static GameObject CreateBasketPrefab()
    {
        const string path = "Assets/EnCare/Prefabs/Basket.prefab";

        var root = new GameObject("Basket");

        // Physics & Grabbable interaction
        var rb = root.AddComponent<Rigidbody>();
        rb.mass                   = 1.5f;
        rb.useGravity             = true;
        rb.isKinematic            = false;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var grab = root.AddComponent<XRGrabInteractable>();
        grab.selectMode       = InteractableSelectMode.Single;
        grab.movementType     = XRGrabInteractable.MovementType.VelocityTracking;
        grab.trackPosition    = true;
        grab.trackRotation    = true;
        grab.useDynamicAttach = true;

        var gripGO = new GameObject("GripPoint");
        gripGO.transform.SetParent(root.transform, false);
        grab.attachTransform = gripGO.transform;

        // Solid walls + bottom (open-top box: 0.28 × 0.25 × 0.28 interior)
        AddSolidWall(root.transform, "Bottom",    new Vector3( 0f,    -0.125f,  0f),   new Vector3(0.30f, 0.015f, 0.30f));
        AddSolidWall(root.transform, "WallFront", new Vector3( 0f,     0.05f, -0.15f), new Vector3(0.30f, 0.25f,  0.015f));
        AddSolidWall(root.transform, "WallBack",  new Vector3( 0f,     0.05f,  0.15f), new Vector3(0.30f, 0.25f,  0.015f));
        AddSolidWall(root.transform, "WallLeft",  new Vector3(-0.15f,  0.05f,  0f),   new Vector3(0.015f, 0.25f, 0.30f));
        AddSolidWall(root.transform, "WallRight", new Vector3( 0.15f,  0.05f,  0f),   new Vector3(0.015f, 0.25f, 0.30f));

        // Visual panels
        CreateBasketVisuals(root.transform);

        // Collection zone (trigger)
        var zoneGO  = new GameObject("CollectionZone");
        zoneGO.transform.SetParent(root.transform, false);
        var trigger       = zoneGO.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size      = new Vector3(0.26f, 0.22f, 0.26f);
        trigger.center    = new Vector3(0f, 0f, 0f);

        var collector = zoneGO.AddComponent<GarbageCollector>();
        var collSO    = new SerializedObject(collector);
        collSO.FindProperty("trashLayers").intValue = ~0; // All layers
        collSO.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void AddSolidWall(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<BoxCollider>().size = size;
    }

    static void CreateBasketVisuals(Transform parent)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            { color = new Color(0.60f, 0.50f, 0.35f) }; // warm wood

        void Panel(string n, Vector3 pos, Vector3 s)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale    = s;
            Object.DestroyImmediate(g.GetComponent<BoxCollider>());
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        Panel("Vis_Bottom",    new Vector3( 0f,    -0.125f,  0f),   new Vector3(0.30f, 0.015f, 0.30f));
        Panel("Vis_WallFront", new Vector3( 0f,     0.05f,  -0.15f), new Vector3(0.30f, 0.25f, 0.015f));
        Panel("Vis_WallBack",  new Vector3( 0f,     0.05f,   0.15f), new Vector3(0.30f, 0.25f, 0.015f));
        Panel("Vis_WallLeft",  new Vector3(-0.15f,  0.05f,   0f),   new Vector3(0.015f, 0.25f, 0.30f));
        Panel("Vis_WallRight", new Vector3( 0.15f,  0.05f,   0f),   new Vector3(0.015f, 0.25f, 0.30f));
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
        // Spread across desk surfaces (y≈0.85) and floor (y≈0.05)
        // Reposition these once your real office environment is in place.
        var positions = new Vector3[]
        {
            new Vector3(-2.5f, 0.85f, -2.2f),  // Desk_1 left
            new Vector3(-1.6f, 0.85f, -1.8f),  // Desk_1 right
            new Vector3(-2.5f, 0.85f,  0.8f),  // Desk_2 left
            new Vector3(-1.6f, 0.85f,  1.2f),  // Desk_2 right
            new Vector3( 1.5f, 0.85f, -2.2f),  // Desk_3 left
            new Vector3( 2.5f, 0.85f, -1.8f),  // Desk_3 right
            new Vector3( 0.0f, 0.05f,  0.0f),  // Floor centre
            new Vector3(-1.0f, 0.05f,  2.5f),  // Floor back-left
            new Vector3( 1.0f, 0.05f, -0.5f),  // Floor mid-right
            new Vector3( 3.5f, 0.05f,  1.0f),  // Floor far-right
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

    static void ConfigureSpawner(RandomTrashSpawner spawner, GameObject[] trashPrefabs, Transform[] spawnPoints)
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

        so.FindProperty("overrideGlowColor").boolValue = true;
        so.FindProperty("levelGlowColor").colorValue   = new Color(0.1f, 1f, 0.8f, 1f);
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
