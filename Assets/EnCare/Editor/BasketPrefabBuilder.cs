using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using EnCare;

namespace EnCare.Editor
{
    [InitializeOnLoad]
    public static class BasketPrefabBuilder
    {
        public const string k_ModelPath    = "Assets/EnCare/Models/Collectors/Basket01.fbx";
        public const string k_MaterialPath = "Assets/EnCare/Models/Materials/M_Blue.mat";
        public const string k_PrefabPath   = "Assets/EnCare/Prefabs/Basket.prefab";

        static BasketPrefabBuilder()
        {
            // Verify on editor startup or script compile if prefab needs building
            EditorApplication.delayCall += CheckAndBuildIfRequested;
        }

        public static void CheckAndBuildIfRequested()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(k_PrefabPath);
            if (existing == null || existing.transform.Find("Model") == null)
            {
                RebuildBasketPrefab();
            }
        }

        [MenuItem("EnCare/\U0001F527 Rebuild Basket Prefab (Basket01 + M_Blue)", false, 1)]
        public static GameObject RebuildBasketPrefab()
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[EnCare] Cannot find model at {k_ModelPath}");
                return null;
            }

            var blueMat = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (blueMat == null)
            {
                Debug.LogError($"[EnCare] Cannot find material at {k_MaterialPath}");
                return null;
            }

            // Create root Basket GameObject
            var root = new GameObject("Basket");

            // Rigidbody for VR physics & tossing
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // XR Grab Interactable for VR grabbing
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.selectMode = InteractableSelectMode.Single;
            grab.movementType = XRGrabInteractable.MovementType.VelocityTracking;
            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.useDynamicAttach = true;

            // Instantiate Basket01 model as child
            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            modelInstance.name = "Model";
            modelInstance.transform.SetParent(root.transform, false);

            // Basket01 raw mesh dimensions are ~8.7 x 9.3 x 11.7 units.
            // Scale by 0.041 to make it realistic VR handheld basket dimensions (~36cm W x 24cm H x 48cm L)
            float modelScale = 0.041f;
            modelInstance.transform.localScale = new Vector3(modelScale, modelScale, modelScale);
            // Offset Y so bottom of basket bucket sits at local Y=0
            modelInstance.transform.localPosition = new Vector3(0f, 0.976f * modelScale, 0f);
            modelInstance.transform.localRotation = Quaternion.identity;

            // Apply M_Blue.mat to all renderers in the model
            var renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var rend in renderers)
            {
                var mats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = blueMat;
                }
                rend.sharedMaterials = mats;
            }

            // GripPoint — where the VR hand attaches when grabbed (at handle)
            var gripGO = new GameObject("GripPoint");
            gripGO.transform.SetParent(root.transform, false);
            gripGO.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            grab.attachTransform = gripGO.transform;

            // Physics Colliders: Bottom and 4 Walls so it behaves as an open container in VR
            float wallThick = 0.02f;
            float basketWidth = 0.36f;
            float basketLength = 0.48f;
            float basketHeight = 0.24f;

            // Bottom
            AddBoxCol(root, "Col_Bottom",
                new Vector3(0f, wallThick * 0.5f, 0f),
                new Vector3(basketWidth, wallThick, basketLength));

            // Wall Front (+Z)
            AddBoxCol(root, "Col_Front",
                new Vector3(0f, basketHeight * 0.5f, basketLength * 0.5f - wallThick * 0.5f),
                new Vector3(basketWidth, basketHeight, wallThick));

            // Wall Back (-Z)
            AddBoxCol(root, "Col_Back",
                new Vector3(0f, basketHeight * 0.5f, -basketLength * 0.5f + wallThick * 0.5f),
                new Vector3(basketWidth, basketHeight, wallThick));

            // Wall Left (-X)
            AddBoxCol(root, "Col_Left",
                new Vector3(-basketWidth * 0.5f + wallThick * 0.5f, basketHeight * 0.5f, 0f),
                new Vector3(wallThick, basketHeight, basketLength));

            // Wall Right (+X)
            AddBoxCol(root, "Col_Right",
                new Vector3(basketWidth * 0.5f - wallThick * 0.5f, basketHeight * 0.5f, 0f),
                new Vector3(wallThick, basketHeight, basketLength));

            // Collection Zone (Trigger) inside the basket cavity
            var zoneGO = new GameObject("CollectionZone");
            zoneGO.transform.SetParent(root.transform, false);
            zoneGO.transform.localPosition = new Vector3(0f, basketHeight * 0.5f, 0f);

            var trigger = zoneGO.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(
                basketWidth - wallThick * 2.5f,
                basketHeight * 0.85f,
                basketLength - wallThick * 2.5f
            );
            trigger.center = Vector3.zero;

            var collector = zoneGO.AddComponent<GarbageCollector>();
            var collSO = new SerializedObject(collector);
            collSO.FindProperty("trashLayers").intValue = ~0; // All layers
            collSO.ApplyModifiedPropertiesWithoutUndo();

            // Save as Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"\u2705 [EnCare] Successfully rebuilt {k_PrefabPath} using model {k_ModelPath} and material {k_MaterialPath}!");
            return prefab;
        }

        static void AddBoxCol(GameObject parent, string name, Vector3 center, Vector3 size)
        {
            var colGO = new GameObject(name);
            colGO.transform.SetParent(parent.transform, false);
            colGO.transform.localPosition = center;
            var box = colGO.AddComponent<BoxCollider>();
            box.size = size;
        }
    }
}
