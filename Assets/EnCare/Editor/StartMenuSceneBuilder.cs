using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Presets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace EnCare.Editor
{
    [InitializeOnLoad]
    public static class StartMenuSceneBuilder
    {
        public const string k_StartMenuScenePath = "Assets/Scenes/Lvl_StartMenu.unity";
        private const string k_XRPrefabTemplate = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";
        private const string k_XRPrefabRig = "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        static StartMenuSceneBuilder()
        {
            EditorApplication.delayCall += CheckAndBuildIfRequested;
        }

        public static void CheckAndBuildIfRequested()
        {
            if (!File.Exists(k_StartMenuScenePath)) return;

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path == k_StartMenuScenePath)
            {
                var canvas = GameObject.Find("StartMenuCanvas");
                var es = Object.FindFirstObjectByType<EventSystem>();
                var xrMod = es != null ? es.GetComponent<XRUIInputModule>() : null;
                if (canvas == null || canvas.GetComponent<LazyFollowView>() == null || xrMod == null)
                {
                    BuildStartMenuInCurrentScene();
                }
            }
        }

        [MenuItem("EnCare/\U0001F3AE Setup Start Menu (Lvl_StartMenu)", false, 2)]
        public static void SetupStartMenuScene()
        {
            var scene = EditorSceneManager.OpenScene(k_StartMenuScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Error", "Could not open " + k_StartMenuScenePath, "OK");
                return;
            }

            BuildStartMenuInCurrentScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("\u2705 Start Menu Configured",
                "Lvl_StartMenu.unity has been fully populated with:\n\n" +
                "• Complete XR Origin & Interaction Manager\n" +
                "• EventSystem with XRUIInputModule (VR Rays + Mouse support)\n" +
                "• Interactive World-Space Start Menu UI on UI Layer\n" +
                "• Whole-card clickable buttons + audio hover/clicks\n" +
                "• LazyFollowView at eye level in front of player\n" +
                "• Ground Platform & Directional Lighting\n" +
                "• Desktop fallback shortcuts (1 & 2)",
                "Got it!");
        }

        public static void BuildStartMenuInCurrentScene()
        {
            // 1. XR Interaction Manager
            if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
            {
                var mgrGO = new GameObject("XR Interaction Manager");
                mgrGO.AddComponent<XRInteractionManager>();
                Debug.Log("[EnCare] Added XRInteractionManager to Start Menu.");
            }

            // 2. XR Origin / VR Camera Setup
            GameObject xrOrigin = GameObject.Find("Complete XR Origin Set Up Variant")
                               ?? GameObject.Find("XR Origin (XR Rig)")
                               ?? GameObject.Find("XR Origin");

            if (xrOrigin == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_XRPrefabTemplate)
                          ?? AssetDatabase.LoadAssetAtPath<GameObject>(k_XRPrefabRig);

                if (prefab != null)
                {
                    xrOrigin = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    xrOrigin.transform.position = Vector3.zero;
                    xrOrigin.transform.rotation = Quaternion.identity;
                }
            }

            // Clean up standalone default Camera if XR Origin exists
            Camera xrCamera = null;
            if (xrOrigin != null)
            {
                var standaloneCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (var cam in standaloneCameras)
                {
                    if (cam.gameObject != null && !cam.transform.IsChildOf(xrOrigin.transform))
                    {
                        Object.DestroyImmediate(cam.gameObject);
                    }
                    else if (cam.gameObject != null && cam.transform.IsChildOf(xrOrigin.transform))
                    {
                        xrCamera = cam;
                    }
                }
            }
            if (xrCamera == null)
            {
                xrCamera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            }

            // 3. EventSystem with XRUIInputModule (CRITICAL for VR rays and mouse interaction)
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var esGO = new GameObject("EventSystem");
                eventSystem = esGO.AddComponent<EventSystem>();
            }

            // Remove any legacy/incompatible input modules
            var oldModules = eventSystem.GetComponents<BaseInputModule>();
            foreach (var mod in oldModules)
            {
                if (mod != null && !(mod is XRUIInputModule))
                {
                    Object.DestroyImmediate(mod);
                }
            }

            var xrUIModule = eventSystem.GetComponent<XRUIInputModule>();
            if (xrUIModule == null)
            {
                xrUIModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
            }

            // Apply official XRI Default XR UI Input Module preset if present
            var preset = AssetDatabase.LoadAssetAtPath<Preset>(
                "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Presets/XRI Default XR UI Input Module.preset");
            if (preset != null && preset.CanBeAppliedTo(xrUIModule))
            {
                preset.ApplyTo(xrUIModule);
            }

            xrUIModule.enableXRInput = true;
            xrUIModule.enableMouseInput = true;
            xrUIModule.enableTouchInput = true;

            // 4. Directional Light & Subtle Environment
            var light = Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                var lightGO = new GameObject("Directional Light");
                light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.88f, 0.94f, 1f);
                light.intensity = 1.0f;
                lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }

            // Ground Reference Platform
            var floorGO = GameObject.Find("LobbyPlatform");
            if (floorGO == null)
            {
                floorGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                floorGO.name = "LobbyPlatform";
                floorGO.transform.position = new Vector3(0f, -0.05f, 0f);
                floorGO.transform.localScale = new Vector3(8f, 0.05f, 8f);

                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(shader)
                {
                    color = new Color(0.06f, 0.08f, 0.13f, 1f)
                };
                floorGO.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            // 5. Start Menu Canvas
            var existingCanvas = GameObject.Find("StartMenuCanvas");
            if (existingCanvas != null)
            {
                Object.DestroyImmediate(existingCanvas);
            }

            // Positioned at eye level (1.35m up, 1.8m forward)
            var canvasGO = new GameObject("StartMenuCanvas");
            canvasGO.transform.position = new Vector3(0f, 1.35f, 1.8f);
            canvasGO.transform.rotation = Quaternion.identity;
            canvasGO.transform.localScale = Vector3.one * 0.0018f;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (xrCamera != null)
            {
                canvas.worldCamera = xrCamera;
            }

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;

            // Dual raycasters for complete VR Ray + Desktop Mouse support
            var trackedRaycaster = canvasGO.GetComponent<TrackedDeviceGraphicRaycaster>() ?? canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();
            trackedRaycaster.ignoreReversedGraphics = false;
            trackedRaycaster.checkFor2DOcclusion = false;
            trackedRaycaster.checkFor3DOcclusion = false;

            var graphicRaycaster = canvasGO.GetComponent<GraphicRaycaster>() ?? canvasGO.AddComponent<GraphicRaycaster>();
            graphicRaycaster.ignoreReversedGraphics = false;
            graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            var menuUI = canvasGO.AddComponent<StartMenuUI>();

            // Add LazyFollowView for dynamic VR gaze centering
            var follow = canvasGO.AddComponent<LazyFollowView>();
            follow.Distance = 1.8f;
            follow.DeadzoneAngle = 25f;

            var canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(1100f, 620f);
            canvasRT.anchoredPosition3D = new Vector3(0f, 1.35f, 1.8f);

            // Audio Source & Clips
            var audioSrc = canvasGO.GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = canvasGO.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;

            var clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/VRTemplateAssets/Audio/Button_22_click.wav");
            var hoverClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/VRTemplateAssets/Audio/Button_14_hover.wav");
            menuUI.ClickClip = clickClip;
            menuUI.HoverClip = hoverClip;
            menuUI.AudioSourceComponent = audioSrc;

            // ── Background Panel ──
            var mainBg = new GameObject("MainBackground");
            mainBg.transform.SetParent(canvasGO.transform, false);
            var mainBgImg = mainBg.AddComponent<Image>();
            mainBgImg.color = new Color(0.02f, 0.05f, 0.11f, 0.94f); // Rich dark navy
            mainBgImg.raycastTarget = false;
            var mainBgRT = mainBg.GetComponent<RectTransform>();
            mainBgRT.anchorMin = Vector2.zero;
            mainBgRT.anchorMax = Vector2.one;
            mainBgRT.sizeDelta = Vector2.zero;

            // Header Border Line
            var borderLine = new GameObject("BorderTop");
            borderLine.transform.SetParent(canvasGO.transform, false);
            var borderImg = borderLine.AddComponent<Image>();
            borderImg.color = new Color(0.12f, 0.85f, 0.95f, 0.85f); // Cyan accent
            borderImg.raycastTarget = false;
            var borderRT = borderLine.GetComponent<RectTransform>();
            borderRT.anchoredPosition = new Vector2(0f, 305f);
            borderRT.sizeDelta = new Vector2(1100f, 4f);

            // ── Header Text ──
            MakeTMP(canvasGO.transform, "AppTitle", "EnCare VR",
                new Vector2(0f, 240f), new Vector2(900f, 60f), 52f,
                new Color(0.20f, 0.95f, 1f), FontStyles.Bold);

            MakeTMP(canvasGO.transform, "Subtitle", "ENVIRONMENT SELECTOR",
                new Vector2(0f, 185f), new Vector2(900f, 35f), 22f,
                new Color(0.70f, 0.85f, 0.95f), FontStyles.Normal);

            MakeTMP(canvasGO.transform, "Hint", "Point controller ray & pull trigger to choose a map",
                new Vector2(0f, 150f), new Vector2(900f, 30f), 18f,
                new Color(0.40f, 0.75f, 0.85f), FontStyles.Italic);

            // ── Side-by-Side Level Cards ──
            // Left Card: Office (Entire card surface is a button for effortless VR aiming)
            var (officeCardBtn, officePlayBtn) = CreateMapCard(
                canvasGO.transform,
                name: "Card_Office",
                posX: -260f,
                badge: "[ INDOOR ENVIRONMENT ]",
                badgeColor: new Color(0.20f, 0.90f, 1f),
                title: "Office Workspace",
                titleColor: Color.white,
                desc: "Search desk stations and floor areas for discarded e-waste components and deposit them into the collection basket.",
                tags: "• 10 Items  •  2:00 Minutes  •  Physical Basket",
                btnLabel: "PLAY OFFICE",
                btnBgColor: new Color(0.08f, 0.58f, 0.82f, 1f),
                btnHighlightColor: new Color(0.18f, 0.75f, 0.98f, 1f),
                cardBorderColor: new Color(0.12f, 0.70f, 0.90f, 0.60f)
            );

            // Right Card: Garden (Entire card surface is a button for effortless VR aiming)
            var (gardenCardBtn, gardenPlayBtn) = CreateMapCard(
                canvasGO.transform,
                name: "Card_Garden",
                posX: 260f,
                badge: "[ OUTDOOR ENVIRONMENT ]",
                badgeColor: new Color(0.25f, 1f, 0.45f),
                title: "Backyard Garden",
                titleColor: Color.white,
                desc: "Explore patio decks, walkway paths, and garden lawns to locate and clear clinical biohazard waste across the yard.",
                tags: "• 15 Spawn Points  •  2:00 Minutes  •  Open Yard",
                btnLabel: "PLAY GARDEN",
                btnBgColor: new Color(0.12f, 0.68f, 0.35f, 1f),
                btnHighlightColor: new Color(0.22f, 0.85f, 0.45f, 1f),
                cardBorderColor: new Color(0.20f, 0.85f, 0.40f, 0.60f)
            );

            // Wire all buttons to StartMenuUI
            menuUI.OfficeButton = officePlayBtn;
            menuUI.OfficeCardButton = officeCardBtn;
            menuUI.GardenButton = gardenPlayBtn;
            menuUI.GardenCardButton = gardenCardBtn;

            UnityEventTools.AddVoidPersistentListener(officePlayBtn.onClick, menuUI.PlayOffice);
            UnityEventTools.AddVoidPersistentListener(officeCardBtn.onClick, menuUI.PlayOffice);
            UnityEventTools.AddVoidPersistentListener(gardenPlayBtn.onClick, menuUI.PlayGarden);
            UnityEventTools.AddVoidPersistentListener(gardenCardBtn.onClick, menuUI.PlayGarden);

            // Set canvas and all UI elements to layer "UI" (Layer 5)
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                SetLayerRecursively(canvasGO, uiLayer);
            }

            EditorUtility.SetDirty(canvasGO);
            Debug.Log("[EnCare] Start Menu built successfully with XRUIInputModule and interactive map cards.");
        }

        private static (Button cardBtn, Button playBtn) CreateMapCard(
            Transform parent,
            string name,
            float posX,
            string badge,
            Color badgeColor,
            string title,
            Color titleColor,
            string desc,
            string tags,
            string btnLabel,
            Color btnBgColor,
            Color btnHighlightColor,
            Color cardBorderColor)
        {
            var cardGO = new GameObject(name);
            cardGO.transform.SetParent(parent, false);

            var cardRT = cardGO.AddComponent<RectTransform>();
            cardRT.anchoredPosition = new Vector2(posX, -70f);
            cardRT.sizeDelta = new Vector2(480f, 380f);

            // Card Background (raycastTarget = true makes the whole card surface responsive)
            var cardBg = cardGO.AddComponent<Image>();
            cardBg.color = new Color(0.04f, 0.09f, 0.16f, 0.94f);
            cardBg.raycastTarget = true;

            var cardBtn = cardGO.AddComponent<Button>();
            cardBtn.targetGraphic = cardBg;
            var cardColors = cardBtn.colors;
            cardColors.normalColor = new Color(0.04f, 0.09f, 0.16f, 0.94f);
            cardColors.highlightedColor = new Color(0.08f, 0.18f, 0.28f, 0.98f);
            cardColors.pressedColor = new Color(0.02f, 0.05f, 0.10f, 0.98f);
            cardColors.selectedColor = cardColors.highlightedColor;
            cardBtn.colors = cardColors;

            // Card Border Outline
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(cardGO.transform, false);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = cardBorderColor;
            borderImg.raycastTarget = false;
            var borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = Vector2.zero;

            // Inset background to leave border visible
            var innerBgGO = new GameObject("InnerBg");
            innerBgGO.transform.SetParent(cardGO.transform, false);
            var innerBgImg = innerBgGO.AddComponent<Image>();
            innerBgImg.color = new Color(0.04f, 0.08f, 0.15f, 0.95f);
            innerBgImg.raycastTarget = false;
            var innerBgRT = innerBgGO.GetComponent<RectTransform>();
            innerBgRT.anchorMin = Vector2.zero;
            innerBgRT.anchorMax = Vector2.one;
            innerBgRT.sizeDelta = new Vector2(-4f, -4f);

            // Badge
            MakeTMP(cardGO.transform, "Badge", badge,
                new Vector2(0f, 150f), new Vector2(440f, 28f), 16f,
                badgeColor, FontStyles.Bold);

            // Title
            MakeTMP(cardGO.transform, "Title", title,
                new Vector2(0f, 110f), new Vector2(440f, 45f), 30f,
                titleColor, FontStyles.Bold);

            // Description
            MakeTMP(cardGO.transform, "Description", desc,
                new Vector2(0f, 35f), new Vector2(420f, 90f), 19f,
                new Color(0.85f, 0.90f, 0.96f), FontStyles.Normal);

            // Tags
            MakeTMP(cardGO.transform, "Tags", tags,
                new Vector2(0f, -45f), new Vector2(420f, 35f), 17f,
                new Color(0.60f, 0.80f, 0.90f), FontStyles.Italic);

            // Prominent Action Button
            var btnGO = new GameObject("PlayButton");
            btnGO.transform.SetParent(cardGO.transform, false);

            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchoredPosition = new Vector2(0f, -135f);
            btnRT.sizeDelta = new Vector2(400f, 62f);

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = btnBgColor;
            btnImg.raycastTarget = true;

            var playBtn = btnGO.AddComponent<Button>();
            playBtn.targetGraphic = btnImg;

            var colors = playBtn.colors;
            colors.normalColor = btnBgColor;
            colors.highlightedColor = btnHighlightColor;
            colors.pressedColor = btnBgColor * 0.7f;
            colors.selectedColor = btnHighlightColor;
            playBtn.colors = colors;

            MakeTMP(btnGO.transform, "Label", btnLabel,
                Vector2.zero, new Vector2(380f, 50f), 24f,
                Color.white, FontStyles.Bold);

            return (cardBtn, playBtn);
        }

        private static GameObject MakeTMP(Transform parent, string name, string text,
            Vector2 pos, Vector2 size, float fontSize, Color color, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            return go;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
