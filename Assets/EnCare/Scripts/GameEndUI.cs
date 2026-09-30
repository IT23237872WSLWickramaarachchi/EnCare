using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EnCare
{
    /// <summary>
    /// Final "Game Complete" screen shown after the garbage man handover.
    /// Displays a congratulations message and a "Return to Menu" button.
    /// Auto-returns to the Start Menu after a configurable timeout.
    ///
    /// ── SETUP ──────────────────────────────────────────────────────────
    /// 1. Create a World Space Canvas called "GameEndUI".
    /// 2. Attach this script.
    /// 3. Add child TMP texts and a Button, drag into the slots below.
    /// 4. Start the canvas INACTIVE (unchecked in Inspector).
    /// 5. Wire GarbageManController.onHandoverComplete → Show()
    ///    OR let CutsceneLevelManager auto-wire it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameEndUI : MonoBehaviour
    {
        // ════════════════════════════════════════════════════════════════
        //  DESTINATION
        // ════════════════════════════════════════════════════════════════
        [Header("═══ DESTINATION ═══")]
        [Tooltip("Scene name to load when returning to the menu.\n" +
                 "Default: 'Lvl_StartMenu'")]
        [SerializeField] private string startMenuScene = "Lvl_StartMenu";

        [Tooltip("Seconds before auto-returning to the menu. Default = 8.")]
        [Range(3f, 30f)]
        [SerializeField] private float autoReturnDelay = 8f;

        // ════════════════════════════════════════════════════════════════
        //  UI ELEMENTS — Drag & drop from your Canvas hierarchy
        // ════════════════════════════════════════════════════════════════
        [Header("═══ UI ELEMENTS (drag & drop) ═══")]
        [Tooltip("OPTIONAL — Title text, e.g. 'Thank You!'")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("OPTIONAL — Body text, e.g. 'The waste has been properly disposed.'")]
        [SerializeField] private TMP_Text bodyText;

        [Tooltip("OPTIONAL — Countdown text, e.g. 'Returning to menu in 8s...'")]
        [SerializeField] private TMP_Text countdownText;

        [Tooltip("OPTIONAL — 'Return to Menu' button.")]
        [SerializeField] private Button returnButton;

        // ════════════════════════════════════════════════════════════════
        //  AUDIO (optional)
        // ════════════════════════════════════════════════════════════════
        [Header("═══ AUDIO (optional) ═══")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Victory / completion jingle.")]
        [SerializeField] private AudioClip completionSound;

        // ════════════════════════════════════════════════════════════════
        //  INTERNAL
        // ════════════════════════════════════════════════════════════════
        private bool isReturning;
        private Coroutine returnCoroutine;

        private void Awake()
        {
            if (returnButton != null)
            {
                returnButton.onClick.RemoveListener(OnReturnClicked);
                returnButton.onClick.AddListener(OnReturnClicked);
            }
        }

        private void OnEnable()
        {
            isReturning = false;
            if (returnCoroutine != null) StopCoroutine(returnCoroutine);
            returnCoroutine = StartCoroutine(AutoReturnRoutine());
        }

        private void OnDisable()
        {
            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
                returnCoroutine = null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Activates the game end screen. Call from CutsceneLevelManager
        /// or wire to GarbageManController.onHandoverComplete.
        /// </summary>
        public void Show()
        {
            var follow = GetComponent<LazyFollowView>();
            if (follow == null) follow = gameObject.AddComponent<LazyFollowView>();

            gameObject.SetActive(true);
            follow.SnapToView();

            if (audioSource != null && completionSound != null)
                audioSource.PlayOneShot(completionSound);

            Debug.Log("[EnCare GameEnd] Game complete! Showing end screen.");
        }

        /// <summary>
        /// Called by the Return to Menu button.
        /// </summary>
        public void OnReturnClicked()
        {
            ReturnToMenu();
        }

        /// <summary>
        /// Public method to load the start menu. Can be called from anywhere.
        /// </summary>
        public void ReturnToMenu()
        {
            if (isReturning) return;
            isReturning = true;

            if (returnButton != null)
                returnButton.interactable = false;

            Debug.Log($"[EnCare GameEnd] Returning to menu: {startMenuScene}");
            SceneManager.LoadScene(startMenuScene);
        }

        // ════════════════════════════════════════════════════════════════
        //  AUTO-RETURN COUNTDOWN
        // ════════════════════════════════════════════════════════════════
        private IEnumerator AutoReturnRoutine()
        {
            float remaining = autoReturnDelay;

            while (remaining > 0f)
            {
                if (countdownText != null)
                {
                    int secs = Mathf.CeilToInt(remaining);
                    countdownText.text = $"Returning to menu in {secs}s...";
                }

                yield return null;
                remaining -= Time.deltaTime;
            }

            ReturnToMenu();
        }

        // ════════════════════════════════════════════════════════════════
        //  EDITOR HELPERS
        // ════════════════════════════════════════════════════════════════
        #if UNITY_EDITOR
        [ContextMenu("▶ Test Show (Play Mode Only)")]
        private void TestShow()
        {
            if (Application.isPlaying) Show();
            else Debug.Log("[EnCare GameEnd] Enter Play mode first.");
        }
        #endif
    }
}
