using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class HoverTeleport : MonoBehaviour
{
    [Header("Teleport")]
    public Transform teleportDestination;
    public Transform xrOrigin;

    [Header("Glow")]
    public Renderer objectRenderer;
    public Color glowColor = Color.cyan;
    public float glowIntensity = 3f;

    private Material material;
    private Color originalEmission;

    private XRBaseInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();

        if (objectRenderer == null)
            objectRenderer = GetComponent<Renderer>();

        material = objectRenderer.material;

        if (material.HasProperty("_EmissionColor"))
        {
            originalEmission = material.GetColor("_EmissionColor");
        }
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.activated.AddListener(OnActivated);
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.activated.RemoveListener(OnActivated);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");

            material.SetColor(
                "_EmissionColor",
                glowColor * glowIntensity
            );
        }
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor(
                "_EmissionColor",
                originalEmission
            );
        }
    }

    private void OnActivated(ActivateEventArgs args)
    {
        if (teleportDestination == null || xrOrigin == null)
        {
            Debug.LogWarning(
                "HoverTeleport: Assign Teleport Destination and XR Origin."
            );
            return;
        }

        xrOrigin.position = teleportDestination.position;
        xrOrigin.rotation = teleportDestination.rotation;
    }
}
