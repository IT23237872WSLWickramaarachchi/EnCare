using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnCare
{
    /// <summary>
    /// Adds a glowing highlight to trash items.
    /// Supports material emission property blocks, inverted-hull mesh outlines, and optional point lights,
    /// rhythmically pulsing opacity between 0 and 1 to draw the player's attention.
    /// Works seamlessly on both primitive shapes and complex multi-part 3D models.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrashGlow : MonoBehaviour
    {
        [Header("Glow Appearance")]
        [ColorUsage(false, true)]
        [SerializeField] private Color glowColor = new Color(0f, 0.9f, 1f, 1f);

        [Min(0)]
        [SerializeField] private float intensity = 2.5f;

        [Header("Pulsing Effect")]
        [Tooltip("Enable a soft pulsing glow to draw the player's attention.")]
        [SerializeField] private bool enablePulse = true;

        [Tooltip("Speed of the pulse cycle in seconds.")]
        [Range(0.2f, 5f)]
        [SerializeField] private float pulseSpeed = 1.2f;

        [Tooltip("Minimum opacity/intensity during pulsing (0 to 1).")]
        [Range(0f, 1f)]
        [SerializeField] private float minPulseOpacity = 0.0f;

        [Tooltip("Maximum opacity/intensity during pulsing (0 to 1).")]
        [Range(0f, 1f)]
        [SerializeField] private float maxPulseOpacity = 1.0f;

        [Header("Reveal Animation")]
        [Tooltip("Duration in seconds for the glow to fade in from darkness when the object first appears. Set to 0 to skip.")]
        [Range(0f, 5f)]
        [SerializeField] private float revealDuration = 1.5f;

        [Header("Light Glow (Optional)")]
        [Tooltip("Automatically create a subtle point light glow child if enabled.")]
        [SerializeField] private bool usePointLight = true;

        [Range(0.2f, 3f)]
        [SerializeField] private float lightRange = 0.8f;

        [Range(0.1f, 5f)]
        [SerializeField] private float lightIntensity = 1.0f;

        [Header("Mesh Outline")]
        [Tooltip("Material using EnCare/TrashOutlineURP.")]
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private MeshRenderer[] sourceRenderers = new MeshRenderer[0];
        [Range(0.001f, 0.03f)] [SerializeField] private float width = 0.006f;
        [SerializeField] private bool smoothOutlineNormals = false;

        private Renderer[] allRenderers;
        private MaterialPropertyBlock propertyBlock;
        private Light pointLight;
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
        private static readonly int OutlineColorPropertyId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthPropertyId = Shader.PropertyToID("_OutlineWidth");

        private readonly List<GameObject> shells = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Material runtimeMaterial;

        private float revealTimer;
        private bool revealComplete;

        public Color GlowColor
        {
            get => glowColor;
            set
            {
                glowColor = value;
                UpdateEmission(1f);
            }
        }

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            InitializeRenderers();
            SetupPointLight();
            SetupMeshOutlines();

            // Start dark if reveal animation is active
            if (revealDuration > 0f)
            {
                revealTimer = 0f;
                revealComplete = false;
                UpdateEmission(0f);
            }
            else
            {
                revealComplete = true;
                UpdateEmission(1f);
            }
        }

        private void InitializeRenderers()
        {
            allRenderers = GetComponentsInChildren<Renderer>(true);

            // Enable emission keyword on all child materials so standard and URP Lit materials glow
            foreach (var rend in allRenderers)
            {
                if (rend == null || rend.gameObject.name == "EnCare Outline") continue;
                foreach (var mat in rend.materials)
                {
                    if (mat != null)
                    {
                        mat.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        private void SetupPointLight()
        {
            if (!usePointLight) return;

            var lightObj = new GameObject("Glow_PointLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = Vector3.zero;

            pointLight = lightObj.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.range = lightRange;
            pointLight.intensity = lightIntensity;
            pointLight.color = glowColor;
            pointLight.shadows = LightShadows.None;
        }

        private void SetupMeshOutlines()
        {
            // If runtime material not created, create one
            if (runtimeMaterial == null)
            {
                if (outlineMaterial != null)
                {
                    runtimeMaterial = new Material(outlineMaterial);
                }
                else
                {
                    Shader shader = Shader.Find("EnCare/TrashOutlineURP");
                    if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader != null)
                    {
                        runtimeMaterial = new Material(shader);
                    }
                }
            }

            if (runtimeMaterial == null) return;

            // Find all mesh filters in children if sourceRenderers was not explicitly configured
            var targetFilters = new List<(MeshFilter filter, Renderer rend)>();
            if (sourceRenderers != null && sourceRenderers.Length > 0)
            {
                foreach (var sr in sourceRenderers)
                {
                    if (sr == null) continue;
                    var mf = sr.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        targetFilters.Add((mf, sr));
                }
            }
            else
            {
                var filters = GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in filters)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (mf.gameObject.name == "EnCare Outline") continue;
                    var rend = mf.GetComponent<Renderer>();
                    if (rend != null)
                        targetFilters.Add((mf, rend));
                }
            }

            foreach (var (filter, sourceRend) in targetFilters)
            {
                Mesh mesh = filter.sharedMesh;
                if (smoothOutlineNormals && mesh.isReadable)
                {
                    mesh = Instantiate(mesh);
                    SmoothNormals(mesh);
                    meshes.Add(mesh);
                }

                var shell = new GameObject("EnCare Outline");
                shell.layer = sourceRend.gameObject.layer;
                shell.transform.SetParent(filter.transform, false);
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localRotation = Quaternion.identity;
                shell.transform.localScale = Vector3.one;

                var shellFilter = shell.AddComponent<MeshFilter>();
                shellFilter.sharedMesh = mesh;

                var renderer = shell.AddComponent<MeshRenderer>();
                var materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = runtimeMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Bounds bounds = mesh.bounds;
                bounds.Expand(width * 4f);
                renderer.localBounds = bounds;
                shells.Add(shell);
            }
        }

        private void Update()
        {
            // Reveal phase: fade in from darkness
            float revealFactor = 1f;
            if (!revealComplete)
            {
                revealTimer += Time.deltaTime;
                if (revealTimer >= revealDuration)
                {
                    revealComplete = true;
                }
                else
                {
                    revealFactor = Mathf.SmoothStep(0f, 1f, revealTimer / revealDuration);
                }
            }

            // Pulse phase: opacity oscillating rhythmically between minPulseOpacity (0) and maxPulseOpacity (1)
            float pulseOpacity = 1f;
            if (enablePulse)
            {
                // Sine wave normalized to [0, 1]
                float sine = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                // Smooth Hermite interpolation for pleasing pulse rhythm
                float smoothSine = Mathf.SmoothStep(0f, 1f, sine);
                pulseOpacity = Mathf.Lerp(minPulseOpacity, maxPulseOpacity, smoothSine);
            }

            float currentFactor = revealFactor * pulseOpacity;
            UpdateEmission(currentFactor);
        }

        private void UpdateEmission(float factor)
        {
            // Color with alpha/opacity for transparency blending
            Color activeOutlineColor = new Color(
                glowColor.r * intensity,
                glowColor.g * intensity,
                glowColor.b * intensity,
                Mathf.Clamp01(factor)
            );

            // Update Outline Material
            if (runtimeMaterial != null)
            {
                runtimeMaterial.SetColor(OutlineColorPropertyId, activeOutlineColor);
                runtimeMaterial.SetFloat(OutlineWidthPropertyId, width);
            }

            // Update Child Material Emission
            Color activeEmissionColor = glowColor * (intensity * factor);
            if (allRenderers != null)
            {
                for (int i = 0; i < allRenderers.Length; i++)
                {
                    var rend = allRenderers[i];
                    if (rend == null || rend.gameObject.name == "EnCare Outline") continue;

                    rend.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(EmissionColorPropertyId, activeEmissionColor);
                    rend.SetPropertyBlock(propertyBlock);
                }
            }

            // Update Light intensity
            if (pointLight != null && usePointLight)
            {
                pointLight.color = glowColor;
                pointLight.intensity = lightIntensity * factor;
            }
        }

        public void SetColor(Color color)
        {
            glowColor = color;
            UpdateEmission(1f);
        }

        public void SetGlowEnabled(bool enabledState)
        {
            this.enabled = enabledState;
            if (pointLight != null)
            {
                pointLight.enabled = enabledState;
            }

            foreach (var shell in shells)
            {
                if (shell != null) shell.SetActive(enabledState);
            }

            if (!enabledState && allRenderers != null)
            {
                for (int i = 0; i < allRenderers.Length; i++)
                {
                    var rend = allRenderers[i];
                    if (rend == null || rend.gameObject.name == "EnCare Outline") continue;

                    rend.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(EmissionColorPropertyId, Color.black);
                    rend.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private static void SmoothNormals(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals.Length != vertices.Length) return;
            var sums = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                sums.TryGetValue(vertices[i], out Vector3 sum);
                sums[vertices[i]] = sum + normals[i];
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = sums[vertices[i]].normalized;
            mesh.normals = normals;
        }

        private void OnEnable()
        {
            foreach (GameObject shell in shells) if (shell != null) shell.SetActive(true);
            if (pointLight != null) pointLight.enabled = true;
        }

        private void OnDisable()
        {
            foreach (GameObject shell in shells) if (shell != null) shell.SetActive(false);
            if (pointLight != null) pointLight.enabled = false;
        }

        private void OnDestroy()
        {
            foreach (GameObject shell in shells) if (shell != null) Destroy(shell);
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}

