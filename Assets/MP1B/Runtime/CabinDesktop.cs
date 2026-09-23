using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackTide.MP1B
{
    /// <summary>Keyboard/mouse interaction using the same physical items and locks as XR.</summary>
    public sealed class CabinDesktop : MonoBehaviour
    {
        public PiratePlayer player;
        public float interactionDistance = 3.2f;
        public CabinItem HeldItem { get; private set; }
        float holdDistance = 1.25f;
        string hint;
        Quaternion holdRotation;
        GUIStyle hintStyle;
        GUIStyle controlStyle;

        void Awake() { if (!player) player = GetComponent<PiratePlayer>(); }
        void OnDisable() { if (HeldItem) ReleaseItem(HeldItem); }

        // Called by PiratePlayer immediately after its mouse-look code. No duplicate Update input.
        public void Tick()
        {
            if (!player || player.IsXR || !player.view || CabinTransition.IsTravelling) return;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (HeldItem)
            {
                float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
                holdDistance = Mathf.Clamp(holdDistance + scroll * .0015f, .55f, 2.5f);
                UpdateHeldPose();
            }
            bool hitSomething = Cast(out var hit);
            CabinButton button = hitSomething ? hit.collider.GetComponentInParent<CabinButton>() : null;
            CabinCollectible collectible = hitSomething ? hit.collider.GetComponentInParent<CabinCollectible>() : null;
            CabinItem item = hitSomething ? hit.collider.GetComponentInParent<CabinItem>() : null;
            CabinSocket socket = hitSomething ? hit.collider.GetComponentInParent<CabinSocket>() : null;
            hint = button ? "E / click  ·  " + button.prompt
                : collectible ? "E / click  ·  Collect ruby"
                : HeldItem ? "E / click  ·  " + (socket ? "Place " + HeldItem.displayName : "Release " + HeldItem.displayName)
                : item && !item.IsDocked && !item.IsHeld ? "E / click  ·  Grab " + item.displayName : string.Empty;
            bool pressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                (keyboard != null && keyboard.eKey.wasPressedThisFrame);
            if (HeldItem && keyboard != null && keyboard.gKey.wasPressedThisFrame)
            {
                DropAtAim(hitSomething, hit);
                return;
            }
            if (!pressed) return;
            // Buttons remain usable while carrying an item, including the scene-transition door.
            if (button) { button.Press(); return; }
            if (collectible) { collectible.Collect(); return; }
            if (HeldItem) { DropAtAim(hitSomething, hit); return; }
            if (item && !item.IsHeld && !item.IsDocked) GrabItem(item);
        }

        bool Cast(out RaycastHit hit)
        {
            var ray = new Ray(player.view.transform.position, player.view.transform.forward);
            var hits = Physics.RaycastAll(ray, interactionDistance, ~(1 << 2), QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var candidate in hits)
            {
                if (candidate.collider.transform.IsChildOf(player.transform)) continue;
                if (HeldItem && candidate.collider.GetComponentInParent<CabinItem>() == HeldItem) continue;
                // Unrelated trigger volumes must not obscure a reachable interaction.
                if (candidate.collider.isTrigger && !candidate.collider.GetComponentInParent<CabinSocket>() &&
                    !candidate.collider.GetComponentInParent<CabinButton>() &&
                    !candidate.collider.GetComponentInParent<CabinCollectible>()) continue;
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
            SetPlayerCollision(item, true);
            UpdateHeldPose();
        }

        void UpdateHeldPose()
        {
            var view = player.view.transform;
            Vector3 desired = view.position + view.forward * holdDistance + view.right * .22f - view.up * .2f;
            Vector3 direction = desired - view.position;
            var hits = Physics.RaycastAll(view.position, direction.normalized, direction.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore);
            float safeDistance = direction.magnitude;
            foreach (var candidate in hits)
            {
                if (candidate.collider.transform.IsChildOf(player.transform) ||
                    candidate.collider.GetComponentInParent<CabinItem>() == HeldItem) continue;
                safeDistance = Mathf.Min(safeDistance, Mathf.Max(.2f, candidate.distance - .13f));
            }
            HeldItem.transform.SetPositionAndRotation(view.position + direction.normalized * safeDistance,
                view.rotation * holdRotation);
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

        public void ReleaseItem(CabinItem item)
        {
            if (!item || HeldItem != item) return;
            HeldItem = null;
            SetPlayerCollision(item, false);
            item.SetDesktopHolder(null);
        }

        void SetPlayerCollision(CabinItem item, bool ignore)
        {
            if (!player || !player.body) return;
            foreach (var collider in item.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(collider, player.body, ignore);
        }

        void OnGUI()
        {
            if (!player || player.IsXR || CabinTransition.IsTravelling) return;
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                hintStyle.normal.textColor = new Color(1f, .87f, .58f);
                controlStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                controlStyle.normal.textColor = Color.white;
            }
            float width = Mathf.Min(Screen.width - 24f, 780f);
            float x = (Screen.width - width) * .5f;
            var room = CabinRoom.Instance;
            if (room && room.isActiveAndEnabled && !string.IsNullOrEmpty(room.CurrentMessage))
                GUI.Box(new Rect(x, 18f, width, 72f), room.CurrentMessage, hintStyle);
            if (!string.IsNullOrEmpty(hint)) GUI.Box(new Rect(x, Screen.height - 96f, width, 38f), hint, hintStyle);
            GUI.Box(new Rect(x, Screen.height - 48f, width, 31f),
                HeldItem ? "WASD move   ·   Hold right mouse to look   ·   G drop   ·   Scroll adjust reach"
                : "WASD move   ·   Hold right mouse to look   ·   Aim at objects, then E or click", controlStyle);
        }
    }
}
