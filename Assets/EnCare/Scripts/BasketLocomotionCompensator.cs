using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using Unity.XR.CoreUtils;

namespace EnCare
{
    /// <summary>
    /// Explicit locomotion transaction component that compensates held basket and retained trash
    /// for artificial locomotion (walking, climbing steps, joystick rotation) in XR.
    /// Hooks into XRBodyTransformer's beforeApplyTransformations / afterApplyTransformations
    /// to bracket artificial root movement, preventing double application and catch-up velocities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BasketLocomotionCompensator : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Inspector toggle for A/B testing and easy reversion. When disabled, compensation does not run but the registry remains intact.")]
        [SerializeField] private bool compensationEnabled = true;

        [Tooltip("Transform actually moved by locomotion (the XR Origin / CharacterController root), not the camera or hand.")]
        [SerializeField] private Transform locomotionRoot;

        [Tooltip("Rigidbody of the carried basket.")]
        [SerializeField] private Rigidbody basketBody;

        [Tooltip("XRGrabInteractable of the basket, used for grab lifecycle and history rebasing.")]
        [SerializeField] private XRGrabInteractable basketGrabInteractable;

        [Tooltip("The XRBodyTransformer that applies queued artificial locomotion transformations.")]
        [SerializeField] private XRBodyTransformer bodyTransformer;

        // Registry containing only items accepted by the owner collector.
        // True = held by hand (do not compensate), False = unheld retained (compensate).
        private readonly Dictionary<Rigidbody, bool> retained = new Dictionary<Rigidbody, bool>();
        private readonly List<BodySnapshot> snapshots = new List<BodySnapshot>();
        private readonly List<Rigidbody> deadBodies = new List<Rigidbody>();

        private bool basketHeld;
        private bool transactionOpen;
        private bool isSubscribedToLocomotion;
        private Vector3 oldRootPosition;
        private Quaternion oldRootRotation;
        private Vector3 oldRootScale;
        private BodySnapshot basketSnapshot;

        private static readonly MethodInfo s_OnTeleportedMethod =
            typeof(XRGrabInteractable).GetMethod("OnTeleported",
                BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Event invoked when locomotion finishes so grab history / targets can be rebased.
        /// </summary>
        public event Action<LocomotionDelta> RebaseGrabHistory;

        public bool CompensationEnabled
        {
            get => compensationEnabled;
            set => compensationEnabled = value;
        }

        public bool BasketHeld => basketHeld;
        public int RetainedTrackedCount => retained.Count;

        public readonly struct LocomotionDelta
        {
            public readonly Vector3 OldOrigin;
            public readonly Vector3 NewOrigin;
            public readonly Quaternion Rotation;

            public LocomotionDelta(Vector3 oldOrigin, Vector3 newOrigin, Quaternion rotation)
            {
                OldOrigin = oldOrigin;
                NewOrigin = newOrigin;
                Rotation = rotation;
            }

            public Vector3 TransformPoint(Vector3 point)
                => NewOrigin + Rotation * (point - OldOrigin);
            public Quaternion TransformRotation(Quaternion rotation) => Rotation * rotation;
            public Vector3 TransformVector(Vector3 vector) => Rotation * vector;
        }

        private struct BodySnapshot
        {
            public Rigidbody Body;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;

            public BodySnapshot(Rigidbody body)
            {
                Body = body;
                Position = body.position;
                Rotation = body.rotation;
                Velocity = GetVelocity(body);
                AngularVelocity = body.angularVelocity;
            }
        }

        private static Vector3 GetVelocity(Rigidbody body)
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }

        private static void SetVelocity(Rigidbody body, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
        }

        private void Awake()
        {
            FindComponents();
        }

        private void OnEnable()
        {
            FindComponents();

            RebaseGrabHistory += HandleRebaseGrabHistory;

            if (basketGrabInteractable != null)
            {
                basketGrabInteractable.firstSelectEntered.AddListener(OnBasketFirstSelectEntered);
                basketGrabInteractable.lastSelectExited.AddListener(OnBasketLastSelectExited);

                if (basketGrabInteractable.isSelected)
                {
                    BasketGrabbed();
                }
            }
        }

