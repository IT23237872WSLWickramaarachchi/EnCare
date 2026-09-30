using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EnCare
{
    public sealed class CleanupMission : MonoBehaviour
    {
        public enum Phase { Setup, Ready, Running, Completed, Failed }

        [Min(1)] public int targetCount = 10;
        [Min(1)] public float timeLimitSeconds = 120f;
        [SerializeField] private RandomTrashSpawner spawner;

        public UnityEvent onCompleted = new UnityEvent();
        public UnityEvent onFailed = new UnityEvent();
        public event Action Changed;

        public Phase State { get; private set; } = Phase.Setup;
        public int TargetCount => targetCount;
        public float Remaining => State == Phase.Running
            ? Mathf.Max(0f, (float)(deadline - Time.timeAsDouble)) : remaining;
        public float RemainingSeconds => Remaining;
        public bool CanInteract => State == Phase.Ready || State == Phase.Running;
        public bool IsRunning => State == Phase.Running;
        public string LastGrabbed { get; private set; } = "";

        // Authoritative count: permanently credited items + currently retained credited items
        public int Collected => permanentlyCredited + retainedCredited.Count;
        public int PermanentlyCredited => permanentlyCredited;
        public int RetainedCredited => retainedCredited.Count;

        private int permanentlyCredited;
        private readonly HashSet<TrashItem> retainedCredited = new HashSet<TrashItem>();
        private readonly HashSet<TrashItem> registered = new HashSet<TrashItem>();
        private readonly List<GarbageCollector> collectors = new List<GarbageCollector>();

        private double deadline;
        private float remaining;

        private void Start()
        {
            remaining = Mathf.Max(1f, timeLimitSeconds);
            if (targetCount < 1 || spawner == null || !spawner.Spawn(this))
            {
                Debug.LogError("EnCare: fix the mission/spawner setup and restart Play mode.", this);
                State = Phase.Failed;
                Changed?.Invoke();
                return;
            }
            State = Phase.Ready;
            Changed?.Invoke();
        }

        internal void Register(TrashItem item)
        {
            if (item != null)
            {
                registered.Add(item);
            }
        }

        internal void Unregister(TrashItem item)
        {
            if (item != null)
            {
                registered.Remove(item);
                retainedCredited.Remove(item);
            }
        }

        internal void RegisterCollector(GarbageCollector collector)
        {
            if (collector != null && !collectors.Contains(collector))
            {
                collectors.Add(collector);
            }
        }

        internal void UnregisterCollector(GarbageCollector collector)
        {
            if (collector != null)
            {
                collectors.Remove(collector);
            }
        }

        internal bool NotifyGrab(TrashItem item)
        {
            if (!CanInteract || item == null || !registered.Contains(item) || CheckTimeout())
                return false;

            if (State == Phase.Ready)
            {
                deadline = Time.timeAsDouble + remaining;
                State = Phase.Running;
            }

            LastGrabbed = item.DisplayName;
            Changed?.Invoke();
            return true;
        }

        // Credit a newly deposited item
        internal bool Credit(TrashItem item)
        {
            if (State != Phase.Running || CheckTimeout() || item == null ||
                !registered.Contains(item) || item.Consumed || item.IsHeld || !item.WasGrabbed)
            {
                return false;
            }

            if (retainedCredited.Contains(item))
            {
                return false;
            }

            retainedCredited.Add(item);
            LastGrabbed = "";

            EvaluateProgress();
            return true;
        }

        // Withdraw credit when a retained item spills / departs
        internal void Withdraw(TrashItem item)
        {
            if (item == null) return;
            if (retainedCredited.Remove(item))
            {
                // Terminal Completed / Failed states freeze progress
                if (State == Phase.Running)
                {
                    Changed?.Invoke();
                }
            }
        }

        // Permanently convert a retained credit to a permanent credit (capacity eviction)
        internal bool MakePermanent(TrashItem item)
        {
            if (item == null) return false;
            if (retainedCredited.Remove(item))
            {
                permanentlyCredited++;
                registered.Remove(item);
                // Total Collected count is unchanged: (permanentlyCredited increases, retainedCredited decreases)
                return true;
            }
            return false;
        }

        // Backward compatibility method
        internal bool TryDeposit(TrashItem item)
        {
            if (!Credit(item)) return false;
            MakePermanent(item);
            item.ConsumePermanently();
            return true;
        }

        private void EvaluateProgress()
        {
            if (State != Phase.Running) return;

            if (Collected >= targetCount)
            {
                remaining = Remaining;
                State = Phase.Completed;
                Changed?.Invoke();
                onCompleted.Invoke();
            }
            else
            {
                Changed?.Invoke();
            }
        }

        private void Update()
        {
            if (State == Phase.Running)
            {
                CheckTimeout();
            }
        }

        private bool CheckTimeout()
        {
            if (State != Phase.Running || Time.timeAsDouble < deadline) return false;

            remaining = 0f;
            State = Phase.Failed;

            // Lock interaction on all surviving trash (including retained items)
            foreach (TrashItem item in registered)
            {
                if (item != null)
                {
                    item.LockInteraction();
                }
            }

            Changed?.Invoke();
            onFailed.Invoke();
            return true;
        }
    }
}


