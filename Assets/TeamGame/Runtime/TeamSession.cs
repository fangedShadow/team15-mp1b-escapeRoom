using System;
using System.Collections;
using BlackTide;
using BlackTide.MP1B;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

namespace Team15
{
    [DefaultExecutionOrder(-10000)]
    public sealed class TeamSession : MonoBehaviour
    {
        public const int MenuLayer = 30;
        enum MenuMode { Hidden, Start, Pause, Complete }
        public static TeamSession Instance { get; private set; }
        public static readonly string[] Scenes = { "DeckScene", "Storage_Room", "BlackTideCabin", "NavigationScene" };
        public static readonly string[] Names = { "Deck", "Storage Room", "Captain's Cabin", "Navigation Room" };
        public static bool GameplayBlocked => Instance && (Instance.MenuVisible || Instance.Restarting || Time.frameCount <= Instance.resumeAfterFrame);
        public PiratePlayer Player { get; private set; }
        public TeamRoom ActiveRoom { get; private set; }
        public bool Restarting { get; private set; }
        public bool MenuVisible => mode != MenuMode.Hidden;
        public double ElapsedSeconds { get; private set; }
        public bool HasFinished { get; private set; }

        static bool nextSessionAutoPlay;
        readonly bool[] completed = new bool[4];
        InputAction menuAction;
        GameObject menu, continueButton, restartButton, quitButton, menuPanel, startArtwork;
        TMP_Text menuTitle, menuDetail, continueLabel;
        Material menuMaterial;
        TeamMenuInteractionFilter menuFilter;
        ActionHandler watchedNavigation;
        MenuMode mode;
        bool initialized, running, autoPlay, positionMenuNextFrame;
        int resumeAfterFrame = -1;
        double nextTimerText;
        float noticeUntil;
        string notice;
        Coroutine celebration;
        GUIStyle titleStyle, detailStyle, buttonStyle, hudStyle;
        Texture2D buttonNormal, buttonHover, buttonPressed, startShip;
        Material startSeaMaterial;
        RenderTexture startSeaBack, startSeaFront;
        RectTransform startShipTransform;
        Vector3 startShipRestPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            nextSessionAutoPlay = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public static TeamSession Ensure(GameObject playerPrefab)
        {
            if (Instance) return Instance;
            var go = new GameObject("Team 15 - Game Session");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TeamSession>();
            Instance.autoPlay = nextSessionAutoPlay;
            nextSessionAutoPlay = false;
            Instance.Player = Instantiate(playerPrefab).GetComponent<PiratePlayer>();
            Instance.Player.name = "Shared Player";
            CabinTravelRuntime.Get(Instance.Player);
            CabinTransition.TravelAllowed = Instance.AllowTravel;
            Instance.menuAction = new InputAction("Game menu", InputActionType.Button, "<Keyboard>/escape");
            Instance.menuAction.AddBinding("<XRController>{LeftHand}/menuButton");
            Instance.menuAction.Enable();
            Instance.BuildMenu();
            Instance.menuFilter = go.AddComponent<TeamMenuInteractionFilter>();
            Instance.menuFilter.Bind(Instance, Instance.Player);
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
            if (watchedNavigation != room.navigation)
            {
                if (watchedNavigation) watchedNavigation.Won -= OnVictory;
                watchedNavigation = room.navigation;
                if (watchedNavigation) watchedNavigation.Won += OnVictory;
            }
            if (!initialized)
            {
                initialized = true;
                // A direct later-scene launch stays useful for editing individual rooms.
                if (room.roomIndex == 0 && !autoPlay) ShowMenu(MenuMode.Start);
                else StartGame();
            }
            Show(Names[room.roomIndex] + "  |  Room " + (room.roomIndex + 1) + " / 4");
        }
        public bool AllowTravel(CabinTransition door)
        {
            if (GameplayBlocked || !running || HasFinished || !ActiveRoom || door.gameObject.scene != SceneManager.GetActiveScene()) return false;
            ActiveRoom.RefreshProgress();
            int index = ActiveRoom.roomIndex;
            bool allowed = index < 3 && door.targetScene == Scenes[index + 1]
                && completed[index] && PreviousRoomsComplete(index);
            if (!allowed) Show("Complete all required puzzles and unlock this room's exit first.");
            return allowed;
        }
        public bool AllowVictory() => !GameplayBlocked && running && !HasFinished && ActiveRoom && ActiveRoom.roomIndex == 3
            && PreviousRoomsComplete(3) && ActiveRoom.navigation && ActiveRoom.navigation.IsRoomComplete;
        public void Show(string text) { notice = text; noticeUntil = Time.unscaledTime + 4f; }
        public bool AllowsMenuTarget(Transform target) => menu && target && target.IsChildOf(menu.transform);

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
            if (running && !HasFinished && !GameplayBlocked && !CabinTransition.IsTravelling)
                ElapsedSeconds += Time.unscaledDeltaTime;
            if (menu)
            {
                bool visibleInXR = MenuVisible && Player.IsXR;
                if (visibleInXR && !menu.activeSelf) positionMenuNextFrame = true;
                menu.SetActive(visibleInXR);
            }
            if (MenuVisible && Time.unscaledTimeAsDouble >= nextTimerText)
            {
                nextTimerText = Time.unscaledTimeAsDouble + .25;
                UpdateMenuText();
            }
            if (mode == MenuMode.Start) AnimateStartArtwork();
        }

