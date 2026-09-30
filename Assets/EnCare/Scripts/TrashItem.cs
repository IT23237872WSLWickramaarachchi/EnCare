using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EnCare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class TrashItem : MonoBehaviour
    {
        [SerializeField] private string displayName = "E-waste";
        [Tooltip("Put this near the object's centre, within its physical collider.")]
        [SerializeField] private Transform depositPoint;
        [Tooltip("Optional explicit reference to the XRGrabInteractable component.")]
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private UnityEvent onRetained = new UnityEvent();
        [SerializeField] private UnityEvent onAvailable = new UnityEvent();

        private CleanupMission mission;
        private bool isHeldByHand;
        private Rigidbody rb;

        public CleanupMission Mission => mission;
        public Rigidbody Rigidbody
        {
            get
            {
                if (rb == null) rb = GetComponent<Rigidbody>();
                return rb;
            }
        }
        public Vector3 DepositPosition => depositPoint != null ? depositPoint.position : transform.position;
        public string DisplayName => displayName;
        public bool WasGrabbed { get; private set; }
        public bool Consumed { get; private set; }
        public bool IsHeld => (grabInteractable != null && grabInteractable.isSelected) || isHeldByHand;
        public GarbageCollector Owner { get; private set; }
        public UnityEvent OnRetained => onRetained;
        public UnityEvent OnAvailable => onAvailable;

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }
        }

        private void OnEnable()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (grabInteractable != null)
            {
                grabInteractable.firstSelectEntered.AddListener(OnFirstSelectEntered);
                grabInteractable.lastSelectExited.AddListener(OnLastSelectExited);
            }

            if (mission != null)
            {
                mission.Register(this);
            }
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.firstSelectEntered.RemoveListener(OnFirstSelectEntered);
                grabInteractable.lastSelectExited.RemoveListener(OnLastSelectExited);
            }

            isHeldByHand = false;
            if (!Consumed && Owner != null)
            {
                Owner.Release(this);
            }
            if (mission != null)
            {
                mission.Unregister(this);
            }
        }

        public void Initialize(CleanupMission value)
        {
            if (Consumed) return;
            if (mission == value)
            {
                if (mission != null) mission.Register(this);
                return;
            }
            if (Owner != null) Owner.Release(this);
            if (mission != null) mission.Unregister(this);
            mission = value;
            if (mission != null && isActiveAndEnabled)
            {
                mission.Register(this);
            }
        }

        private void OnFirstSelectEntered(SelectEnterEventArgs args)
        {
            NotifyGrab();
        }

        private void OnLastSelectExited(SelectExitEventArgs args)
        {
            NotifyRelease();
        }

        public void NotifyGrab()
        {
            if (Consumed) return;
            isHeldByHand = true;
            WasGrabbed = true;
            if (Owner != null && Owner.LocomotionCompensator != null)
            {
                Owner.LocomotionCompensator.SetRetainedHeld(Rigidbody, true);
            }
            if (mission != null)
            {
                mission.NotifyGrab(this);
            }
        }

        public void NotifyRelease()
        {
            isHeldByHand = false;
            if (Owner != null && Owner.LocomotionCompensator != null)
            {
                Owner.LocomotionCompensator.SetRetainedHeld(Rigidbody, false);
            }
        }

        internal void SetOwner(GarbageCollector value)
        {
            Owner = value;
        }

        internal void PublishRetained()
        {
            onRetained.Invoke();
        }

        internal void PublishAvailable()
        {
            onAvailable.Invoke();
        }

        // Called when permanently converting retained item or during immediate collection.
        internal void ConsumePermanently()
        {
            Consumed = true;
            Owner = null;
            isHeldByHand = false;
            if (mission != null)
            {
                mission.Unregister(this);
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        // Backward compatibility
        internal void Consume()
        {
            ConsumePermanently();
        }

        public void LockInteraction()
        {
            if (grabInteractable != null)
            {
                grabInteractable.enabled = false;
            }
        }
    }
}


