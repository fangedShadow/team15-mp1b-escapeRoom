using UnityEngine;

public class Cannonball : MonoBehaviour
{
    [SerializeField] private float destroyDelay = 0.1f;

    private bool hasHitTarget;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHitTarget)
            return;

        if (collision.gameObject.CompareTag("MovingTarget"))
        {
            hasHitTarget = true;

            KeyReveal keyReveal = FindFirstObjectByType<KeyReveal>();

            if (keyReveal != null)
            {
                keyReveal.RevealKey();
            }

            Destroy(gameObject, destroyDelay);
        }
    }
}

