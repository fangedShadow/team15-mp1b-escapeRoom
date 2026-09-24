using UnityEngine;


namespace Team15.Storage
{
public class MirrorLock : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable handMirror;
    public GameObject mirrorReachZone;

    private bool unlocked = false;
    public bool IsUnlocked => unlocked;

    private void Start()
    {
        if (mirrorReachZone != null)
            mirrorReachZone.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (unlocked)
            return;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabbedObject =
            other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (handMirror != null && grabbedObject != null && grabbedObject == handMirror)
        {
            unlocked = true;

            if (mirrorReachZone != null)
                mirrorReachZone.SetActive(true);

            Debug.Log("Mirror Lock unlocked");
        }
    }
}
}
