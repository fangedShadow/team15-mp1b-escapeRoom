using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;
using InputDevice = UnityEngine.InputSystem.InputDevice;

namespace BlackTide
{
    public sealed class PiratePlayer : MonoBehaviour
    {
        public XROrigin origin;
        public Camera view;
        public CharacterController body;
        public Transform leftHand;
        public Transform rightHand;
        public TrackedPoseDriver headTracking;
        public InputActionAsset inputs;
        public Transform interiorAnchor;
        public Transform exteriorAnchor;
        public float moveSpeed = 2.5f;
        public float snapAngle = 30f;
        public bool IsOutside { get; private set; }
        public bool IsXR { get; private set; }
        public bool IsSimulatedXR { get; private set; }
        public Transform AimTransform => IsXR && rightHand.gameObject.activeSelf ? rightHand : view.transform;
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        readonly List<InputDevice> simulatedDevices = new List<InputDevice>();
        ReadOnlyArray<InputDevice>? previousInputDevices;
        bool simulationInputFilter;
        PirateInteractable desktopHover;
        float refreshAt;
        float pitch;
        bool turnLatched;
        bool modeSet;
        bool diagnosticRequested;

        void OnEnable()
        {
            // Start only runs once; the cached rig can be enabled again during scene travel.
            if (inputs) inputs.Enable();
        }

        void Start()
        {
            inputs.Enable();
            RefreshMode();
            Invoke(nameof(PrintXRInputReport), 3f);
        }
        void RefreshMode()
        {
            displays.Clear();
            SubsystemManager.GetSubsystems(displays);
            bool simulated = false;
            simulatedDevices.Clear();
            foreach (var device in InputSystem.devices)
            {
                if (device.layout != "XRSimulatedHMD" && device.layout != "XRSimulatedController") continue;
                simulatedDevices.Add(device);
                if (device.layout == "XRSimulatedHMD") simulated = true;
            }
            // The simulator creates Input System devices, but no running XR display.
            bool xr = simulated || displays.Exists(d => d.running);
            UpdateSimulationInputFilter(simulated);
            if (modeSet && xr == IsXR && simulated == IsSimulatedXR) return;
            modeSet = true;
            IsXR = xr;
            IsSimulatedXR = simulated;
            headTracking.enabled = xr;
            leftHand.gameObject.SetActive(xr);
            rightHand.gameObject.SetActive(xr);
            if (simulated)
            {
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
                // Simulated HMD poses start at local zero, without a native floor origin.
                origin.CameraFloorOffsetObject.transform.localPosition = Vector3.up * origin.CameraYOffset;
            }
            else if (!xr)
            {
                origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
                view.transform.localPosition = new Vector3(0f, 1.65f, 0f);
                view.transform.localRotation = Quaternion.identity;
                pitch = 0f;
            }
            else origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        void UpdateSimulationInputFilter(bool simulated)
        {
            if (!simulated) { RestoreInputDevices(); return; }
            if (!simulationInputFilter)
            {
                var previous = inputs.devices;
                // The setter reuses its backing storage, so preserve a copy of this view.
                previousInputDevices = previous.HasValue
                    ? new ReadOnlyArray<InputDevice>(previous.Value.ToArray())
                    : (ReadOnlyArray<InputDevice>?)null;
                simulationInputFilter = true;
            }
            // Filter only this game's actions. The simulator keeps its own keyboard actions.
            // This prevents T/R/Q and WASD from also firing desktop shortcuts or movement.
            var current = inputs.devices;
            bool sameDevices = current.HasValue && current.Value.Count == simulatedDevices.Count;
            for (int i = 0; sameDevices && i < simulatedDevices.Count; i++)
                sameDevices = current.Value[i] == simulatedDevices[i];
            if (!sameDevices) inputs.devices = simulatedDevices.ToArray();
        }
        void RestoreInputDevices()
        {
            if (!simulationInputFilter) return;
            if (inputs) inputs.devices = previousInputDevices;
            previousInputDevices = null;
            simulationInputFilter = false;
        }
        void OnDisable()
        {
            RestoreInputDevices();
            modeSet = false;
        }
        void Update()
        {
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .5f; RefreshMode(); }
            if (diagnosticRequested || (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame))
            { diagnosticRequested = false; PrintXRInputReport(); }
            if (BlackTide.MP1B.CabinTransition.IsTravelling) return;
            Move();
            if (IsXR) SnapTurn();
            else DesktopLookAndInteract();
        }

        [ContextMenu("Log VR controller input on next game frame")]
        public void RequestXRInputReport() => diagnosticRequested = true;

