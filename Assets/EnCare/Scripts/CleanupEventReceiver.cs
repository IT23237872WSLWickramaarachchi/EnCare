using UnityEngine;

namespace EnCare
{
    /// <summary>
    /// Bridges CleanupMission UnityEvents to scene GameObjects.
    /// Wire mission.onCompleted → OnSuccess() and mission.onFailed → OnFailure().
    /// Ensures pass/fail panels pop up tracked to the player's view with lazy follow delay.
    /// </summary>
    public sealed class CleanupEventReceiver : MonoBehaviour
    {
        [Header("═══ WIN SCREEN (drag & drop) ═══")]
        [Tooltip("RECOMMENDED — Drag the WinScreenUI here.\n" +
                 "It shows a Proceed button + 10s countdown, then loads the cutscene.\n" +
                 "If assigned, this takes priority over the old successPanel.")]
        [SerializeField] private WinScreenUI winScreenUI;

        [Header("═══ PANELS (drag & drop) ═══")]
        [Tooltip("FALLBACK — Old static success panel (used only if winScreenUI is empty).")]
        [SerializeField] GameObject successPanel;

        [Tooltip("REQUIRED — Failure panel shown when the player runs out of time.")]
        [SerializeField] GameObject failurePanel;

        public void OnSuccess()
        {
            // Prefer the new WinScreenUI with countdown + proceed
            if (winScreenUI != null)
            {
                winScreenUI.Show();
                return;
            }

            // Fallback to old static success panel
            if (successPanel != null)
            {
                var follow = successPanel.GetComponent<LazyFollowView>();
                if (follow == null) follow = successPanel.AddComponent<LazyFollowView>();
                successPanel.SetActive(true);
                follow.SnapToView();
            }
        }

        public void OnFailure()
        {
            if (failurePanel != null)
            {
                var follow = failurePanel.GetComponent<LazyFollowView>();
                if (follow == null) follow = failurePanel.AddComponent<LazyFollowView>();
                failurePanel.SetActive(true);
                follow.SnapToView();
            }
        }
    }
}
