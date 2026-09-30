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
/// One-click editor tools that build the EnCare cutscene scenes with primitive objects.
/// Run via:
///   EnCare ▶ Build Office Cutscene (Lvl_OfficeCutScene)
///   EnCare ▶ Build Backyard Cutscene (Lvl_BackyardCutScene)
///
/// These create fully-functional prototype scenes with:
/// • Exterior environment (ground, building backdrop)
/// • Garbage truck (cubes + cylinders)
/// • Garbage man NPC (capsule + sphere + cube arms)
/// • Drop-off trigger zone
/// • Grabbable basket
/// • XR Origin for VR interaction
/// • CutsceneLevelManager, GameEndUI
/// • Instruction text canvas
///
/// All primitives are intended to be replaced with real 3D models later.
/// </summary>
public static class CutsceneSceneBuilder
{
    // ─── Paths ────────────────────────────────────────────────────────────────
    const string k_OfficeScenePath   = "Assets/Scenes/Lvl_OfficeCutScene.unity";
    const string k_BackyardScenePath = "Assets/Scenes/Lvl_BackyardCutScene.unity";
    const string k_PrefabRoot        = "Assets/EnCare/Prefabs";
    const string k_XRPrefabRig       = "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string k_XRPrefabTemplate  = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";

    // ─── Menu Entries ─────────────────────────────────────────────────────────

    [MenuItem("EnCare/🚛 Build Office Cutscene (Lvl_OfficeCutScene)", false, 10)]
    public static void BuildOfficeCutscene()
    {
        if (!EditorUtility.DisplayDialog("Build Office Cutscene",
            "Creates Assets/Scenes/Lvl_OfficeCutScene.unity\n" +
            "with primitive garbage truck, NPC, and handover system.\n\n" +
            "Save your current scene before proceeding.\n\nBuild?",
            "Build It", "Cancel")) return;

        BuildCutsceneScene(k_OfficeScenePath, isBackyard: false);
    }

    [MenuItem("EnCare/🚛 Build Backyard Cutscene (Lvl_BackyardCutScene)", false, 11)]
    public static void BuildBackyardCutscene()
    {
        if (!EditorUtility.DisplayDialog("Build Backyard Cutscene",
            "Creates Assets/Scenes/Lvl_BackyardCutScene.unity\n" +
            "with primitive garbage truck, NPC, and handover system.\n\n" +
            "Save your current scene before proceeding.\n\nBuild?",
            "Build It", "Cancel")) return;

        BuildCutsceneScene(k_BackyardScenePath, isBackyard: true);
    }

    // ─── Main Builder ─────────────────────────────────────────────────────────

    static void BuildCutsceneScene(string scenePath, bool isBackyard)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // ── 1. Lighting ──
        SetupOutdoorLighting();

        // ── 2. Environment ──
        CreateExteriorEnvironment(shader, isBackyard);

        // ── 3. XR Origin ──
        GameObject xrOrigin = PlaceXROrigin();

        // ── 4. Garbage Truck ──
        GameObject truck = CreateGarbageTruck(shader);

        // ── 5. Garbage Man NPC ──
        var (garbageManRoot, garbageManCtrl) = CreateGarbageMan(shader, truck);

        // ── 6. Basket (grabbable) ──
        GameObject basket = CreateCutsceneBasket(shader, garbageManCtrl);

        // ── 7. Spawn Points ──
        var playerSpawn = new GameObject("PlayerSpawnPoint");
        playerSpawn.transform.position = new Vector3(0f, 0f, -3f);
        playerSpawn.transform.rotation = Quaternion.LookRotation(Vector3.forward);

        var basketSpawn = new GameObject("BasketSpawnPoint");
        basketSpawn.transform.position = new Vector3(0.5f, 0.05f, -2f);

        // ── 8. Instruction UI ──
        GameObject instructionUI = CreateInstructionUI();

        // ── 9. Game End UI ──
        GameObject gameEndUIObj = CreateGameEndUI();
        var gameEndUI = gameEndUIObj.GetComponent<GameEndUI>();