        private void OnDisable()
        {
            RebaseGrabHistory -= HandleRebaseGrabHistory;

            if (basketGrabInteractable != null)
            {
                basketGrabInteractable.firstSelectEntered.RemoveListener(OnBasketFirstSelectEntered);
                basketGrabInteractable.lastSelectExited.RemoveListener(OnBasketLastSelectExited);
            }

            BasketReleased();
            ClearRetained();
        }

        private void FindComponents()
        {
            if (basketBody == null)
            {
                if (!TryGetComponent(out basketBody))
                {
                    basketBody = GetComponentInParent<Rigidbody>();
                }
            }

            if (basketGrabInteractable == null)
            {
                if (!TryGetComponent(out basketGrabInteractable))
                {
                    basketGrabInteractable = GetComponentInParent<XRGrabInteractable>();
                }
            }

            FindLocomotionComponents();
        }

        private void FindLocomotionComponents()
        {
            if (bodyTransformer == null)
            {
                bodyTransformer = UnityEngine.Object.FindFirstObjectByType<XRBodyTransformer>();
            }

            if (locomotionRoot == null)
            {
                if (bodyTransformer != null && bodyTransformer.xrOrigin != null && bodyTransformer.xrOrigin.Origin != null)
                {
                    locomotionRoot = bodyTransformer.xrOrigin.Origin.transform;
                }
                else
                {
                    var xrOrigin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
                    if (xrOrigin != null && xrOrigin.Origin != null)
                    {
                        locomotionRoot = xrOrigin.Origin.transform;
                    }
                }
            }
        }

        private void OnBasketFirstSelectEntered(SelectEnterEventArgs args)
        {
            BasketGrabbed();
        }

        private void OnBasketLastSelectExited(SelectExitEventArgs args)
        {
            BasketReleased();
        }

        // Call from basket's FIRST selection / LAST deselection callbacks.
        public void BasketGrabbed()
        {
            basketHeld = true;
            CancelLocomotion();
            SubscribeToLocomotion();
        }

        public void BasketReleased()
        {
            basketHeld = false;
            CancelLocomotion();
            UnsubscribeFromLocomotion();
        }

        private void SubscribeToLocomotion()
        {
            if (isSubscribedToLocomotion) return;

            if (bodyTransformer == null)
            {
                FindLocomotionComponents();
            }

            if (bodyTransformer != null)
            {
                bodyTransformer.beforeApplyTransformations += OnBeforeApplyTransformations;
                bodyTransformer.afterApplyTransformations += OnAfterApplyTransformations;
                isSubscribedToLocomotion = true;
            }
        }

        private void UnsubscribeFromLocomotion()
        {
            if (!isSubscribedToLocomotion) return;

            if (bodyTransformer != null)
            {
                bodyTransformer.beforeApplyTransformations -= OnBeforeApplyTransformations;
                bodyTransformer.afterApplyTransformations -= OnAfterApplyTransformations;
            }

            isSubscribedToLocomotion = false;
        }

        private void OnBeforeApplyTransformations(XRBodyTransformer transformer)
        {
            BeginLocomotion();
        }

        private void OnAfterApplyTransformations(ApplyBodyTransformationsEventArgs args)
        {
            EndLocomotion();
        }

        // Call only after collector accepts item and assigns ownership.
        public void TrackRetained(Rigidbody body, bool currentlyHeld = false)
        {
            if (!body || body == basketBody) return;
            retained[body] = currentlyHeld;
        }

        // Call before spill, eviction, ownership transfer, disable or destruction.
        public void UntrackRetained(Rigidbody body)
        {
            if (!ReferenceEquals(body, null)) retained.Remove(body);
        }

        // Bind through item's actual XRI state. Do not compensate a held item:
        // its own grab-follow system controls it even while inside the basket.
        public void SetRetainedHeld(Rigidbody body, bool held)
        {
            if (body && retained.ContainsKey(body)) retained[body] = held;
        }

