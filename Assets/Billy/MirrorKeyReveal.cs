using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MirrorKeyReveal : MonoBehaviour
{
    public GameObject mirrorKeyVisual;
    public Renderer realKeyRenderer;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;
    private Rigidbody rb;

    private void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        if (realKeyRenderer != null)
            realKeyRenderer.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        grab.selectEntered.AddListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        Debug.Log("RealKey grabbed from mirror");

        if (realKeyRenderer != null)
            realKeyRenderer.enabled = true;

        if (mirrorKeyVisual != null)
            mirrorKeyVisual.SetActive(false);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }
}