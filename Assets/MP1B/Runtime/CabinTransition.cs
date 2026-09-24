using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace BlackTide.MP1B
{
    /// <summary>Reusable door. Rooms are cached additively; held objects keep their identity.</summary>
    public sealed class CabinTransition : MonoBehaviour
    {
        public string targetScene = "TransitionCabin";
        public string targetSpawn = "FromCaptain";
        public bool requireCabinClear = true;
        // Null keeps standalone cabin behavior; a combined game can guard every legacy door.
        public static System.Func<CabinTransition, bool> TravelAllowed { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTravelGate() => TravelAllowed = null;

        public static bool IsTravelling => CabinTravelRuntime.Current && CabinTravelRuntime.Current.Travelling;

        public void Travel()
        {
            if (IsTravelling || (TravelAllowed != null && !TravelAllowed(this))) return;
            if (requireCabinClear && (!CabinRoom.Instance || !CabinRoom.Instance.IsCleared))
            {
                if (CabinRoom.Instance) CabinRoom.Instance.ShowMessage("The captain's gold key unlocks this door.");
                return;
            }
            var loaded = SceneManager.GetSceneByName(targetScene);
            if (string.IsNullOrWhiteSpace(targetScene) || (!loaded.isLoaded && !Application.CanStreamedLevelBeLoaded(targetScene)))
            {
                if (CabinRoom.Instance) CabinRoom.Instance.ShowMessage("Destination is not configured in Build Profiles.");
                Debug.LogError("MP1B door destination is not in the build scene list: " + targetScene, this);
                return;
            }
            var player = Object.FindFirstObjectByType<PiratePlayer>();
            if (!player) { Debug.LogError("MP1B scene travel requires a PiratePlayer rig.", this); return; }
            CabinTravelRuntime.Get(player).Begin(targetScene, targetSpawn);
        }

        internal static void ReturnReleasedItem(CabinItem item)
        {
            if (item && !item.IsHeld && !item.IsDocked && CabinTravelRuntime.Current)
                CabinTravelRuntime.Current.ReturnReleased(item.gameObject);
        }
    }

    // This runtime object survives room deactivation so the fade and load always complete.
    public sealed class CabinTravelRuntime : MonoBehaviour
    {
        public static CabinTravelRuntime Current { get; private set; }
        public bool Travelling { get; private set; }
        sealed class CachedRoom
        {
            public readonly Dictionary<GameObject, bool> roots = new Dictionary<GameObject, bool>();
        }
        readonly Dictionary<Scene, CachedRoom> rooms = new Dictionary<Scene, CachedRoom>();
        readonly List<GameObject> carried = new List<GameObject>();
        readonly HashSet<GameObject> exported = new HashSet<GameObject>();
        readonly HashSet<GameObject> sessionRoots = new HashSet<GameObject>();
        PiratePlayer player;
        XRInteractionManager interactionManager;
        Canvas fadeCanvas;
        Image fadeImage;
        bool shuttingDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearStatic() => Current = null;

        public static CabinTravelRuntime Get(PiratePlayer rig)
        {
            if (Current) return Current;
            var root = new GameObject("MP1B Persistent Session");
            DontDestroyOnLoad(root);
            Current = root.AddComponent<CabinTravelRuntime>();
            Current.player = rig;
            Current.PersistRig();
            Current.BuildFade();
            return Current;
        }

        void OnDestroy() { if (Current == this) Current = null; }
        void Persist(GameObject root)
        {
            root.transform.SetParent(null, true);
            if (root.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        }
        void PersistRig()
        {
            Scene source = player.gameObject.scene;
            Persist(player.gameObject);
            foreach (var manager in Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None))
                if (manager.gameObject.scene == source)
                {
                    if (!interactionManager) interactionManager = manager;
                    if (manager.gameObject != player.gameObject) sessionRoots.Add(manager.gameObject);
                    Persist(manager.gameObject);
                }
            foreach (var input in Object.FindObjectsByType<InputActionManager>(FindObjectsSortMode.None))
                if (input.gameObject.scene == source)
                {
                    if (input.gameObject != player.gameObject) sessionRoots.Add(input.gameObject);
                    Persist(input.gameObject);
                }
        }

        void BuildFade()
        {
            var panel = new GameObject("Room travel fade", typeof(RectTransform), typeof(Canvas));
            panel.transform.SetParent(player.view.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(.15f, player.view.nearClipPlane + .03f));
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(4f, 4f);
            fadeCanvas = panel.GetComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.WorldSpace;
            fadeCanvas.sortingOrder = 32767;
            var surface = new GameObject("Black", typeof(RectTransform), typeof(Image));
            surface.transform.SetParent(panel.transform, false);
            var surfaceRect = surface.GetComponent<RectTransform>();
            surfaceRect.anchorMin = Vector2.zero;
            surfaceRect.anchorMax = Vector2.one;
            surfaceRect.offsetMin = surfaceRect.offsetMax = Vector2.zero;
            fadeImage = surface.GetComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false;
            fadeCanvas.enabled = false;
        }

        public void Begin(string sceneName, string spawnId)
        {
            if (Travelling || shuttingDown) return;
            Travelling = true;
            StartCoroutine(TravelRoutine(sceneName, spawnId));
        }

        /// <summary>Ends this session before the caller destroys its player and loads a fresh first room.</summary>
        public void ShutdownForRestart()
        {
            if (shuttingDown) return;
            shuttingDown = true;
            StopAllCoroutines();
            Travelling = false;
            // Clear before release callbacks, so no old session can export objects into the new game.
            if (Current == this) Current = null;
            CabinTransition.TravelAllowed = null;
            var oldExports = new HashSet<GameObject>(exported);
            if (player)
            {
                var desktop = player.GetComponent<CabinDesktop>();
                if (desktop)
                {
                    if (desktop.HeldItem) desktop.ReleaseItem(desktop.HeldItem);
                    desktop.ReleaseXRItem();
                }
                foreach (var hand in player.GetComponentsInChildren<XRBaseInteractor>(true))
                    if (hand.interactionManager)
                        hand.interactionManager.CancelInteractorSelection((IXRSelectInteractor)hand);
            }
            foreach (var item in oldExports) if (item) Destroy(item);
            foreach (var root in sessionRoots) if (root && (!player || root != player.gameObject)) Destroy(root);
            if (fadeCanvas) Destroy(fadeCanvas.gameObject);
            carried.Clear();
            exported.Clear();
            sessionRoots.Clear();
            rooms.Clear();
            Destroy(gameObject);
        }

        /// <summary>Places the persistent player and anything currently held at a room's entrance.</summary>
        public void PlaceAt(Transform spawn)
        {
            if (!spawn || !player || Travelling || shuttingDown) return;
            CaptureHeldObjects();
            PlaceRig(spawn);
            player.interiorAnchor = spawn;
            Physics.SyncTransforms();
        }

        IEnumerator Fade(float from, float to)
        {
            fadeCanvas.enabled = true;
            const float duration = .22f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, elapsed / duration));
                yield return null;
            }
            fadeImage.color = new Color(0f, 0f, 0f, to);
            fadeCanvas.enabled = to > 0f;
        }

        void CaptureHeldObjects()
        {
            carried.Clear();
            var desktop = player.GetComponent<CabinDesktop>();
            if (desktop && desktop.HeldObject) carried.Add(desktop.HeldObject);
            // Both XR hands, including a teammate's ordinary XRGrabInteractable, retain selection.
            foreach (var grab in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
            {
                if (!grab.isSelected) continue;
                grab.retainTransformParent = false;
                if (!carried.Contains(grab.gameObject)) carried.Add(grab.gameObject);
            }
            foreach (var item in carried) { Persist(item); exported.Add(item); }
        }

        void Cache(Scene source)
        {
            var state = new CachedRoom();
            foreach (var root in source.GetRootGameObjects()) state.roots[root] = root.activeSelf;
            rooms[source] = state;
        }
        void SetRoomActive(Scene scene, bool active)
        {
            if (!rooms.TryGetValue(scene, out var state)) return;
            foreach (var pair in state.roots)
                if (pair.Key && pair.Key.scene == scene) pair.Key.SetActive(active && pair.Value);
        }

        IEnumerator TravelRoutine(string sceneName, string spawnId)
        {
            Scene source = SceneManager.GetActiveScene();
            yield return Fade(0f, 1f);
            CaptureHeldObjects();
            Cache(source);
            var target = SceneManager.GetSceneByName(sceneName);
            if (!target.isLoaded)
            {
                AsyncOperation loading = null;
                try { loading = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); }
                catch (System.Exception exception) { Debug.LogException(exception); }
                if (loading == null)
                {
                    foreach (var item in carried) ReturnReleased(item);
                    yield return Fade(1f, 0f);
                    Travelling = false;
                    yield break;
                }
                while (!loading.isDone) yield return null;
                target = SceneManager.GetSceneByName(sceneName);
            }
            if (!target.IsValid() || !target.isLoaded)
            {
                Debug.LogError("MP1B destination failed to load: " + sceneName);
                yield return Fade(1f, 0f);
                Travelling = false;
                yield break;
            }
            RemoveDuplicateRig(target);
            if (!rooms.ContainsKey(target)) Cache(target);
            SetRoomActive(source, false);
            SetRoomActive(target, true);
            SceneManager.SetActiveScene(target);
            var spawn = FindSpawn(target, spawnId);
            if (spawn)
            {
                PlaceRig(spawn.transform);
                player.interiorAnchor = spawn.transform;
            }
            else
            {
                Debug.LogError("MP1B destination has no CabinSpawn named '" + spawnId + "'. Returning to the previous room.");
                SetRoomActive(target, false);
                SetRoomActive(source, true);
                SceneManager.SetActiveScene(source);
            }
            if (player.inputs) player.inputs.Enable();
            Physics.SyncTransforms();
            // Give XR tracking and grab transformers a frame to settle before showing the room.
            yield return null;
            yield return Fade(1f, 0f);
            Travelling = false;
        }

        void RemoveDuplicateRig(Scene scene)
        {
            foreach (var other in Object.FindObjectsByType<PiratePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (other != player && other.gameObject.scene == scene) other.gameObject.SetActive(false);
            foreach (var manager in Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None))
                if (manager != interactionManager && manager.gameObject.scene == scene) manager.enabled = false;
            foreach (var input in Object.FindObjectsByType<InputActionManager>(FindObjectsSortMode.None))
                if (input.gameObject.scene == scene) input.enabled = false;
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.gameObject.scene == scene && camera.CompareTag("MainCamera")) camera.enabled = false;
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.gameObject.scene == scene) listener.enabled = false;
            if (interactionManager)
                foreach (var interactable in Object.FindObjectsByType<XRBaseInteractable>(FindObjectsSortMode.None))
                    if (interactable.gameObject.scene == scene) interactable.interactionManager = interactionManager;
        }

        static CabinSpawn FindSpawn(Scene scene, string id)
        {
            foreach (var spawn in Object.FindObjectsByType<CabinSpawn>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (spawn.gameObject.scene == scene && spawn.spawnId == id) return spawn;
            return null;
        }

        void PlaceRig(Transform spawn)
        {
            Vector3 oldPosition = player.transform.position;
            Quaternion oldRotation = player.transform.rotation;
            bool wasEnabled = player.body && player.body.enabled;
            if (player.body) player.body.enabled = false;
            float yaw = spawn.eulerAngles.y - player.view.transform.eulerAngles.y;
            player.origin.RotateAroundCameraUsingOriginUp(yaw);
            Vector3 headOffset = Vector3.ProjectOnPlane(player.view.transform.position - player.transform.position, Vector3.up);
            player.transform.position = spawn.position - headOffset;
            Quaternion rotationDelta = player.transform.rotation * Quaternion.Inverse(oldRotation);
            foreach (var item in carried)
                if (item) item.transform.SetPositionAndRotation(
                    player.transform.position + rotationDelta * (item.transform.position - oldPosition),
                    rotationDelta * item.transform.rotation);
            if (player.body) player.body.enabled = wasEnabled;
        }

        public void ReturnReleased(GameObject item)
        {
            if (!item || item.scene != gameObject.scene || item == player.gameObject) return;
            // A selection in either XR hand or the desktop hand must remain attached throughout travel.
            var cabinItem = item.GetComponent<CabinItem>();
            var grab = item.GetComponent<XRGrabInteractable>();
            if ((cabinItem && cabinItem.IsHeld) || (grab && grab.isSelected)) return;
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded) return;
            item.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(item, active);
            exported.Remove(item);
        }
        void Update()
        {
            if (Travelling || exported.Count == 0) return;
            var pending = new List<GameObject>(exported);
            foreach (var item in pending)
            {
                if (!item) { exported.Remove(item); continue; }
                ReturnReleased(item);
            }
        }
    }
}
