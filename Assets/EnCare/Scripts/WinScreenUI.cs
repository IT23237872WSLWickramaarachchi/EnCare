using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EnCare
{
    /// <summary>
    /// Win screen that appears when the player completes a cleanup level.
    /// Shows a countdown timer (10 seconds) and a Proceed button.
    /// Pressing the button OR waiting for the countdown transitions to the cutscene level.
    ///
    /// ── SETUP ──────────────────────────────────────────────────────────
    /// 1. Attach this to a World Space Canvas GameObject.
    /// 2. Set "Level Id" to the CURRENT scene name:
    ///       • "CleanupScene"  → will load "Lvl_OfficeCutScene"
    ///       • "Lvl_Backyard"  → will load "Lvl_BackyardCutScene"
    /// 3. Drag your TMP text objects and button into the Inspector slots.
    /// 4. Wire CleanupMission.onCompleted → this object's Show() method.
    /// 5. Start the canvas GameObject as INACTIVE (unchecked in Inspector).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WinScreenUI : MonoBehaviour
    {
        // ════════════════════════════════════════════════════════════════
        //  LEVEL ROUTING — Which cutscene to load
        // ════════════════════════════════════════════════════════════════
        [Header("═══ LEVEL ROUTING ═══")]
        [Tooltip("Set this to the CURRENT gameplay scene name.\n\n" +
                 "• \"CleanupScene\" → loads \"Lvl_OfficeCutScene\"\n" +
                 "• \"Lvl_Backyard\" → loads \"Lvl_BackyardCutScene\"")]
        [SerializeField] private string levelId = "CleanupScene";

        [Tooltip("(Optional) Override: type the exact cutscene scene name here.\n" +
                 "Leave EMPTY to use the automatic mapping above.")]
        [SerializeField] private string cutsceneSceneOverride = "";

        // ════════════════════════════════════════════════════════════════
        //  COUNTDOWN SETTINGS
        // ════════════════════════════════════════════════════════════════
        [Header("═══ COUNTDOWN ═══")]
        [Tooltip("Seconds before auto-transition. Default = 10.")]
        [Range(3f, 30f)]
        [SerializeField] private float countdownDuration = 10f;

        // ════════════════════════════════════════════════════════════════
        //  UI ELEMENTS — Drag & drop from your Canvas hierarchy
        // ════════════════════════════════════════════════════════════════
        [Header("═══ UI ELEMENTS (drag & drop) ═══")]
        [Tooltip("REQUIRED — The big title text, e.g. 'Mission Complete!'")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("REQUIRED — Shows the countdown number, e.g. '10'")]
        [SerializeField] private TMP_Text countdownText;

        [Tooltip("OPTIONAL — Subtitle / body text, e.g. 'All waste collected!'")]
        [SerializeField] private TMP_Text bodyText;

        [Tooltip("REQUIRED — The 'Proceed' button the player can click/point at.")]
        [SerializeField] private Button proceedButton;

        // ════════════════════════════════════════════════════════════════
        //  AUDIO (optional)
        // ════════════════════════════════════════════════════════════════
        [Header("═══ AUDIO (optional) ═══")]
        [Tooltip("Audio source for click / countdown sounds.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Sound when the player clicks Proceed.")]
        [SerializeField] private AudioClip clickSound;

        [Tooltip("Sound each second of the countdown tick.")]
        [SerializeField] private AudioClip tickSound;

        // ════════════════════════════════════════════════════════════════
        //  INTERNAL STATE
        // ════════════════════════════════════════════════════════════════
        private bool isTransitioning;
        private Coroutine countdownCoroutine;

        // ── Scene name mapping ──────────────────────────────────────────
        private string TargetScene
        {
            get
            {
                if (!string.IsNullOrEmpty(cutsceneSceneOverride))
                    return cutsceneSceneOverride;

                switch (levelId)
                {
                    case "CleanupScene": return "Lvl_OfficeCutScene";
                    case "Lvl_Backyard": return "Lvl_BackyardCutScene";
                    default:
                        Debug.LogWarning($"[EnCare WinScreen] Unknown levelId '{levelId}'. " +
                                         "Set it to 'CleanupScene' or 'Lvl_Backyard', " +
                                         "or fill in cutsceneSceneOverride.");
                        return levelId + "CutScene";
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════════════════
        private void Awake()
        {
            // Wire the button
            if (proceedButton != null)
            {
                proceedButton.onClick.RemoveListener(OnProceedClicked);
                proceedButton.onClick.AddListener(OnProceedClicked);
            }
        }

        private void OnEnable()
        {
            // Every time this panel is activated, start the countdown
            isTransitioning = false;
            if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
            countdownCoroutine = StartCoroutine(CountdownRoutine());
        }

        private void OnDisable()
        {
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
                countdownCoroutine = null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  PUBLIC API — Wire CleanupMission.onCompleted → Show()
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Call this from CleanupMission.onCompleted (via Inspector UnityEvent).
        /// Activates this panel and starts the countdown.
        /// </summary>
        public void Show()
        {
            // Snap to player's view using LazyFollowView if present
            var follow = GetComponent<LazyFollowView>();
            if (follow == null) follow = gameObject.AddComponent<LazyFollowView>();

            gameObject.SetActive(true);
            follow.SnapToView();
        }

        /// <summary>
        /// Called by the Proceed button's OnClick event.
        /// </summary>
        public void OnProceedClicked()
        {
            if (isTransitioning) return;

            if (audioSource != null && clickSound != null)
                audioSource.PlayOneShot(clickSound);

            TransitionToCutscene();
        }

        // ════════════════════════════════════════════════════════════════
        //  COUNTDOWN COROUTINE
        // ════════════════════════════════════════════════════════════════
        private IEnumerator CountdownRoutine()
        {
            float remaining = countdownDuration;

            while (remaining > 0f)
            {
                int seconds = Mathf.CeilToInt(remaining);

                if (countdownText != null)
                    countdownText.text = $"Proceeding in {seconds}s...";

                // Tick sound on each whole second
                if (audioSource != null && tickSound != null && Mathf.Approximately(remaining, Mathf.Ceil(remaining)))
                    audioSource.PlayOneShot(tickSound, 0.5f);

                yield return null;
                remaining -= Time.deltaTime;
            }

            if (countdownText != null)
                countdownText.text = "Proceeding...";

            TransitionToCutscene();
        }

        // ════════════════════════════════════════════════════════════════
        //  SCENE TRANSITION
        // ════════════════════════════════════════════════════════════════
        private void TransitionToCutscene()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            if (proceedButton != null)
                proceedButton.interactable = false;

            string target = TargetScene;
            Debug.Log($"[EnCare WinScreen] Transitioning to cutscene: {target}");
            SceneManager.LoadScene(target);
        }

        // ════════════════════════════════════════════════════════════════
        //  EDITOR HELPERS
        // ════════════════════════════════════════════════════════════════
        #if UNITY_EDITOR
        [ContextMenu("▶ Test Show (Play Mode Only)")]
        private void TestShow()
        {
            if (Application.isPlaying) Show();
            else Debug.Log("[EnCare WinScreen] Enter Play mode first to test Show().");
        }
        #endif
    }
}