        // ── 10. Cutscene Level Manager ──
        var managerGO = new GameObject("CutsceneLevelManager");
        var manager = managerGO.AddComponent<CutsceneLevelManager>();
        {
            var so = new SerializedObject(manager);
            so.FindProperty("garbageManController").objectReferenceValue = garbageManCtrl;
            so.FindProperty("gameEndUI").objectReferenceValue = gameEndUI;
            so.FindProperty("basket").objectReferenceValue = basket;
            so.FindProperty("basketSpawnPoint").objectReferenceValue = basketSpawn.transform;
            so.FindProperty("playerSpawnPoint").objectReferenceValue = playerSpawn.transform;
            so.FindProperty("xrOrigin").objectReferenceValue = xrOrigin;
            so.FindProperty("instructionUI").objectReferenceValue = instructionUI;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Save ──
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(activeScene, scenePath);
        AssetDatabase.Refresh();

        string envType = isBackyard ? "Backyard" : "Office";
        Debug.Log($"[EnCare] {envType} Cutscene built → {scenePath}");
        EditorUtility.DisplayDialog($"✅ {envType} Cutscene Created",
            $"Saved to {scenePath}\n\n" +
            "Scene contains:\n" +
            "• Exterior environment with primitive objects\n" +
            "• Garbage truck (cubes + cylinders)\n" +
            "• Garbage man NPC with GarbageManController\n" +
            "• Grabbable basket with trigger detection\n" +
            "• Drop-off zone (green trigger in front of NPC)\n" +
            "• CutsceneLevelManager (auto-wired)\n" +
            "• Game End UI with auto-return to menu\n" +
            "• Instruction text for the player\n" +
            "• XR Origin for VR play\n\n" +
            "Next steps:\n" +
            "1. Add this scene to Build Settings (File → Build Profiles)\n" +
            "2. Replace primitives with your real 3D models\n" +
            "3. Add Animator to garbage man for real animations\n" +
            "4. Test the full flow: gameplay → win → cutscene → end",
            "Got it!");
    }

    // ─── Outdoor Lighting ─────────────────────────────────────────────────────

