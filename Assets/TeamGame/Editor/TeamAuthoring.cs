using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BlackTide;
using BlackTide.MP1B;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace Team15.Editor
{
    public static class TeamAuthoring
    {
        public static readonly string[] Paths = {
            "Assets/Scenes/DeckScene.unity", "Assets/Scenes/Billy/Storage_Room.unity",
            "Assets/Pirate/Scenes/BlackTideCabin.unity", "Assets/Scenes/NavigationScene.unity" };
        const string Generated = "Assets/TeamGame/Generated";
        const string PlayerPath = Generated + "/SharedPlayer.prefab";
        static Material signMaterial;

        [MenuItem("Team 15/Configure combined game")]
        public static void Configure()
        {
            if (!Path.GetFullPath(Application.dataPath).Replace('\\','/').Equals("C:/UnityProjects/team15_mp1b/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Configure only the approved team15_mp1b integration project.");
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            signMaterial = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/RouteSign.mat");
            if (!signMaterial)
            {
                signMaterial = SceneKit.Lit("Dark ship timber", new Color(.085f,.047f,.021f));
                AssetDatabase.CreateAsset(signMaterial, Generated + "/RouteSign.mat");
            }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath)) ExportPlayer();
            for (int i = 0; i < Paths.Length; i++) ConfigureRoom(i);
            EditorBuildSettings.scenes = Paths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            PlayerSettings.productName = "The Black Tide - Team 15";
            PlayerSettings.companyName = "Team 15";
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "edu.illinois.cs417.team15.blacktide");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Paths[0]);
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/combined-configured.txt", DateTime.UtcNow.ToString("O"));
            Debug.Log("TEAM15_CONFIGURE_PASS");
        }

        static T[] All<T>() where T : Component => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
        static GameObject Named(string name) => All<Transform>().FirstOrDefault(t => t.name == name)?.gameObject;
        static void ExportPlayer()
        {
            EditorSceneManager.OpenScene(Paths[2]);
            var source = All<PiratePlayer>().Single();
            var copy = Object.Instantiate(source.gameObject);
            copy.name = "Shared Player";
            copy.transform.SetParent(null, true);
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.transform.localScale = Vector3.one;
            var rig = copy.GetComponent<PiratePlayer>();
            rig.body.minMoveDistance = 0f;
            copy.GetComponent<CabinDesktop>().enableHandReach = true;
            rig.interiorAnchor = rig.exteriorAnchor = null;
            var managerGO = new GameObject("Shared XR Interaction Manager");
            managerGO.transform.SetParent(copy.transform, false);
            var manager = managerGO.AddComponent<XRInteractionManager>();
            foreach (var interactor in copy.GetComponentsInChildren<XRBaseInteractor>(true))
            {
                interactor.interactionManager = manager;
                interactor.interactionLayers = -1;
                if (interactor is XRBaseInputInteractor input)
                {
                    string side = interactor.transform.IsChildOf(rig.leftHand) ? "Left" : "Right";
                    input.activateInput = new XRInputButtonReader("Use", "Use value", false, XRInputButtonReader.InputSourceMode.InputActionReference)
                    {
                        inputActionReferencePerformed = AssetDatabase.LoadAssetAtPath<InputActionReference>("Assets/MP1B/Generated/" + side + "Select.asset"),
                        inputActionReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionReference>("Assets/MP1B/Generated/" + side + "SelectValue.asset")
                    };
                    input.allowHoveredActivate = interactor is XRRayInteractor;
                }
            }
            foreach (var direct in copy.GetComponentsInChildren<XRDirectInteractor>(true))
            {
                direct.gameObject.tag = "PlayerHand";
                foreach (var collider in direct.GetComponentsInChildren<Collider>(true)) collider.gameObject.tag = "PlayerHand";
            }
            PrefabUtility.SaveAsPrefabAsset(copy, PlayerPath);
            Object.DestroyImmediate(copy);
            // Leave the source scene intact until its integration pass.
        }

        static void RemoveLocalPlayers()
        {
            foreach (var origin in All<XROrigin>())
            {
                var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(origin.gameObject);
                if (outer) PrefabUtility.UnpackPrefabInstance(outer, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Object.DestroyImmediate(origin.gameObject);
            }
            foreach (var manager in All<XRInteractionManager>()) Object.DestroyImmediate(manager);
            foreach (var input in All<InputActionManager>()) Object.DestroyImmediate(input);
            foreach (var listener in All<AudioListener>()) Object.DestroyImmediate(listener);
            foreach (var camera in All<Camera>())
            {
                if (camera.targetTexture)
                {
                    if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) { data.allowXRRendering = false; data.SetRenderer(0); }
                    continue;
                }
                if (camera.CompareTag("MainCamera")) camera.gameObject.SetActive(false);
            }
            var debugHand = Named("Debughand"); if (debugHand) debugHand.SetActive(false);
            var quit = Named("QuitManager"); if (quit) Object.DestroyImmediate(quit);
        }

        static void ConfigureRoom(int index)
        {
            EditorSceneManager.OpenScene(Paths[index]);
            if (All<TeamRoom>().Length > 0) { Debug.Log("Preserving configured room: " + Paths[index]); return; }
            RemoveLocalPlayers();
            Transform environment = null;
            if (index == 3)
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                environment = new GameObject("Navigation environment - human scale").transform;
                foreach (var root in roots) root.transform.SetParent(environment, true);
                environment.localScale = Vector3.one * .35f;
            }
            var game = new GameObject("Team 15 - " + TeamSession.Names[index]);
            var room = game.AddComponent<TeamRoom>(); room.roomIndex = index;
            room.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var spawnGO = SceneKit.Group("Arrival", game.transform);
            room.spawn = spawnGO.AddComponent<CabinSpawn>(); room.spawn.spawnId = "Arrival";
            Vector3 entry = index == 0 ? new Vector3(3.4f,4.72f,-2.2f)
                : index == 1 ? new Vector3(-2.5f,.06f,.5f)
                : index == 2 ? new Vector3(2.6f,.12f,-5.8f) : new Vector3(0,.10f,.9f);
            spawnGO.transform.SetPositionAndRotation(entry, Quaternion.Euler(0, index == 1 ? 90 : index == 2 ? -20 : index == 3 ? 180 : 0, 0));
            if (index == 0) ConfigureDeck(room);
            if (index == 1) ConfigureStorage(room);
            if (index == 2) ConfigureCaptain(room);
            if (index == 3) ConfigureNavigation(room, environment);
            foreach (var grab in All<XRGrabInteractable>())
            {
                if (grab.TryGetComponent<Rigidbody>(out var rb)) rb.mass = Mathf.Clamp(rb.mass, .1f, 1.5f);
                // Use the persistent manager selected when the shared rig starts.
                grab.interactionManager = null;
            }
            foreach (var interactable in All<XRSimpleInteractable>()) interactable.interactionManager = null;
            foreach (var body in All<Rigidbody>())
            {
                if (body.gameObject.isStatic) body.gameObject.isStatic = false;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            DumpRoom(index);
        }
        static void ConfigureDeck(TeamRoom room)
        {
            room.deck = All<Deck.LockManager>().Single();
            var keyMap = new Dictionary<string,string> { {"Lock2","Cannon Key"}, {"Lock2 (1)","Box Key"}, {"Lock2 (2)","Coin Key"} };
            foreach (var detector in All<Deck.LockDetector>())
            {
                detector.expectedKey = Named(keyMap[detector.name]);
                string label = keyMap[detector.name].Replace(" Key", "").ToUpperInvariant();
                var sign = Sign(label + " KEY", detector.transform.position + new Vector3(0,.42f,-.08f), 180, new Vector2(.65f,.22f));
                sign.transform.root.SetParent(room.transform, true);
            }
            var boxKey = Named("Box Key"); if (boxKey && boxKey.transform.position.y < 4.8f)
                boxKey.transform.position = new Vector3(boxKey.transform.position.x, 5.1f, boxKey.transform.position.z);
            foreach (var teleport in All<Deck.HoverTeleport>()) teleport.xrOrigin = null;
            MakeExit(room, new Vector3(3.238f,6.1f,-5.95f), new Vector3(3.2f,3f,.7f),
                new Vector3(3.238f,8.2f,-5.55f), 180);
        }
        static void ConfigureStorage(TeamRoom room)
        {
            room.lantern = All<Storage.LanternLock>().Single();
            room.mirror = All<Storage.MirrorLock>().Single();
            room.storageExit = All<Storage.ExitLockZone>().Single();
            var mirrorGrab = Named("Mirror").GetComponent<XRGrabInteractable>();
            room.mirror.handMirror = mirrorGrab;
            All<Storage.MirrorReachZone>().Single().handMirrorGrab = mirrorGrab;
            room.storageExit.requiredLantern = room.lantern;
            room.storageExit.requiredMirror = room.mirror;
            MakeExit(room, new Vector3(16.8f,1.3f,-6.4f), new Vector3(2.7f,2.7f,.65f),
                new Vector3(16.7f,3f,-5.9f), 180);
        }
        static void ConfigureCaptain(TeamRoom room)
        {
            room.captain = All<CabinRoom>().Single();
            var door = Named("Captain's cabin exit");
            var hinge = SceneKit.Group("Cabin exit hinge", room.transform, new Vector3(1.85f,0,-7.03f)).transform;
            door.transform.SetParent(hinge, true); room.captainDoor = hinge;
            room.captainDoorColliders = door.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
            foreach (var transition in All<CabinTransition>())
            {
                transition.targetScene = TeamSession.Scenes[3]; transition.targetSpawn = "Arrival";
            }
            // The opening is still a wall-mounted portal; walking into the open doorway starts travel.
            MakeExit(room, new Vector3(2.7f,1.25f,-7.04f), new Vector3(1.35f,2.5f,.42f),
                new Vector3(2.7f,3.05f,-6.82f), 180);
            foreach (var text in door.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.text.Contains("THEN PRESS")) text.text = "GOLD KEY → KEYHOLE\nTHEN WALK THROUGH THE EXIT";
                if (text.text.Contains("TO THE SHIP")) text.text = "<b>NAVIGATION ROOM</b>\nFind the gold key\ninside the captain's chest.";
            }
        }
        static void ConfigureNavigation(TeamRoom room, Transform environment)
        {
            SetNavigationLighting(environment);
            HideLegacyNavigationInstructions(environment);
            room.navigation = All<ActionHandler>().Single();
            room.navigation.roomSpace = environment;
            room.navigation.quitAction = room.navigation.lightAction = room.navigation.interactAction = room.navigation.spawnShipAction = room.navigation.teleportToWinAction = null;
            foreach (var door in All<KeyLockDoor>())
            {
                door.lockIndex = Mathf.Clamp(door.levelAfterUnlock - 2, 0, 2);
                door.requiredKeyId = new[] { "map", "bucket", "final" }[door.lockIndex];
                door.puzzleHandler = room.navigation;
            }
            if (room.navigation.key2InBucket) EnsureKey(room.navigation.key2InBucket).keyId = "bucket";
            if (room.navigation.finalKey) EnsureKey(room.navigation.finalKey).keyId = "final";
            if (room.navigation.keyPrefab)
            {
                var path = AssetDatabase.GetAssetPath(room.navigation.keyPrefab);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                EnsureKey(prefab).keyId = "map";
                PrefabUtility.SaveAsPrefabAsset(prefab,path); PrefabUtility.UnloadPrefabContents(prefab);
            }
            if (room.navigation.winRoomPoint)
            {
                var position = room.navigation.winRoomPoint.position;
                room.navigation.winRoomPoint.position = new Vector3(position.x,.12f,position.z);
            }
            var instructions = Sign("<b>NAVIGATION ROOM</b>\nAim at the map, select a destination, then launch a ship.\nPC: E/click select · F launch · L change light · T victory\nVR: right trigger select · B launch · Y light · X victory",
                new Vector3(0,2.2f,2.4f),0,new Vector2(2.5f,.8f));
            instructions.transform.root.SetParent(room.transform,true);
        }
        static void SetNavigationLighting(Transform environment)
        {
            // The original room uses five 500-intensity point lights. Keep their
            // illumination consistent with the environment's 0.35 uniform scale.
            foreach (var light in environment.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Point) continue;
                light.intensity = 500f * .35f * .35f;
                light.range = 500f * .35f;
            }
        }
        static void HideLegacyNavigationInstructions(Transform environment)
        {
            foreach (var label in environment.GetComponentsInChildren<TMP_Text>(true))
                if (label.text.StartsWith("To Play") && label.text.Contains("Press B to Spawn Ship"))
                    label.gameObject.SetActive(false);
        }
        public static void RepairNavigationPresentation()
        {
            EditorSceneManager.OpenScene(Paths[3]);
            var room = All<TeamRoom>().Single();
            room.spawn.transform.SetPositionAndRotation(new Vector3(0,.1f,.9f),Quaternion.Euler(0,180,0));
            foreach (var label in room.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!label.text.StartsWith("<b>NAVIGATION ROOM</b>")) continue;
                var sign = label.transform;
                while (sign.parent && sign.parent != room.transform) sign = sign.parent;
                sign.SetPositionAndRotation(new Vector3(0,2.2f,2.4f),Quaternion.identity);
            }
            SetNavigationLighting(room.navigation.roomSpace);
            HideLegacyNavigationInstructions(room.navigation.roomSpace);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            DumpRoom(3);
            EditorSceneManager.OpenScene(Paths[0]);
            Debug.Log("TEAM15_NAVIGATION_PRESENTATION_PASS");
        }
        static KeyItem EnsureKey(GameObject root) => root.GetComponentInChildren<KeyItem>(true) ?? root.AddComponent<KeyItem>();
        static void MakeExit(TeamRoom room, Vector3 at, Vector3 size, Vector3 signAt, float yaw)
        {
            var go = SceneKit.Group("Exit to " + TeamSession.Names[room.roomIndex+1],room.transform,at);
            var volume=go.AddComponent<BoxCollider>(); volume.isTrigger=true;volume.size=size;
            var transition=go.AddComponent<CabinTransition>(); transition.requireCabinClear=false;
            transition.targetScene=TeamSession.Scenes[room.roomIndex+1];transition.targetSpawn="Arrival";
            go.AddComponent<TeamExit>().room=room;
            room.exitLabel=Sign("<b>EXIT LOCKED</b>\nComplete this room's puzzles\nand unlock the exit.",signAt,yaw,new Vector2(2f,.65f));
            room.exitLabel.transform.root.SetParent(room.transform,true);
        }
        static TMP_Text Sign(string text, Vector3 at, float yaw, Vector2 size)
        {
            var root=SceneKit.Group("Voyage sign").transform;
            root.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));
            SceneKit.Box("Timber backing",root,new Vector3(0,0,.045f),new Vector3(size.x+.08f,size.y+.08f,.06f),signMaterial);
            return SceneKit.Panel("Route instruction",root,Vector3.zero,size,text,27);
        }
        static void DumpRoom(int index)
        {
            Directory.CreateDirectory("Verification");
            var lines=All<Transform>().Where(t=>t.GetComponent<Collider>() || t.GetComponent<MonoBehaviour>()).Select(t=>
                t.name+" | "+t.position.ToString("F3")+" | scale="+t.lossyScale.ToString("F3")+" | "+string.Join(",",t.GetComponents<Component>().Select(c=>c?c.GetType().FullName:"MISSING")));
            File.WriteAllLines("Verification/room-"+index+"-objects.txt",lines);
        }
        public static void RepairKeyReferences()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(Paths[0]);
            var keyMap = new Dictionary<string,string> { {"Lock2","Cannon Key"}, {"Lock2 (1)","Box Key"}, {"Lock2 (2)","Coin Key"} };
            foreach(var detector in All<Deck.LockDetector>())
            {
                detector.expectedKey=Named(keyMap[detector.name]);
                if(!detector.expectedKey)throw new Exception("Missing assigned Deck key: "+keyMap[detector.name]);
            }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            DumpRoom(0);
            EditorSceneManager.OpenScene(Paths[1]);
            var reveal=All<Storage.MirrorKeyReveal>().Single();
            var realKey=reveal.GetComponent<XRGrabInteractable>();
            if(!realKey) throw new Exception("Restored Storage key is missing XRGrabInteractable");
            All<Storage.ExitLockZone>().Single().exitKey=realKey;
            All<Storage.MirrorReachZone>().Single().realKeyGrab=realKey;
            reveal.realKeyRenderer=reveal.GetComponentInChildren<Renderer>(true);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            DumpRoom(1);
            AssetDatabase.SaveAssets();
            Debug.Log("TEAM15_REFERENCE_REPAIR_PASS");
        }
        public static void RepairDeckLabels()
        {
            EditorSceneManager.OpenScene(Paths[0]);
            var room = All<TeamRoom>().Single();
            foreach (var label in room.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text != "CANNON KEY" && label.text != "BOX KEY" && label.text != "COIN KEY") continue;
                var sign = label.transform;
                while (sign.parent && sign.parent != room.transform) sign = sign.parent;
                sign.rotation = Quaternion.Euler(0, 180, 0);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("TEAM15_DECK_LABELS_PASS");
        }
        [MenuItem("Team 15/Build Windows game")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64,"Builds/Desktop/BlackTideTeam15.exe");
        [MenuItem("Team 15/Open game start")]
        public static void OpenGameStart() => EditorSceneManager.OpenScene(Paths[0]);
        [MenuItem("Team 15/Build Quest game")]
        public static void BuildQuest() => Build(BuildTarget.Android,"Builds/Quest/BlackTideTeam15.apk");
        static void Build(BuildTarget target,string output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(Paths,output,target,BuildOptions.None);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Team15 build failed: "+report.summary.result);
            Debug.Log("TEAM15_BUILD_PASS: "+output+" "+report.summary.totalSize);
        }
    }
}
