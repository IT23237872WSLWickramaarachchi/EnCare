using System;
using UnityEngine;

namespace EnCare.VR
{
    /// <summary>
    /// Adds a customizable glow/highlight effect to an E-Waste item.
    /// Can use Material Emission, Point Light, or both for clear visibility in VR,
    /// rhythmically pulsing opacity between 0 and 1.
    /// Works with all 3D models and multi-renderer prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public class EWasteGlow : MonoBehaviour
    {
        [Header("Glow Appearance")]
        [Tooltip("The glow color. Supports HDR for bright Bloom in Universal RP.")]
        [ColorUsage(true, true)]
        [SerializeField] private Color m_GlowColor = new Color(0f, 0.9f, 1f, 1f); // Neon Cyan default

        [Tooltip("Base glow intensity multiplier.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float m_BaseIntensity = 2.5f;

        [Header("Pulsing Effect")]
        [Tooltip("Enable a soft pulsing glow to draw the player's attention.")]
        [SerializeField] private bool m_EnablePulse = true;

        [Tooltip("Speed of the pulse cycle in seconds.")]
        [Range(0.2f, 5f)]
        [SerializeField] private float m_PulseSpeed = 1.2f;

        [Tooltip("Minimum opacity/intensity during pulsing (0 to 1).")]
        [Range(0f, 1f)]
        [SerializeField] private float m_MinPulseOpacity = 0.0f;

        [Tooltip("Maximum opacity/intensity during pulsing (0 to 1).")]
        [Range(0f, 1f)]
        [SerializeField] private float m_MaxPulseOpacity = 1.0f;

        [Header("Reveal Animation")]
        [Tooltip("Duration in seconds for the glow to fade in from darkness when the object first appears. Set to 0 to skip.")]
        [Range(0f, 5f)]
        [SerializeField] private float m_RevealDuration = 1.5f;

        [Header("Light Glow (Optional)")]
        [Tooltip("Automatically create a subtle point light glow child if enabled.")]
        [SerializeField] private bool m_UsePointLight = true;

        [Tooltip("Range of the point light.")]
        [Range(0.2f, 3f)]
        [SerializeField] private float m_LightRange = 0.8f;

        [Tooltip("Intensity of the point light.")]
        [Range(0.1f, 5f)]
        [SerializeField] private float m_LightIntensity = 1.0f;

        private Renderer[] m_Renderers;
        private MaterialPropertyBlock m_PropertyBlock;
        private Light m_PointLight;
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

        private float m_RevealTimer;
        private bool m_RevealComplete;

        public Color GlowColor
        {
            get => m_GlowColor;
            set
            {
                m_GlowColor = value;
                UpdateGlow(1f);
            }
        }

        public void SetColor(Color color)
        {
            GlowColor = color;
        }

        private void Awake()
        {
            m_PropertyBlock = new MaterialPropertyBlock();
            InitializeRenderers();
            SetupPointLight();

            // Start dark if reveal animation is active
            if (m_RevealDuration > 0f)
            {
                m_RevealTimer = 0f;
                m_RevealComplete = false;
                UpdateGlow(0f);
            }
            else
            {
                m_RevealComplete = true;
                UpdateGlow(1f);
            }
        }

        private void InitializeRenderers()
        {
            m_Renderers = GetComponentsInChildren<Renderer>(true);

            // Enable emission keyword on all materials across all child renderers
            foreach (var rend in m_Renderers)
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
            if (!m_UsePointLight) return;

            GameObject lightObj = new GameObject("Glow_PointLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = Vector3.zero;

            m_PointLight = lightObj.AddComponent<Light>();
            m_PointLight.type = LightType.Point;
            m_PointLight.range = m_LightRange;
            m_PointLight.intensity = m_LightIntensity;
            m_PointLight.color = m_GlowColor;
            m_PointLight.shadows = LightShadows.None;
        }

        private void Update()
        {
            // Reveal phase: fade in from darkness
            float revealFactor = 1f;
            if (!m_RevealComplete)
            {
                m_RevealTimer += Time.deltaTime;
                if (m_RevealTimer >= m_RevealDuration)
                {
                    m_RevealComplete = true;
                }
                else
                {
                    revealFactor = Mathf.SmoothStep(0f, 1f, m_RevealTimer / m_RevealDuration);
                }
            }

            // Pulse phase: rhythmic breathing between min (0) and max (1)
            float pulseFactor = 1f;
            if (m_EnablePulse)
            {
                float sine = (Mathf.Sin(Time.time * m_PulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                float smoothSine = Mathf.SmoothStep(0f, 1f, sine);
                pulseFactor = Mathf.Lerp(m_MinPulseOpacity, m_MaxPulseOpacity, smoothSine);
            }

            UpdateGlow(revealFactor * pulseFactor);
        }

        private void UpdateGlow(float factor)
        {
            Color activeEmission = m_GlowColor * (m_BaseIntensity * factor);

            if (m_Renderers != null)
            {
                for (int i = 0; i < m_Renderers.Length; i++)
                {
                    var rend = m_Renderers[i];
                    if (rend == null) continue;

                    rend.GetPropertyBlock(m_PropertyBlock);
                    m_PropertyBlock.SetColor(EmissionColorPropertyId, activeEmission);
                    rend.SetPropertyBlock(m_PropertyBlock);
                }
            }

            if (m_PointLight != null && m_UsePointLight)
            {
                m_PointLight.color = m_GlowColor;
                m_PointLight.intensity = m_LightIntensity * factor;
            }
        }

        /// <summary>
        /// Turn the glow on or off (e.g. when collected into the basket).
        /// </summary>
        public void SetGlowEnabled(bool enabledState)
        {
            this.enabled = enabledState;
            if (m_PointLight != null)
            {
                m_PointLight.enabled = enabledState;
            }

            if (!enabledState && m_Renderers != null)
            {
                for (int i = 0; i < m_Renderers.Length; i++)
                {
                    var rend = m_Renderers[i];
                    if (rend == null) continue;

                    rend.GetPropertyBlock(m_PropertyBlock);
                    m_PropertyBlock.SetColor(EmissionColorPropertyId, Color.black);
                    rend.SetPropertyBlock(m_PropertyBlock);
                }
            }
        }
    }
}

