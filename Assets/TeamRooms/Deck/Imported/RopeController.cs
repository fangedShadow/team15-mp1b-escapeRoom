using UnityEngine;

namespace Team15.Deck
{
    public class RopeController : MonoBehaviour
    {
        public GameObject box;
        public bool IsCut { get; private set; }
        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Sword>()) Cut();
        }
        public bool Cut()
        {
            if (IsCut || !box || !box.TryGetComponent(out Rigidbody body)) return false;
            IsCut = true;
            if (box.TryGetComponent(out HangingBreeze breeze)) breeze.enabled = false;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = true;
            foreach (Collider collider in GetComponents<Collider>()) collider.enabled = false;
            Destroy(gameObject);
            return true;
        }
    }
}