        public bool BeginLocomotion()
        {
            if (transactionOpen)
            {
                // In case of unexpected interruption, reset rather than throw
                CancelLocomotion();
            }

            if (!isActiveAndEnabled || !compensationEnabled || !basketHeld ||
                !basketBody || !locomotionRoot) return false;

            oldRootPosition = locomotionRoot.position;
            oldRootRotation = locomotionRoot.rotation;
            oldRootScale = locomotionRoot.lossyScale;
            basketSnapshot = new BodySnapshot(basketBody);
            snapshots.Clear();
            deadBodies.Clear();

            foreach (var entry in retained)
            {
                Rigidbody body = entry.Key;
                if (!body)
                {
                    deadBodies.Add(body);
                    continue;
                }
                if (entry.Value || !body.gameObject.activeInHierarchy || body.isKinematic)
                    continue;

                snapshots.Add(new BodySnapshot(body));
            }

            foreach (var body in deadBodies)
            {
                retained.Remove(body);
            }

            transactionOpen = true;
            return true;
        }

        public void EndLocomotion()
        {
            if (!transactionOpen) return;
            transactionOpen = false;

            if (!compensationEnabled || !basketHeld || !basketBody || !locomotionRoot)
            {
                snapshots.Clear();
                return;
            }

            if ((locomotionRoot.lossyScale - oldRootScale).sqrMagnitude > 0.000001f)
            {
                snapshots.Clear();
                Debug.LogError("[EnCare] Locomotion compensation supports rigid translation/rotation, not rig scaling.", this);
                return;
            }

            var delta = new LocomotionDelta(
                oldRootPosition,
                locomotionRoot.position,
                locomotionRoot.rotation * Quaternion.Inverse(oldRootRotation));

            if ((delta.NewOrigin - delta.OldOrigin).sqrMagnitude < 1e-14f &&
                Quaternion.Angle(delta.Rotation, Quaternion.identity) < 0.00001f)
            {
                snapshots.Clear();
                return;
            }

            // Set from the pre-locomotion snapshot, not the current pose.
            // This is a common frame correction, NOT a velocity-driven sweep or force.
            Apply(basketSnapshot, delta);

            foreach (var snapshot in snapshots)
            {
                Rigidbody body = snapshot.Body;
                if (!body || !body.gameObject.activeInHierarchy || body.isKinematic ||
                    !retained.TryGetValue(body, out bool held) || held)
                    continue;

                Apply(snapshot, delta);
            }
            snapshots.Clear();

            // Synchronize physics collider poses immediately for subsequent queries
            Physics.SyncTransforms();

            RebaseGrabHistory?.Invoke(delta);
        }

        private static void Apply(BodySnapshot snapshot, LocomotionDelta delta)
        {
            Rigidbody body = snapshot.Body;
            if (!body) return;

            body.position = delta.TransformPoint(snapshot.Position);
            body.rotation = delta.TransformRotation(snapshot.Rotation);

            if (!body.isKinematic)
            {
                // Preserve existing physical motion in the rotated frame.
                // Never add root displacement / deltaTime as launch velocity.
                SetVelocity(body, delta.TransformVector(snapshot.Velocity));
                body.angularVelocity = delta.TransformVector(snapshot.AngularVelocity);
            }

            body.WakeUp();
        }

        private void HandleRebaseGrabHistory(LocomotionDelta delta)
        {
            if (basketGrabInteractable == null) return;

            // Rebase grab-follow target pose so grab follow does not perceive a catch-up error
            Pose curTarget = basketGrabInteractable.GetTargetPose();
            Vector3 rebasedPos = delta.TransformPoint(curTarget.position);
            Quaternion rebasedRot = delta.TransformRotation(curTarget.rotation);
            basketGrabInteractable.SetTargetPose(new Pose(rebasedPos, rebasedRot));

            // Rebase throw smoothing reference pose so detaching doesn't derive a huge throw spike
            if (s_OnTeleportedMethod != null)
            {
                try
                {
                    Pose beforePose = new Pose(delta.OldOrigin, oldRootRotation);
                    Pose afterPose = new Pose(delta.NewOrigin, delta.Rotation * oldRootRotation);
                    Pose deltaPose = new Pose(delta.NewOrigin - delta.OldOrigin, delta.Rotation);
                    s_OnTeleportedMethod.Invoke(basketGrabInteractable, new object[] { beforePose, afterPose, deltaPose });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[EnCare] Rebase throw smoothing notice: {ex.Message}");
                }
            }
        }

        public void CancelLocomotion()
        {
            transactionOpen = false;
            snapshots.Clear();
        }

        public void ClearRetained()
        {
            CancelLocomotion();
            retained.Clear();
        }
    }
}
