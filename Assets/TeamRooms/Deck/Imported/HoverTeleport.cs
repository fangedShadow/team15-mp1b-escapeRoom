using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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

        struct CarriedPose
        {
            public Transform transform;
            public Rigidbody body;
            public XRGrabInteractable grab;
            public Vector3 position;
            public Quaternion rotation;
        }

        sealed class ThrowSuppression
        {
            public bool originalThrow;
            public float restoreAt;
        }

        // A second teleport extends the same interval instead of remembering the temporary false value.
        static readonly Dictionary<XRGrabInteractable, ThrowSuppression> throwSuppressions =
            new Dictionary<XRGrabInteractable, ThrowSuppression>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetThrowSuppressions()
        {
            foreach (var pair in throwSuppressions)
                if (pair.Key) pair.Key.throwOnDetach = pair.Value.originalThrow;
            throwSuppressions.Clear();
        }

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
            if (Team15.TeamSession.GameplayBlocked) return;
            if (!teleportDestination || !xrOrigin) return;
            Vector3 beforePosition = xrOrigin.position;
            Quaternion beforeRotation = xrOrigin.rotation;
            var carried = CaptureCarriedObjects();
            CharacterController body = xrOrigin.GetComponent<CharacterController>();
            bool wasEnabled = body && body.enabled;
            if (wasEnabled) body.enabled = false;
            try
            {
                XROrigin origin = xrOrigin.GetComponent<XROrigin>();
                if (origin && origin.Camera)
                {
                    origin.RotateAroundCameraUsingOriginUp(teleportDestination.eulerAngles.y - origin.Camera.transform.eulerAngles.y);
                    Vector3 offset = Vector3.ProjectOnPlane(origin.Camera.transform.position - xrOrigin.position, Vector3.up);
                    xrOrigin.position = teleportDestination.position - offset;
                }
                else xrOrigin.SetPositionAndRotation(teleportDestination.position, teleportDestination.rotation);

                Quaternion rotationDelta = xrOrigin.rotation * Quaternion.Inverse(beforeRotation);
                foreach (var item in carried)
                {
                    if (!item.transform) continue;
                    Vector3 position = xrOrigin.position + rotationDelta * (item.position - beforePosition);
                    Quaternion rotation = rotationDelta * item.rotation;
                    item.transform.SetPositionAndRotation(position, rotation);
                    // Keep XRI's smoothing/ease-in cache in the same space as its moved attachment.
                    if (item.grab && item.grab.isSelected)
                        item.grab.SetTargetPose(new Pose(position, rotation));
                    if (!item.body) continue;
                    item.body.position = position;
                    item.body.rotation = rotation;
                    if (!item.body.isKinematic)
                    {
                        item.body.linearVelocity = Vector3.zero;
                        item.body.angularVelocity = Vector3.zero;
                    }
                }
                Physics.SyncTransforms();
            }
            finally
            {
                if (body) body.enabled = wasEnabled;
            }
        }

        List<CarriedPose> CaptureCarriedObjects()
        {
            var objects = new HashSet<GameObject>();
            var desktop = xrOrigin.GetComponent<BlackTide.MP1B.CabinDesktop>();
            if (desktop && desktop.HeldObject) objects.Add(desktop.HeldObject);
            foreach (var hand in xrOrigin.GetComponentsInChildren<XRBaseInteractor>(true))
                foreach (var selected in hand.interactablesSelected)
                    if (selected is XRGrabInteractable grab) objects.Add(grab.gameObject);

            var carried = new List<CarriedPose>(objects.Count);
            foreach (var item in objects)
            {
                if (!item) continue;
                var grab = item.GetComponent<XRGrabInteractable>();
                carried.Add(new CarriedPose
                {
                    transform = item.transform,
                    body = item.GetComponent<Rigidbody>(),
                    grab = grab,
                    position = item.transform.position,
                    rotation = item.transform.rotation
                });
                if (grab && grab.isSelected) SuppressTeleportThrow(grab);
            }
            return carried;
        }

        void SuppressTeleportThrow(XRGrabInteractable grab)
        {
            float restoreAt = Time.time + Mathf.Max(.05f, grab.throwSmoothingDuration + Time.fixedDeltaTime * 2f);
            if (throwSuppressions.TryGetValue(grab, out var pending))
            {
                pending.restoreAt = Mathf.Max(pending.restoreAt, restoreAt);
                return;
            }
            // Desktop grabbing already disables throwing and owns its restoration on release.
            if (!grab.throwOnDetach) return;
            pending = new ThrowSuppression { originalThrow = grab.throwOnDetach, restoreAt = restoreAt };
            throwSuppressions.Add(grab, pending);
            grab.throwOnDetach = false;
            // The shared player survives room changes, unlike the rope/sign that initiated this teleport.
            var player = xrOrigin.GetComponent<BlackTide.PiratePlayer>();
            MonoBehaviour runner = player ? player : this;
            runner.StartCoroutine(RestoreThrow(grab, pending));
        }

        static IEnumerator RestoreThrow(XRGrabInteractable grab, ThrowSuppression pending)
        {
            while (grab && Time.time < pending.restoreAt) yield return null;
            if (grab) grab.throwOnDetach = pending.originalThrow;
            throwSuppressions.Remove(grab);
        }
    }
}
