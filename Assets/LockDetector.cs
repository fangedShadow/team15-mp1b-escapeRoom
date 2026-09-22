using UnityEngine;

public class LockDetector : MonoBehaviour
{
    [SerializeField] private LockManager lockManager;
    [SerializeField] private string keyTag = "Key";
    private bool isUnlocked = false;

    private void OnTriggerEnter(Collider other)
    {
        // Prevent trigger firing multiple times
        if (isUnlocked) return;

        // Check if the object touching the lock has the "Key" tag
        if (other.CompareTag(keyTag))
        {
            isUnlocked = true;

            // Destroy the key that touched the lock
            Destroy(other.gameObject);

            // Notify the manager to destroy the lock and increment the opened count
            if (lockManager != null)
            {
                lockManager.RegisterLockOpened(gameObject);
            }
        }
    }
}