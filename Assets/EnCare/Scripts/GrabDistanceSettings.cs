using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace EnCare
{
    /// <summary>
    /// Limits and customizes the interaction and grab distance in VR.
    /// Provides inspector-editable controls for ray grab reach and touch-only modes.
    /// </summary>
    [DisallowMultipleComponent]
    public class GrabDistanceSettings : MonoBehaviour
    {
        [Header("Grab Distance Settings")]
        [Tooltip("Maximum reach in meters for far ray grabbing of trash items and the basket. Default is 2.5m.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float m_MaxGrabDistance = 2.5f;

        [Tooltip("If enabled, completely disables far ray casting so objects must be grabbed within physical touch reach.")]
        [SerializeField] private bool m_DisableFarGrab = false;

        public float MaxGrabDistance
        {
            get => m_MaxGrabDistance;
            set
            {
                m_MaxGrabDistance = Mathf.Max(0.2f, value);
                ApplySettings();
            }
        }

        public bool DisableFarGrab
        {
            get => m_DisableFarGrab;
            set
            {
                m_DisableFarGrab = value;
                ApplySettings();
            }
        }

        private void Awake()
        {
            ApplySettings();
        }

        private void Start()
        {
            ApplySettings();
        }

        private void OnValidate()
        {
            ApplySettings();
        }

        /// <summary>
        /// Automatically ensures grab settings are applied when entering Play Mode.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureSettings()
        {
            var existing = Object.FindFirstObjectByType<GrabDistanceSettings>();
            if (existing == null)
            {
                var go = new GameObject("GrabDistanceSettings (Auto)");
                existing = go.AddComponent<GrabDistanceSettings>();
            }
            existing.ApplySettings();
        }

        /// <summary>
        /// Finds and configures all interactors and casters in the scene.
        /// </summary>
        [ContextMenu("Apply Grab Settings Now")]
        public void ApplySettings()
        {
            // 1. Update CurveInteractionCaster components (used by NearFarInteractor in XRI 3.x)
            var casters = Object.FindObjectsByType<CurveInteractionCaster>(FindObjectsSortMode.None);
            foreach (var caster in casters)
            {
                if (caster == null) continue;
                caster.castDistance = m_MaxGrabDistance;
            }

            // 2. Update XRRayInteractors (excluding teleport rays)
            var rayInteractors = Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
            foreach (var ray in rayInteractors)
            {
                if (ray == null) continue;
                if (!ray.gameObject.name.ToLower().Contains("teleport"))
                {
                    ray.maxRaycastDistance = m_MaxGrabDistance;
                }
            }

            // 3. Update NearFarInteractors
            var nearFarInteractors = Object.FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
            foreach (var nf in nearFarInteractors)
            {
                if (nf == null) continue;
                nf.enableFarCasting = !m_DisableFarGrab;
            }
        }
    }
}
