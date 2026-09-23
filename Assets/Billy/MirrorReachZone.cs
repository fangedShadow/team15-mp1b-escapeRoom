using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MirrorReachZone : MonoBehaviour
{
    public Transform mirrorPlane;
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable realKeyGrab;
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable handMirrorGrab;

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
            if (handMirrorGrab != null)
                handMirrorGrab.enabled = false;

            if (realKeyGrab != null)
                realKeyGrab.enabled = true;

            Debug.Log("Hand crossed through mirror");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("PlayerHand"))
            return;

        enteredFromFront = false;

        if (!keyTaken)
        {
            if (realKeyGrab != null)
                realKeyGrab.enabled = false;

            if (handMirrorGrab != null)
                handMirrorGrab.enabled = true;

            Debug.Log("Hand left mirror without key");
        }
    }

    private void OnKeyGrabbed(SelectEnterEventArgs args)
    {
        keyTaken = true;

        if (handMirrorGrab != null)
            handMirrorGrab.enabled = true;

        Debug.Log("Key successfully taken from mirror");
    }
}