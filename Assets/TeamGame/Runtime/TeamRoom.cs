using BlackTide;
using BlackTide.MP1B;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team15
{
    [DefaultExecutionOrder(-9000)]
    public sealed class TeamRoom : MonoBehaviour
    {
        public int roomIndex;
        public GameObject playerPrefab;
        public CabinSpawn spawn;
        public Deck.LockManager deck;
        public Storage.LanternLock lantern;
        public Storage.MirrorLock mirror;
        public Storage.ExitLockZone storageExit;
        public CabinRoom captain;
        public ActionHandler navigation;
        public TMP_Text exitLabel;
        public Transform captainDoor;
        public GameObject captainPortal;
        public Collider[] captainDoorColliders;
        public bool IsComplete { get; private set; }
        float nextCheck;
        Transform aim;
        PiratePlayer player;
        Quaternion doorClosed;
        InputAction light, ship, select, celebrate;

        void Awake()
        {
            var session = TeamSession.Ensure(playerPrefab);
            player = session.Player;
            BindPlayer(player);
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                session.Enter(this);
                player.ReturnToCabin();
            }
            if (captainDoor) doorClosed = captainDoor.localRotation;
        }
        public void BindPlayer(PiratePlayer rig)
        {
            player = rig;
            rig.body.slopeLimit = roomIndex == 0 ? 60f : 45f;
            rig.interiorAnchor = spawn.transform;
            rig.exteriorAnchor = spawn.transform;
            rig.view.cullingMask = roomIndex == 1 ? ~(1 << 9) : ~0;
            if (rig.view.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData)) cameraData.SetRenderer(roomIndex == 2 ? 1 : 0);
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var teleport in root.GetComponentsInChildren<Deck.HoverTeleport>(true)) teleport.xrOrigin = rig.transform;
            }
            if (!navigation) return;
            if (!aim)
            {
                aim = new GameObject("Navigation aim").transform;
                aim.SetParent(transform, false);
            }
            navigation.player = rig.transform;
            navigation.viewCamera = rig.view;
            navigation.controller = aim;
            navigation.VictoryAllowed = () => TeamSession.Instance && TeamSession.Instance.AllowVictory();
            navigation.quitAction = navigation.lightAction = navigation.interactAction = navigation.spawnShipAction = navigation.teleportToWinAction = null;
            if (light == null)
            {
                light = MakeAction("Navigation light", "<Keyboard>/l", "<XRController>{LeftHand}/secondaryButton");
                ship = MakeAction("Navigation ship", "<Keyboard>/f", "<XRController>{RightHand}/secondaryButton");
                select = MakeAction("Navigation select", "<Keyboard>/e", "<XRController>{RightHand}/triggerPressed");
                select.AddBinding("<Mouse>/leftButton");
                celebrate = MakeAction("Navigation celebration", "<Keyboard>/t", "<XRController>{LeftHand}/primaryButton");
            }
        }
        static InputAction MakeAction(string name, string desktop, string xr)
        {
            var action = new InputAction(name, InputActionType.Button, desktop);
            action.AddBinding(xr); action.Enable(); return action;
        }
        void Update()
        {
            if (Team15.TeamSession.GameplayBlocked) return;
            if (!player || SceneManager.GetActiveScene() != gameObject.scene || CabinTransition.IsTravelling) return;
            if (navigation)
            {
                aim.SetPositionAndRotation(player.AimTransform.position, player.AimTransform.rotation);
                if (light.WasPressedThisFrame()) navigation.ChangeLightColor();
                if (select.WasPressedThisFrame()) navigation.InteractWithTarget();
                if (ship.WasPressedThisFrame()) navigation.SpawnSelectedShip();
                if (celebrate.WasPressedThisFrame()) navigation.TryTeleportToWinRoom();
            }
            if (Time.unscaledTime >= nextCheck) { nextCheck = Time.unscaledTime + .1f; RefreshProgress(); }
            if (captainDoor)
                captainDoor.localRotation = Quaternion.Slerp(captainDoor.localRotation,
                    doorClosed * Quaternion.Euler(0, IsComplete ? -95f : 0, 0), Time.deltaTime * 3f);
        }
        public void RefreshProgress()
        {
            IsComplete = roomIndex == 0 ? deck && deck.IsComplete
                : roomIndex == 1 ? lantern && lantern.IsLit && mirror && mirror.IsUnlocked && storageExit && storageExit.IsUnlocked
                : roomIndex == 2 ? captain && captain.IsCleared
                : navigation && navigation.IsRoomComplete;
            if (TeamSession.Instance) TeamSession.Instance.RecordCompletion(roomIndex, IsComplete);
            if (captainDoorColliders != null)
                foreach (var collider in captainDoorColliders) if (collider) collider.enabled = !IsComplete;
            if (captainPortal && captainPortal.activeSelf != IsComplete) captainPortal.SetActive(IsComplete);
            if (exitLabel)
            {
                exitLabel.text = IsComplete ? "<b>EXIT UNLOCKED</b>\nWalk through to " + TeamSession.Names[Mathf.Min(3, roomIndex + 1)]
                    : "<b>EXIT LOCKED</b>\nComplete this room's puzzles\nand unlock the exit.";
                exitLabel.color = IsComplete ? new Color(.35f,1f,.55f) : new Color(1f,.85f,.5f);
            }
        }
        void OnDestroy() { light?.Dispose(); ship?.Dispose(); select?.Dispose(); celebrate?.Dispose(); }
        void OnGUI()
        {
            if (Team15.TeamSession.GameplayBlocked) return;
            if (!navigation || !player || player.IsXR || SceneManager.GetActiveScene() != gameObject.scene) return;
            GUI.Box(new Rect(12, 180, 360, 58), "Navigation: aim + E/click selects map\nF launch ship  |  L light  |  T enter celebration");
        }
    }
}
