using UnityEngine;

namespace EnCare
{
    /// <summary>
    /// Smoothly positions a UI canvas in front of the VR player's view with a soft deadzone,
    /// warmup delay, and ease-in-out repositioning. Prevents constant view obstruction while
    /// keeping the panel accessible. Includes a scale-pop animation on first appear.
    /// Ideal for Pass/Fail and mission conclusion panels.
    /// </summary>
    [DisallowMultipleComponent]
    public class LazyFollowView : MonoBehaviour
    {
        [Header("Target Tracking")]
        [Tooltip("The camera to follow. If null, automatically uses Camera.main.")]
        [SerializeField] private Camera m_TargetCamera;

        [Header("Distance & Offsets")]
        [Tooltip("Distance in meters to place the UI in front of the player's eyes.")]
        [Range(0.5f, 4f)]
        [SerializeField] private float m_Distance = 1.8f;

        [Tooltip("Vertical height offset relative to the camera eye line.")]
        [Range(-1f, 1f)]
        [SerializeField] private float m_HeightOffset = 0.0f;

        [Header("Reposition Easing")]
        [Tooltip("Soft deadzone angle (in degrees). The UI will only begin catching up when the head turns beyond this threshold.")]
        [Range(5f, 45f)]
        [SerializeField] private float m_DeadzoneAngle = 18f;

