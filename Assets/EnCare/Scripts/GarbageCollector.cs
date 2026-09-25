using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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

        [Header("Basket Retention")]
        [Tooltip("Number of collected objects to keep inside the basket. Older objects are removed in FIFO order (the very 1st added is deleted first). Set to 0 to destroy immediately.")]
        [SerializeField] [Min(0)] int itemsToKeep = 3;

        public int ItemsToKeep
        {
            get => itemsToKeep;
            set => itemsToKeep = Mathf.Max(0, value);
        }

        BoxCollider zone;
        readonly Collider[] hits = new Collider[64];
        readonly Queue<TrashItem> retainedTrash = new Queue<TrashItem>();
        XRGrabInteractable basketGrab;

        void Awake()
        {
            zone = GetComponent<BoxCollider>();
            if (zone != null) zone.isTrigger = true;

            // Subscribe to basket drop events so items tumble out when released
            basketGrab = GetComponentInParent<XRGrabInteractable>();
            if (basketGrab != null)
            {
                basketGrab.selectExited.AddListener(OnBasketDropped);
            }

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
            if (item == null || item.IsHeld || !item.WasGrabbed) return;

            // Skip items already riding in the basket
            if (item.GetComponent<BasketPassenger>() != null) return;

            // Spatial check: the deposit point must be inside the collection zone
            Vector3 p = transform.InverseTransformPoint(item.DepositPosition) - zone.center;
            Vector3 half = zone.size * 0.5f;
            if (Mathf.Abs(p.x) > half.x || Mathf.Abs(p.y) > half.y || Mathf.Abs(p.z) > half.z) return;

            // ── Re-deposit path: item was previously collected but fell out of basket ──
            if (item.ReadyForRedeposit)
            {
                if (depositAudio != null) depositAudio.Play();
                Transform basketParent = transform.parent != null ? transform.parent : transform;
                item.OnDepositedInBasket(basketParent);

                while (retainedTrash.Count >= itemsToKeep && itemsToKeep > 0)
                {
                    TrashItem oldest = retainedTrash.Dequeue();
                    if (oldest != null) Destroy(oldest.gameObject);
                }
                if (itemsToKeep > 0) retainedTrash.Enqueue(item);
                return;
            }

            // ── Normal first-time deposit ──
            if (item.Mission != mission || item.Consumed) return;

            if (mission.TryDeposit(item))
            {
                if (depositAudio != null) depositAudio.Play();

                if (itemsToKeep <= 0)
                {
                    item.Consume();
                }
                else
                {
                    Transform basketParent = transform.parent != null ? transform.parent : transform;
                    item.OnDepositedInBasket(basketParent);

                    // If we already have itemsToKeep, delete the very 1st object added (FIFO)
                    while (retainedTrash.Count >= itemsToKeep)
                    {
                        TrashItem oldest = retainedTrash.Dequeue();
                        if (oldest != null)
                        {
                            Destroy(oldest.gameObject);
                        }
                    }

                    retainedTrash.Enqueue(item);
                }
            }
        }

        /// <summary>
        /// Called when the basket is released from the player's hand.
        /// All retained items tumble out so the player must re-collect them.
        /// </summary>
        void OnBasketDropped(SelectExitEventArgs args)
        {
            ReleaseAllRetainedItems();
        }

        void ReleaseAllRetainedItems()
        {
            while (retainedTrash.Count > 0)
            {
                TrashItem item = retainedTrash.Dequeue();
                if (item != null)
                {
                    item.ReleaseFromBasket();
                }
            }
        }

        void OnDestroy()
        {
            // Unsubscribe from basket events
            if (basketGrab != null)
            {
                basketGrab.selectExited.RemoveListener(OnBasketDropped);
            }

            while (retainedTrash.Count > 0)
            {
                TrashItem rem = retainedTrash.Dequeue();
                if (rem != null) Destroy(rem.gameObject);
            }
        }
    }
}
