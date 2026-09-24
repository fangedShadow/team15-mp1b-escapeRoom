using UnityEngine;

namespace Team15.Storage
{
public class KeyLockDoor : MonoBehaviour
{
    public Transform lockTransform;
    public Transform leftDoor;
    public Transform rightDoor;
    public float keyRotation = 90f;
    public float leftDoorRotation = 270f;
    public float rightDoorRotation = 90f;
    public float openScaleX = 3f;
    public float openDuration = 2.5f;
    public bool destroyKeyOnUnlock = true;
    public ActionHandler puzzleHandler;
    public int levelAfterUnlock = 2;

    private bool unlocked;

    void OnTriggerEnter(Collider other)
    {
        KeyItem key = other.GetComponentInParent<KeyItem>();
        if (key != null && key.TryClaim())
            Unlock(key.gameObject);
    }

    public void Unlock(GameObject key)
    {
        if (unlocked)
            return;

        unlocked = true;
        if (puzzleHandler != null)
        {
            puzzleHandler.SetPuzzleLevel(levelAfterUnlock);
            if (levelAfterUnlock >= 4)
                puzzleHandler.WinGame();
        }
        StartCoroutine(OpenSequence(key));
    }

    System.Collections.IEnumerator OpenSequence(GameObject key)
    {
        Transform keyTransform = key.transform;
        Rigidbody keyBody = key.GetComponentInParent<Rigidbody>();
        if (keyBody == null)
            keyBody = key.GetComponentInChildren<Rigidbody>();
        bool hadGravity = keyBody != null && keyBody.useGravity;
        bool wasKinematic = keyBody != null && keyBody.isKinematic;

        if (keyBody != null)
        {
            keyBody.useGravity = false;
            keyBody.isKinematic = true;
        }

        Quaternion keyStart = keyTransform.rotation;
        Quaternion keyTarget = keyStart * Quaternion.Euler(0f, keyRotation, 0f);
        Quaternion lockStart = lockTransform != null ? lockTransform.localRotation : Quaternion.identity;
        Quaternion leftStart = leftDoor != null ? leftDoor.localRotation : Quaternion.identity;
        Quaternion rightStart = rightDoor != null ? rightDoor.localRotation : Quaternion.identity;
        Vector3 leftStartScale = leftDoor != null ? leftDoor.localScale : Vector3.one;
        Vector3 rightStartScale = rightDoor != null ? rightDoor.localScale : Vector3.one;
        Vector3 leftTargetScale = leftStartScale;
        Vector3 rightTargetScale = rightStartScale;

        if (leftDoor != null)
            leftTargetScale.x *= openScaleX;
        if (rightDoor != null)
            rightTargetScale.x *= openScaleX;

        float elapsed = 0f;
        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / openDuration);
            float eased = progress * progress * (3f - 2f * progress);

            keyTransform.rotation = Quaternion.Slerp(keyStart, keyTarget, eased);
            if (lockTransform != null)
                lockTransform.localRotation = Quaternion.Slerp(lockStart, lockStart * Quaternion.Euler(0f, keyRotation, 0f), eased);
            SetDoorState(leftDoor, leftStart, leftStartScale, leftTargetScale, leftDoorRotation, eased);
            SetDoorState(rightDoor, rightStart, rightStartScale, rightTargetScale, rightDoorRotation, eased);
            yield return null;
        }

        if (destroyKeyOnUnlock)
            Destroy(key);
        else if (keyBody != null)
        {
            keyBody.useGravity = hadGravity;
            keyBody.isKinematic = wasKinematic;
        }
    }

    void SetDoorState(Transform door, Quaternion startRotation, Vector3 startScale, Vector3 targetScale, float rotation, float progress)
    {
        if (door == null)
            return;

        door.localRotation = Quaternion.Slerp(startRotation, startRotation * Quaternion.Euler(0f, rotation, 0f), progress);
        door.localScale = Vector3.Lerp(startScale, targetScale, progress);
    }
}

}
