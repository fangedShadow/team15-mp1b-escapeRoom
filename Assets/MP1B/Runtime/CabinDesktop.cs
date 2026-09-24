using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlackTide.MP1B
{
    /// <summary>Keyboard/mouse interaction using the same physical items and locks as XR.</summary>
    public sealed class CabinDesktop : MonoBehaviour
    {
        public PiratePlayer player;
        public float interactionDistance = 3.2f;
        public bool enableHandReach;
        public CabinItem HeldItem { get; private set; }
        public XRGrabInteractable HeldXRItem { get; private set; }
        public GameObject HeldObject => HeldItem ? HeldItem.gameObject : HeldXRItem ? HeldXRItem.gameObject : null;
        float holdDistance = 1.25f;
        string hint;
        Quaternion holdRotation;
        bool heldThrowOnDetach;
        CabinDesktopInteractor desktopHand;
        CabinDesktopInteractor buttonHand;
        XRSimpleInteractable pressedButton;
        SphereCollider reachCollider;
        bool reachTagMissing;
        bool reaching;
        float reachDistance;
        float reachTargetDistance;
        sealed class ReleasedSettings
        {
            public XRGrabInteractable item;
            public bool throwOnDetach;
            public int frame;
        }
        readonly List<ReleasedSettings> releasedSettings = new List<ReleasedSettings>();
        GUIStyle hintStyle;
        GUIStyle controlStyle;

        void Awake() { if (!player) player = GetComponent<PiratePlayer>(); }
        void OnDisable()
        {
            if (HeldItem) ReleaseItem(HeldItem);
            ReleaseXRItem();
            RestoreReleasedSettings(true);
            if (reachCollider) reachCollider.enabled = false;
            reaching = false;
            if (desktopHand) desktopHand.gameObject.SetActive(false);
            if (buttonHand) buttonHand.gameObject.SetActive(false);
        }

        void Update()
        {
            RestoreReleasedSettings(false);
            // PiratePlayer only calls Tick in desktop mode. Clear the temporary hand if a headset connects.
            if (player && player.IsXR && desktopHand && desktopHand.gameObject.activeSelf)
            {
                if (HeldItem) ReleaseItem(HeldItem);
                ReleaseXRItem();
                if (reachCollider) reachCollider.enabled = false;
                reaching = false;
                desktopHand.gameObject.SetActive(false);
                if (buttonHand) buttonHand.gameObject.SetActive(false);
            }
        }

        CabinDesktopInteractor ButtonHand
        {
            get
            {
                if (!buttonHand)
                {
                    var root = new GameObject("Desktop button pointer");
                    root.transform.SetParent(player.transform, false);
                    buttonHand = root.AddComponent<CabinDesktopInteractor>();
                    buttonHand.attachTransform = root.transform;
                }
                if (!buttonHand.gameObject.activeSelf) buttonHand.gameObject.SetActive(true);
                return buttonHand;
            }
        }

        CabinDesktopInteractor Hand
        {
            get
            {
                if (!desktopHand)
                {
                    var root = new GameObject("Desktop interaction hand");
                    root.transform.SetParent(player.transform, false);
                    desktopHand = root.AddComponent<CabinDesktopInteractor>();
                    desktopHand.attachTransform = root.transform;
                }
                if (!desktopHand.gameObject.activeSelf) desktopHand.gameObject.SetActive(true);
                return desktopHand;
            }
        }

        // Called by PiratePlayer immediately after its mouse-look code. No duplicate Update input.
        public void Tick()
        {
            if (Team15.TeamSession.GameplayBlocked) return;
            if (!player || player.IsXR || !player.view || CabinTransition.IsTravelling) return;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            bool pressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                (keyboard != null && keyboard.eKey.wasPressedThisFrame);
            bool interactionHeld = (mouse != null && mouse.leftButton.isPressed) ||
                (keyboard != null && keyboard.eKey.isPressed);
            if (pressedButton && !interactionHeld)
            {
                ButtonHand.EndSelection();
                pressedButton = null;
            }
            if (HeldObject)
            {
                float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
                holdDistance = Mathf.Clamp(holdDistance + scroll * .0015f, .55f, 2.5f);
                if (HeldXRItem && keyboard != null)
                {
                    float yaw = (keyboard.xKey.isPressed ? 1f : 0f) - (keyboard.zKey.isPressed ? 1f : 0f);
                    float pitch = (keyboard.bKey.isPressed ? 1f : 0f) - (keyboard.vKey.isPressed ? 1f : 0f);
                    float roll = (keyboard.nKey.isPressed ? 1f : 0f) - (keyboard.cKey.isPressed ? 1f : 0f);
                    holdRotation *= Quaternion.Euler(new Vector3(pitch, yaw, roll) * (80f * Time.deltaTime));
                    if (keyboard.spaceKey.wasPressedThisFrame) ActivateHeldItem();
                    if (!keyboard.spaceKey.isPressed) Hand.Deactivate();
                }
                UpdateHeldPose();
            }
            bool hitSomething = Cast(out var hit);
            CabinButton button = hitSomething ? hit.collider.GetComponentInParent<CabinButton>() : null;
            CabinCollectible collectible = hitSomething ? hit.collider.GetComponentInParent<CabinCollectible>() : null;
            CabinItem item = hitSomething ? hit.collider.GetComponentInParent<CabinItem>() : null;
            CabinSocket socket = hitSomething ? hit.collider.GetComponentInParent<CabinSocket>() : null;
            XRBaseInteractable xrTarget = hitSomething ? hit.collider.GetComponentInParent<XRBaseInteractable>() : null;
            var xrGrab = xrTarget as XRGrabInteractable;
            var xrButton = xrTarget as XRSimpleInteractable;
            UpdateHandReach(keyboard, mouse, hitSomething, hit);
            if (reaching)
            {
                // The physical hand can reach an enabled key behind a mirror surface without ray selection
                // passing through the mirror. Its original trigger and grab callbacks still control access.
                xrGrab = FindReachableItem();
                xrTarget = xrGrab;
                xrButton = null;
                button = null;
                collectible = null;
                item = null;
            }
            // Captain objects keep their existing desktop handlers and must not receive duplicate XRI events.
            Hand.SetAim(button || collectible || item ? null : xrTarget);
            if (!HeldXRItem && !reaching)
                Hand.transform.SetPositionAndRotation(hitSomething ? hit.point : player.view.transform.position,
                    player.view.transform.rotation);
            hint = button ? "E / click  ·  " + button.prompt
                : collectible ? "E / click  ·  Collect ruby"
                : xrButton ? "E / click  ·  " + xrButton.name
                : HeldItem ? "E / click  ·  " + (socket ? "Place " + HeldItem.displayName : "Release " + HeldItem.displayName)
                : HeldXRItem ? "E / click  ·  Place " + HeldXRItem.name + "   ·   Space: use"
                : item && !item.IsDocked && !item.IsHeld ? "E / click  ·  Grab " + item.displayName
                : xrGrab && xrGrab.isActiveAndEnabled && !xrGrab.isSelected ? "E / click  ·  Grab " + xrGrab.name
                : reaching ? "Hold H + scroll to reach   ·   E / click to grab at your hand" : string.Empty;
            if (HeldObject && keyboard != null && keyboard.gKey.wasPressedThisFrame)
            {
                if (HeldItem) DropAtAim(hitSomething, hit);
                else DropXRAtAim(hitSomething, hit);
                return;
            }
            if (!pressed) return;
            // Buttons remain usable while carrying an item, including the scene-transition door.
            if (button) { button.Press(); return; }
            if (collectible) { collectible.Collect(); return; }
            if (xrButton && xrButton.isActiveAndEnabled)
            {
                var pointer = ButtonHand;
                pointer.transform.SetPositionAndRotation(hit.point, player.view.transform.rotation);
                if (pointer.BeginSelection(xrButton))
                {
                    pressedButton = xrButton;
                    pointer.Activate(xrButton);
                }
                return;
            }
            if (HeldItem) { DropAtAim(hitSomething, hit); return; }
            if (HeldXRItem) { DropXRAtAim(hitSomething, hit); return; }
            if (item && !item.IsHeld && !item.IsDocked) GrabItem(item);
            else if (xrGrab) TryGrabXRItem(xrGrab);
        }

        void UpdateHandReach(Keyboard keyboard, Mouse mouse, bool hasHit, RaycastHit hit)
        {
            bool requested = enableHandReach && !HeldObject && keyboard != null && keyboard.hKey.isPressed;
            if (!requested)
            {
                if (reachCollider) reachCollider.enabled = false;
                reaching = false;
                return;
            }
            if (!reachCollider && !reachTagMissing)
            {
                // The combined project defines PlayerHand. Standalone cabins need no extra tag or collider.
                try { Hand.gameObject.tag = "PlayerHand"; }
                catch (UnityException) { reachTagMissing = true; return; }
                var body = Hand.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                reachCollider = Hand.gameObject.AddComponent<SphereCollider>();
                reachCollider.radius = .08f;
                reachCollider.isTrigger = true;
            }
            if (!reachCollider) return;
            if (!reaching)
                reachDistance = reachTargetDistance = Mathf.Clamp(hasHit ? hit.distance - .12f : holdDistance, .2f, interactionDistance);
            reaching = true;
            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            reachTargetDistance = Mathf.Clamp(reachTargetDistance + scroll * .0015f, .2f, interactionDistance);
            // Move through thin trigger zones over several physics frames instead of jumping across them.
            reachDistance = Mathf.MoveTowards(reachDistance, reachTargetDistance, .8f * Time.deltaTime);
            var view = player.view.transform;
            Hand.transform.SetPositionAndRotation(view.position + view.forward * reachDistance, view.rotation);
            reachCollider.enabled = true;
        }

        XRGrabInteractable FindReachableItem()
        {
            XRGrabInteractable nearest = null;
            float distance = float.PositiveInfinity;
            var center = Hand.transform.position;
            foreach (var collider in Physics.OverlapSphere(center, .13f, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                if (collider.transform.IsChildOf(player.transform)) continue;
                var grab = collider.GetComponentInParent<XRGrabInteractable>();
                if (!grab || !grab.isActiveAndEnabled || grab.isSelected || grab.GetComponent<CabinItem>()) continue;
                float candidate = (collider.ClosestPoint(center) - center).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate;
                nearest = grab;
            }
            return nearest;
        }

        bool Cast(out RaycastHit hit)
        {
            var ray = new Ray(player.view.transform.position, player.view.transform.forward);
            var hits = Physics.RaycastAll(ray, interactionDistance, ~(1 << 2), QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var candidate in hits)
            {
                if (candidate.collider.transform.IsChildOf(player.transform)) continue;
                if (HeldObject && candidate.collider.transform.IsChildOf(HeldObject.transform)) continue;
                // Unrelated trigger volumes must not obscure a reachable interaction.
                if (candidate.collider.isTrigger && !candidate.collider.GetComponentInParent<CabinSocket>() &&
                    !candidate.collider.GetComponentInParent<CabinButton>() &&
                    !candidate.collider.GetComponentInParent<CabinCollectible>() &&
                    !candidate.collider.GetComponentInParent<XRBaseInteractable>()) continue;
                hit = candidate;
                return true;
            }
            hit = default;
            return false;
        }

        void GrabItem(CabinItem item)
        {
            HeldItem = item;
            holdRotation = Quaternion.Inverse(player.view.transform.rotation) * item.transform.rotation;
            item.transform.SetParent(null, true);
            item.SetDesktopHolder(this);
            SetPlayerCollision(item.gameObject, true);
            UpdateHeldPose();
        }

        public bool TryGrabXRItem(XRGrabInteractable item)
        {
            if (!item || !player || player.IsXR || HeldObject || item.GetComponent<CabinItem>()) return false;
            // A quick re-grab must remember the original setting, not the temporary release setting.
            for (int i = releasedSettings.Count - 1; i >= 0; i--)
                if (releasedSettings[i].item == item)
                {
                    item.throwOnDetach = releasedSettings[i].throwOnDetach;
                    releasedSettings.RemoveAt(i);
                }
            var hand = Hand;
            var attach = item.GetAttachTransform(hand);
            hand.transform.SetPositionAndRotation(attach.position, attach.rotation);
            holdRotation = Quaternion.Inverse(player.view.transform.rotation) * attach.rotation;
            heldThrowOnDetach = item.throwOnDetach;
            item.throwOnDetach = false;
            item.retainTransformParent = false;
            HeldXRItem = item;
            item.selectExited.AddListener(OnXRReleased);
            SetPlayerCollision(item.gameObject, true);
            if (!hand.BeginSelection(item))
            {
                FinishXRRelease(item);
                return false;
            }
            if (item && HeldXRItem == item) UpdateHeldPose();
            return HeldXRItem == item;
        }

        public void ActivateHeldItem()
        {
            if (HeldXRItem && !player.IsXR) Hand.Activate(HeldXRItem);
        }

        void UpdateHeldPose()
        {
            if (!HeldObject) return;
            var view = player.view.transform;
            Vector3 desired = view.position + view.forward * holdDistance + view.right * .22f - view.up * .2f;
            Vector3 direction = desired - view.position;
            var hits = Physics.RaycastAll(view.position, direction.normalized, direction.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore);
            float safeDistance = direction.magnitude;
            foreach (var candidate in hits)
            {
                if (candidate.collider.transform.IsChildOf(player.transform) ||
                    candidate.collider.transform.IsChildOf(HeldObject.transform)) continue;
                safeDistance = Mathf.Min(safeDistance, Mathf.Max(.2f, candidate.distance - .13f));
            }
            Vector3 position = view.position + direction.normalized * safeDistance;
            Quaternion rotation = view.rotation * holdRotation;
            if (HeldItem) HeldItem.transform.SetPositionAndRotation(position, rotation);
            else Hand.transform.SetPositionAndRotation(position, rotation);
        }

        void DropAtAim(bool hasHit, RaycastHit hit)
        {
            var item = HeldItem;
            var socket = hasHit ? hit.collider.GetComponentInParent<CabinSocket>() : null;
            if (hasHit)
            {
                Vector3 point = hit.point + hit.normal * .12f;
                if (socket && item.itemId == socket.requiredId && !socket.IsUnlocked)
                    point = socket.snapPoint ? socket.snapPoint.position : socket.transform.position;
                item.transform.position = point;
            }
            ReleaseItem(item);
            Physics.SyncTransforms();
            if (socket) socket.TryAccept(item);
        }

        void DropXRAtAim(bool hasHit, RaycastHit hit)
        {
            var item = HeldXRItem;
            bool canMove = item && item.trackPosition;
            ReleaseXRItem();
            if (hasHit && item && canMove)
            {
                item.transform.position = hit.point + hit.normal * .12f;
                var body = item.GetComponent<Rigidbody>();
                if (body) body.position = item.transform.position;
                Physics.SyncTransforms();
            }
        }

        public void ReleaseXRItem()
        {
            var item = HeldXRItem;
            if (desktopHand) desktopHand.EndSelection();
            if (item && HeldXRItem == item) FinishXRRelease(item);
        }

        void OnXRReleased(SelectExitEventArgs args)
        {
            if (ReferenceEquals(args.interactorObject, desktopHand) && HeldXRItem)
                FinishXRRelease(HeldXRItem);
        }

        void FinishXRRelease(XRGrabInteractable item)
        {
            item.selectExited.RemoveListener(OnXRReleased);
            // XRGrab applies detach velocity in LateUpdate, after selectExited has fired.
            // Keep throwing disabled until that step finishes, then restore the object's VR setting.
            if (isActiveAndEnabled)
                releasedSettings.Add(new ReleasedSettings { item = item, throwOnDetach = heldThrowOnDetach, frame = Time.frameCount });
            else item.throwOnDetach = heldThrowOnDetach;
            SetPlayerCollision(item.gameObject, false);
            HeldXRItem = null;
            if (CabinTravelRuntime.Current) CabinTravelRuntime.Current.ReturnReleased(item.gameObject);
        }

        void RestoreReleasedSettings(bool immediately)
        {
            for (int i = releasedSettings.Count - 1; i >= 0; i--)
            {
                var settings = releasedSettings[i];
                if (!immediately && settings.frame == Time.frameCount) continue;
                if (settings.item) settings.item.throwOnDetach = settings.throwOnDetach;
                releasedSettings.RemoveAt(i);
            }
        }

        public void ReleaseItem(CabinItem item)
        {
            if (!item || HeldItem != item) return;
            HeldItem = null;
            SetPlayerCollision(item.gameObject, false);
            item.SetDesktopHolder(null);
        }

        void SetPlayerCollision(GameObject item, bool ignore)
        {
            if (!player || !player.body) return;
            foreach (var collider in item.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(collider, player.body, ignore);
        }

        void OnGUI()
        {
            if (Team15.TeamSession.GameplayBlocked) return;
            if (!player || player.IsXR || CabinTransition.IsTravelling) return;
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                hintStyle.normal.textColor = new Color(1f, .87f, .58f);
                controlStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                controlStyle.normal.textColor = Color.white;
            }
            float width = Mathf.Min(Screen.width - 24f, 1040f);
            float x = (Screen.width - width) * .5f;
            var room = CabinRoom.Instance;
            if (room && room.isActiveAndEnabled && !string.IsNullOrEmpty(room.CurrentMessage))
                GUI.Box(new Rect(x, 18f, width, 72f), room.CurrentMessage, hintStyle);
            if (!string.IsNullOrEmpty(hint)) GUI.Box(new Rect(x, Screen.height - 96f, width, 38f), hint, hintStyle);
            GUI.Box(new Rect(x, Screen.height - 48f, width, 31f),
                HeldXRItem ? "WASD move · Right mouse look · G drop · Scroll reach · Space use · Z/X, V/B, C/N rotate"
                : HeldItem ? "WASD move   ·   Hold right mouse to look   ·   G drop   ·   Scroll adjust reach"
                : enableHandReach ? "WASD move · Right mouse look · E/click interact · Hold H + scroll to reach through mirrors"
                : "WASD move   ·   Hold right mouse to look   ·   Aim at objects, then E or click", controlStyle);
        }
    }
}