    static void SetupOutdoorLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);

        var lightGO = new GameObject("Directional Light (Sun)");
        lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        lightGO.transform.position = new Vector3(0f, 10f, 0f);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.shadows = LightShadows.Soft;
    }

    // ─── Exterior Environment ─────────────────────────────────────────────────

    static void CreateExteriorEnvironment(Shader shader, bool isBackyard)
    {
        var env = new GameObject("Environment [REPLACE with real assets]");

        // Ground
        Color groundColor = isBackyard
            ? new Color(0.25f, 0.50f, 0.18f) // Grass green
            : new Color(0.55f, 0.55f, 0.55f); // Concrete grey

        var groundMat = new Material(shader) { color = groundColor };
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(env.transform);
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        // Building backdrop (behind player)
        var wallMat = new Material(shader) { color = new Color(0.75f, 0.72f, 0.68f) };
        var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = "Building_Backdrop [REPLACE]";
        building.transform.SetParent(env.transform);
        building.transform.position = new Vector3(0f, 3f, -8f);
        building.transform.localScale = new Vector3(20f, 6f, 1f);
        building.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // Door (darker rectangle on building)
        var doorMat = new Material(shader) { color = new Color(0.35f, 0.25f, 0.15f) };
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door [REPLACE]";
        door.transform.SetParent(env.transform);
        door.transform.position = new Vector3(0f, 1.2f, -7.49f);
        door.transform.localScale = new Vector3(1.2f, 2.4f, 0.05f);
        door.GetComponent<MeshRenderer>().sharedMaterial = doorMat;
        Object.DestroyImmediate(door.GetComponent<BoxCollider>());

        // Sidewalk / path from building to truck area
        var pathMat = new Material(shader) { color = new Color(0.65f, 0.63f, 0.60f) };
        var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.name = "Path [REPLACE]";
        path.transform.SetParent(env.transform);
        path.transform.position = new Vector3(0f, 0.01f, 0f);
        path.transform.localScale = new Vector3(3f, 0.02f, 15f);
        path.GetComponent<MeshRenderer>().sharedMaterial = pathMat;

        // Parking area under truck
        var parkMat = new Material(shader) { color = new Color(0.35f, 0.35f, 0.38f) };
        var parking = GameObject.CreatePrimitive(PrimitiveType.Cube);
        parking.name = "ParkingLot [REPLACE]";
        parking.transform.SetParent(env.transform);
        parking.transform.position = new Vector3(4f, 0.005f, 6f);
        parking.transform.localScale = new Vector3(8f, 0.01f, 6f);
        parking.GetComponent<MeshRenderer>().sharedMaterial = parkMat;

        if (isBackyard)
        {
            // Add some garden elements for backyard
            var fenceMat = new Material(shader) { color = new Color(0.60f, 0.45f, 0.25f) };

            // Fence left
            var fenceL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fenceL.name = "Fence_Left [REPLACE]";
            fenceL.transform.SetParent(env.transform);
            fenceL.transform.position = new Vector3(-10f, 0.6f, 0f);
            fenceL.transform.localScale = new Vector3(0.15f, 1.2f, 20f);
            fenceL.GetComponent<MeshRenderer>().sharedMaterial = fenceMat;

            // Fence right
            var fenceR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fenceR.name = "Fence_Right [REPLACE]";
            fenceR.transform.SetParent(env.transform);
            fenceR.transform.position = new Vector3(10f, 0.6f, 0f);
            fenceR.transform.localScale = new Vector3(0.15f, 1.2f, 20f);
            fenceR.GetComponent<MeshRenderer>().sharedMaterial = fenceMat;
        }
    }

    // ─── Garbage Truck ────────────────────────────────────────────────────────

    static GameObject CreateGarbageTruck(Shader shader)
    {
        var truck = new GameObject("GarbageTruck [REPLACE with real model]");
        truck.transform.position = new Vector3(4f, 0f, 6f);
        // Truck faces left (toward NPC drop-off area)
        truck.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

        var bodyMat = new Material(shader) { color = new Color(0.15f, 0.40f, 0.15f) }; // Dark green
        var cabMat = new Material(shader) { color = new Color(0.20f, 0.45f, 0.20f) };
        var wheelMat = new Material(shader) { color = new Color(0.15f, 0.15f, 0.15f) };
        var bedMat = new Material(shader) { color = new Color(0.12f, 0.35f, 0.12f) };

        // ── Truck Body (large cube) ──
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Truck_Body [REPLACE]";
        body.transform.SetParent(truck.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.2f, -0.5f);
        body.transform.localScale = new Vector3(2.5f, 2.0f, 4.0f);
        body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

        // ── Cab ──
        var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cab.name = "Truck_Cab [REPLACE]";
        cab.transform.SetParent(truck.transform, false);
        cab.transform.localPosition = new Vector3(0f, 1.0f, 2.2f);
        cab.transform.localScale = new Vector3(2.3f, 1.8f, 1.8f);
        cab.GetComponent<MeshRenderer>().sharedMaterial = cabMat;

        // ── Windshield ──
        var windshield = GameObject.CreatePrimitive(PrimitiveType.Cube);
        windshield.name = "Windshield [REPLACE]";
        windshield.transform.SetParent(truck.transform, false);
        windshield.transform.localPosition = new Vector3(0f, 1.4f, 3.11f);
        windshield.transform.localScale = new Vector3(1.8f, 1.0f, 0.05f);
        var windshieldMat = new Material(shader) { color = new Color(0.5f, 0.7f, 0.85f, 0.6f) };
        windshield.GetComponent<MeshRenderer>().sharedMaterial = windshieldMat;
        Object.DestroyImmediate(windshield.GetComponent<BoxCollider>());

        // ── Open-top collection bed ──
        var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bed.name = "Truck_Bed_Opening [REPLACE]";
        bed.transform.SetParent(truck.transform, false);
        bed.transform.localPosition = new Vector3(0f, 2.25f, -0.5f);
        bed.transform.localScale = new Vector3(2.2f, 0.1f, 3.5f);
        bed.GetComponent<MeshRenderer>().sharedMaterial = bedMat;

        // ── Truck Unload Point (empty — NPC carries basket here) ──
        var unloadPoint = new GameObject("TruckUnloadPoint");
        unloadPoint.transform.SetParent(truck.transform, false);
        // Position at the side opening of the truck bed
        unloadPoint.transform.localPosition = new Vector3(-1.4f, 1.8f, -0.5f);
        unloadPoint.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

        // ── Wheels ──
        CreateWheel(truck.transform, "Wheel_FL", new Vector3(-1.15f, 0.35f, 1.8f), wheelMat);
        CreateWheel(truck.transform, "Wheel_FR", new Vector3(1.15f, 0.35f, 1.8f), wheelMat);
        CreateWheel(truck.transform, "Wheel_RL", new Vector3(-1.15f, 0.35f, -1.5f), wheelMat);
        CreateWheel(truck.transform, "Wheel_RR", new Vector3(1.15f, 0.35f, -1.5f), wheelMat);

        // ── Label ──
        var labelCanvas = new GameObject("LabelCanvas");
        labelCanvas.transform.SetParent(truck.transform, false);
        labelCanvas.transform.localPosition = new Vector3(-1.26f, 1.5f, -0.5f);
        labelCanvas.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        labelCanvas.transform.localScale = Vector3.one * 0.005f;

        var canvas = labelCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        labelCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 80);

        var labelGO = new GameObject("LabelText");
        labelGO.transform.SetParent(labelCanvas.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "EnCare Waste Collection";
        tmp.fontSize = 36;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        labelGO.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 80);

        return truck;
    }

    static void CreateWheel(Transform parent, string name, Vector3 localPos, Material mat)
    {
        var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wheel.name = name + " [REPLACE]";
        wheel.transform.SetParent(parent, false);
        wheel.transform.localPosition = localPos;
        wheel.transform.localScale = new Vector3(0.7f, 0.15f, 0.7f);
        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        wheel.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(wheel.GetComponent<CapsuleCollider>());
    }

    // ─── Garbage Man NPC ──────────────────────────────────────────────────────

    static (GameObject root, GarbageManController ctrl) CreateGarbageMan(Shader shader, GameObject truck)
    {
        var npc = new GameObject("GarbageMan [REPLACE with character model]");
        // Position between player path and truck, facing the player approach direction
        npc.transform.position = new Vector3(1.5f, 0f, 4f);
        npc.transform.rotation = Quaternion.LookRotation(Vector3.back); // Face toward player

        var vestMat = new Material(shader) { color = new Color(1.0f, 0.55f, 0.0f) };    // Orange hi-vis
        var skinMat = new Material(shader) { color = new Color(0.85f, 0.65f, 0.50f) };   // Skin
        var pantsMat = new Material(shader) { color = new Color(0.20f, 0.22f, 0.28f) };  // Dark pants
        var bootsMat = new Material(shader) { color = new Color(0.15f, 0.15f, 0.15f) };  // Black boots

        // ── Body (capsule) ──
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body [REPLACE]";
        body.transform.SetParent(npc.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        body.transform.localScale = new Vector3(0.5f, 0.5f, 0.3f);
        body.GetComponent<MeshRenderer>().sharedMaterial = vestMat;
        Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

        // ── Head (sphere) ──
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head [REPLACE]";
        head.transform.SetParent(npc.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        head.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
        head.GetComponent<MeshRenderer>().sharedMaterial = skinMat;
        Object.DestroyImmediate(head.GetComponent<SphereCollider>());

        // ── Hard Hat (flattened sphere on top of head) ──
        var hat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hat.name = "HardHat [REPLACE]";
        hat.transform.SetParent(npc.transform, false);
        hat.transform.localPosition = new Vector3(0f, 1.92f, 0f);
        hat.transform.localScale = new Vector3(0.32f, 0.12f, 0.32f);
        var hatMat = new Material(shader) { color = new Color(0.95f, 0.85f, 0.05f) }; // Yellow hard hat
        hat.GetComponent<MeshRenderer>().sharedMaterial = hatMat;
        Object.DestroyImmediate(hat.GetComponent<SphereCollider>());

        // ── Left Arm ──
        var armL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armL.name = "Arm_Left [REPLACE]";
        armL.transform.SetParent(npc.transform, false);
        armL.transform.localPosition = new Vector3(-0.35f, 0.95f, 0f);
        armL.transform.localScale = new Vector3(0.12f, 0.55f, 0.12f);
        armL.GetComponent<MeshRenderer>().sharedMaterial = vestMat;
        Object.DestroyImmediate(armL.GetComponent<BoxCollider>());

        // ── Right Arm ──
        var armR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armR.name = "Arm_Right [REPLACE]";
        armR.transform.SetParent(npc.transform, false);
        armR.transform.localPosition = new Vector3(0.35f, 0.95f, 0f);
        armR.transform.localScale = new Vector3(0.12f, 0.55f, 0.12f);
        armR.GetComponent<MeshRenderer>().sharedMaterial = vestMat;
        Object.DestroyImmediate(armR.GetComponent<BoxCollider>());

        // ── Legs ──
        var legL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        legL.name = "Leg_Left [REPLACE]";
        legL.transform.SetParent(npc.transform, false);
        legL.transform.localPosition = new Vector3(-0.12f, 0.30f, 0f);
        legL.transform.localScale = new Vector3(0.15f, 0.60f, 0.15f);
        legL.GetComponent<MeshRenderer>().sharedMaterial = pantsMat;
        Object.DestroyImmediate(legL.GetComponent<BoxCollider>());

        var legR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        legR.name = "Leg_Right [REPLACE]";
        legR.transform.SetParent(npc.transform, false);
        legR.transform.localPosition = new Vector3(0.12f, 0.30f, 0f);
        legR.transform.localScale = new Vector3(0.15f, 0.60f, 0.15f);
        legR.GetComponent<MeshRenderer>().sharedMaterial = pantsMat;
        Object.DestroyImmediate(legR.GetComponent<BoxCollider>());

        // ── Hand Point (where basket attaches) ──
        var handPoint = new GameObject("HandPoint");
        handPoint.transform.SetParent(npc.transform, false);
        handPoint.transform.localPosition = new Vector3(0f, 0.7f, -0.4f);

        // ── Drop-Off Zone (trigger in front of NPC) ──
        var dropZoneGO = new GameObject("DropOffZone");
        dropZoneGO.transform.SetParent(npc.transform, false);
        dropZoneGO.transform.localPosition = new Vector3(0f, 0.5f, -1.5f);
        var dropZone = dropZoneGO.AddComponent<BoxCollider>();
        dropZone.isTrigger = true;
        dropZone.size = new Vector3(2f, 1.5f, 2f);

        // Rigidbody needed for trigger detection between two trigger colliders
        var dropZoneRB = dropZoneGO.AddComponent<Rigidbody>();
        dropZoneRB.isKinematic = true;
        dropZoneRB.useGravity = false;

        // Trigger forwarder — sends OnTriggerEnter events to the parent GarbageManController
        var zoneTrigger = dropZoneGO.AddComponent<DropOffZoneTrigger>();

        // ── Find truck unload point ──
        Transform truckUnloadPoint = truck.transform.Find("TruckUnloadPoint");

        // ── GarbageManController ──
        var ctrl = npc.AddComponent<GarbageManController>();
        {
            var so = new SerializedObject(ctrl);
            so.FindProperty("bodyTransform").objectReferenceValue = body.transform;
            so.FindProperty("headTransform").objectReferenceValue = head.transform;
            so.FindProperty("armLeft").objectReferenceValue = armL.transform;
            so.FindProperty("armRight").objectReferenceValue = armR.transform;
            so.FindProperty("handPoint").objectReferenceValue = handPoint.transform;
            so.FindProperty("truckUnloadPoint").objectReferenceValue = truckUnloadPoint;
            so.FindProperty("dropOffZone").objectReferenceValue = dropZone;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return (npc, ctrl);
    }

    // ─── Cutscene Basket ──────────────────────────────────────────────────────

    static GameObject CreateCutsceneBasket(Shader shader, GarbageManController garbageManCtrl)
    {
        var basket = new GameObject("Basket_Cutscene");
        basket.transform.position = new Vector3(0.5f, 0.05f, -2f);

        // Rigidbody
        var rb = basket.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Physical collider
        var col = basket.AddComponent<BoxCollider>();
        col.size = new Vector3(0.35f, 0.25f, 0.35f);
        col.center = new Vector3(0f, 0.125f, 0f);

        // XR Grab Interactable — so the player can pick it up in VR
        var grab = basket.AddComponent<XRGrabInteractable>();
        grab.movementType = XRGrabInteractable.MovementType.VelocityTracking;
        grab.useDynamicAttach = true;

        // Visual (placeholder box — represents the basket)
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "BasketModel [REPLACE]";
        visual.transform.SetParent(basket.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.125f, 0f);
        visual.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
        var basketMat = new Material(shader) { color = new Color(0.45f, 0.35f, 0.20f) }; // Wicker brown
        visual.GetComponent<MeshRenderer>().sharedMaterial = basketMat;
        Object.DestroyImmediate(visual.GetComponent<BoxCollider>()); // Physics on root

        // Trigger collider for detection by garbage man's drop-off zone
        var triggerGO = new GameObject("BasketTrigger");
        triggerGO.transform.SetParent(basket.transform, false);
        triggerGO.transform.localPosition = new Vector3(0f, 0.125f, 0f);
        var triggerCol = triggerGO.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(0.4f, 0.3f, 0.4f);

        // Wire basket reference into the garbage man
        {
            var so = new SerializedObject(garbageManCtrl);
            so.FindProperty("basket").objectReferenceValue = basket;
            so.FindProperty("basketTrigger").objectReferenceValue = triggerCol;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return basket;
    }

    // ─── XR Origin ────────────────────────────────────────────────────────────

    static GameObject PlaceXROrigin()
    {
        // Ensure XRInteractionManager
        if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
        {
            var mgrGO = new GameObject("XR Interaction Manager");
            mgrGO.AddComponent<XRInteractionManager>();
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_XRPrefabRig)
                  ?? AssetDatabase.LoadAssetAtPath<GameObject>(k_XRPrefabTemplate);

        if (prefab == null)
        {
            Debug.LogWarning("[EnCare Cutscene] XR Origin prefab not found. Add XR Origin manually.");
            var fallback = new GameObject("XR Origin [MISSING — add manually]");
            var camGO = new GameObject("Main Camera");
            camGO.transform.SetParent(fallback.transform);
            camGO.AddComponent<Camera>().tag = "MainCamera";
            camGO.AddComponent<AudioListener>();
            fallback.transform.position = new Vector3(0f, 0f, -3f);
            return fallback;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = new Vector3(0f, 0f, -3f);
        instance.transform.rotation = Quaternion.identity;
        return instance;
    }

    // ─── Instruction UI ───────────────────────────────────────────────────────

    static GameObject CreateInstructionUI()
    {
        var canvasGO = CreateWorldCanvas("InstructionUI", new Vector3(0f, 2.0f, 1f), new Vector2(700, 120));
        canvasGO.transform.localScale = Vector3.one * 0.002f;

        // Remove raycaster — display only
        Object.DestroyImmediate(canvasGO.GetComponent<GraphicRaycaster>());

        // Add LazyFollowView so it tracks the player's head
        canvasGO.AddComponent<LazyFollowView>();

        AddPanelBg(canvasGO, new Color(0.05f, 0.12f, 0.25f, 0.85f));

        MakeTMP(canvasGO.transform, "InstructionText",
            "🗑️ Bring the basket to the EnCare worker →",
            Vector2.zero, new Vector2(680, 100), 36, Color.white);

        return canvasGO;
    }

    // ─── Game End UI ──────────────────────────────────────────────────────────

    static GameObject CreateGameEndUI()
    {
        var canvasGO = CreateWorldCanvas("GameEndUI", new Vector3(0f, 1.6f, 2f), new Vector2(700, 450));
        canvasGO.AddComponent<LazyFollowView>();
        canvasGO.SetActive(false); // Hidden until handover completes

        AddPanelBg(canvasGO, new Color(0.03f, 0.15f, 0.08f, 0.95f)); // Dark green

        var titleGO = MakeTMP(canvasGO.transform, "TitleText",
            "🎉 Thank You!",
            new Vector2(0, 130), new Vector2(660, 100), 56, new Color(0.3f, 1f, 0.55f));

        var bodyGO = MakeTMP(canvasGO.transform, "BodyText",
            "The waste has been properly\ncollected and disposed of.\n\nYou've made a difference!",
            new Vector2(0, 10), new Vector2(640, 150), 28, Color.white);

        var countdownGO = MakeTMP(canvasGO.transform, "CountdownText",
            "Returning to menu in 8s...",
            new Vector2(0, -110), new Vector2(640, 50), 22, new Color(0.7f, 0.9f, 0.8f));

        // Return to Menu button
        var btnGO = new GameObject("ReturnButton");
        btnGO.transform.SetParent(canvasGO.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.55f, 0.25f);
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchoredPosition = new Vector2(0, -175);
        btnRT.sizeDelta = new Vector2(280, 65);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        MakeTMP(btnGO.transform, "ButtonLabel", "Return to Menu",
            Vector2.zero, new Vector2(260, 55), 30, Color.white);

        // GameEndUI component
        var gameEndUI = canvasGO.AddComponent<GameEndUI>();
        {
            var so = new SerializedObject(gameEndUI);
            so.FindProperty("titleText").objectReferenceValue = titleGO.GetComponent<TMP_Text>();
            so.FindProperty("bodyText").objectReferenceValue = bodyGO.GetComponent<TMP_Text>();
            so.FindProperty("countdownText").objectReferenceValue = countdownGO.GetComponent<TMP_Text>();
            so.FindProperty("returnButton").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Wire button OnClick → GameEndUI.OnReturnClicked
        UnityEventTools.AddVoidPersistentListener(btn.onClick, gameEndUI.OnReturnClicked);
        EditorUtility.SetDirty(gameEndUI);

        return canvasGO;
    }

    // ─── UI Helpers (same pattern as CleanupSceneBuilder) ─────────────────────

    static GameObject CreateWorldCanvas(string name, Vector3 worldPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        go.GetComponent<RectTransform>().sizeDelta = sizeDelta;
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * 0.002f;
        return go;
    }

    static void AddPanelBg(GameObject canvas, Color color)
    {
        var bg = new GameObject("Background");
        bg.transform.SetParent(canvas.transform, false);
        var img = bg.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var rt = bg.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
    }

    static GameObject MakeTMP(Transform parent, string name, string text,
                               Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go;
    }

    // ─── Build Settings Helper ────────────────────────────────────────────────

    [MenuItem("EnCare/📋 Add Cutscene Scenes to Build Settings", false, 12)]
    public static void AddCutsceneScenesToBuild()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        bool addedAny = false;
        string[] cutscenePaths = { k_OfficeScenePath, k_BackyardScenePath };

        foreach (string path in cutscenePaths)
        {
            bool exists = false;
            foreach (var s in scenes)
            {
                if (s.path == path) { exists = true; break; }
            }

            if (!exists)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                    addedAny = true;
                    Debug.Log($"[EnCare] Added to Build Settings: {path}");
                }
                else
                {
                    Debug.LogWarning($"[EnCare] Scene not found: {path} — build it first.");
                }
            }
        }

        if (addedAny)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorUtility.DisplayDialog("✅ Build Settings Updated",
                "Added cutscene scenes to Build Settings.\n\n" +
                "• Lvl_OfficeCutScene\n• Lvl_BackyardCutScene", "Got it!");
        }
        else
        {
            EditorUtility.DisplayDialog("ℹ️ No Changes",
                "Both cutscene scenes are already in Build Settings\n" +
                "(or haven't been created yet).", "OK");
        }
    }
}