        void LateUpdate()
        {
            if (!positionMenuNextFrame || !Player || !MenuVisible) return;
            positionMenuNextFrame = false;
            PositionMenu();
        }

        public void StartGame()
        {
            if (Restarting || HasFinished || running) return;
            ElapsedSeconds = 0;
            running = true;
            ShowMenu(MenuMode.Hidden);
            if (ActiveRoom && ActiveRoom.roomIndex == 0) Show("Explore the Deck and find all three keys.");
        }
        public void ToggleMenu()
        {
            if (Restarting || !initialized || mode == MenuMode.Start || HasFinished || CabinTransition.IsTravelling) return;
            ShowMenu(mode == MenuMode.Pause ? MenuMode.Hidden : MenuMode.Pause);
        }
        void ContinueGame()
        {
            if (mode == MenuMode.Start) StartGame();
            else if (mode == MenuMode.Pause) ShowMenu(MenuMode.Hidden);
        }
        void ShowMenu(MenuMode value)
        {
            mode = value;
            bool blocked = MenuVisible;
            Time.timeScale = blocked ? 0f : 1f;
            // The start screen keeps Deck's looping sea ambience audible.
            AudioListener.pause = value == MenuMode.Pause || value == MenuMode.Complete;
            if (menuFilter) menuFilter.SetPaused(blocked);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!blocked) resumeAfterFrame = Time.frameCount + 1;
            if (!menu) return;
            menu.SetActive(blocked && Player && Player.IsXR);
            if (!blocked || !Player) return;
            // The initial spawn is applied by TeamRoom after Enter returns.
            positionMenuNextFrame = true;
            PositionMenu();
            UpdateMenuText();
        }
        void PositionMenu()
        {
            if (!menu || !Player || !Player.view) return;
            var view = Player.view.transform;
            var forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Player.transform.forward;
            float distance = mode == MenuMode.Start ? 2.1f : 1.5f;
            menu.transform.SetPositionAndRotation(view.position + forward * distance, Quaternion.LookRotation(forward));
            // FixedUpdate is stopped while menus are open; update ray-query geometry now.
            Physics.SyncTransforms();
        }
        void OnVictory()
        {
            if (HasFinished || Restarting || !running) return;
            HasFinished = true;
            running = false;
            celebration = StartCoroutine(ShowResultAfterCelebration());
        }
        IEnumerator ShowResultAfterCelebration()
        {
            yield return new WaitForSecondsRealtime(5f);
            celebration = null;
            if (!Restarting && Instance == this) ShowMenu(MenuMode.Complete);
        }

        void BuildMenu()
        {
            menu = new GameObject("Voyage menu");
            menu.transform.SetParent(transform);
            menuMaterial = SceneKit.Lit("Voyage menu walnut", new Color(.07f, .04f, .02f));
            menuPanel = SceneKit.Box("Menu panel", menu.transform, new Vector3(0, 0, .06f), new Vector3(1.65f, 1.55f, .04f), menuMaterial);
            startShip = Resources.Load<Texture2D>("TeamGame/StartShip");
            BuildStartArtwork();
            menuTitle = SceneKit.Panel("Menu title", menu.transform, new Vector3(0, .56f, 0), new Vector2(1.5f, .28f), "THE BLACK TIDE", 33);
            menuDetail = SceneKit.Panel("Menu detail", menu.transform, new Vector3(0, .30f, 0), new Vector2(1.48f, .22f), "", 24);
            continueButton = MenuButton("PLAY", .02f, ContinueGame, out continueLabel);
            restartButton = MenuButton("RESTART", -.25f, RestartVoyage, out _);
            quitButton = MenuButton("QUIT", -.52f, Quit, out _);
            foreach (var child in menu.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = MenuLayer;
            menu.SetActive(false);
        }
        void BuildStartArtwork()
        {
            startArtwork = new GameObject("Start screen artwork", typeof(RectTransform), typeof(Canvas));
            startArtwork.transform.SetParent(menu.transform, false);
            startArtwork.transform.localPosition = new Vector3(0, 0, .035f);
            startArtwork.GetComponent<RectTransform>().sizeDelta = new Vector2(3.05f, 1.8f);
            var canvas = startArtwork.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Player.view;
            var backdrop = new GameObject("Pure black backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            backdrop.transform.SetParent(startArtwork.transform, false);
            backdrop.GetComponent<RectTransform>().sizeDelta = new Vector2(3.05f, 1.8f);
            var black = backdrop.GetComponent<RawImage>();
            black.texture = Texture2D.whiteTexture;
            black.color = Color.black;
            black.raycastTarget = false;
            if (startShip)
            {
                BuildStartSea();
                float fit = Mathf.Min(1.2f / startShip.width, 1.63f / startShip.height);
                var shipSize = new Vector2(startShip.width * fit, startShip.height * fit);
                // The ocean fills the entire width of the menu and reaches its bottom edge.
                var seaSize = new Vector2(3.05f, .9f - shipSize.y * .14f);
                var seaPosition = new Vector3(0, -.9f + seaSize.y * .5f, -.007f);
                AddSeaImage("Dark sea", startSeaBack, seaPosition, seaSize);
                var imageObject = new GameObject("Pirate ship", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                imageObject.transform.SetParent(startArtwork.transform, false);
                var rect = imageObject.GetComponent<RectTransform>();
                rect.sizeDelta = shipSize;
                rect.pivot = new Vector2(.55f, .16f);
                startShipRestPosition = new Vector3(-.86f + shipSize.x * .05f, -shipSize.y * .34f, -.01f);
                rect.localPosition = startShipRestPosition;
                startShipTransform = rect;
                var image = imageObject.GetComponent<RawImage>();
                image.texture = startShip;
                image.color = Color.white;
                image.raycastTarget = false;
                seaPosition.z = -.013f;
                AddSeaImage("Waterline ripples", startSeaFront, seaPosition, seaSize);
            }
            startArtwork.SetActive(false);
        }
        void BuildStartSea()
        {
            var shader = Resources.Load<Shader>("TeamGame/StartSea");
            if (!shader) return;
            startSeaMaterial = new Material(shader);
            startSeaBack = SeaTexture("Start menu dark water");
            startSeaFront = SeaTexture("Start menu waterline");
            RenderStartSea();
        }
        static RenderTexture SeaTexture(string title)
        {
            var texture = new RenderTexture(1024, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = title, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }
        void AddSeaImage(string title, Texture texture, Vector3 position, Vector2 size)
        {
            if (!texture) return;
            var go = new GameObject(title, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(startArtwork.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.localPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }
        // One long swell avoids the small competing oscillations that looked like jitter.
        static float ShipBob(float time) => Mathf.Sin(time * .55f) * .022f;
        static float ShipDrift(float time) => Mathf.Sin(time * .55f + .6f) * .008f;
        static float ShipRoll(float time) => Mathf.Sin(time * .55f + .4f) * 1.8f;
        void AnimateStartArtwork()
        {
            float time = Time.unscaledTime;
            if (startShipTransform)
            {
                var size = startShipTransform.sizeDelta;
                startShipTransform.localPosition = startShipRestPosition + new Vector3(ShipDrift(time) * size.x, ShipBob(time) * size.y, 0);
                startShipTransform.localRotation = Quaternion.Euler(0, 0, ShipRoll(time));
            }
            RenderStartSea();
        }
        void RenderStartSea()
        {
            if (!startSeaMaterial || !startSeaBack || !startSeaFront) return;
            float time = Time.unscaledTime;
            GetSeaMapping(out Rect ship, out Rect sea);
            float contactY = ship.yMax - ship.height * .08f;
            startSeaMaterial.SetFloat("_UnscaledTime", time);
            startSeaMaterial.SetFloat("_Waterline", 1f - (contactY - sea.y) / sea.height);
            startSeaMaterial.SetFloat("_BoatX", (ship.center.x + ShipDrift(time) * ship.width - sea.x) / sea.width);
            startSeaMaterial.SetFloat("_BoatWidth", ship.width / sea.width);
            startSeaMaterial.SetFloat("_BoatBob", ShipBob(time) * ship.height / sea.height);
            var previous = RenderTexture.active;
            bool previousSRGB = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                startSeaMaterial.SetFloat("_Foreground", 0);
                Graphics.Blit(Texture2D.whiteTexture, startSeaBack, startSeaMaterial);
                startSeaMaterial.SetFloat("_Foreground", 1);
                Graphics.Blit(Texture2D.whiteTexture, startSeaFront, startSeaMaterial);
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSRGB;
            }
        }
        GameObject MenuButton(string title, float y, UnityEngine.Events.UnityAction action, out TMP_Text label)
        {
            var go = SceneKit.Group(title, menu.transform, new Vector3(0, y, 0));
            SceneKit.Box("Button", go.transform, Vector3.zero, new Vector3(1.3f, .22f, .06f), menuMaterial);
            go.AddComponent<BoxCollider>().size = new Vector3(1.3f, .22f, .06f);
            var interactable = go.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ => action());
            label = SceneKit.Panel(title + " label", go.transform, new Vector3(0, 0, -.04f), new Vector2(1.12f, .18f), title, 28);
            return go;
        }
        void UpdateMenuText()
        {
            if (!menuTitle) return;
            menuTitle.text = mode == MenuMode.Complete ? "VOYAGE COMPLETE" : mode == MenuMode.Pause ? "PAUSED" : "THE BLACK TIDE";
            menuDetail.text = mode == MenuMode.Start ? "Four rooms. One escape." : "Time: " + FormatTime(ElapsedSeconds);
            continueLabel.text = mode == MenuMode.Start ? "PLAY" : "CONTINUE";
            continueButton.SetActive(mode != MenuMode.Complete);
            restartButton.SetActive(mode != MenuMode.Start);
            bool showArtwork = mode == MenuMode.Start;
            startArtwork.SetActive(showArtwork);
            menuPanel.SetActive(!showArtwork);
            menuTitle.transform.parent.localPosition = new Vector3(showArtwork ? .65f : 0, showArtwork ? .45f : .56f, 0);
            menuDetail.transform.parent.localPosition = new Vector3(showArtwork ? .65f : 0, showArtwork ? .20f : .30f, 0);
            continueButton.transform.localPosition = new Vector3(showArtwork ? .65f : 0, showArtwork ? -.12f : .02f, 0);
            if (mode == MenuMode.Complete) restartButton.transform.localPosition = new Vector3(0, .01f, 0);
            else restartButton.transform.localPosition = new Vector3(0, -.25f, 0);
            quitButton.transform.localPosition = new Vector3(showArtwork ? .65f : 0,
                showArtwork ? -.42f : mode == MenuMode.Pause ? -.52f : -.28f, 0);
            Physics.SyncTransforms();
        }
        public static string FormatTime(double seconds)
        {
            long total = (long)Math.Floor(Math.Max(0, seconds));
            return total >= 3600 ? $"{total / 3600}:{total / 60 % 60:00}:{total % 60:00}" : $"{total / 60:00}:{total % 60:00}";
        }

        public void RestartVoyage()
        {
            if (!Restarting && !CabinTransition.IsTravelling) StartCoroutine(RestartRoutine());
        }
        IEnumerator RestartRoutine()
        {
            Restarting = true;
            if (celebration != null) StopCoroutine(celebration);
            running = false;
            ShowMenu(MenuMode.Hidden);
            CabinTransition.TravelAllowed = null;
            if (watchedNavigation) watchedNavigation.Won -= OnVictory;
            if (CabinTravelRuntime.Current) CabinTravelRuntime.Current.ShutdownForRestart();
            if (Player) Destroy(Player.gameObject);
            nextSessionAutoPlay = true;
            Instance = null;
            yield return null;
            yield return SceneManager.LoadSceneAsync(Scenes[0], LoadSceneMode.Single);
            Destroy(gameObject);
        }
        public void Quit()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        void OnDestroy()
        {
            menuAction?.Dispose();
            if (watchedNavigation) watchedNavigation.Won -= OnVictory;
            if (menuMaterial) Destroy(menuMaterial);
            if (buttonNormal) Destroy(buttonNormal);
            if (buttonHover) Destroy(buttonHover);
            if (buttonPressed) Destroy(buttonPressed);
            if (startSeaMaterial) Destroy(startSeaMaterial);
            if (startSeaBack) { startSeaBack.Release(); Destroy(startSeaBack); }
            if (startSeaFront) { startSeaFront.Release(); Destroy(startSeaFront); }
            if (Instance == this)
            {
                Instance = null;
                CabinTransition.TravelAllowed = null;
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }

        void BuildGuiStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(.96f, .82f, .48f);
            detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            detailStyle.normal.textColor = new Color(.84f, .91f, .94f);
            buttonNormal = Solid(new Color(.14f, .26f, .31f));
            buttonHover = Solid(new Color(.22f, .39f, .43f));
            buttonPressed = Solid(new Color(.36f, .47f, .39f));
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            buttonStyle.normal.background = buttonNormal;
            buttonStyle.hover.background = buttonHover;
            buttonStyle.active.background = buttonPressed;
            buttonStyle.normal.textColor = buttonStyle.hover.textColor = buttonStyle.active.textColor = Color.white;
            hudStyle = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        }
        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }
        static void StartLayout(out float scale, out Vector3 offset, out Rect image, out Rect panel)
        {
            bool stacked = Screen.width < Screen.height * 1.15f;
            float width = stacked ? 600f : 1180f;
            float height = stacked ? 1000f : 720f;
            scale = Mathf.Max(.001f, Mathf.Min(Screen.width / width, Screen.height / height));
            offset = new Vector3((Screen.width - width * scale) * .5f, (Screen.height - height * scale) * .5f, 0);
            image = stacked ? new Rect(85, 38, 430, 550) : new Rect(64, 50, 470, 620);
            panel = stacked ? new Rect(50, 618, 500, 330) : new Rect(604, 178, 500, 350);
        }
        Rect FittedShip(Rect bounds)
        {
            if (!startShip) return bounds;
            float fit = Mathf.Min(bounds.width / startShip.width, bounds.height / startShip.height);
            var size = new Vector2(startShip.width * fit, startShip.height * fit);
            return new Rect(bounds.center - size * .5f, size);
        }
        void GetSeaMapping(out Rect ship, out Rect sea)
        {
            if (Player && Player.IsXR)
            {
                ship = FittedShip(new Rect(-1.46f, -.815f, 1.2f, 1.63f));
                float top = ship.yMax - ship.height * .36f;
                sea = new Rect(-1.525f, top, 3.05f, .9f - top);
                return;
            }
            StartLayout(out float scale, out Vector3 offset, out Rect image, out _);
            ship = FittedShip(image);
            float seaTop = ship.yMax - ship.height * .36f;
            sea = new Rect(-offset.x / scale, seaTop, Screen.width / scale,
                Mathf.Max(1, (Screen.height - offset.y) / scale - seaTop));
        }
        void DrawFloatingShip()
        {
            if (!startShip) return;
            GetSeaMapping(out Rect ship, out Rect sea);
            if (startSeaBack) GUI.DrawTexture(sea, startSeaBack, ScaleMode.StretchToFill, true);
            float time = Time.unscaledTime;
            ship.position += new Vector2(ShipDrift(time) * ship.width, -ShipBob(time) * ship.height);
            Matrix4x4 beforeShip = GUI.matrix;
            GUIUtility.RotateAroundPivot(-ShipRoll(time), new Vector2(ship.x + ship.width * .55f, ship.y + ship.height * .84f));
            GUI.DrawTexture(ship, startShip, ScaleMode.ScaleToFit, true);
            GUI.matrix = beforeShip;
            if (startSeaFront) GUI.DrawTexture(sea, startSeaFront, ScaleMode.StretchToFill, true);
        }
        void DrawStartMenu()
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            StartLayout(out float scale, out Vector3 offset, out _, out Rect panel);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
            DrawFloatingShip();
            GUI.Label(new Rect(panel.x, panel.y + 15, panel.width, 60), "THE BLACK TIDE", titleStyle);
            GUI.Label(new Rect(panel.x, panel.y + 85, panel.width, 42), "Four rooms. One escape.", detailStyle);
            if (GUI.Button(new Rect(panel.x + 70, panel.y + 160, 360, 55), "PLAY", buttonStyle)) ContinueGame();
            if (GUI.Button(new Rect(panel.x + 70, panel.y + 234, 360, 55), "QUIT", buttonStyle)) Quit();
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }
        void OnGUI()
        {
            if (!Player || Player.IsXR || !ActiveRoom || Restarting) return;
            BuildGuiStyles();
            if (MenuVisible)
            {
                if (mode == MenuMode.Start) { DrawStartMenu(); return; }
                MenuMode displayedMode = mode;
                GUI.depth = -1000;
                Matrix4x4 oldMatrix = GUI.matrix;
                Color oldColor = GUI.color;
                float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .5f, 2f);
                GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
                float width = Screen.width / scale, height = Screen.height / scale;
                GUI.color = new Color(.018f, .037f, .05f, displayedMode == MenuMode.Start ? 1f : .95f);
                GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                float panelHeight = displayedMode == MenuMode.Pause ? 430 : 350;
                var panel = new Rect((width - 520) * .5f, (height - panelHeight) * .5f, 520, panelHeight);
                string title = displayedMode == MenuMode.Complete ? "VOYAGE COMPLETE" : displayedMode == MenuMode.Pause ? "PAUSED" : "THE BLACK TIDE";
                GUI.Label(new Rect(panel.x, panel.y + 15, 520, 60), title, titleStyle);
                string detail = displayedMode == MenuMode.Start ? "Four rooms. One escape." : "Your time: " + FormatTime(ElapsedSeconds);
                GUI.Label(new Rect(panel.x, panel.y + 85, 520, 42), detail, detailStyle);
                float y = panel.y + 160;
                if (displayedMode != MenuMode.Complete)
                {
                    if (GUI.Button(new Rect(panel.x + 80, y, 360, 55), displayedMode == MenuMode.Start ? "PLAY" : "CONTINUE", buttonStyle)) ContinueGame();
                    y += 74;
                }
                if (displayedMode != MenuMode.Start)
                {
                    if (GUI.Button(new Rect(panel.x + 80, y, 360, 55), "RESTART", buttonStyle)) RestartVoyage();
                    y += 74;
                }
                if (GUI.Button(new Rect(panel.x + 80, y, 360, 55), "QUIT", buttonStyle)) Quit();
                GUI.color = oldColor; GUI.matrix = oldMatrix;
                return;
            }
            GUI.Box(new Rect(12, 106, 350, 34), "Room " + (ActiveRoom.roomIndex + 1) + " / 4  |  " + Names[ActiveRoom.roomIndex], hudStyle);
            GUI.Box(new Rect(12, 145, 350, 28), "Time " + FormatTime(ElapsedSeconds) + "  |  Esc: pause menu", hudStyle);
            if (Time.unscaledTime < noticeUntil && ActiveRoom.roomIndex != 2)
                GUI.Box(new Rect(12, 12, Mathf.Min(760, Screen.width - 24), 60), notice, hudStyle);
        }
    }
}
