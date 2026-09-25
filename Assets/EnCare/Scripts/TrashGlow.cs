using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnCare
{
    /// <summary>
    /// Adds a glowing highlight to trash items.
    /// Supports material emission property blocks and optional point lights,
    /// working seamlessly on both primitive shapes and complex 3D meshes with slow pulsing.
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
        [SerializeField] private float pulseSpeed = 1.5f;

        [Tooltip("Minimum intensity during pulsing (fraction of base intensity).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float minPulseFraction = 0.35f;

        [Header("Reveal Animation")]
        [Tooltip("Duration in seconds for the glow to fade in from darkness when the object first appears. Set to 0 to skip.")]
        [Range(0f, 5f)]
        [SerializeField] private float revealDuration = 2.0f;

        [Header("Light Glow (Optional)")]
        [Tooltip("Automatically create a subtle point light glow child if enabled.")]
        [SerializeField] private bool usePointLight = true;

        [Range(0.2f, 3f)]
        [SerializeField] private float lightRange = 0.8f;

        [Range(0.1f, 5f)]
        [SerializeField] private float lightIntensity = 1.0f;

        [Header("Legacy Mesh Outline (Optional)")]
        [Tooltip("Material using EnCare/TrashOutlineURP (optional fallback).")]
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private MeshRenderer[] sourceRenderers = new MeshRenderer[0];
        [Range(0.001f, 0.03f)] [SerializeField] private float width = 0.006f;
        [SerializeField] private bool smoothOutlineNormals = false;

        private Renderer[] allRenderers;
        private MaterialPropertyBlock propertyBlock;
        private Light pointLight;
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

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
                if (runtimeMaterial != null) ApplyOutlineColor();
            }
        }

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            InitializeRenderers();
            SetupPointLight();

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
                if (rend == null) continue;
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

        private void Start()
        {
            // Optional legacy outline if outlineMaterial is assigned and has valid source renderers
            if (outlineMaterial != null && sourceRenderers != null && sourceRenderers.Length > 0)
            {
                SetupLegacyOutline();
            }
        }

        private void SetupLegacyOutline()
        {
            runtimeMaterial = new Material(outlineMaterial);
            ApplyOutlineColor();
            foreach (MeshRenderer source in sourceRenderers)
            {
                if (source == null) continue;
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh;
                if (smoothOutlineNormals && mesh.isReadable)
                {
                    mesh = Instantiate(mesh);
                    SmoothNormals(mesh);
                    meshes.Add(mesh);
                }
                var shell = new GameObject("EnCare Outline");
                shell.layer = source.gameObject.layer;
                shell.transform.SetParent(source.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = shell.AddComponent<MeshRenderer>();
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

            // Pulse phase: slow breathing glow
            float pulseFactor = 1f;
            if (enablePulse)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
                pulseFactor = Mathf.Lerp(minPulseFraction, 1f, t);
            }

            UpdateEmission(revealFactor * pulseFactor);
        }

        private void UpdateEmission(float factor)
        {
            Color activeColor = glowColor * (intensity * factor);

            if (allRenderers != null)
            {
                for (int i = 0; i < allRenderers.Length; i++)
                {
                    var rend = allRenderers[i];
                    if (rend == null) continue;

                    rend.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(EmissionColorPropertyId, activeColor);
                    rend.SetPropertyBlock(propertyBlock);
                }
            }

            if (pointLight != null && usePointLight)
            {
                pointLight.color = glowColor;
                pointLight.intensity = lightIntensity * factor;
            }
        }

        public void SetColor(Color color)
        {
            GlowColor = color;
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
                    if (rend == null) continue;

                    rend.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(EmissionColorPropertyId, Color.black);
                    rend.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private void ApplyOutlineColor()
        {
            if (runtimeMaterial == null) return;
            runtimeMaterial.SetColor("_OutlineColor", glowColor * intensity);
            runtimeMaterial.SetFloat("_OutlineWidth", width);
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
