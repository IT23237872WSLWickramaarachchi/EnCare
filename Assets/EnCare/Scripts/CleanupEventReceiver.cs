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
        [SerializeField] GameObject successPanel;
        [SerializeField] GameObject failurePanel;

        public void OnSuccess()
        {
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
