using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team15.Storage
{
    public class MirrorReachZone : MonoBehaviour
    {
        public Transform mirrorPlane;
        public XRGrabInteractable realKeyGrab, handMirrorGrab;
        private readonly HashSet<Collider> enteredFromFront = new HashSet<Collider>();
        private readonly HashSet<Collider> crossedHands = new HashSet<Collider>();
        private bool keyTaken;

        void Start()
        {
            if (realKeyGrab == null) return;
            realKeyGrab.enabled = false;
            realKeyGrab.selectEntered.AddListener(OnKeyGrabbed);
        }

        bool IsHand(Collider other) => other != null && other.CompareTag("PlayerHand");
        float Side(Collider other) => Vector3.Dot(mirrorPlane.forward, other.transform.position - mirrorPlane.position);

        void OnTriggerEnter(Collider other)
        {
            if (!IsHand(other) || mirrorPlane == null || keyTaken) return;
            if (Side(other) > 0f) enteredFromFront.Add(other);
        }

        void OnTriggerStay(Collider other)
        {
            if (!IsHand(other) || mirrorPlane == null || keyTaken) return;
            if (enteredFromFront.Contains(other) && Side(other) < 0f)
            {
                crossedHands.Add(other);
                // A second hand reaching through must not release the hand holding the mirror.
                // A resting mirror is disabled temporarily so desktop reach selects the key.
                if (handMirrorGrab != null && !handMirrorGrab.isSelected) handMirrorGrab.enabled = false;
                if (realKeyGrab != null) realKeyGrab.enabled = true;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsHand(other)) return;
            enteredFromFront.Remove(other);
            crossedHands.Remove(other);
            if (!keyTaken && crossedHands.Count == 0)
            {
                if (realKeyGrab != null) realKeyGrab.enabled = false;
                if (handMirrorGrab != null) handMirrorGrab.enabled = true;
            }
        }

        void OnKeyGrabbed(SelectEnterEventArgs args)
        {
            keyTaken = true;
            enteredFromFront.Clear();
            crossedHands.Clear();
            if (handMirrorGrab != null) handMirrorGrab.enabled = true;
        }

        void OnDisable()
        {
            enteredFromFront.Clear();
            crossedHands.Clear();
            if (!keyTaken && realKeyGrab != null) realKeyGrab.enabled = false;
            if (handMirrorGrab != null) handMirrorGrab.enabled = true;
        }

        void OnDestroy()
        {
            if (realKeyGrab != null) realKeyGrab.selectEntered.RemoveListener(OnKeyGrabbed);
        }
    }
}
