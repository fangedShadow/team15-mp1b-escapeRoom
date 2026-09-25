using UnityEngine;


public class MirrorLock : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable handMirror;
    public GameObject mirrorReachZone;
    public LanternLock requiredLantern;
    public ShadowBarrierReveal requiredReveal;

    public bool IsUnlocked => unlocked;

    private bool unlocked = false;

    private void Start()
    {
        if (mirrorReachZone != null)
            mirrorReachZone.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryUnlock(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryUnlock(other);
    }

    private void TryUnlock(Collider other)
    {
        if (unlocked)
            return;

        if (requiredLantern != null && !requiredLantern.IsLit)
            return;
        if (requiredReveal != null && !requiredReveal.IsRevealed)
            return;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabbedObject =
            other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (handMirror != null && grabbedObject == handMirror)
        {
            unlocked = true;

            if (mirrorReachZone != null)
                mirrorReachZone.SetActive(true);

            Debug.Log("Mirror Lock unlocked");
        }
    }
}
