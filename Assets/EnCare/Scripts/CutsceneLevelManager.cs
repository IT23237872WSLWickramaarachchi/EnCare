using UnityEngine;
using UnityEngine.Events;

namespace EnCare
{
    /// <summary>
    /// Manages the cutscene level flow from scene load to game end.
    /// Orchestrates the player, basket, garbage man, and game end UI.
    ///
    /// ── SETUP ──────────────────────────────────────────────────────────
    /// 1. Create an empty GameObject called "CutsceneLevelManager".
    /// 2. Attach this script.
    /// 3. Drag in the references below.
    /// 4. Wire GarbageManController.onHandoverComplete → this.OnHandoverComplete()
    ///    OR let this script auto-wire if garbageManController is assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneLevelManager : MonoBehaviour
    {
        // ════════════════════════════════════════════════════════════════
        //  SCENE REFERENCES — Drag & drop from scene hierarchy
        // ════════════════════════════════════════════════════════════════
        [Header("═══ GARBAGE MAN (drag & drop) ═══")]
        [Tooltip("REQUIRED — The GarbageManController component in the scene.\n" +
                 "This script auto-wires the handover complete event.")]
        [SerializeField] private GarbageManController garbageManController;

        [Header("═══ GAME END UI (drag & drop) ═══")]
        [Tooltip("REQUIRED — The GameEndUI component in the scene.\n" +
                 "Shown when the garbage man finishes the handover.")]
        [SerializeField] private GameEndUI gameEndUI;

        [Header("═══ BASKET (drag & drop) ═══")]
        [Tooltip("OPTIONAL — The basket GameObject.\n" +
                 "If assigned, it will be positioned at basketSpawnPoint on scene load.\n" +
                 "Leave empty if the basket is already placed in the scene.")]
        [SerializeField] private GameObject basket;

        [Tooltip("OPTIONAL — Where to place the basket when the scene loads.\n" +
                 "Typically near the player's starting position.")]
        [SerializeField] private Transform basketSpawnPoint;

        // ════════════════════════════════════════════════════════════════
        //  PLAYER SETTINGS
        // ════════════════════════════════════════════════════════════════
        [Header("═══ PLAYER SPAWN (optional) ═══")]
        [Tooltip("OPTIONAL — Where the VR player spawns.\n" +
                 "If assigned, XR Origin is moved here on scene load.")]
        [SerializeField] private Transform playerSpawnPoint;

        [Tooltip("OPTIONAL — The XR Origin / player root.\n" +
                 "Auto-discovered if not assigned.")]
        [SerializeField] private GameObject xrOrigin;

        // ════════════════════════════════════════════════════════════════
        //  INSTRUCTION UI
        // ════════════════════════════════════════════════════════════════
        [Header("═══ INSTRUCTION TEXT (optional) ═══")]
        [Tooltip("OPTIONAL — A world-space canvas with instructions like\n" +
                 "'Bring the basket to the EnCare worker'.\n" +
                 "Hidden when handover begins.")]
        [SerializeField] private GameObject instructionUI;

        // ════════════════════════════════════════════════════════════════
        //  EVENTS
        // ════════════════════════════════════════════════════════════════
        [Header("═══ EVENTS ═══")]
        [Tooltip("Fired when the cutscene scene finishes loading and is ready.")]
        public UnityEvent onSceneReady = new UnityEvent();

        [Tooltip("Fired when the handover sequence completes.")]
        public UnityEvent onGameEnd = new UnityEvent();

        // ════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════════════════
        private void Awake()
        {
            // Auto-find XR Origin
            if (xrOrigin == null)
            {
                xrOrigin = GameObject.Find("XR Origin (XR Rig)")
                        ?? GameObject.Find("XR Origin")
                        ?? GameObject.Find("Complete XR Origin Set Up Variant");
            }
        }

        private void Start()
        {
            // Position player at spawn
            if (playerSpawnPoint != null && xrOrigin != null)
            {
                xrOrigin.transform.position = playerSpawnPoint.position;
                xrOrigin.transform.rotation = playerSpawnPoint.rotation;
            }

            // Position basket at spawn
            if (basket != null && basketSpawnPoint != null)
            {
                basket.transform.position = basketSpawnPoint.position;
                basket.transform.rotation = basketSpawnPoint.rotation;
            }

            // Auto-wire garbage man handover complete event
            if (garbageManController != null)
            {
                garbageManController.onHandoverComplete.AddListener(OnHandoverComplete);
                garbageManController.onHandoverStarted.AddListener(OnHandoverStarted);
            }

            // Show instructions
            if (instructionUI != null)
                instructionUI.SetActive(true);

            // Hide game end UI
            if (gameEndUI != null)
                gameEndUI.gameObject.SetActive(false);

            Debug.Log("[EnCare CutsceneLevel] Scene ready. Bring the basket to the EnCare worker!");
            onSceneReady.Invoke();
        }

        // ════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called when the basket enters the garbage man's drop-off zone.
        /// </summary>
        public void OnHandoverStarted()
        {
            // Hide instruction text
            if (instructionUI != null)
                instructionUI.SetActive(false);

            Debug.Log("[EnCare CutsceneLevel] Handover started — hiding instructions.");
        }

        /// <summary>
        /// Called when the garbage man finishes the entire handover sequence.
        /// Wire GarbageManController.onHandoverComplete → this method,
        /// OR it auto-wires in Start() if garbageManController is assigned.
        /// </summary>
        public void OnHandoverComplete()
        {
            Debug.Log("[EnCare CutsceneLevel] Handover complete! Showing game end screen.");

            if (gameEndUI != null)
            {
                gameEndUI.Show();
            }

            onGameEnd.Invoke();
        }

        // ════════════════════════════════════════════════════════════════
        //  EDITOR HELPERS
        // ════════════════════════════════════════════════════════════════
        #if UNITY_EDITOR
        [ContextMenu("▶ Force Show Game End UI (Play Mode Only)")]
        private void ForceShowGameEnd()
        {
            if (Application.isPlaying) OnHandoverComplete();
            else Debug.Log("[EnCare CutsceneLevel] Enter Play mode first.");
        }

        private void OnDrawGizmosSelected()
        {
            if (playerSpawnPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(playerSpawnPoint.position, 0.3f);
                Gizmos.DrawRay(playerSpawnPoint.position, playerSpawnPoint.forward);
            }

            if (basketSpawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(basketSpawnPoint.position, Vector3.one * 0.25f);
            }
        }
        #endif
    }
}
