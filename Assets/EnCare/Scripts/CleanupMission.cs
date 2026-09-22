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
        [SerializeField] RandomTrashSpawner spawner;
        public UnityEvent onCompleted = new UnityEvent();
        public UnityEvent onFailed = new UnityEvent();
        public event Action Changed;
        public Phase State { get; private set; } = Phase.Setup;
        public int Collected { get; private set; }
        public string LastGrabbed { get; private set; } = "";
        public float Remaining => State == Phase.Running
            ? Mathf.Max(0f, (float)(deadline - Time.timeAsDouble)) : remaining;
        public bool CanInteract => State == Phase.Ready || State == Phase.Running;
        readonly HashSet<TrashItem> registered = new HashSet<TrashItem>();
        double deadline;
        float remaining;

        void Start()
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

        internal void Register(TrashItem item) => registered.Add(item);

        internal bool NotifyGrab(TrashItem item)
        {
            if (!CanInteract || !registered.Contains(item) || CheckTimeout()) return false;
            if (State == Phase.Ready)
            {
                deadline = Time.timeAsDouble + remaining;
                State = Phase.Running;
            }
            LastGrabbed = item.DisplayName;
            Changed?.Invoke();
            return true;
        }

        // Only the collector calls this after its spatial checks.
        internal bool TryDeposit(TrashItem item)
        {
            if (State != Phase.Running || CheckTimeout() || item == null ||
                !registered.Contains(item) || item.Consumed || !item.WasGrabbed || item.IsHeld)
                return false;
            registered.Remove(item); // Compound colliders cannot count twice.
            item.Consume();
            Collected++;
            LastGrabbed = "";
            if (Collected >= targetCount)
            {
                remaining = Remaining;
                State = Phase.Completed;
                Changed?.Invoke();
                onCompleted.Invoke();
            }
            else Changed?.Invoke();
            return true;
        }

        void Update() { if (State == Phase.Running) CheckTimeout(); }

        bool CheckTimeout()
        {
            if (State != Phase.Running || Time.timeAsDouble < deadline) return false;
            remaining = 0f;
            State = Phase.Failed;
            foreach (TrashItem item in registered)
                if (item != null) item.LockInteraction();
            Changed?.Invoke();
            onFailed.Invoke();
            return true;
        }
    }
}
