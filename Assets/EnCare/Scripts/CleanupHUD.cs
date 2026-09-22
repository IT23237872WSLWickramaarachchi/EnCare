using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnCare
{
    public sealed class CleanupHUD : MonoBehaviour
    {
        [SerializeField] CleanupMission mission;
        [SerializeField] TMP_Text countText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Image progressFill;
        int lastSecond = -1;
        int lastCollected = -1;
        int lastTarget = -1;
        CleanupMission.Phase lastPhase = (CleanupMission.Phase)(-1);
        string lastItem = null;

        void OnEnable()
        {
            if (mission == null) return;
            mission.Changed += Refresh;
            lastSecond = -1;
            lastCollected = -1;
            lastTarget = -1;
            lastPhase = (CleanupMission.Phase)(-1);
            lastItem = null;
            Refresh();
        }
        void OnDisable() { if (mission != null) mission.Changed -= Refresh; }
        void Update() { RefreshTimer(); }
        void Refresh()
        {
            if (mission == null) return;
            if (mission.Collected != lastCollected || mission.targetCount != lastTarget)
            {
                lastCollected = mission.Collected;
                lastTarget = mission.targetCount;
                if (countText != null) countText.text = $"Trash Collected\n{mission.Collected}/{mission.targetCount}";
                if (progressFill != null) progressFill.fillAmount = mission.Collected / (float)Mathf.Max(1, mission.targetCount);
            }
            if (statusText != null && (mission.State != lastPhase || mission.LastGrabbed != lastItem))
            {
                lastPhase = mission.State;
                lastItem = mission.LastGrabbed;
                switch (mission.State)
                {
                    case CleanupMission.Phase.Setup: statusText.text = "Preparing..."; break;
                    case CleanupMission.Phase.Ready: statusText.text = "Grab an item to start"; break;
                    case CleanupMission.Phase.Running:
                        statusText.text = string.IsNullOrEmpty(mission.LastGrabbed)
                            ? "Collect the remaining e-waste" : "Picked up: " + mission.LastGrabbed;
                        break;
                    case CleanupMission.Phase.Completed: statusText.text = "All collected! Handover..."; break;
                    case CleanupMission.Phase.Failed: statusText.text = "Round ended — retry"; break;
                }
            }
            RefreshTimer();
        }
        void RefreshTimer()
        {
            if (mission == null || timerText == null) return;
            int seconds = Mathf.CeilToInt(mission.Remaining);
            if (seconds == lastSecond) return;
            lastSecond = seconds;
            timerText.text = $"Time Remaining\n{seconds / 60:00}:{seconds % 60:00}";
        }
    }
}
