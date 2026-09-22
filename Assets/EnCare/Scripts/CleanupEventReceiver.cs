using UnityEngine;

namespace EnCare
{
    /// <summary>
    /// Bridges CleanupMission UnityEvents to scene GameObjects.
    /// Wire mission.onCompleted → OnSuccess() and mission.onFailed → OnFailure()
    /// via the Inspector or the CleanupSceneBuilder editor tool.
    /// </summary>
    public sealed class CleanupEventReceiver : MonoBehaviour
    {
        [SerializeField] GameObject successPanel;
        [SerializeField] GameObject failurePanel;

        public void OnSuccess()
        {
            if (successPanel != null) successPanel.SetActive(true);
        }

        public void OnFailure()
        {
            if (failurePanel != null) failurePanel.SetActive(true);
        }
    }
}
