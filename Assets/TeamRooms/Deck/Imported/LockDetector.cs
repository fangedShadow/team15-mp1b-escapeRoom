using UnityEngine;

namespace Team15.Deck
{
    public class LockDetector : MonoBehaviour
    {
        [SerializeField] private LockManager lockManager;
        public GameObject expectedKey;
        public LockManager Manager => lockManager;
        public bool IsUnlocked { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            if (IsUnlocked || !expectedKey || !lockManager || !other) return;
            if (other.transform == expectedKey.transform || other.transform.IsChildOf(expectedKey.transform))
                lockManager.TryUnlock(this, expectedKey);
        }

        internal void MarkUnlocked() => IsUnlocked = true;
    }
}
