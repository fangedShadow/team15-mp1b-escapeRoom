using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team15.Storage
{
    public class MatchKey : MonoBehaviour
    {
        public bool isLit = false;
        public GameObject matchFlame;
        public Light matchLight;
        public BoxCollider storageBox;

        XRGrabInteractable grab;
        Collider[] matchColliders;
        BoxCollider cachedBox;
        MatchStrikeZone[] strikeZones;
        bool readyAfterRemoval;

        void OnEnable()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (!grab) return;
            grab.selectEntered.AddListener(OnPickedUp);
            grab.selectExited.AddListener(OnReleased);
        }

        void OnPickedUp(SelectEnterEventArgs _) => readyAfterRemoval = false;
        void OnReleased(SelectExitEventArgs _) => readyAfterRemoval = false;

        public bool CanStrike => !isLit && IsHeld && readyAfterRemoval && !TouchesBox();
        bool IsHeld
        {
            get
            {
                if (!grab) grab = GetComponent<XRGrabInteractable>();
                return grab && grab.isSelected;
            }
        }

        void Start()
        {
            isLit = false;
            if (matchFlame) matchFlame.SetActive(false);
            if (matchLight) matchLight.enabled = false;
        }

        void FixedUpdate()
        {
            // Picking up or jostling a stored match is not a strike. Each hold
            // starts with removing it completely, then returning to a striker.
            if (!IsHeld || TouchesBox())
            {
                readyAfterRemoval = false;
                return;
            }
            if (readyAfterRemoval) return;
            if (cachedBox != storageBox || strikeZones == null)
            {
                cachedBox = storageBox;
                strikeZones = storageBox
                    ? storageBox.GetComponentsInChildren<MatchStrikeZone>()
                    : new MatchStrikeZone[0];
            }
            foreach (var zone in strikeZones)
                if (zone && Touches(zone.GetComponent<Collider>())) return;
            readyAfterRemoval = true;
        }

        bool TouchesBox() => storageBox && Touches(storageBox);

        bool Touches(Collider target)
        {
            if (!target || !target.enabled) return false;
            if (matchColliders == null) matchColliders = GetComponentsInChildren<Collider>();
            foreach (var part in matchColliders)
                if (part && part.enabled && !part.isTrigger && part.gameObject.activeInHierarchy &&
                    Physics.ComputePenetration(part, part.transform.position, part.transform.rotation,
                        target, target.transform.position, target.transform.rotation, out _, out _)) return true;
            return false;
        }

        void OnDisable()
        {
            readyAfterRemoval = false;
            if (!grab) return;
            grab.selectEntered.RemoveListener(OnPickedUp);
            grab.selectExited.RemoveListener(OnReleased);
        }

        public void LightMatch()
        {
            isLit = true;
            if (matchFlame) matchFlame.SetActive(true);
            if (matchLight) matchLight.enabled = true;
        }
    }
}
