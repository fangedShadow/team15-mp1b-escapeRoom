using UnityEngine;

namespace Team15.Deck
{
    public class KeyRevealBox : MonoBehaviour
    {
        public GameObject key;
        public bool IsRevealed { get; private set; }
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.scene == gameObject.scene && collision.gameObject.CompareTag("Ground"))
                RevealKeyAndRemoveBox();
        }
        public void RevealKeyAndRemoveBox()
        {
            if (IsRevealed) return;
            IsRevealed = true;
            if (key) key.SetActive(true);
            Destroy(gameObject);
        }
    }
}
