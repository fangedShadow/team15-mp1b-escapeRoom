using UnityEngine;

namespace Team15.Deck
{
    public class KeyReveal : MonoBehaviour
    {
        [SerializeField] private GameObject keyObject;
        public bool IsRevealed { get; private set; }
        private void Awake()
        {
            if (!keyObject) keyObject = gameObject;
            if (!IsRevealed) keyObject.SetActive(false);
        }
        public void RevealKey()
        {
            if (IsRevealed) return;
            if (!keyObject) keyObject = gameObject;
            IsRevealed = true;
            keyObject.SetActive(true);
        }
    }
}
