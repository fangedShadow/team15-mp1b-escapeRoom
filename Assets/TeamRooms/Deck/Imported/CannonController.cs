using UnityEngine;

namespace Team15.Deck
{
    public class CannonController : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private GameObject cannonballPrefab;
        [SerializeField] private float launchForce = 15f;
        [SerializeField] private float cooldown = 1f;
        [SerializeField] private KeyReveal keyReveal;
        private float lastFireTime = -Mathf.Infinity;

        public void Fire()
        {
            if (Time.time < lastFireTime + cooldown) return;
            if (!muzzle || !cannonballPrefab)
            {
                Debug.LogWarning("Cannon is missing a muzzle or cannonball prefab.", this);
                return;
            }
            if (!keyReveal)
                foreach (GameObject sceneRoot in gameObject.scene.GetRootGameObjects())
                {
                    keyReveal = sceneRoot.GetComponentInChildren<KeyReveal>(true);
                    if (keyReveal) break;
                }
            lastFireTime = Time.time;
            GameObject ball = Instantiate(cannonballPrefab, muzzle.position, muzzle.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ball, gameObject.scene);
            if (ball.TryGetComponent(out Cannonball projectile)) projectile.Initialize(keyReveal);
            if (ball.TryGetComponent(out Rigidbody body))
            {
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.AddForce(muzzle.forward * launchForce, ForceMode.Impulse);
            }
            else Debug.LogWarning("Cannonball prefab needs a Rigidbody.", ball);
        }
    }
}
