using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class KeyLockDoor : MonoBehaviour
{
    public Transform lockTransform, leftDoor, rightDoor;
    public float keyRotation = 90f, leftDoorRotation = 270f, rightDoorRotation = 90f;
    public float openScaleX = 3f, openDuration = 2.5f;
    public bool destroyKeyOnUnlock = true;
    public ActionHandler puzzleHandler;
    public int levelAfterUnlock = 2;
    public int lockIndex = -1;
    public string requiredKeyId;
    public bool IsUnlocked { get; private set; }

    int Index => lockIndex >= 0 ? lockIndex : levelAfterUnlock - 2;

    void OnTriggerEnter(Collider other)
    {
        KeyItem key = other.GetComponentInParent<KeyItem>();
        if (key == null && other.attachedRigidbody != null)
            key = other.attachedRigidbody.GetComponentInChildren<KeyItem>(true);
        if (key != null)
            Unlock(key.gameObject);
    }

    void OnTriggerStay(Collider other) => OnTriggerEnter(other);

    public void Unlock(GameObject keyObject)
    {
        if (IsUnlocked || keyObject == null || puzzleHandler == null)
            return;
        KeyItem key = keyObject.GetComponentInParent<KeyItem>();
        if (key == null)
            key = keyObject.GetComponentInChildren<KeyItem>(true);
        string expected = string.IsNullOrEmpty(requiredKeyId)
            ? ActionHandler.KeyForLock(Index) : requiredKeyId;
        if (key == null || key.keyId != expected || !puzzleHandler.CanUnlockLock(Index, key) || !key.TryClaim())
            return;

        IsUnlocked = true;
        GameObject pickup = key.PickupObject;
        XRGrabInteractable grab = pickup.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            while (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.SelectExit(grab.interactorsSelecting[0], grab);
            grab.enabled = false;
        }
        foreach (Collider collider in pickup.GetComponentsInChildren<Collider>())
            collider.enabled = false;
        puzzleHandler.RegisterLockOpened(Index);
        StartCoroutine(OpenSequence(pickup));
    }

    System.Collections.IEnumerator OpenSequence(GameObject key)
    {
        Transform keyTransform = key.transform;
        Rigidbody keyBody = key.GetComponent<Rigidbody>();
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
        Vector3 leftScale = leftDoor != null ? leftDoor.localScale : Vector3.one;
        Vector3 rightScale = rightDoor != null ? rightDoor.localScale : Vector3.one;
        float duration = Mathf.Max(0.01f, openDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (keyTransform != null)
                keyTransform.rotation = Quaternion.Slerp(keyStart, keyTarget, eased);
            if (lockTransform != null)
                lockTransform.localRotation = Quaternion.Slerp(lockStart, lockStart * Quaternion.Euler(0f, keyRotation, 0f), eased);
            SetDoorState(leftDoor, leftStart, leftScale, leftDoorRotation, eased);
            SetDoorState(rightDoor, rightStart, rightScale, rightDoorRotation, eased);
            yield return null;
        }
        if (destroyKeyOnUnlock && key != null)
            Destroy(key);
        else if (keyBody != null)
        {
            keyBody.useGravity = hadGravity;
            keyBody.isKinematic = wasKinematic;
        }
    }

    void SetDoorState(Transform door, Quaternion rotation, Vector3 scale, float angle, float progress)
    {
        if (door == null)
            return;
        Vector3 targetScale = scale;
        targetScale.x *= openScaleX;
        door.localRotation = Quaternion.Slerp(rotation, rotation * Quaternion.Euler(0f, angle, 0f), progress);
        door.localScale = Vector3.Lerp(scale, targetScale, progress);
    }
}
