using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlackTide.MP1B
{
    /// <summary>A single physical object identity, shared by desktop and XR interaction.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class CabinItem : MonoBehaviour
    {
        public string itemId;
        public string ownerId = "captain";
        public string displayName = "Object";
        public bool consumable;
        public XRGrabInteractable grab;
        public bool IsHeld => desktopHolder || (grab && grab.isSelected);
        public bool IsDocked { get; private set; }
        public Rigidbody Body { get { EnsureInitialized(); return body; } }
        public static IReadOnlyList<CabinItem> All => Scan();

        static readonly HashSet<CabinItem> registry = new HashSet<CabinItem>();
        Rigidbody body;
        Transform homeParent;
        Scene homeScene;
        Vector3 homePosition;
        Quaternion homeRotation;
        bool initialized;
        CabinDesktop desktopHolder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearRegistry() => registry.Clear();

        static List<CabinItem> Scan()
        {
            registry.RemoveWhere(item => !item);
            // Awake has not run on initially hidden rewards. Include inactive loaded objects.
            foreach (var item in Object.FindObjectsByType<CabinItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.gameObject.scene.IsValid() && item.gameObject.scene.isLoaded) registry.Add(item);
            return new List<CabinItem>(registry);
        }

        public static CabinItem Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var item in Scan()) if (item.itemId == id) return item;
            return null;
        }

        void Awake() => EnsureInitialized();
        void OnEnable() { EnsureInitialized(); registry.Add(this); }
        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            body = GetComponent<Rigidbody>();
            if (!grab) grab = GetComponent<XRGrabInteractable>();
            homeParent = transform.parent;
            homeScene = gameObject.scene;
            homePosition = transform.position;
            homeRotation = transform.rotation;
            if (grab)
            {
                // A carried item must never reparent itself into a cached, inactive room.
                grab.retainTransformParent = false;
                grab.throwOnDetach = false;
                grab.selectExited.AddListener(OnXRReleased);
            }
            registry.Add(this);
        }
        void OnDestroy()
        {
            registry.Remove(this);
            if (grab) grab.selectExited.RemoveListener(OnXRReleased);
        }
        void OnXRReleased(SelectExitEventArgs _) { if (isActiveAndEnabled) StartCoroutine(FinishXRRelease()); }
        IEnumerator FinishXRRelease()
        {
            yield return null;
            if (!IsHeld && !IsDocked) CabinTransition.ReturnReleasedItem(this);
        }

        public void SetAvailable(bool value)
        {
            EnsureInitialized();
            if (!value) ReleaseHeld();
            gameObject.SetActive(value);
            if (value && grab) grab.enabled = !IsDocked;
        }

        internal void SetDesktopHolder(CabinDesktop holder)
        {
            EnsureInitialized();
            desktopHolder = holder;
            if (grab) grab.enabled = !holder && !IsDocked;
            if (holder)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }
            else if (!IsDocked)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                CabinTransition.ReturnReleasedItem(this);
            }
        }

        public void ReleaseHeld()
        {
            EnsureInitialized();
            if (desktopHolder) desktopHolder.ReleaseItem(this);
            if (grab && grab.isSelected && grab.interactionManager)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
        }

        public void Dock(Transform point)
        {
            EnsureInitialized();
            ReleaseHeld();
            IsDocked = true;
            if (grab) grab.enabled = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
            if (point)
            {
                transform.SetParent(point, true);
                transform.SetPositionAndRotation(point.position, point.rotation);
            }
        }

        public void Restore(Vector3 position, Quaternion rotation)
        {
            EnsureInitialized();
            ReleaseHeld();
            IsDocked = false;
            transform.SetParent(null, true);
            if (homeScene.IsValid() && homeScene.isLoaded && gameObject.scene != homeScene)
                SceneManager.MoveGameObjectToScene(gameObject, homeScene);
            if (homeParent) transform.SetParent(homeParent, true);
            transform.SetPositionAndRotation(position, rotation);
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            if (grab) grab.enabled = true;
        }

        public void RestoreHome()
        {
            EnsureInitialized();
            Restore(homePosition, homeRotation);
            SetAvailable(true);
        }
    }
}
