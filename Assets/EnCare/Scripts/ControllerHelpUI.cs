using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnCare
{
    /// <summary>
    /// Displays individual glance-activated controller button callouts in VR.
    /// Each implemented control has a compact label linked to its physical button.
    /// Callouts fade in smoothly only when the player glances towards that controller,
    /// and smoothly fade out during forward-looking gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public class ControllerHelpUI : MonoBehaviour
    {
        public enum ControllerHand { Left, Right, Custom }

        [Serializable]
        public class ControlCallout
        {
            [Tooltip("Identifier for the control (e.g. 'Thumbstick', 'Grip', 'Trigger').")]
            public string controlId = "Thumbstick";

            [Tooltip("Concise description of what this control does in the game.")]
            public string actionDescription = "Move";

            [Tooltip("Transform of the physical button on the controller model. If null, fallbackLocalAnchor is used.")]
            public Transform buttonAnchor;

            [Tooltip("Fallback local position relative to the controller root if no anchor Transform is assigned.")]
            public Vector3 fallbackLocalAnchor = Vector3.zero;

            [Tooltip("Offset in world/controller space where the label floats relative to the button anchor.")]
            public Vector3 labelOffset = new Vector3(0.04f, 0.03f, 0f);

            [Tooltip("Optional context condition or dynamic override text.")]
            [HideInInspector]
            public Func<string> dynamicTextProvider;

            // Runtime UI handles
            [HideInInspector] public GameObject calloutGO;
            [HideInInspector] public RectTransform labelRT;
            [HideInInspector] public TextMeshProUGUI controlIdTMP;
            [HideInInspector] public TextMeshProUGUI actionTMP;
            [HideInInspector] public LineRenderer leaderLine;
        }

        [Header("Handedness & Callouts")]
        [SerializeField] private ControllerHand m_Hand = ControllerHand.Left;
        [SerializeField] private List<ControlCallout> m_Callouts = new List<ControlCallout>();

        [Header("Gaze & Glance Settings")]
        [Tooltip("The XR camera representing the player's head. Defaults to Camera.main.")]
        [SerializeField] private Camera m_PlayerCamera;

        [Tooltip("Transform representing the controller's gaze reference point. Defaults to this transform.")]
        [SerializeField] private Transform m_ControllerReference;

        [Tooltip("Dot product threshold (0.0 to 1.0) for gaze detection. 0.72 = ~44 degree cone.")]
        [Range(0.50f, 0.95f)]
        [SerializeField] private float m_FacingThreshold = 0.72f;

        [Tooltip("Hysteresis offset to prevent flickering at the threshold boundary.")]
        [Range(0.01f, 0.20f)]
        [SerializeField] private float m_Hysteresis = 0.08f;

        [Tooltip("Minimum distance from head in meters for hints to show.")]
        [SerializeField] private float m_MinDistance = 0.15f;

        [Tooltip("Maximum distance from head in meters for hints to show.")]
        [SerializeField] private float m_MaxDistance = 1.25f;

        [Tooltip("Delay in seconds of steady glance before callouts appear.")]
        [Range(0f, 1.0f)]
        [SerializeField] private float m_DwellDelay = 0.18f;

        [Tooltip("Speed of fade in / fade out.")]
        [SerializeField] private float m_FadeSpeed = 5.0f;

        [Header("Visual Styling")]
        [SerializeField] private Color m_CardBgColor = new Color(0.03f, 0.07f, 0.14f, 0.88f);
        [SerializeField] private Color m_BorderColor = new Color(0.12f, 0.85f, 0.95f, 0.55f);
        [SerializeField] private Color m_AccentColor = new Color(0.20f, 0.92f, 1.00f, 1.00f);
        [SerializeField] private Color m_TextColor = Color.white;
        [SerializeField] private bool m_ShowLeaderLines = true;

        [Header("UI Containers")]
        [SerializeField] private CanvasGroup m_CanvasGroup;
        [SerializeField] private GameObject m_VisualsRoot;

        // Runtime state
        private bool m_IsLookingAt;
        private float m_GazeTimer;
        private float m_CurrentAlpha;
        private bool m_Initialized;
        private Material m_LineMaterial;

        public ControllerHand Hand
        {
            get => m_Hand;
            set => m_Hand = value;
        }

        public List<ControlCallout> Callouts => m_Callouts;

        private void Awake()
        {
            EnsureCamera();
            EnsureControllerReference();
            if (!m_Initialized)
            {
                InitializeCallouts();
            }
        }

        private void Start()
        {
            // Set initial hidden state cleanly without disabling this MonoBehaviour
            SetAlphaImmediate(0f);
        }

        private void EnsureCamera()
        {
            if (m_PlayerCamera == null)
                m_PlayerCamera = Camera.main;

            if (m_PlayerCamera == null)
            {
                var camObj = GameObject.FindWithTag("MainCamera");
                if (camObj != null) m_PlayerCamera = camObj.GetComponent<Camera>();
            }

            if (m_PlayerCamera == null)
                m_PlayerCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        }

        private void EnsureControllerReference()
        {
            if (m_ControllerReference == null)
                m_ControllerReference = transform;
        }

        /// <summary>
        /// Populates default verified controls if none were configured in the Inspector.
        /// Verified controls:
        /// Left: Thumbstick -> Move Player, Grip -> Grab Objects
        /// Right: Thumbstick -> Rotate Player, Grip -> Grab Objects
        /// </summary>
        private void EnsureDefaultEntries()
        {
            if (m_Callouts != null && m_Callouts.Count > 0)
                return;

            m_Callouts = new List<ControlCallout>();

            if (m_Hand == ControllerHand.Left)
            {
                // Left Thumbstick: Walk / Move
                m_Callouts.Add(new ControlCallout
                {
                    controlId = "Thumbstick",
                    actionDescription = "Move Player",
                    buttonAnchor = FindChildRecursive(transform, "ThumbStick") ?? FindChildRecursive(transform, "ThumbStick_Base"),
                    fallbackLocalAnchor = new Vector3(0.009f, 0.002f, -0.007f),
                    labelOffset = new Vector3(0.055f, 0.035f, 0.010f)
                });

                // Left Grip: Grab Objects
                m_Callouts.Add(new ControlCallout
                {
                    controlId = "Grip",
                    actionDescription = "Grab Objects",
                    buttonAnchor = FindChildRecursive(transform, "Bumper") ?? FindChildRecursive(transform, "Grip"),
                    fallbackLocalAnchor = new Vector3(-0.013f, -0.029f, 0.027f),
                    labelOffset = new Vector3(-0.060f, -0.015f, 0.025f)
                });
            }
            else if (m_Hand == ControllerHand.Right)
            {
                // Right Thumbstick: Rotate Player (Snap Turn)
                m_Callouts.Add(new ControlCallout
                {
                    controlId = "Thumbstick",
                    actionDescription = "Rotate Player",
                    buttonAnchor = FindChildRecursive(transform, "ThumbStick") ?? FindChildRecursive(transform, "ThumbStick_Base"),
                    fallbackLocalAnchor = new Vector3(0.009f, -0.003f, -0.007f),
                    labelOffset = new Vector3(-0.055f, 0.035f, 0.010f)
                });

                // Right Grip: Grab Objects
                m_Callouts.Add(new ControlCallout
                {
                    controlId = "Grip",
                    actionDescription = "Grab Objects",
                    buttonAnchor = FindChildRecursive(transform, "Bumper") ?? FindChildRecursive(transform, "Grip"),
                    fallbackLocalAnchor = new Vector3(-0.013f, -0.029f, 0.027f),
                    labelOffset = new Vector3(0.060f, -0.015f, 0.025f)
                });
            }
        }

        /// <summary>
        /// Builds the visual hierarchy for each button callout label and leader line.
        /// </summary>
        public void InitializeCallouts()
        {
            EnsureCamera();
            EnsureControllerReference();
            EnsureDefaultEntries();

            // Clear old runtime visual children if re-initializing
            if (m_VisualsRoot != null)
            {
                DestroyImmediate(m_VisualsRoot);
            }

            // Create VisualsRoot as a child of this object so ControllerHelpUI is never self-disabled
            m_VisualsRoot = new GameObject("CalloutsVisuals");
            m_VisualsRoot.transform.SetParent(transform, false);
            m_VisualsRoot.transform.localPosition = Vector3.zero;
            m_VisualsRoot.transform.localRotation = Quaternion.identity;
            m_VisualsRoot.transform.localScale = Vector3.one;

            var canvas = m_VisualsRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 3000; // Above standard world geometry

            m_CanvasGroup = m_VisualsRoot.AddComponent<CanvasGroup>();
            m_CanvasGroup.blocksRaycasts = false;
            m_CanvasGroup.interactable = false;

            // Simple additive/unlit material for clean leader lines
            if (m_ShowLeaderLines && m_LineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Unlit/Color");
                m_LineMaterial = new Material(shader);
            }

            // Construct visual elements for each callout entry
            for (int i = 0; i < m_Callouts.Count; i++)
            {
                var callout = m_Callouts[i];
                CreateCalloutVisual(callout, i, m_VisualsRoot.transform);
            }

            m_Initialized = true;
            SetAlphaImmediate(0f);
        }

        private void CreateCalloutVisual(ControlCallout callout, int index, Transform parent)
        {
            // Callout parent container
            var itemGO = new GameObject($"Callout_{index}_{callout.controlId}");
            itemGO.transform.SetParent(parent, false);
            callout.calloutGO = itemGO;

            // Canvas RectTransform for the floating pill
            var rt = itemGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(170f, 44f);
            rt.localScale = Vector3.one * 0.00045f;
            callout.labelRT = rt;

            // Translucent Card Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(itemGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = m_CardBgColor;
            bgImg.raycastTarget = false;
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // Thin Accent Border
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(itemGO.transform, false);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = m_BorderColor;
            borderImg.raycastTarget = false;
            var borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = new Vector2(2f, 2f);
            borderGO.transform.SetAsFirstSibling();

            // Accent Pill / Badge for Control Identifier
            var badgeGO = new GameObject("Badge");
            badgeGO.transform.SetParent(itemGO.transform, false);
            var badgeImg = badgeGO.AddComponent<Image>();
            badgeImg.color = new Color(m_AccentColor.r, m_AccentColor.g, m_AccentColor.b, 0.22f);
            badgeImg.raycastTarget = false;
            var badgeRT = badgeGO.GetComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0f, 0.5f);
            badgeRT.anchorMax = new Vector2(0f, 0.5f);
            badgeRT.pivot = new Vector2(0f, 0.5f);
            badgeRT.anchoredPosition = new Vector2(6f, 0f);
            badgeRT.sizeDelta = new Vector2(62f, 32f);

            // Badge Text (Control Name)
            var badgeTextGO = new GameObject("BadgeText");
            badgeTextGO.transform.SetParent(badgeGO.transform, false);
            var badgeTMP = badgeTextGO.AddComponent<TextMeshProUGUI>();
            badgeTMP.text = callout.controlId;
            badgeTMP.fontSize = 11;
            badgeTMP.fontStyle = FontStyles.Bold;
            badgeTMP.color = m_AccentColor;
            badgeTMP.alignment = TextAlignmentOptions.Center;
            badgeTMP.raycastTarget = false;
            var badgeTextRT = badgeTextGO.GetComponent<RectTransform>();
            badgeTextRT.anchorMin = Vector2.zero;
            badgeTextRT.anchorMax = Vector2.one;
            badgeTextRT.sizeDelta = Vector2.zero;
            callout.controlIdTMP = badgeTMP;

            // Action Text (Description)
            var actionTextGO = new GameObject("ActionText");
            actionTextGO.transform.SetParent(itemGO.transform, false);
            var actionTMP = actionTextGO.AddComponent<TextMeshProUGUI>();
            actionTMP.text = callout.actionDescription;
            actionTMP.fontSize = 11.5f;
            actionTMP.fontStyle = FontStyles.Normal;
            actionTMP.color = m_TextColor;
            actionTMP.alignment = TextAlignmentOptions.MidlineLeft;
            actionTMP.raycastTarget = false;
            var actionTextRT = actionTextGO.GetComponent<RectTransform>();
            actionTextRT.anchorMin = new Vector2(0f, 0f);
            actionTextRT.anchorMax = new Vector2(1f, 1f);
            actionTextRT.pivot = new Vector2(0f, 0.5f);
            actionTextRT.offsetMin = new Vector2(74f, 2f);
            actionTextRT.offsetMax = new Vector2(-6f, -2f);
            callout.actionTMP = actionTMP;

            // Leader Line
            if (m_ShowLeaderLines)
            {
                var lineGO = new GameObject($"LeaderLine_{callout.controlId}");
                lineGO.transform.SetParent(parent, false);
                var lr = lineGO.AddComponent<LineRenderer>();
                lr.material = m_LineMaterial;
                lr.startColor = new Color(m_AccentColor.r, m_AccentColor.g, m_AccentColor.b, 0.70f);
                lr.endColor = new Color(m_AccentColor.r, m_AccentColor.g, m_AccentColor.b, 0.20f);
                lr.startWidth = 0.0012f;
                lr.endWidth = 0.0007f;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                callout.leaderLine = lr;
            }
        }

        private void Update()
        {
            if (m_PlayerCamera == null)
            {
                EnsureCamera();
                if (m_PlayerCamera == null) return;
            }

            EnsureControllerReference();

            // If the controller GameObject itself is inactive, hide hints immediately
            if (!gameObject.activeInHierarchy)
            {
                SetAlphaImmediate(0f);
                return;
            }

            EvaluateGaze();
            UpdateVisibilityFade();

            // If visible or fading, update callout positions, billboarding, and leader lines
            if (m_CurrentAlpha > 0.001f)
            {
                UpdateCalloutTransforms();
            }
        }

        private void EvaluateGaze()
        {
            Vector3 camPos = m_PlayerCamera.transform.position;
            Vector3 refPos = m_ControllerReference.position;
            Vector3 toController = refPos - camPos;
            float dist = toController.magnitude;

            bool inRange = dist >= m_MinDistance && dist <= m_MaxDistance;

            if (inRange && dist > 0.001f)
            {
                Vector3 toControllerDir = toController / dist;
                float dot = Vector3.Dot(m_PlayerCamera.transform.forward, toControllerDir);

                // Gaze evaluation with hysteresis to prevent edge flickering
                if (!m_IsLookingAt && dot >= m_FacingThreshold)
                {
                    m_IsLookingAt = true;
                }
                else if (m_IsLookingAt && dot < (m_FacingThreshold - m_Hysteresis))
                {
                    m_IsLookingAt = false;
                    m_GazeTimer = 0f;
                }
            }
            else
            {
                m_IsLookingAt = false;
                m_GazeTimer = 0f;
            }

            // Dwell timer: require player to look at controller steadily for dwellDelay
            if (m_IsLookingAt)
            {
                m_GazeTimer += Time.deltaTime;
            }
            else
            {
                m_GazeTimer = 0f;
            }
        }

        private void UpdateVisibilityFade()
        {
            bool shouldShow = m_IsLookingAt && m_GazeTimer >= m_DwellDelay;
            float targetAlpha = shouldShow ? 1.0f : 0.0f;

            if (m_VisualsRoot != null && targetAlpha > 0f && !m_VisualsRoot.activeSelf)
            {
                m_VisualsRoot.SetActive(true);
            }

            m_CurrentAlpha = Mathf.MoveTowards(m_CurrentAlpha, targetAlpha, Time.deltaTime * m_FadeSpeed);

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = m_CurrentAlpha;
            }

            // Update leader line alpha to match CanvasGroup
            if (m_ShowLeaderLines && m_Callouts != null)
            {
                Color cStart = new Color(m_AccentColor.r, m_AccentColor.g, m_AccentColor.b, 0.70f * m_CurrentAlpha);
                Color cEnd = new Color(m_AccentColor.r, m_AccentColor.g, m_AccentColor.b, 0.20f * m_CurrentAlpha);
                for (int i = 0; i < m_Callouts.Count; i++)
                {
                    var lr = m_Callouts[i].leaderLine;
                    if (lr != null)
                    {
                        lr.startColor = cStart;
                        lr.endColor = cEnd;
                        lr.enabled = m_CurrentAlpha > 0.01f;
                    }
                }
            }

            // Disable visuals container only when fully faded out to save draw calls
            if (m_CurrentAlpha <= 0.001f && targetAlpha == 0f && m_VisualsRoot != null && m_VisualsRoot.activeSelf)
            {
                m_VisualsRoot.SetActive(false);
            }
        }

        private void UpdateCalloutTransforms()
        {
            Vector3 camPos = m_PlayerCamera.transform.position;

            for (int i = 0; i < m_Callouts.Count; i++)
            {
                var callout = m_Callouts[i];
                if (callout.calloutGO == null) continue;

                // Determine physical anchor world position
                Vector3 anchorWorldPos;
                if (callout.buttonAnchor != null)
                {
                    anchorWorldPos = callout.buttonAnchor.position;
                }
                else
                {
                    anchorWorldPos = transform.TransformPoint(callout.fallbackLocalAnchor);
                }

                // Compute label world position (anchor + rotated offset)
                Vector3 worldOffset = transform.TransformDirection(callout.labelOffset);
                Vector3 labelWorldPos = anchorWorldPos + worldOffset;
                callout.calloutGO.transform.position = labelWorldPos;

                // Stable billboarding towards camera (facing the player)
                Vector3 lookDir = labelWorldPos - camPos;
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    callout.calloutGO.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
                }

                // Dynamic contextual description check if provided
                if (callout.dynamicTextProvider != null)
                {
                    string dynamicStr = callout.dynamicTextProvider.Invoke();
                    if (!string.IsNullOrEmpty(dynamicStr) && callout.actionTMP != null && callout.actionTMP.text != dynamicStr)
                    {
                        callout.actionTMP.text = dynamicStr;
                    }
                }

                // Update Leader Line points
                if (m_ShowLeaderLines && callout.leaderLine != null)
                {
                    callout.leaderLine.SetPosition(0, anchorWorldPos);
                    // Connect to closest edge of the callout label
                    Vector3 lineTarget = labelWorldPos - (lookDir.normalized * 0.005f);
                    callout.leaderLine.SetPosition(1, lineTarget);
                }
            }
        }

        public void SetAlphaImmediate(float alpha)
        {
            m_CurrentAlpha = alpha;
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = alpha;
            }

            if (m_VisualsRoot != null)
            {
                m_VisualsRoot.SetActive(alpha > 0.001f);
            }

            if (m_ShowLeaderLines && m_Callouts != null)
            {
                for (int i = 0; i < m_Callouts.Count; i++)
                {
                    if (m_Callouts[i].leaderLine != null)
                        m_Callouts[i].leaderLine.enabled = alpha > 0.001f;
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                    return child;

                var found = FindChildRecursive(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        // ─── Backward Compatibility Methods ─────────────────────────────────

        /// <summary>
        /// Creates or retrieves a ControllerHelpUI component on the specified controller transform.
        /// Retained for compatibility with existing scene builders.
        /// </summary>
        public static ControllerHelpUI CreateHelpUI(Transform parent, ControllerHand hand)
        {
            if (parent == null) return null;
            var existing = parent.GetComponentInChildren<ControllerHelpUI>(true);
            if (existing != null)
            {
                existing.m_Hand = hand;
                existing.InitializeCallouts();
                return existing;
            }

            string objName = hand == ControllerHand.Left ? "LeftControllerHelpUI" : "RightControllerHelpUI";
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var helpComp = go.AddComponent<ControllerHelpUI>();
            helpComp.m_Hand = hand;
            helpComp.InitializeCallouts();
            return helpComp;
        }

        /// <summary>
        /// Manual configure method for compatibility.
        /// </summary>
        public void Configure(ControllerHand hand, string title = null, string instructions = null)
        {
            m_Hand = hand;
            InitializeCallouts();
        }
    }
}
