using UnityEngine;

public class LockManager : MonoBehaviour
{
    [Header("Door Setup")]
    [SerializeField] private GameObject door1;
    [SerializeField] private GameObject door2;

    [Header("Lock Count")]
    [SerializeField] private int totalLocksRequired = 3;
    private int locksOpenedCount = 0;

    public void RegisterLockOpened(GameObject lockObject)
    {
        // Destroy the lock object that was unlocked
        Destroy(lockObject);

        locksOpenedCount++;

        // Check if all locks have been cleared
        if (locksOpenedCount >= totalLocksRequired)
        {
            OpenDoors();
        }
    }

    private void OpenDoors()
    {
        if (door1 != null) Destroy(door1);
        if (door2 != null) Destroy(door2);
    }
}
