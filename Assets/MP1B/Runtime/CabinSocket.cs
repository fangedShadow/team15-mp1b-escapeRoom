using System;
using UnityEngine;

namespace BlackTide.MP1B
{
    /// <summary>Independent physical lock; it accepts only its released matching key.</summary>
    public sealed class CabinSocket : MonoBehaviour
    {
        public string requiredId;
        public Transform snapPoint;
        public bool retainItem = true;
        public bool IsUnlocked { get; private set; }
        public event Action<CabinItem> Accepted;
        Collider trigger;

        void Awake() { trigger = GetComponent<Collider>(); trigger.isTrigger = true; }
        void OnTriggerStay(Collider other)
        {
            var item = other.GetComponentInParent<CabinItem>();
            if (item) TryAccept(item);
        }

        public bool TryAccept(CabinItem item)
        {
            if (IsUnlocked || !item || !item.gameObject.activeInHierarchy || item.IsHeld || item.IsDocked ||
                string.IsNullOrEmpty(requiredId) || item.itemId != requiredId) return false;
            if (!trigger) trigger = GetComponent<Collider>();
            bool overlaps = false;
            foreach (var shape in item.GetComponentsInChildren<Collider>())
                if (shape.enabled && trigger.bounds.Intersects(shape.bounds)) { overlaps = true; break; }
            if (!overlaps) return false;
            IsUnlocked = true;
            if (retainItem) item.Dock(snapPoint ? snapPoint : transform);
            else
            {
                // The exit remembers unlocking while the physical gold key remains reusable.
                item.Body.linearVelocity = Vector3.zero;
                item.Body.angularVelocity = Vector3.zero;
            }
            Accepted?.Invoke(item);
            return true;
        }

        public void ResetSocket() => IsUnlocked = false;
    }
}
