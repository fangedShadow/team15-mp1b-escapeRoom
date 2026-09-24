using UnityEngine;

namespace Team15.Deck
{
    public class Sword : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            // Let the rope own release; destroying it here used to race its box-release callback.
            RopeController rope = other.GetComponentInParent<RopeController>();
            if (rope) rope.Cut();
        }
    }
}
