using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team15.Deck
{
    public class Collectible : MonoBehaviour
    {
        public CollectibleManager manager;
        private XRGrabInteractable grabInteractable;
        public bool IsCollected { get; private set; }

        private void Awake() => grabInteractable = GetComponent<XRGrabInteractable>();
        private void OnEnable()
        {
            if (grabInteractable) grabInteractable.selectEntered.AddListener(OnGrabbed);
        }
        private void OnDisable()
        {
            if (grabInteractable) grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        }
        private void OnGrabbed(SelectEnterEventArgs args) => TryCollect();
        public bool TryCollect()
        {
            if (IsCollected || !manager || !manager.CollectItem(this)) return false;
            IsCollected = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}