        void PrintXRInputReport()
        {
            var text = new StringBuilder("[Captain VR input] ");
            text.AppendLine($"version=meta-input-1 XR={IsXR} simulated={IsSimulatedXR} appFocused={Application.isFocused} playerEnabled={isActiveAndEnabled} travelling={BlackTide.MP1B.CabinTransition.IsTravelling}");
            text.AppendLine($"background={InputSystem.settings.backgroundBehavior}");
            foreach (var device in InputSystem.devices)
            {
                if (!(device is UnityEngine.InputSystem.XR.XRController)) continue;
                text.Append($"{device.layout} usages={string.Join(",", device.usages)} enabled={device.enabled}");
                foreach (var name in new[] { "thumbstick", "primary2DAxis", "triggerPressed", "gripPressed" })
                {
                    var control = device.TryGetChildControl(name);
                    if (control != null) text.Append($" {name}={control.ReadValueAsObject()}");
                }
                text.AppendLine();
            }
            if (inputs)
                foreach (var name in new[] { "Gameplay/Move", "Gameplay/Turn", "Left/Select", "Right/Select", "Left/Grip", "Right/Grip" })
                {
                    var action = inputs.FindAction(name);
                    text.AppendLine(action == null ? name + " MISSING" : $"{name} enabled={action.enabled} controls={action.controls.Count} value={action.ReadValueAsObject() ?? "neutral"}");
                }
            Debug.Log(text.ToString(), this);
        }
        void Move()
        {
            Vector2 input = inputs.FindAction("Gameplay/Move", true).ReadValue<Vector2>();
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = (forward * input.y + right * input.x) * moveSpeed;
            body.height = Mathf.Clamp(view.transform.localPosition.y + origin.CameraFloorOffsetObject.transform.localPosition.y, 1f, 2.2f);
            Vector3 eye = transform.InverseTransformPoint(view.transform.position);
            body.center = new Vector3(eye.x, body.height * .5f + .02f, eye.z);
            move.y = -2f;
            body.Move(move * Time.deltaTime);
            if (transform.position.y < -5f) ReturnToCabin();
        }
        void SnapTurn()
        {
            float x = inputs.FindAction("Gameplay/Turn", true).ReadValue<Vector2>().x;
            if (Mathf.Abs(x) < .3f) turnLatched = false;
            if (!turnLatched && Mathf.Abs(x) > .7f)
            {
                origin.RotateAroundCameraUsingOriginUp(Mathf.Sign(x) * snapAngle);
                turnLatched = true;
            }
        }
        void DesktopLookAndInteract()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * .12f;
                transform.Rotate(0f, delta.x, 0f);
                pitch = Mathf.Clamp(pitch - delta.y, -75f, 75f);
                view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            var cabinDesktop = GetComponent<BlackTide.MP1B.CabinDesktop>();
            if (cabinDesktop) { cabinDesktop.Tick(); return; }
            PirateInteractable target = null;
            if (Physics.Raycast(view.transform.position, view.transform.forward, out RaycastHit hit, 9f,
                    ~(1 << 2), QueryTriggerInteraction.Ignore))
                target = hit.collider.GetComponentInParent<PirateInteractable>();
            if (target != desktopHover)
            {
                if (desktopHover) desktopHover.Highlight(false);
                desktopHover = target;
                if (desktopHover) desktopHover.Highlight(true);
            }
            if (inputs.FindAction("Gameplay/DesktopInteract", true).WasPressedThisFrame() && desktopHover)
                desktopHover.Activate();
        }
        public void ToggleLookout()
        {
            IsOutside = !IsOutside;
            MoveTo(IsOutside ? exteriorAnchor : interiorAnchor);
        }
        public void ReturnToCabin() { IsOutside = false; MoveTo(interiorAnchor); }
        void MoveTo(Transform anchor)
        {
            body.enabled = false;
            // Rotate about the tracked head, then place its horizontal projection at the anchor.
            // Moving only the rig to the anchor would incorrectly retain the user's room-scale offset.
            float yaw = anchor.eulerAngles.y - view.transform.eulerAngles.y;
            origin.RotateAroundCameraUsingOriginUp(yaw);
            Vector3 horizontalOffset = Vector3.ProjectOnPlane(view.transform.position - transform.position, Vector3.up);
            transform.position = anchor.position - horizontalOffset;
            body.enabled = true;
        }
        void OnGUI()
        {
            if (IsXR) return;
            GUI.color = desktopHover ? new Color(1f, .8f, .25f) : Color.white;
            GUI.Label(new Rect(Screen.width * .5f - 4f, Screen.height * .5f - 9f, 20f, 20f), "+");
            GUI.color = Color.white;
        }
    }
}
