using UnityEngine;


public class MirrorLock : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable handMirror;
    public GameObject mirrorReachZone;

    private bool unlocked = false;

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

        if (grabbedObject == handMirror)
        {
            unlocked = true;

            if (mirrorReachZone != null)
                mirrorReachZone.SetActive(true);

            Debug.Log("Mirror Lock unlocked");
        }
    }
}