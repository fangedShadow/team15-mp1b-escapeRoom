using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MirrorReachZone : MonoBehaviour
{
    public Transform mirrorPlane;
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable realKeyGrab;

    private bool enteredFromFront = false;
    private bool keyTaken = false;

    private void Start()
    {
        if (realKeyGrab != null)
        {
            realKeyGrab.enabled = false;
            realKeyGrab.selectEntered.AddListener(OnKeyGrabbed);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Mirror zone entered by: " + other.name);

        if (!other.CompareTag("PlayerHand"))
            return;

        Vector3 toHand = other.transform.position - mirrorPlane.position;
        float side = Vector3.Dot(mirrorPlane.forward, toHand);

        if (side > 0f)
        {
            enteredFromFront = true;
            Debug.Log("Hand entered from FRONT");
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("PlayerHand"))
            return;

        if (!enteredFromFront || keyTaken)
            return;

        Vector3 toHand = other.transform.position - mirrorPlane.position;
        float side = Vector3.Dot(mirrorPlane.forward, toHand);

        if (side < 0f)
        {
            Debug.Log("Hand crossed through mirror");

            if (realKeyGrab != null)
                realKeyGrab.enabled = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("PlayerHand"))
            return;

        enteredFromFront = false;

        // Player reached through but did NOT take the key.
        if (!keyTaken && realKeyGrab != null)
        {
            realKeyGrab.enabled = false;
            Debug.Log("Hand left mirror without key");
        }
    }

    private void OnKeyGrabbed(SelectEnterEventArgs args)
    {
        keyTaken = true;
        Debug.Log("Key successfully taken from mirror");
    }
}