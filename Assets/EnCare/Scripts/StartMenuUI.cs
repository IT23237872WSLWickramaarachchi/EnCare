using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EnCare
{
    /// <summary>
    /// VR Start Menu controller allowing the player to choose between the Office and Garden maps.
    /// Supports VR Ray pointing/clicking, desktop mouse clicks, and keyboard shortcuts (1 and 2).
    /// Automatically aligns the menu at eye level directly in front of the player's camera.
    /// </summary>
    [DisallowMultipleComponent]
    public class StartMenuUI : MonoBehaviour
    {
        [Header("Scene Destinations")]
        [Tooltip("The name of the Office Cleanup scene to load.")]
        [SerializeField] private string m_OfficeSceneName = "CleanupScene";

        [Tooltip("The name of the Garden / Backyard scene to load.")]
        [SerializeField] private string m_GardenSceneName = "Lvl_Backyard";

        [Header("UI Buttons")]
        [SerializeField] private Button m_OfficeButton;
        [SerializeField] private Button m_GardenButton;
        [SerializeField] private Button m_OfficeCardButton;
        [SerializeField] private Button m_GardenCardButton;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource m_AudioSource;
        [SerializeField] private AudioClip m_ClickClip;
        [SerializeField] private AudioClip m_HoverClip;

        [Header("Placement Settings")]
        [Tooltip("Distance in meters in front of the player's camera to position the menu.")]
        [SerializeField] private float m_Distance = 1.25f;

        [Tooltip("Default eye height in meters if camera tracking is not yet active.")]
        [SerializeField] private float m_DefaultHeight = 1.35f;

        [Header("Controller Boost")]
        [Tooltip("Automatically extends VR ray and interactor reach to 50 meters for effortless pointing.")]
        [SerializeField] private bool m_BoostControllerReach = true;

        [Header("Keyboard Shortcuts (Desktop Debug)")]
        [SerializeField] private bool m_EnableKeyboardShortcuts = true;

        private bool m_IsLoading;
        private int m_TrackingSettleFrames;
        private Camera m_PlayerCamera;

        public string OfficeSceneName
        {
            get => m_OfficeSceneName;
            set => m_OfficeSceneName = value;
        }

        public string GardenSceneName
        {
            get => m_GardenSceneName;
            set => m_GardenSceneName = value;
        }

        public Button OfficeButton
        {
            get => m_OfficeButton;
            set => m_OfficeButton = value;
        }

        public Button GardenButton
        {
            get => m_GardenButton;
            set => m_GardenButton = value;
        }

        public Button OfficeCardButton
        {
            get => m_OfficeCardButton;
            set => m_OfficeCardButton = value;
        }

        public Button GardenCardButton
        {
            get => m_GardenCardButton;
            set => m_GardenCardButton = value;
        }

        public AudioSource AudioSourceComponent
        {
            get => m_AudioSource;
            set => m_AudioSource = value;
        }

        public AudioClip ClickClip
        {
            get => m_ClickClip;
            set => m_ClickClip = value;
        }

        public AudioClip HoverClip
        {
            get => m_HoverClip;
            set => m_HoverClip = value;
        }

        private void Awake()
        {
            EnsureAudio();
            EnsureButtons();
            EnsureFollow();
        }

        private void OnEnable()
        {
            m_TrackingSettleFrames = 0;
            PositionInFrontOfCamera();
            if (m_BoostControllerReach) BoostControllerReach();
        }

        private void Start()
        {
            PositionInFrontOfCamera();
            if (m_BoostControllerReach) BoostControllerReach();
        }

        private void LateUpdate()
        {
            // Settle position across first few frames as OpenXR / Mock HMD tracking activates
            if (m_TrackingSettleFrames < 10)
            {
                m_TrackingSettleFrames++;
                PositionInFrontOfCamera();
            }
        }

        private void EnsureFollow()
        {
            // If LazyFollowView is not already present, attach it for smooth VR head-following
            var follow = GetComponent<LazyFollowView>();
            if (follow == null)
            {
                follow = gameObject.AddComponent<LazyFollowView>();
                follow.Distance = m_Distance;
                follow.DeadzoneAngle = 25f;
            }
        }

        private void EnsureAudio()
        {
            if (m_AudioSource == null)
            {
                m_AudioSource = GetComponent<AudioSource>();
                if (m_AudioSource == null)
                {
                    m_AudioSource = gameObject.AddComponent<AudioSource>();
                    m_AudioSource.playOnAwake = false;
                }
            }
        }

        private void EnsureButtons()
        {
            if (m_OfficeButton == null)
            {
                var btn = transform.Find("Card_Office/PlayButton")
                       ?? transform.Find("Card_Office/Button")
                       ?? transform.Find("OfficeButton");
                if (btn != null) m_OfficeButton = btn.GetComponent<Button>();
            }

            if (m_OfficeCardButton == null)
            {
                var card = transform.Find("Card_Office");
                if (card != null) m_OfficeCardButton = card.GetComponent<Button>();
            }

            if (m_GardenButton == null)
            {
                var btn = transform.Find("Card_Garden/PlayButton")
                       ?? transform.Find("Card_Garden/Button")
                       ?? transform.Find("GardenButton");
                if (btn != null) m_GardenButton = btn.GetComponent<Button>();
            }

            if (m_GardenCardButton == null)
            {
                var card = transform.Find("Card_Garden");
                if (card != null) m_GardenCardButton = card.GetComponent<Button>();
            }

            if (m_OfficeButton != null)
            {
                m_OfficeButton.onClick.RemoveListener(PlayOffice);
                m_OfficeButton.onClick.AddListener(PlayOffice);
            }
            if (m_OfficeCardButton != null)
            {
                m_OfficeCardButton.onClick.RemoveListener(PlayOffice);
                m_OfficeCardButton.onClick.AddListener(PlayOffice);
            }

            if (m_GardenButton != null)
            {
                m_GardenButton.onClick.RemoveListener(PlayGarden);
                m_GardenButton.onClick.AddListener(PlayGarden);
            }
            if (m_GardenCardButton != null)
            {
                m_GardenCardButton.onClick.RemoveListener(PlayGarden);
                m_GardenCardButton.onClick.AddListener(PlayGarden);
            }
        }

        /// <summary>
        /// Explicitly positions the canvas directly in front of the active camera at eye height.
        /// </summary>
        public void PositionInFrontOfCamera()
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
            if (m_PlayerCamera == null) return;

            // Wire canvas world camera for raycasting
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
            {
                canvas.worldCamera = m_PlayerCamera;
            }

            Vector3 camPos = m_PlayerCamera.transform.position;
            Vector3 forward = m_PlayerCamera.transform.forward;

            // Flatten forward so panel is upright
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.forward;
            }
            flatForward.Normalize();

            float eyeY = camPos.y > 0.4f ? camPos.y : m_DefaultHeight;
            Vector3 targetPos = new Vector3(camPos.x, eyeY, camPos.z) + flatForward * m_Distance;

            transform.position = targetPos;
            transform.rotation = Quaternion.LookRotation(flatForward);
        }

        private void Update()
        {
            if (m_IsLoading) return;

            if (!m_EnableKeyboardShortcuts) return;

            // Desktop testing shortcuts: 1 for Office, 2 for Garden
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                {
                    PlayOffice();
                }
                else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                {
                    PlayGarden();
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                PlayOffice();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                PlayGarden();
            }
#endif
        }

        /// <summary>
        /// Extends the reach of VR controller rays to 50 meters and ensures UI interaction is active.
        /// </summary>
        public void BoostControllerReach()
        {
            try
            {
                int uiMask = (1 << LayerMask.NameToLayer("UI")) | 1;

                var rays = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(FindObjectsSortMode.None);
                foreach (var ray in rays)
                {
                    if (ray != null)
                    {
                        ray.maxRaycastDistance = 50f;
                        ray.enableUIInteraction = true;
                        ray.raycastMask |= uiMask;
                    }
                }

                var nearFars = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsSortMode.None);
                foreach (var nf in nearFars)
                {
                    if (nf != null)
                    {
                        nf.enableUIInteraction = true;
                    }
                }

                Debug.Log("[EnCare StartMenu] Controller ray reach boosted to 50m with UI interaction enabled.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[EnCare StartMenu] Note on boosting interactor reach: " + ex.Message);
            }
        }

        public void PlayOffice()
        {
            if (m_IsLoading) return;
            PlayClickSound();
            StartCoroutine(LoadSceneRoutine(m_OfficeSceneName));
        }

        public void PlayGarden()
        {
            if (m_IsLoading) return;
            PlayClickSound();
            StartCoroutine(LoadSceneRoutine(m_GardenSceneName));
        }

        public void PlayHoverSound()
        {
            if (m_AudioSource != null && m_HoverClip != null)
            {
                m_AudioSource.PlayOneShot(m_HoverClip, 0.7f);
            }
        }

        public void PlayClickSound()
        {
            if (m_AudioSource != null && m_ClickClip != null)
            {
                m_AudioSource.PlayOneShot(m_ClickClip, 1.0f);
            }
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            m_IsLoading = true;
            if (m_OfficeButton != null) m_OfficeButton.interactable = false;
            if (m_GardenButton != null) m_GardenButton.interactable = false;
            if (m_OfficeCardButton != null) m_OfficeCardButton.interactable = false;
            if (m_GardenCardButton != null) m_GardenCardButton.interactable = false;

            // Brief moment for audio feedback
            yield return new WaitForSecondsRealtime(0.12f);

            Debug.Log($"[EnCare StartMenu] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
    }
}
