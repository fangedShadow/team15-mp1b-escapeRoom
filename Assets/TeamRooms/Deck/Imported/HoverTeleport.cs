using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team15.Deck
{
    public class HoverTeleport : MonoBehaviour
    {
        public Transform teleportDestination;
        public Transform xrOrigin;
        public Renderer objectRenderer;
        public Color glowColor = Color.cyan;
        public float glowIntensity = 3f;
        private Material material;
        private Color originalEmission;
        private XRBaseInteractable interactable;

        private void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            if (!objectRenderer) objectRenderer = GetComponent<Renderer>();
            if (objectRenderer)
            {
                material = objectRenderer.material;
                if (material.HasProperty("_EmissionColor")) originalEmission = material.GetColor("_EmissionColor");
            }
        }
        private void OnEnable()
        {
            if (!interactable) return;
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            interactable.activated.AddListener(OnActivated);
        }
        private void OnDisable()
        {
            if (!interactable) return;
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.activated.RemoveListener(OnActivated);
            RestoreGlow();
        }
        private void OnDestroy()
        {
            if (material) Destroy(material);
        }
        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (!material || !material.HasProperty("_EmissionColor")) return;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", glowColor * glowIntensity);
        }
        private void OnHoverExited(HoverExitEventArgs args) => RestoreGlow();
        private void RestoreGlow()
        {
            if (material && material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", originalEmission);
        }
        private void OnActivated(ActivateEventArgs args) => Teleport();

        public void Teleport()
        {
            if (!teleportDestination || !xrOrigin) return;
            CharacterController body = xrOrigin.GetComponent<CharacterController>();
            bool wasEnabled = body && body.enabled;
            if (wasEnabled) body.enabled = false;
            XROrigin origin = xrOrigin.GetComponent<XROrigin>();
            if (origin && origin.Camera)
            {
                origin.RotateAroundCameraUsingOriginUp(teleportDestination.eulerAngles.y - origin.Camera.transform.eulerAngles.y);
                Vector3 offset = Vector3.ProjectOnPlane(origin.Camera.transform.position - xrOrigin.position, Vector3.up);
                xrOrigin.position = teleportDestination.position - offset;
            }
            else xrOrigin.SetPositionAndRotation(teleportDestination.position, teleportDestination.rotation);
            if (wasEnabled) body.enabled = true;
        }
    }
}
