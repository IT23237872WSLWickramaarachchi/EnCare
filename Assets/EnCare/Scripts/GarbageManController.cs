using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace EnCare
{
    /// <summary>
    /// Controls the EnCare garbage man NPC in the cutscene level.
    /// Uses a simple state machine with coroutine-based transform animations
    /// on primitive objects. Replace the coroutine animations with
    /// Animator.SetTrigger() calls when you swap in your real character model.
    ///
    /// ── SETUP ──────────────────────────────────────────────────────────
    /// 1. Attach this to the root of your Garbage Man GameObject.
    /// 2. Build your garbage man from primitives (or your real model):
    ///       Body   = Capsule (orange hi-vis)
    ///       Head   = Sphere (skin tone)
    ///       Arm_L  = Cube (left arm)
    ///       Arm_R  = Cube (right arm — this does the thumbs up)
    /// 3. Create an empty child called "HandPoint" — this is where the
    ///    basket snaps to when the garbage man "takes" it.
    /// 4. Create an empty child called "TruckUnloadPoint" — position it
    ///    at the truck's collection bin opening.
    /// 5. Create a child with a Box Collider set to "Is Trigger" called
    ///    "DropOffZone" — place it in front of the garbage man. This is
    ///    where the player's basket triggers the handover.
    /// 6. Drag everything into the Inspector slots below.
    /// 7. Wire the onHandoverComplete event to GameEndUI.Show() or
    ///    CutsceneLevelManager as needed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarbageManController : MonoBehaviour
    {
        // ════════════════════════════════════════════════════════════════
        //  NPC STATE
        // ════════════════════════════════════════════════════════════════
        public enum NpcState
        {
            Idle,           // Standing, waiting for basket
            TakeBasket,     // Reaching for / grabbing the basket
            WalkToTruck,    // Carrying basket to the truck
            Unload,         // Dumping basket contents into truck
            ThumbsUp,       // Turning back and giving approval
            Done            // Sequence finished
        }

        [Header("═══ CURRENT STATE (read-only) ═══")]
        [Tooltip("The garbage man's current animation state. Read-only at runtime.")]
        [SerializeField] private NpcState currentState = NpcState.Idle;
        public NpcState CurrentState => currentState;

        // ════════════════════════════════════════════════════════════════
        //  BODY PARTS — Drag & drop primitive GameObjects
        // ════════════════════════════════════════════════════════════════
        [Header("═══ BODY PARTS (drag & drop) ═══")]
        [Tooltip("OPTIONAL — The NPC body (Capsule). Used for idle bob animation.")]
        [SerializeField] private Transform bodyTransform;

        [Tooltip("OPTIONAL — The NPC head (Sphere). Used for look-at-player.")]
        [SerializeField] private Transform headTransform;

        [Tooltip("OPTIONAL — Left arm (Cube). Used for carrying animation.")]
        [SerializeField] private Transform armLeft;

        [Tooltip("REQUIRED — Right arm (Cube). Used for thumbs-up gesture.")]
        [SerializeField] private Transform armRight;

        // ════════════════════════════════════════════════════════════════
        //  KEY POSITIONS — Empty GameObjects as waypoints
        // ════════════════════════════════════════════════════════════════
        [Header("═══ KEY POSITIONS (drag & drop empties) ═══")]
        [Tooltip("REQUIRED — Empty child where the basket attaches when picked up.\n" +
                 "Position this at the NPC's hand/chest level.")]
        [SerializeField] private Transform handPoint;

        [Tooltip("REQUIRED — Empty positioned at the truck's collection bin opening.\n" +
                 "The garbage man carries the basket here to 'unload' it.")]
        [SerializeField] private Transform truckUnloadPoint;

        [Tooltip("REQUIRED — The trigger collider zone in front of the garbage man.\n" +
                 "When the player's basket enters this zone, the handover begins.\n" +
                 "Must have a BoxCollider with 'Is Trigger' checked.")]
        [SerializeField] private BoxCollider dropOffZone;

        // ════════════════════════════════════════════════════════════════
        //  BASKET REFERENCE
        // ════════════════════════════════════════════════════════════════
        [Header("═══ BASKET (drag & drop) ═══")]
        [Tooltip("REQUIRED — The basket GameObject in the scene.\n" +
                 "It must have a Collider (trigger) on it or a child.\n" +
                 "When it enters the DropOffZone, the handover begins.")]
        [SerializeField] private GameObject basket;

        [Tooltip("OPTIONAL — Specific trigger collider on the basket.\n" +
                 "If empty, uses the basket's root collider.")]
        [SerializeField] private Collider basketTrigger;

        // ════════════════════════════════════════════════════════════════
        //  ANIMATION TIMING
        // ════════════════════════════════════════════════════════════════
        [Header("═══ ANIMATION TIMING ═══")]
        [Tooltip("How long the 'take basket' reach animation plays (seconds).")]
        [Range(0.5f, 3f)]
        [SerializeField] private float takeBasketDuration = 1.5f;

        [Tooltip("How long the 'walk to truck' animation plays (seconds).")]
        [Range(1f, 5f)]
        [SerializeField] private float walkToTruckDuration = 2.5f;

        [Tooltip("How long the 'unload' animation plays (seconds).")]
        [Range(0.5f, 3f)]
        [SerializeField] private float unloadDuration = 1.5f;

        [Tooltip("How long the 'thumbs up' gesture holds (seconds).")]
        [Range(1f, 5f)]
        [SerializeField] private float thumbsUpDuration = 2.5f;

        // ════════════════════════════════════════════════════════════════
        //  OPTIONAL ANIMATOR (for when you add real character model)
        // ════════════════════════════════════════════════════════════════
        [Header("═══ ANIMATOR (optional — for real model) ═══")]
        [Tooltip("If assigned, the script will use Animator triggers instead of\n" +
                 "primitive transform tweens. Set up states:\n" +
                 "  • 'TakeBasket'\n  • 'WalkToTruck'\n  • 'Unload'\n  • 'ThumbsUp'")]
        [SerializeField] private Animator npcAnimator;

        // ════════════════════════════════════════════════════════════════
        //  EVENTS
        // ════════════════════════════════════════════════════════════════
        [Header("═══ EVENTS ═══")]
        [Tooltip("Fired when the basket enters the drop-off zone and handover begins.")]
        public UnityEvent onHandoverStarted = new UnityEvent();

        [Tooltip("Fired when the entire sequence (take → unload → thumbs up) finishes.\n" +
                 "Wire this to GameEndUI.Show() to display the end screen.")]
        public UnityEvent onHandoverComplete = new UnityEvent();

        // ════════════════════════════════════════════════════════════════
        //  AUDIO (optional)
        // ════════════════════════════════════════════════════════════════
        [Header("═══ AUDIO (optional) ═══")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Sound when the garbage man takes the basket.")]
        [SerializeField] private AudioClip takeSound;
        [Tooltip("Sound when basket is unloaded into truck.")]
        [SerializeField] private AudioClip unloadSound;
        [Tooltip("Sound for the thumbs-up / approval.")]
        [SerializeField] private AudioClip approvalSound;

        // ════════════════════════════════════════════════════════════════
        //  INTERNAL
        // ════════════════════════════════════════════════════════════════
        private bool handoverTriggered;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 armRightIdleRot;

        private void Start()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            if (armRight != null) armRightIdleRot = armRight.localEulerAngles;

            if (dropOffZone != null) dropOffZone.isTrigger = true;
        }

        // ════════════════════════════════════════════════════════════════
        //  TRIGGER DETECTION — Called by DropOffZoneTrigger child component
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by the DropOffZoneTrigger component on the child DropOffZone object.
        /// You can also call this directly for testing via the context menu.
        /// </summary>
        public void HandleDropOffTrigger(Collider other)
        {
            if (handoverTriggered) return;
            if (currentState != NpcState.Idle) return;

            // Check if the entering collider belongs to our basket
            if (basket == null) return;

            bool isBasket = false;

            // Check if it's the specific basket trigger
            if (basketTrigger != null && other == basketTrigger)
            {
                isBasket = true;
            }
            // Or check if the collider is on the basket or a child of it
            else if (other.transform == basket.transform ||
                     other.transform.IsChildOf(basket.transform))
            {
                isBasket = true;
            }

            if (isBasket)
            {
                handoverTriggered = true;
                Debug.Log("[EnCare GarbageMan] Basket detected in drop-off zone! Starting handover.");
                StartCoroutine(HandoverSequence());
            }
        }

        // Keep OnTriggerEnter as fallback in case the controller IS on the trigger object
        private void OnTriggerEnter(Collider other)
        {
            HandleDropOffTrigger(other);
        }

        // ════════════════════════════════════════════════════════════════
        //  HANDOVER SEQUENCE — The full NPC animation chain
        // ════════════════════════════════════════════════════════════════
        private IEnumerator HandoverSequence()
        {
            onHandoverStarted.Invoke();

            // ── State 1: Take Basket ────────────────────────────────────
            currentState = NpcState.TakeBasket;
            Debug.Log("[EnCare GarbageMan] State → TakeBasket");

            if (npcAnimator != null)
            {
                npcAnimator.SetTrigger("TakeBasket");
                yield return new WaitForSeconds(takeBasketDuration);
            }
            else
            {
                yield return StartCoroutine(AnimateTakeBasket());
            }

            // Parent basket to hand point
            if (basket != null && handPoint != null)
            {
                // Disable physics on basket so it follows the NPC
                var rb = basket.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // Disable any XR grab interactable
                var grabInteractable = basket.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grabInteractable != null)
                    grabInteractable.enabled = false;

                basket.transform.SetParent(handPoint);
                basket.transform.localPosition = Vector3.zero;
                basket.transform.localRotation = Quaternion.identity;
            }

            PlaySound(takeSound);

            // ── State 2: Walk to Truck ──────────────────────────────────
            currentState = NpcState.WalkToTruck;
            Debug.Log("[EnCare GarbageMan] State → WalkToTruck");

            if (npcAnimator != null)
            {
                npcAnimator.SetTrigger("WalkToTruck");
                yield return new WaitForSeconds(walkToTruckDuration);
            }
            else
            {
                yield return StartCoroutine(AnimateWalkToTruck());
            }

            // ── State 3: Unload ─────────────────────────────────────────
            currentState = NpcState.Unload;
            Debug.Log("[EnCare GarbageMan] State → Unload");

            if (npcAnimator != null)
            {
                npcAnimator.SetTrigger("Unload");
                yield return new WaitForSeconds(unloadDuration);
            }
            else
            {
                yield return StartCoroutine(AnimateUnload());
            }

            PlaySound(unloadSound);

            // Hide the basket (it's been "dumped" into the truck)
            if (basket != null)
            {
                basket.transform.SetParent(null);
                basket.SetActive(false);
            }

            // ── State 4: Turn back and Thumbs Up ────────────────────────
            currentState = NpcState.ThumbsUp;
            Debug.Log("[EnCare GarbageMan] State → ThumbsUp");

            if (npcAnimator != null)
            {
                npcAnimator.SetTrigger("ThumbsUp");
                yield return new WaitForSeconds(thumbsUpDuration);
            }
            else
            {
                yield return StartCoroutine(AnimateThumbsUp());
            }

            PlaySound(approvalSound);

            // ── State 5: Done ───────────────────────────────────────────
            currentState = NpcState.Done;
            Debug.Log("[EnCare GarbageMan] State → Done. Handover complete!");
            onHandoverComplete.Invoke();
        }

        // ════════════════════════════════════════════════════════════════
        //  PRIMITIVE ANIMATIONS (replace these when you add real model)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Primitive animation: NPC bends forward slightly to "reach" for the basket.
        /// </summary>
        private IEnumerator AnimateTakeBasket()
        {
            // Lean forward
            Quaternion startRot = transform.rotation;
            Quaternion leanRot = startRot * Quaternion.Euler(15f, 0f, 0f);

            float t = 0f;
            float halfDur = takeBasketDuration * 0.5f;

            // Lean forward
            while (t < halfDur)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, leanRot, t / halfDur);
                yield return null;
            }

            // Lean back
            t = 0f;
            while (t < halfDur)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(leanRot, startRot, t / halfDur);
                yield return null;
            }

            transform.rotation = startRot;
        }

        /// <summary>
        /// Primitive animation: NPC slides toward the truck unload point.
        /// </summary>
        private IEnumerator AnimateWalkToTruck()
        {
            if (truckUnloadPoint == null) yield break;

            Vector3 startPos = transform.position;
            // Move to a position near the truck unload point (offset back slightly so NPC faces truck)
            Vector3 targetPos = truckUnloadPoint.position - truckUnloadPoint.forward * 0.8f;
            targetPos.y = startPos.y; // Keep same height

            // Face the truck
            Vector3 lookDir = (truckUnloadPoint.position - startPos);
            lookDir.y = 0f;
            Quaternion targetRot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir)
                : transform.rotation;

            float t = 0f;
            Quaternion startRot = transform.rotation;

            while (t < walkToTruckDuration)
            {
                t += Time.deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, t / walkToTruckDuration);
                transform.position = Vector3.Lerp(startPos, targetPos, progress);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, progress);

                // Simple walk bob
                if (bodyTransform != null)
                {
                    float bob = Mathf.Sin(t * 8f) * 0.03f;
                    bodyTransform.localPosition = new Vector3(0f, bob, 0f);
                }
                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;
            if (bodyTransform != null) bodyTransform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Primitive animation: NPC tips forward to "dump" the basket.
        /// </summary>
        private IEnumerator AnimateUnload()
        {
            // Tip forward as if dumping
            Quaternion startRot = transform.rotation;
            Quaternion tipRot = startRot * Quaternion.Euler(30f, 0f, 0f);

            float t = 0f;
            float halfDur = unloadDuration * 0.5f;

            while (t < halfDur)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, tipRot, t / halfDur);
                yield return null;
            }

            t = 0f;
            while (t < halfDur)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(tipRot, startRot, t / halfDur);
                yield return null;
            }

            transform.rotation = startRot;
        }

        /// <summary>
        /// Primitive animation: NPC turns back to face player spawn and raises right arm.
        /// </summary>
        private IEnumerator AnimateThumbsUp()
        {
            // Turn back toward initial facing
            Vector3 turnDir = (initialPosition + initialRotation * Vector3.forward) - transform.position;
            turnDir.y = 0f;
            Quaternion facePlayerRot = turnDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(turnDir)
                : initialRotation;

            // Turn to face player
            float t = 0f;
            float turnDur = thumbsUpDuration * 0.3f;
            Quaternion startRot = transform.rotation;
            while (t < turnDur)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, facePlayerRot, t / turnDur);
                yield return null;
            }
            transform.rotation = facePlayerRot;

            // Raise right arm (thumbs up gesture)
            if (armRight != null)
            {
                Vector3 startArmRot = armRight.localEulerAngles;
                Vector3 raisedRot = new Vector3(-150f, 0f, 0f); // Arm raised up and forward

                t = 0f;
                float raiseDur = thumbsUpDuration * 0.3f;
                while (t < raiseDur)
                {
                    t += Time.deltaTime;
                    armRight.localEulerAngles = Vector3.Lerp(startArmRot, raisedRot, t / raiseDur);
                    yield return null;
                }
                armRight.localEulerAngles = raisedRot;
            }

            // Hold the pose
            yield return new WaitForSeconds(thumbsUpDuration * 0.4f);
        }

        // ════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════
        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        // ════════════════════════════════════════════════════════════════
        //  EDITOR HELPERS
        // ════════════════════════════════════════════════════════════════
        #if UNITY_EDITOR
        [ContextMenu("▶ Test Handover (Play Mode Only)")]
        private void TestHandover()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[EnCare GarbageMan] Enter Play mode first.");
                return;
            }
            if (currentState != NpcState.Idle)
            {
                Debug.Log("[EnCare GarbageMan] Already in progress.");
                return;
            }
            handoverTriggered = true;
            StartCoroutine(HandoverSequence());
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize the drop-off zone
            if (dropOffZone != null)
            {
                Gizmos.color = new Color(0f, 1f, 0.3f, 0.3f);
                Gizmos.matrix = dropOffZone.transform.localToWorldMatrix;
                Gizmos.DrawCube(dropOffZone.center, dropOffZone.size);
                Gizmos.DrawWireCube(dropOffZone.center, dropOffZone.size);
            }

            // Visualize hand point
            if (handPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(handPoint.position, 0.1f);
            }

            // Visualize truck unload point
            if (truckUnloadPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(truckUnloadPoint.position, 0.15f);
                Gizmos.DrawLine(truckUnloadPoint.position, truckUnloadPoint.position + truckUnloadPoint.forward * 0.5f);
            }
        }
        #endif
    }
}
