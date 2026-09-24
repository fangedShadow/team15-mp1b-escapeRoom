using System.Collections.Generic;
using UnityEngine;

namespace Team15.Deck
{
    public class LockManager : MonoBehaviour
    {
        [SerializeField] private GameObject door1;
        [SerializeField] private GameObject door2;
        [SerializeField, Min(1)] private int totalLocksRequired = 3;
        private readonly HashSet<LockDetector> openedLocks = new HashSet<LockDetector>();
        private readonly HashSet<GameObject> claimedKeys = new HashSet<GameObject>();

        public int OpenedCount => openedLocks.Count;
        public bool IsComplete { get; private set; }
        public event System.Action Completed;

        public bool TryUnlock(LockDetector detector, GameObject key)
        {
            if (IsComplete || !detector || !key || detector.Manager != this ||
                detector.expectedKey != key || detector.IsUnlocked ||
                openedLocks.Contains(detector) ||
                claimedKeys.Contains(key)) return false;

            // Claim before hiding or destroying either object: physics can send multiple contacts this frame.
            openedLocks.Add(detector);
            claimedKeys.Add(key);
            detector.MarkUnlocked();
            key.SetActive(false);
            detector.gameObject.SetActive(false);
            Destroy(key);
            Destroy(detector.gameObject);

            if (openedLocks.Count >= totalLocksRequired)
            {
                IsComplete = true;
                if (door1) Destroy(door1);
                if (door2) Destroy(door2);
                Completed?.Invoke();
            }
            return true;
        }
    }
}
