using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnCare
{
    public sealed class CleanupHUD : MonoBehaviour
    {
        [Header("Mission Source")]
        [Tooltip("CleanupMission reference. If null, automatically located in the active scene at runtime.")]
        [SerializeField] private CleanupMission mission;

        [Header("UI Element Bindings")]
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image progressFill;

        private int lastSecond = -1;
        private int lastCollected = -1;
        private int lastTarget = -1;
        private CleanupMission.Phase lastPhase = (CleanupMission.Phase)(-1);
        private string lastItem = null;
        private bool isFrozen;

        public CleanupMission Mission
        {
            get => mission;
            set => SetMission(value);
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            isFrozen = false;
            if (mission != null)
            {
                mission.Changed -= Refresh;
                mission.Changed += Refresh;
            }
            lastSecond = -1;
            lastCollected = -1;
            lastTarget = -1;
            lastPhase = (CleanupMission.Phase)(-1);
            lastItem = null;
            Refresh();
        }

        private void Start()
        {
            EnsureReferences();
            Refresh();
        }

        private void OnDisable()
        {
            if (mission != null)
            {
                mission.Changed -= Refresh;
            }
        }

        private void Update()
        {
            // If already frozen in a terminal pass or fail state, do not update UI anymore
            if (isFrozen)
            {
                // If mission was reset or restarted back to active play, unfreeze
                if (mission != null && (mission.State == CleanupMission.Phase.Setup || mission.State == CleanupMission.Phase.Ready || mission.State == CleanupMission.Phase.Running))
                {
                    isFrozen = false;
                    mission.Changed -= Refresh;
                    mission.Changed += Refresh;
                    Refresh();
                }
                return;
            }

            // If mission wasn't ready yet or HUD was dynamically instantiated, auto-bind
            if (mission == null)
            {
                EnsureReferences();
                if (mission != null)
                {
                    mission.Changed -= Refresh;
                    mission.Changed += Refresh;
                    Refresh();
                }
            }
            else
            {
                // Fallback polling to guarantee UI sync even if events fired during scene transitions
                if (mission.Collected != lastCollected || mission.State != lastPhase)
                {
                    Refresh();
                }
            }

            if (!isFrozen)
            {
                RefreshTimer();
            }
        }

        public void SetMission(CleanupMission newMission)
        {
            if (mission != null)
            {
                mission.Changed -= Refresh;
            }

            mission = newMission;
            isFrozen = false;

            if (mission != null)
            {
                mission.Changed -= Refresh;
                mission.Changed += Refresh;
            }

            Refresh();
        }

        private void EnsureReferences()
        {
            // 1. Auto-discover CleanupMission in the scene if not explicitly serialized
            if (mission == null)
            {
                mission = Object.FindFirstObjectByType<CleanupMission>();
            }

            // 2. Auto-discover text/fill elements from children if unlinked
            if (countText == null || timerText == null || statusText == null || progressFill == null)
            {
                var tmpTexts = GetComponentsInChildren<TMP_Text>(true);
                foreach (var tmp in tmpTexts)
                {
                    string nameLower = tmp.gameObject.name.ToLowerInvariant();
                    if (countText == null && nameLower.Contains("count"))
                    {
                        countText = tmp;
                    }
                    else if (timerText == null && nameLower.Contains("timer"))
                    {
                        timerText = tmp;
                    }
                    else if (statusText == null && nameLower.Contains("status"))
                    {
                        statusText = tmp;
                    }
                }

                if (progressFill == null)
                {
                    var images = GetComponentsInChildren<Image>(true);
                    foreach (var img in images)
                    {
                        string nameLower = img.gameObject.name.ToLowerInvariant();
                        if (nameLower.Contains("fill") || (nameLower.Contains("progress") && img.type == Image.Type.Filled))
                        {
                            progressFill = img;
                            break;
                        }
                    }
                }
            }
        }

        private void Refresh()
        {
            if (mission == null) return;
            if (isFrozen) return;

            // Update item collection count and progress fill
            if (mission.Collected != lastCollected || mission.targetCount != lastTarget)
            {
                lastCollected = mission.Collected;
                lastTarget = mission.targetCount;
                if (countText != null)
                {
                    countText.text = $"Trash Collected\n{mission.Collected}/{mission.targetCount}";
                }
                if (progressFill != null)
                {
                    progressFill.fillAmount = mission.Collected / (float)Mathf.Max(1, mission.targetCount);
                }
            }

            // Update status text based on phase
            if (statusText != null && (mission.State != lastPhase || mission.LastGrabbed != lastItem))
            {
                lastPhase = mission.State;
                lastItem = mission.LastGrabbed;
                switch (mission.State)
                {
                    case CleanupMission.Phase.Setup:
                        statusText.text = "Preparing...";
                        break;
                    case CleanupMission.Phase.Ready:
                        statusText.text = "Grab an item to start";
                        break;
                    case CleanupMission.Phase.Running:
                        statusText.text = string.IsNullOrEmpty(mission.LastGrabbed)
                            ? "Collect remaining waste"
                            : "Picked up: " + mission.LastGrabbed;
                        break;
                    case CleanupMission.Phase.Completed:
                        statusText.text = "All collected! Handover...";
                        break;
                    case CleanupMission.Phase.Failed:
                        statusText.text = "Round ended — retry";
                        break;
                }
            }

            RefreshTimer();

            // When level passes or fails, freeze wrist UI so it no longer updates
            if (mission.State == CleanupMission.Phase.Completed || mission.State == CleanupMission.Phase.Failed)
            {
                isFrozen = true;
                mission.Changed -= Refresh;
            }
        }

        private void RefreshTimer()
        {
            if (mission == null || timerText == null) return;

            int seconds = Mathf.CeilToInt(mission.Remaining);
            if (seconds == lastSecond) return;
            lastSecond = seconds;

            timerText.text = $"Time Remaining\n{seconds / 60:00}:{seconds % 60:00}";
        }
    }
}
