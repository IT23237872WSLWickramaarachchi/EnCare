using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EnCare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class TrashItem : MonoBehaviour
    {
        [SerializeField] string displayName = "E-waste";
        [Tooltip("Put this near the object's centre, within its physical collider.")]
        [SerializeField] Transform depositPoint;
        XRGrabInteractable grab;

        bool wasReGrabbed;

        public CleanupMission Mission { get; private set; }
        public bool WasGrabbed { get; private set; }
        public bool Consumed { get; private set; }

        /// <summary>True when this item has been released from the basket and needs to be picked up and re-deposited.</summary>
        public bool NeedsRedeposit { get; private set; }

        /// <summary>True when the player has grabbed a released item and it is now ready to be re-deposited into the basket.</summary>
        internal bool ReadyForRedeposit => NeedsRedeposit && wasReGrabbed;

        public bool IsHeld => grab != null && grab.isSelected;
        public string DisplayName => displayName;
        public Vector3 DepositPosition => depositPoint != null ? depositPoint.position : transform.position;

        void Awake()
        {
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
        }

        void OnEnable()
        {
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
            if (grab != null) grab.selectEntered.AddListener(OnGrabbed);
        }

        void OnDisable()
        {
            if (grab != null) grab.selectEntered.RemoveListener(OnGrabbed);
        }

        internal void Initialize(CleanupMission mission)
        {
            Mission = mission;
            mission.Register(this);
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            // If the player picks up a released basket item, mark it as re-grabbed
            if (NeedsRedeposit)
            {
                wasReGrabbed = true;
                return;
            }
            if (Mission != null && Mission.NotifyGrab(this)) WasGrabbed = true;
        }

        internal void MarkConsumed()
        {
            Consumed = true;
        }

        /// <summary>
        /// Attach this item as a visual passenger of the basket.
        /// Uses BasketPassenger to track position without parenting (no compound physics issues).
        /// </summary>
        internal void OnDepositedInBasket(Transform basket)
        {
            Consumed = true;
            NeedsRedeposit = false;
            wasReGrabbed = false;
            LockInteraction();

            // Disable glow effects
            var trashGlow = GetComponentInChildren<TrashGlow>();
            if (trashGlow != null) trashGlow.SetGlowEnabled(false);

            var eWasteGlow = GetComponentInChildren<EnCare.VR.EWasteGlow>();
            if (eWasteGlow != null) eWasteGlow.SetGlowEnabled(false);

            // Attach as visual passenger (no parenting, no physics conflicts)
            var passenger = GetComponent<BasketPassenger>();
            if (passenger == null) passenger = gameObject.AddComponent<BasketPassenger>();
            passenger.Attach(basket);
        }

        /// <summary>
        /// Called when the basket is dropped. Re-enables physics and grab
        /// so the item tumbles out and must be picked up again as a challenge.
        /// </summary>
        internal void ReleaseFromBasket()
        {
            NeedsRedeposit = true;
            wasReGrabbed = false;

            var passenger = GetComponent<BasketPassenger>();
            if (passenger != null) passenger.Release();

            // Re-enable grab so the player can pick it up
            if (grab != null) grab.enabled = true;
        }

        internal void Consume()
        {
            Consumed = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        internal void LockInteraction()
        {
            if (grab != null) grab.enabled = false;
        }
    }
}
