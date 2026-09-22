using UnityEngine;

namespace EnCare
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GarbageCollector : MonoBehaviour
    {
        [SerializeField] CleanupMission mission;
        [Tooltip("Unity physics layers of trash colliders, not XRI interaction layers.")]
        [SerializeField] LayerMask trashLayers = ~0;
        [Tooltip("Optional sound source; it stays alive when an item disappears.")]
        [SerializeField] AudioSource depositAudio;
        BoxCollider zone;
        readonly Collider[] hits = new Collider[64];

        void Awake()
        {
            zone = GetComponent<BoxCollider>();
            if (zone != null) zone.isTrigger = true;
            if (mission == null)
            {
                mission = Object.FindFirstObjectByType<CleanupMission>();
            }
            if (mission == null)
            {
                Debug.LogError("EnCare: assign the mission on the collector.", this);
                enabled = false;
            }
        }
        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }

        void FixedUpdate()
        {
            if (!zone.enabled || !mission.CanInteract) return;
            Vector3 scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 half = Vector3.Scale(zone.size * 0.5f, scale);
            Vector3 centre = transform.TransformPoint(zone.center);
            int count = Physics.OverlapBoxNonAlloc(centre, half, hits, transform.rotation,
                trashLayers, QueryTriggerInteraction.Ignore);
            // An unusually large number of colliders must not silently hide an item.
            if (count == hits.Length)
            {
                Collider[] all = Physics.OverlapBox(centre, half, transform.rotation,
                    trashLayers, QueryTriggerInteraction.Ignore);
                foreach (Collider hit in all) TryCollect(hit);
            }
            else for (int i = 0; i < count; i++) TryCollect(hits[i]);
        }

        void TryCollect(Collider hit)
        {
            if (hit == null) return;
            TrashItem item = hit.GetComponentInParent<TrashItem>();
            if (item == null || item.Mission != mission || item.Consumed || item.IsHeld || !item.WasGrabbed)
                return;
            // A corner brushing the basket is insufficient: the deposit point must be inside.
            Vector3 p = transform.InverseTransformPoint(item.DepositPosition) - zone.center;
            Vector3 half = zone.size * 0.5f;
            if (Mathf.Abs(p.x) > half.x || Mathf.Abs(p.y) > half.y || Mathf.Abs(p.z) > half.z) return;
            if (mission.TryDeposit(item) && depositAudio != null) depositAudio.Play();
        }
    }
}
