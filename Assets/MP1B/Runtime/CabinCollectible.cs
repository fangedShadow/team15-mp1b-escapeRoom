using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlackTide.MP1B
{
    [DisallowMultipleComponent]
    public sealed class CabinCollectible : MonoBehaviour
    {
        public string collectibleId;
        public string prompt = "Collect ruby";
        public CabinRoom room;
        public GameObject visual;
        public AudioSource sound;
        public bool IsCollected { get; private set; }
        XRSimpleInteractable _xr;
        Collider[] _colliders;

        void Awake()
        {
            _xr = GetComponent<XRSimpleInteractable>();
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        void OnEnable() { if (_xr) _xr.selectEntered.AddListener(OnSelected); }
        void OnDisable() { if (_xr) _xr.selectEntered.RemoveListener(OnSelected); }
        void OnSelected(SelectEnterEventArgs _) => Collect();

        public void Collect()
        {
            if (IsCollected || !isActiveAndEnabled) return;
            IsCollected = true;
            if (sound) sound.Play();
            SetVisible(false);
            var targetRoom = room ? room : CabinRoom.Instance;
            if (targetRoom) targetRoom.ShowMessage("Ruby collected. Optional treasure — keep exploring!");
        }

        public void ResetCollectible()
        {
            IsCollected = false;
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            if (_colliders == null) _colliders = GetComponentsInChildren<Collider>(true);
            foreach (var collider in _colliders) if (collider) collider.enabled = visible;
            if (visual && visual != gameObject) visual.SetActive(visible);
            else foreach (var mesh in GetComponentsInChildren<Renderer>(true)) mesh.enabled = visible;
        }
    }
}
