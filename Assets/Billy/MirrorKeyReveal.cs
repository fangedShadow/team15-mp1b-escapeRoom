using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MirrorKeyReveal : MonoBehaviour
{
    public GameObject mirrorKeyVisual;
    public Renderer realKeyRenderer;
    public Transform keyAnchor;

    public bool hasBeenTaken = false;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;
    private Rigidbody rb;

    private void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        // Real key starts invisible
        if (realKeyRenderer != null)
            realKeyRenderer.enabled = false;

        // Real key starts frozen and follows the mirror anchor
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }
    }

    private void FixedUpdate()
    {
        // Before the key is taken, keep it aligned with the mirror
        if (!hasBeenTaken && keyAnchor != null && rb != null)
        {
            rb.MovePosition(keyAnchor.position);
            rb.MoveRotation(keyAnchor.rotation);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        hasBeenTaken = true;

        Debug.Log("RealKey grabbed from mirror");

        // Show the real key
        if (realKeyRenderer != null)
            realKeyRenderer.enabled = true;

        // Hide the fake mirror-only key
        if (mirrorKeyVisual != null)
            mirrorKeyVisual.SetActive(false);

        // Do NOT change gravity here
        // XRI is controlling the Rigidbody while it is being held
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Debug.Log("RealKey released - gravity enabled");
    }

    private void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
    }
}
