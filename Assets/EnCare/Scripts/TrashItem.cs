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
        public CleanupMission Mission { get; private set; }
        public bool WasGrabbed { get; private set; }
        public bool Consumed { get; private set; }
        public bool IsHeld => grab != null && grab.isSelected;
        public string DisplayName => displayName;
        public Vector3 DepositPosition => depositPoint != null ? depositPoint.position : transform.position;

        void Awake() { if (grab == null) grab = GetComponent<XRGrabInteractable>(); }
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
            if (Mission != null && Mission.NotifyGrab(this)) WasGrabbed = true;
        }
        internal void Consume()
        {
            Consumed = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        internal void LockInteraction() { grab.enabled = false; }
    }
}
