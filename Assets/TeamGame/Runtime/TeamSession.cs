using System.Collections;
using BlackTide;
using BlackTide.MP1B;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

namespace Team15
{
    public sealed class TeamSession : MonoBehaviour
    {
        public static TeamSession Instance { get; private set; }
        public static readonly string[] Scenes = { "DeckScene", "Storage_Room", "BlackTideCabin", "NavigationScene" };
        public static readonly string[] Names = { "Deck", "Storage Room", "Captain's Cabin", "Navigation Room" };
        public PiratePlayer Player { get; private set; }
        public TeamRoom ActiveRoom { get; private set; }
        public bool Restarting { get; private set; }
        readonly bool[] completed = new bool[4];
        InputAction menuAction;
        GameObject menu;
        float noticeUntil;
        string notice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public static TeamSession Ensure(GameObject playerPrefab)
        {
            if (Instance) return Instance;
            var go = new GameObject("Team 15 - Game Session");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TeamSession>();
            Instance.Player = Instantiate(playerPrefab).GetComponent<PiratePlayer>();
            Instance.Player.name = "Shared Player";
            CabinTravelRuntime.Get(Instance.Player);
            CabinTransition.TravelAllowed = Instance.AllowTravel;
            Instance.menuAction = new InputAction("Game menu", InputActionType.Button, "<Keyboard>/escape");
            Instance.menuAction.AddBinding("<XRController>{LeftHand}/menuButton");
            Instance.menuAction.Enable();
            Instance.BuildMenu();
            return Instance;
        }

        public bool IsComplete(int index) => index >= 0 && index < 4 && completed[index];
        public bool PreviousRoomsComplete(int index)
        {
            for (int i = 0; i < index; i++) if (!completed[i]) return false;
            return true;
        }
        public void RecordCompletion(int index, bool value)
        {
            if (index < 0 || index >= 4) return;
            completed[index] = value;
            if (!value) for (int i = index + 1; i < completed.Length; i++) completed[i] = false;
        }
        public void Enter(TeamRoom room)
        {
            ActiveRoom = room;
            Player.interiorAnchor = room.spawn.transform;
            Player.exteriorAnchor = room.spawn.transform;
            room.BindPlayer(Player);
            Show(Names[room.roomIndex] + "  |  Room " + (room.roomIndex + 1) + " / 4");
        }
        public bool AllowTravel(CabinTransition door)
        {
            if (Restarting || !ActiveRoom || door.gameObject.scene != SceneManager.GetActiveScene()) return false;
            ActiveRoom.RefreshProgress();
            int index = ActiveRoom.roomIndex;
            bool allowed = index < 3 && door.targetScene == Scenes[index + 1]
                && completed[index] && PreviousRoomsComplete(index);
            if (!allowed) Show("Complete all required puzzles and unlock this room's exit first.");
            return allowed;
        }
        public bool AllowVictory() => ActiveRoom && ActiveRoom.roomIndex == 3
            && PreviousRoomsComplete(3) && ActiveRoom.navigation && ActiveRoom.navigation.IsRoomComplete;
        public void Show(string text) { notice = text; noticeUntil = Time.unscaledTime + 4f; }

        void Update()
        {
            if (Restarting || !Player) return;
            if (menuAction != null && menuAction.WasPressedThisFrame()) ToggleMenu();
            var scene = SceneManager.GetActiveScene();
            if (!ActiveRoom || ActiveRoom.gameObject.scene != scene)
                foreach (var root in scene.GetRootGameObjects())
                {
                    var room = root.GetComponentInChildren<TeamRoom>();
                    if (room) { Enter(room); break; }
                }
        }
        public void ToggleMenu()
        {
            bool show = !menu.activeSelf;
            menu.SetActive(show);
            if (!show) return;
            var view = Player.view.transform;
            var forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            menu.transform.SetPositionAndRotation(view.position + forward * 1.5f, Quaternion.LookRotation(forward));
        }
        void BuildMenu()
        {
            menu = new GameObject("Voyage menu");
            menu.transform.SetParent(transform);
            var background = SceneKit.Lit("Voyage menu walnut", new Color(.07f,.04f,.02f));
            SceneKit.Box("Menu panel", menu.transform, new Vector3(0,0,.06f), new Vector3(1.45f,1.1f,.04f), background);
            SceneKit.Panel("Menu title", menu.transform, new Vector3(0,.38f,0), new Vector2(1.35f,.22f), "THE BLACK TIDE", 32);
            MenuButton("CONTINUE", .12f, () => menu.SetActive(false), background);
            MenuButton("RESTART VOYAGE", -.17f, RestartVoyage, background);
            MenuButton("QUIT", -.46f, Quit, background);
            menu.SetActive(false);
        }
        void MenuButton(string title, float y, UnityEngine.Events.UnityAction action, Material mat)
        {
            var go = SceneKit.Group(title, menu.transform, new Vector3(0,y,0));
            SceneKit.Box("Button", go.transform, Vector3.zero, new Vector3(1.2f,.22f,.06f), mat);
            go.AddComponent<BoxCollider>().size = new Vector3(1.2f,.22f,.06f);
            var interactable = go.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ => action());
            SceneKit.Panel(title + " label", go.transform, new Vector3(0,0,-.04f), new Vector2(.98f,.18f), title, 27);
        }
        public void RestartVoyage()
        {
            if (!Restarting) StartCoroutine(RestartRoutine());
        }
        IEnumerator RestartRoutine()
        {
            Restarting = true;
            menu.SetActive(false);
            CabinTransition.TravelAllowed = null;
            if (CabinTravelRuntime.Current) CabinTravelRuntime.Current.ShutdownForRestart();
            if (Player) Destroy(Player.gameObject);
            Instance = null;
            yield return null;
            yield return SceneManager.LoadSceneAsync(Scenes[0], LoadSceneMode.Single);
            Destroy(gameObject);
        }
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        void OnDestroy()
        {
            menuAction?.Dispose();
            if (Instance == this) { Instance = null; CabinTransition.TravelAllowed = null; }
        }
        void OnGUI()
        {
            if (!Player || Player.IsXR || !ActiveRoom || Restarting) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            GUI.Box(new Rect(12, 106, 290, 34), "Room " + (ActiveRoom.roomIndex + 1) + " / 4  |  " + Names[ActiveRoom.roomIndex], style);
            GUI.Box(new Rect(12, 145, 290, 28), "Esc: voyage menu / restart", style);
            if (Time.unscaledTime < noticeUntil && ActiveRoom.roomIndex != 2)
                GUI.Box(new Rect(12, 12, Mathf.Min(760, Screen.width - 24), 60), notice, style);
        }
    }
}
