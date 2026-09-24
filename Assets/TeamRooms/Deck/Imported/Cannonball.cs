using UnityEngine;

namespace Team15.Deck
{
    public class Cannonball : MonoBehaviour
    {
        [SerializeField] private float destroyDelay = 0.1f;
        private KeyReveal keyReveal;
        private bool hasHitTarget;
        public void Initialize(KeyReveal reveal) => keyReveal = reveal;
        private void Start() => Destroy(gameObject, 15f);

        private void OnCollisionEnter(Collision collision)
        {
            if (hasHitTarget || collision.gameObject.scene != gameObject.scene ||
                !collision.gameObject.CompareTag("MovingTarget")) return;
            hasHitTarget = true;
            collision.gameObject.SetActive(false);
            if (!keyReveal)
                foreach (GameObject sceneRoot in gameObject.scene.GetRootGameObjects())
                {
                    keyReveal = sceneRoot.GetComponentInChildren<KeyReveal>(true);
                    if (keyReveal) break;
                }
            if (keyReveal) keyReveal.RevealKey();
            else Debug.LogError("Deck cannon target has no assigned key reward.", this);
            Destroy(gameObject, destroyDelay);
        }
    }
}
