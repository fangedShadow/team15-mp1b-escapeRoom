using UnityEngine;


namespace Team15.Storage
{
public class ExitLockZone : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable exitKey;
    public MirrorKeyReveal keyReveal;
    public DoubleDoorOpen door;
    public AudioSource unlockAudio;
    public LanternLock requiredLantern;
    public MirrorLock requiredMirror;

    private bool unlocked = false;
    public bool IsUnlocked => unlocked;

    private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

    private void OnTriggerEnter(Collider other)
    {
        if (unlocked)
            return;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabbedObject =
            other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (exitKey != null && grabbedObject != null && grabbedObject == exitKey &&
            requiredLantern != null && requiredLantern.IsLit &&
            requiredMirror != null && requiredMirror.IsUnlocked &&
            keyReveal != null &&
            keyReveal.hasBeenTaken)
        {
            unlocked = true;

            // The used key stays visible in the lock, without blocking the open doorway.
            while (exitKey.isSelected && exitKey.interactionManager != null)
                exitKey.interactionManager.SelectExit(exitKey.interactorsSelecting[0], exitKey);
            exitKey.enabled = false;
            foreach (Collider keyCollider in exitKey.GetComponentsInChildren<Collider>(true))
                keyCollider.enabled = false;
            if (exitKey.TryGetComponent<Rigidbody>(out var keyBody))
            {
                if (!keyBody.isKinematic)
                {
                    keyBody.linearVelocity = Vector3.zero;
                    keyBody.angularVelocity = Vector3.zero;
                }
                keyBody.useGravity = false;
                keyBody.isKinematic = true;
            }

            Debug.Log("Exit door unlocked");

            if (unlockAudio != null)
                unlockAudio.Play();

            if (door != null)
                door.OpenDoor();
        }
    }
}
}