        [Tooltip("Tolerance for camera distance before repositioning activates.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float m_DistanceTolerance = 0.35f;

        [Tooltip("Warm-up delay in seconds before the UI starts moving after leaving the deadzone. Creates an ease-in feel.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_WarmupDelay = 0.25f;

        [Tooltip("Base ease duration used to modulate the SmoothDamp damping time. Higher values = slower, more pronounced easing.")]
        [Range(0.2f, 2f)]
        [SerializeField] private float m_EaseDuration = 0.8f;

        [Tooltip("Rotation interpolation speed to face the player.")]
        [Range(1f, 15f)]
        [SerializeField] private float m_RotationSpeed = 4.5f;

        [Header("Scale Animation")]
        [Tooltip("If true, plays a scale-pop animation when the panel first appears via SnapToView.")]
        [SerializeField] private bool m_EnableScaleAnim = true;

        [Tooltip("Duration of the scale-pop animation on appear.")]
        [Range(0.1f, 1.5f)]
        [SerializeField] private float m_ScaleAnimDuration = 0.45f;

        [Header("Behavior")]
        [Tooltip("If true, snaps instantly into position when enabled.")]
        [SerializeField] private bool m_SnapOnEnable = true;

        // SmoothDamp state
        private Vector3 m_CurrentVelocity;

        // Reposition state
        private bool m_IsRepositioning;
        private float m_WarmupTimer;

        // Scale animation state
        private Vector3 m_OriginalScale = Vector3.one;
        private bool m_ScaleAnimPlaying;
        private float m_ScaleTimer;

        public float Distance
        {
            get => m_Distance;
            set => m_Distance = Mathf.Max(0.5f, value);
        }

        public float RepositionDelay
        {
            get => m_WarmupDelay;
            set => m_WarmupDelay = Mathf.Max(0f, value);
        }

        public float DeadzoneAngle
        {
            get => m_DeadzoneAngle;
            set => m_DeadzoneAngle = Mathf.Clamp(value, 1f, 80f);
        }

        private void Awake()
        {
            EnsureCamera();
            m_OriginalScale = transform.localScale;
        }

        private void OnEnable()
        {
            EnsureCamera();
            if (m_SnapOnEnable && m_TargetCamera != null)
            {
                SnapToView();
            }
        }

        private void EnsureCamera()
        {
            if (m_TargetCamera == null)
            {
                m_TargetCamera = Camera.main;
            }
        }

        /// <summary>
        /// Immediately snaps the UI directly in front of the camera view,
        /// with an optional scale-pop animation.
        /// </summary>
        public void SnapToView()
        {
            if (m_TargetCamera == null) EnsureCamera();
            if (m_TargetCamera == null) return;

            Vector3 camPos = m_TargetCamera.transform.position;
            Vector3 camForward = m_TargetCamera.transform.forward;

            Vector3 targetPos = camPos + camForward * m_Distance + Vector3.up * m_HeightOffset;
            transform.position = targetPos;
            transform.rotation = Quaternion.LookRotation(targetPos - camPos);
            m_CurrentVelocity = Vector3.zero;
            m_IsRepositioning = false;
            m_WarmupTimer = 0f;

            // Scale-pop animation
            if (m_EnableScaleAnim)
            {
                m_ScaleAnimPlaying = true;
                m_ScaleTimer = 0f;
                transform.localScale = Vector3.zero;
            }
            else
            {
                transform.localScale = m_OriginalScale;
            }
        }

        private void LateUpdate()
        {
            if (m_TargetCamera == null)
            {
                EnsureCamera();
                if (m_TargetCamera == null) return;
            }

            // ─── Scale-Pop Animation ───
            if (m_ScaleAnimPlaying)
            {
                m_ScaleTimer += Time.deltaTime;
                float t = Mathf.Clamp01(m_ScaleTimer / m_ScaleAnimDuration);
                float eased = EaseOutBack(t);
                transform.localScale = m_OriginalScale * eased;
                if (t >= 1f)
                {
                    m_ScaleAnimPlaying = false;
                    transform.localScale = m_OriginalScale;
                }
            }

            // ─── Reposition Follow ───
            Vector3 camPos = m_TargetCamera.transform.position;
            Vector3 camForward = m_TargetCamera.transform.forward;

            Vector3 toCurrentUI = transform.position - camPos;
            float currentDist = toCurrentUI.magnitude;
            float angleFromCenter = Vector3.Angle(camForward, toCurrentUI.normalized);

            // Check if head moved beyond deadzone or distance threshold
            bool outsideDeadzone = angleFromCenter > m_DeadzoneAngle;
            bool outsideDistance = Mathf.Abs(currentDist - m_Distance) > m_DistanceTolerance;

            if (outsideDeadzone || outsideDistance)
            {
                if (!m_IsRepositioning)
                {
                    m_IsRepositioning = true;
                    m_WarmupTimer = 0f;
                }
            }
            else if (angleFromCenter < m_DeadzoneAngle * 0.4f && !outsideDistance)
            {
                // Returned close to centre, reset reposition state
                m_IsRepositioning = false;
                m_WarmupTimer = 0f;
            }

            if (m_IsRepositioning)
            {
                // Warmup delay: ease-in by holding still until warmup completes
                m_WarmupTimer += Time.deltaTime;
                if (m_WarmupTimer < m_WarmupDelay) return;

                // Ramp factor: 0→1 over half the ease duration after warmup
                float elapsed = m_WarmupTimer - m_WarmupDelay;
                float rampUp = Mathf.Clamp01(elapsed / (m_EaseDuration * 0.5f));
                float easedRamp = SmoothStepEaseInOut(rampUp);

                // Modulate damping time: high at start (slow), decreasing (faster).
                // SmoothDamp naturally ease-outs as we approach the target.
                float dynamicDampTime = Mathf.Lerp(m_EaseDuration * 2f, m_EaseDuration * 0.25f, easedRamp);

                Vector3 desiredPos = camPos + camForward * m_Distance + Vector3.up * m_HeightOffset;
                transform.position = Vector3.SmoothDamp(
                    transform.position, desiredPos, ref m_CurrentVelocity, dynamicDampTime);

                // Smoothly rotate to face the camera with eased speed
                Vector3 lookDir = transform.position - camPos;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion desiredRot = Quaternion.LookRotation(lookDir);
                    float rotSpeed = Mathf.Lerp(1f, m_RotationSpeed, easedRamp);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, desiredRot, Time.deltaTime * rotSpeed);
                }
            }
        }

        /// <summary>Hermite smoothstep: ease-in-out curve.</summary>
        static float SmoothStepEaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Ease-out with slight overshoot for a bouncy "pop" feel.</summary>
        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
