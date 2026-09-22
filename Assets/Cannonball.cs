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

            // Make target disappear
            collision.gameObject.SetActive(false);

            // Find KeyReveal, including inactive objects
            KeyReveal keyReveal = FindFirstObjectByType<KeyReveal>(
                FindObjectsInactive.Include
            );

            if (keyReveal != null)
            {
                Debug.Log("FOUND KEY REVEAL!");
                keyReveal.RevealKey();
            }
            else
            {
                Debug.LogError("COULD NOT FIND KEY REVEAL!");
            }

            // Destroy cannonball
            Destroy(gameObject, destroyDelay);
        }
    }
}
