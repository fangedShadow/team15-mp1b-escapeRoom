using UnityEngine;


public class ExitLockZone : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable exitKey;
    public MirrorKeyReveal keyReveal;
    public DoubleDoorOpen door;
    public AudioSource unlockAudio;
    public MirrorLock requiredMirrorLock;
    public LanternLock requiredLantern;

    private bool unlocked = false;

    private void OnTriggerEnter(Collider other)
    {
        if (unlocked)
            return;

        if (requiredMirrorLock != null && !requiredMirrorLock.IsUnlocked)
            return;
        if (requiredLantern != null && !requiredLantern.IsLit)
            return;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabbedObject =
            other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (exitKey != null && grabbedObject == exitKey &&
            keyReveal != null &&
            keyReveal.hasBeenTaken)
        {
            unlocked = true;

            Debug.Log("Exit door unlocked");

            if (unlockAudio != null)
                unlockAudio.Play();

            if (door != null)
                door.OpenDoor();
        }
    }
}
