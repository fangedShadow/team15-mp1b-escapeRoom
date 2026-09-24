using UnityEngine;

namespace Team15.Deck
{
    public class FallingBox : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.scene != gameObject.scene || !collision.gameObject.CompareTag("Ground")) return;
            if (TryGetComponent(out KeyRevealBox reveal)) reveal.RevealKeyAndRemoveBox();
            else Destroy(gameObject);
        }
    }
}
