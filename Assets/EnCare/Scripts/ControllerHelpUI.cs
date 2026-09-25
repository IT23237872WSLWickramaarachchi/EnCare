using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnCare
{
    /// <summary>
    /// Displays contextual controller instructions and control labels in VR.
    /// Automatically reveals itself smoothly when the player looks at the controller,
    /// and fades out when looking away.
    /// </summary>
    [DisallowMultipleComponent]
    public class ControllerHelpUI : MonoBehaviour
    {
        public enum ControllerHand { Left, Right, Custom }

        [Header("Handedness & Content")]
        [SerializeField] private ControllerHand m_Hand = ControllerHand.Left;
        [SerializeField] private string m_TitleText = "Controls";
        [TextArea(2, 4)]
        [SerializeField] private string m_InstructionsText = "";

        [Header("Gaze Detection Settings")]
        [Tooltip("The camera representing the player's head. Defaults to Camera.main.")]
        [SerializeField] private Camera m_PlayerCamera;

        [Tooltip("Dot product threshold (0.0 to 1.0) for gaze detection. 0.65 = ~49 degree cone.")]
        [Range(0.4f, 0.95f)]
        [SerializeField] private float m_FacingThreshold = 0.65f;

        [Tooltip("Hysteresis offset to prevent flickering at the threshold edge.")]
        [Range(0.01f, 0.15f)]
        [SerializeField] private float m_Hysteresis = 0.08f;

        [Tooltip("Minimum distance from head in meters for tooltip to show.")]
        [SerializeField] private float m_MinDistance = 0.10f;

        [Tooltip("Maximum distance from head in meters for tooltip to show.")]
        [SerializeField] private float m_MaxDistance = 1.40f;

        [Tooltip("Speed of fade in / fade out.")]
        [SerializeField] private float m_FadeSpeed = 4.0f;

        [Tooltip("Delay in seconds before the tooltip starts appearing after looking at the controller.")]
        [Range(0f, 1.5f)]
        [SerializeField] private float m_PopupDelay = 0.25f;

        [Header("UI References")]
        [SerializeField] private CanvasGroup m_CanvasGroup;
        [SerializeField] private TMP_Text m_TitleTMP;
        [SerializeField] private TMP_Text m_BodyTMP;

        private bool m_IsLookingAt;
        private float m_GazeTimer;

        private void Awake()
        {
            EnsureCamera();
            EnsureUI();
            ApplyPresetContent();

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 0f;
                m_CanvasGroup.gameObject.SetActive(false);
            }
        }

        private void EnsureCamera()
        {
            if (m_PlayerCamera == null)
            {
                m_PlayerCamera = Camera.main;
            }
            if (m_PlayerCamera == null)
            {
                var camObj = GameObject.FindWithTag("MainCamera");
                if (camObj != null) m_PlayerCamera = camObj.GetComponent<Camera>();
            }
            if (m_PlayerCamera == null)
            {
                m_PlayerCamera = Object.FindFirstObjectByType<Camera>();
            }
        }

        private void EnsureUI()
        {
            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = GetComponentInChildren<CanvasGroup>(true);
            }
        }

        private void ApplyPresetContent()
        {
            if (m_Hand == ControllerHand.Left)
            {
                if (string.IsNullOrEmpty(m_TitleText) || m_TitleText == "Controls")
                    m_TitleText = "Left Hand";
                if (string.IsNullOrEmpty(m_InstructionsText))
                    m_InstructionsText = "🕹️ <b>Thumbstick:</b> Walk / Move\n✊ <b>Grip:</b> Grab Trash & Basket\n👆 <b>Trigger:</b> Select / Interact";
            }
            else if (m_Hand == ControllerHand.Right)
            {
                if (string.IsNullOrEmpty(m_TitleText) || m_TitleText == "Controls")
                    m_TitleText = "Right Hand";
                if (string.IsNullOrEmpty(m_InstructionsText))
                    m_InstructionsText = "🕹️ <b>Thumbstick:</b> Snap Turn / Teleport\n✊ <b>Grip:</b> Grab Trash & Basket\n🗑️ <b>Basket:</b> Release inside to score";
            }

            if (m_TitleTMP != null) m_TitleTMP.text = m_TitleText;
            if (m_BodyTMP != null) m_BodyTMP.text = m_InstructionsText;
        }

        private void Update()
        {
            if (m_PlayerCamera == null)
            {
                EnsureCamera();
                if (m_PlayerCamera == null) return;
            }

            // ─── Gaze detection ───
            Vector3 camPos = m_PlayerCamera.transform.position;
            Vector3 toController = transform.position - camPos;
            float dist = toController.magnitude;

            bool inRange = dist >= m_MinDistance && dist <= m_MaxDistance;
            if (inRange)
            {
                Vector3 toControllerDir = toController / dist;
                float dot = Vector3.Dot(m_PlayerCamera.transform.forward, toControllerDir);

                // Gaze evaluation with hysteresis
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

            // ─── Popup delay ───
            if (m_IsLookingAt)
            {
                m_GazeTimer += Time.deltaTime;
            }
            else
            {
                m_GazeTimer = 0f;
            }

            bool shouldShow = m_IsLookingAt && m_GazeTimer >= m_PopupDelay;

            // ─── Animate fade ───
            if (m_CanvasGroup != null)
            {
                float targetAlpha = shouldShow ? 1.0f : 0.0f;
                if (targetAlpha > 0f && !m_CanvasGroup.gameObject.activeSelf)
                {
                    m_CanvasGroup.gameObject.SetActive(true);
                }

                m_CanvasGroup.alpha = Mathf.MoveTowards(m_CanvasGroup.alpha, targetAlpha, Time.deltaTime * m_FadeSpeed);

                if (m_CanvasGroup.alpha <= 0.001f && targetAlpha == 0f && m_CanvasGroup.gameObject.activeSelf)
                {
                    m_CanvasGroup.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Automatically instantiates help tooltips for controllers in Play Mode if missing.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureControllerUI()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in transforms)
            {
                if (t == null) continue;
                string lower = t.name.ToLowerInvariant();
                if (lower.Contains("left") && (lower.Contains("controller") || lower.Contains("hand")))
                {
                    if (t.GetComponentInChildren<ControllerHelpUI>(true) == null)
                    {
                        CreateHelpUI(t, ControllerHand.Left);
                    }
                }
                else if (lower.Contains("right") && (lower.Contains("controller") || lower.Contains("hand")))
                {
                    if (t.GetComponentInChildren<ControllerHelpUI>(true) == null)
                    {
                        CreateHelpUI(t, ControllerHand.Right);
                    }
                }
            }
        }

        public static ControllerHelpUI CreateHelpUI(Transform parent, ControllerHand hand)
        {
            if (parent == null) return null;
            var existing = parent.GetComponentInChildren<ControllerHelpUI>(true);
            if (existing != null) return existing;

            string objName = hand == ControllerHand.Left ? "LeftControllerHelpUI" : "RightControllerHelpUI";
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);

            go.transform.localPosition = new Vector3(0f, 0.08f, 0.03f);
            go.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            go.transform.localScale = Vector3.one * 0.00035f;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>();
            var group = go.AddComponent<CanvasGroup>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(340f, 220f);

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.08f, 0.16f, 0.90f);
            bgImg.raycastTarget = false;
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // Border
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(go.transform, false);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(0.1f, 0.85f, 0.95f, 0.4f);
            borderImg.raycastTarget = false;
            var borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = new Vector2(4f, 4f);
            borderGO.transform.SetAsFirstSibling();

            string title = hand == ControllerHand.Left ? "Left Hand" : "Right Hand";
            string body = hand == ControllerHand.Left
                ? "🕹️ <b>Thumbstick:</b> Walk / Move\n✊ <b>Grip:</b> Grab Trash & Basket\n👆 <b>Trigger:</b> Select / Interact"
                : "🕹️ <b>Thumbstick:</b> Snap Turn / Teleport\n✊ <b>Grip:</b> Grab Trash & Basket\n🗑️ <b>Basket:</b> Release inside to score";

            var titleGO = new GameObject("TitleText");
            titleGO.transform.SetParent(go.transform, false);
            var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text = title;
            titleTMP.fontSize = 20;
            titleTMP.color = new Color(0.2f, 0.9f, 1f);
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.raycastTarget = false;
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchoredPosition = new Vector2(0f, 75f);
            titleRT.sizeDelta = new Vector2(300f, 45f);

            var bodyGO = new GameObject("BodyText");
            bodyGO.transform.SetParent(go.transform, false);
            var bodyTMP = bodyGO.AddComponent<TextMeshProUGUI>();
            bodyTMP.text = body;
            bodyTMP.fontSize = 14;
            bodyTMP.color = Color.white;
            bodyTMP.alignment = TextAlignmentOptions.Center;
            bodyTMP.raycastTarget = false;
            var bodyRT = bodyGO.GetComponent<RectTransform>();
            bodyRT.anchoredPosition = new Vector2(0f, -15f);
            bodyRT.sizeDelta = new Vector2(300f, 130f);

            var helpComp = go.AddComponent<ControllerHelpUI>();
            helpComp.m_Hand = hand;
            helpComp.m_TitleText = title;
            helpComp.m_InstructionsText = body;
            helpComp.m_CanvasGroup = group;
            helpComp.m_TitleTMP = titleTMP;
            helpComp.m_BodyTMP = bodyTMP;

            return helpComp;
        }

        /// <summary>
        /// Configure the UI manually with hand preset.
        /// </summary>
        public void Configure(ControllerHand hand, string title, string instructions)
        {
            m_Hand = hand;
            m_TitleText = title;
            m_InstructionsText = instructions;
            ApplyPresetContent();
        }
    }
}

