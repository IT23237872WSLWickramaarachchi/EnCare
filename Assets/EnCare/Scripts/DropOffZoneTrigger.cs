using UnityEngine;

namespace EnCare
{
    /// <summary>
    /// Small helper placed on the DropOffZone child object.
    /// Forwards trigger events up to the parent GarbageManController.
    /// This is needed because the DropOffZone collider is on a child object,
    /// but the GarbageManController script is on the parent NPC root.
    ///
    /// ── SETUP ──────────────────────────────────────────────────────────
    /// Automatically added by CutsceneSceneBuilder. If you're setting up
    /// manually, attach this to the same GameObject as the DropOffZone
    /// BoxCollider (the one with Is Trigger checked).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DropOffZoneTrigger : MonoBehaviour
    {
        [Header("═══ PARENT CONTROLLER (drag & drop) ═══")]
        [Tooltip("REQUIRED — The GarbageManController on the NPC root.\n" +
                 "Auto-discovered from parent if not set.")]
        [SerializeField] private GarbageManController controller;

        private void Awake()
        {
            if (controller == null)
                controller = GetComponentInParent<GarbageManController>();

            if (controller == null)
                Debug.LogError("[EnCare DropOffZone] No GarbageManController found in parent hierarchy!", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Forward the trigger event to the controller
            if (controller != null)
                controller.HandleDropOffTrigger(other);
        }
    }
}
