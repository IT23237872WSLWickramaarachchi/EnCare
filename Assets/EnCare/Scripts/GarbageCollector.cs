using System.Collections.Generic;
using UnityEngine;

namespace EnCare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GarbageCollector : MonoBehaviour
    {
        public enum CollectionMode
        {
            ConsumeImmediately,
            RetainPhysicalItems
        }

        [Header("Collection Mode & Retention")]
        [Tooltip("When set to RetainPhysicalItems, accepted items stay physically inside the basket up to capacity. Spilling items deducts credit. When set to ConsumeImmediately, items are consumed immediately.")]
        [SerializeField] private CollectionMode mode = CollectionMode.RetainPhysicalItems;

        [Tooltip("Maximum number of items retained physically in the basket before older items are permanently consumed.")]
        [SerializeField, Min(1)] private int retainedCapacity = 3;

        [Tooltip("World-space metres beyond the zone before an accepted item is considered to have spilled.")]
        [SerializeField, Min(0f)] private float exitMargin = 0.015f;

        [Header("Mission & Physics Settings")]
        [SerializeField] private CleanupMission mission;
        [SerializeField] private BoxCollider zone;

        [Tooltip("Unity physics layers of trash colliders, not XRI interaction layers.")]
        [SerializeField] private LayerMask trashLayers = ~0;

        [Tooltip("Optional sound source; it stays alive when an item disappears.")]
        [SerializeField] private AudioSource depositAudio;

        [Header("Locomotion Compensation")]
        [Tooltip("Optional locomotion compensator reference. Auto-discovered on parent/self if not set.")]
        [SerializeField] private BasketLocomotionCompensator locomotionCompensator;

        public BasketLocomotionCompensator LocomotionCompensator => locomotionCompensator;

        private readonly List<TrashItem> retained = new List<TrashItem>();
        private readonly HashSet<TrashItem> seen = new HashSet<TrashItem>();
        private readonly List<TrashItem> candidates = new List<TrashItem>();
        private readonly Collider[] buffer = new Collider[64];

        public CollectionMode Mode
        {
            get => mode;
            set
            {
                mode = value;
                if (isActiveAndEnabled)
                {
                    TrimToCapacity();
                }
            }
        }

        public bool RetainPhysicalItems
        {
            get => mode == CollectionMode.RetainPhysicalItems;
            set => Mode = value ? CollectionMode.RetainPhysicalItems : CollectionMode.ConsumeImmediately;
        }

        public int RetainedCapacity
        {
            get => retainedCapacity;
            set
            {
                retainedCapacity = Mathf.Max(1, value);
                if (isActiveAndEnabled)
                {
                    TrimToCapacity();
                }
            }
        }

        public int RetainedCount => retained.Count;
        public IReadOnlyList<TrashItem> RetainedItems => retained;

        private bool ZoneActive => zone != null && zone.enabled && zone.gameObject.activeInHierarchy;

        private void Awake()
        {
            if (zone == null)
            {
                zone = GetComponent<BoxCollider>();
            }
            if (zone != null)
            {
                zone.isTrigger = true;
            }
            if (mission == null)
            {
                mission = Object.FindFirstObjectByType<CleanupMission>();
            }
            if (locomotionCompensator == null)
            {
                locomotionCompensator = GetComponentInParent<BasketLocomotionCompensator>();
                if (locomotionCompensator == null)
                {
                    locomotionCompensator = GetComponent<BasketLocomotionCompensator>();
                }
            }
        }

        private void Reset()
        {
            zone = GetComponent<BoxCollider>();
            if (zone != null) zone.isTrigger = true;
        }

        private void OnValidate()
        {
            if (zone == null) zone = GetComponent<BoxCollider>();
            if (zone != null) zone.isTrigger = true;
            retainedCapacity = Mathf.Max(1, retainedCapacity);
            if (Application.isPlaying && isActiveAndEnabled)
            {
                TrimToCapacity();
            }
        }

        private void OnEnable()
        {
            if (zone == null) zone = GetComponent<BoxCollider>();
            if (zone != null) zone.isTrigger = true;

            if (mission == null) mission = Object.FindFirstObjectByType<CleanupMission>();

            if (mission == null || zone == null || !zone.isTrigger)
            {
                Debug.LogError("EnCare: Assign a mission and an internal trigger BoxCollider.", this);
                enabled = false;
                return;
            }

            mission.RegisterCollector(this);
        }

        private void OnDisable()
        {
            while (retained.Count > 0)
            {
                Release(retained[retained.Count - 1]);
            }
            if (locomotionCompensator != null)
            {
                locomotionCompensator.ClearRetained();
            }
            if (mission != null)
            {
                mission.UnregisterCollector(this);
            }
        }

        private void FixedUpdate()
        {
            if (mode == CollectionMode.RetainPhysicalItems)
            {
                CheckDepartures();
            }
            CheckDeposits();
        }

        [ContextMenu("Toggle Physical Retention")]
        public void ToggleRetention()
        {
            RetainPhysicalItems = !RetainPhysicalItems;
        }

        public bool Contains(Vector3 worldPoint, float margin)
        {
            if (zone == null) return false;
            Vector3 p = zone.transform.InverseTransformPoint(worldPoint) - zone.center;
            Vector3 s = zone.transform.lossyScale;
            Vector3 h = zone.size * 0.5f;
            h += new Vector3(
                margin / Mathf.Max(Mathf.Abs(s.x), 0.00001f),
                margin / Mathf.Max(Mathf.Abs(s.y), 0.00001f),
                margin / Mathf.Max(Mathf.Abs(s.z), 0.00001f));
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        internal void CheckDepartures()
        {
            for (int i = retained.Count - 1; i >= 0; i--)
            {
                TrashItem item = retained[i];
                if (!item || !item.isActiveAndEnabled || !ZoneActive ||
                    !Contains(item.DepositPosition, Mathf.Max(0f, exitMargin)))
                {
                    Release(item);
                }
            }
        }

        internal void Release(TrashItem item)
        {
            if (!retained.Remove(item)) return;
            if (locomotionCompensator != null && item != null)
            {
                locomotionCompensator.UntrackRetained(item.Rigidbody);
            }
            if (mission != null) mission.Withdraw(item);
            if (item != null && item.Owner == this)
            {
                item.SetOwner(null);
                if (!item.Consumed && item.isActiveAndEnabled)
                {
                    item.PublishAvailable();
                }
            }
        }

        internal void CheckDeposits()
        {
            if (mission == null || !mission.IsRunning || !ZoneActive) return;

            TrimToCapacity(); // Handles Inspector capacity reduction or mode change in Play Mode

            Vector3 s = zone.transform.lossyScale;
            Vector3 half = Vector3.Scale(zone.size * 0.5f,
                new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
            Vector3 center = zone.transform.TransformPoint(zone.center);
            Quaternion rotation = zone.transform.rotation;

            int count = Physics.OverlapBoxNonAlloc(center, half, buffer, rotation,
                trashLayers, QueryTriggerInteraction.Ignore);
            Collider[] hits = buffer;
            if (count == buffer.Length)
            {
                hits = Physics.OverlapBox(center, half, rotation, trashLayers,
                    QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            seen.Clear();
            candidates.Clear();
            for (int i = 0; i < count; i++)
            {
                if (hits[i] == null) continue;
                TrashItem item = hits[i].GetComponentInParent<TrashItem>();
                if (item != null && seen.Add(item))
                {
                    candidates.Add(item);
                }
            }

            // Stable tie-break for multiple deposits in one physics tick
            candidates.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

            foreach (TrashItem item in candidates)
            {
                if (!item || !item.isActiveAndEnabled || item.Mission != mission ||
                    item.Consumed || item.Owner != null || !item.WasGrabbed || item.IsHeld ||
                    !Contains(item.DepositPosition, 0f))
                {
                    continue;
                }

                if (!mission.Credit(item)) continue;

                item.SetOwner(this);
                retained.Add(item);

                if (locomotionCompensator != null)
                {
                    locomotionCompensator.TrackRetained(item.Rigidbody, item.IsHeld);
                }

                // Evict before visual callbacks; do not expose more than capacity
                TrimToCapacity();

                if (item && !item.Consumed)
                {
                    item.PublishRetained();
                }

                if (depositAudio != null)
                {
                    depositAudio.Play();
                }
            }
        }

        private void TrimToCapacity()
        {
            int limit = mode == CollectionMode.ConsumeImmediately ? 0 : Mathf.Max(1, retainedCapacity);
            while (retained.Count > limit)
            {
                TrashItem oldest = retained[0];
                if (!oldest)
                {
                    Release(oldest);
                    continue;
                }
                if (!mission.MakePermanent(oldest)) break;
                retained.RemoveAt(0);

                if (locomotionCompensator != null)
                {
                    locomotionCompensator.UntrackRetained(oldest.Rigidbody);
                }

                oldest.SetOwner(null);
                oldest.ConsumePermanently();
            }
        }

        public void SyncRetainedHeldStates()
        {
            if (locomotionCompensator == null) return;
            for (int i = 0; i < retained.Count; i++)
            {
                TrashItem it = retained[i];
                if (it != null && it.Rigidbody != null)
                {
                    locomotionCompensator.SetRetainedHeld(it.Rigidbody, it.IsHeld);
                }
            }
        }
    }
}


