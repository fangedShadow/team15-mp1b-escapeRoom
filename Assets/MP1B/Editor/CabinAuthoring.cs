using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackTide;
using BlackTide.MP1B;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using static BlackTide.SceneKit;
using Object = UnityEngine.Object;

namespace BlackTide.MP1B.Editor
{
    // An additive authoring pass over the saved MP1a cabin. Never regenerates its furniture/walls.
    public static class CabinAuthoring
    {
        public const string CabinPath = "Assets/Pirate/Scenes/BlackTideCabin.unity";
        public const string PassagePath = "Assets/MP1B/Scenes/TransitionCabin.unity";
        const string Generated = "Assets/MP1B/Generated";
        static Material walnut, brass, paper, ink, ruby, darkLamp;
        static Transform root;
        static CabinRoom room;
        static Mesh coinMesh;
        static Material coinMaterial;

        [MenuItem("MP1b/1. Upgrade copied cabin")]
        public static void Build()
        {
            if (Path.GetFileName(Path.GetFullPath(Application.dataPath + "/..")).ToLowerInvariant() != "mp1b")
                throw new InvalidOperationException("Run this authoring pass only in C:/UnityProjects/mp1b.");
            EditorSceneManager.OpenScene(CabinPath);
            if (Object.FindAnyObjectByType<CabinRoom>())
            { Debug.Log("MP1b already authored; existing edits preserved. Use Validate or build."); return; }
            Directory.CreateDirectory("Assets/MP1B/Original");
            if (!File.Exists("Assets/MP1B/Original/MP1aCabin.unity"))
                AssetDatabase.CopyAsset(CabinPath, "Assets/MP1B/Original/MP1aCabin.unity");
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory("Assets/MP1B/Scenes");
            AssetDatabase.Refresh();
            var old = Object.FindAnyObjectByType<PirateGame>();
            if (!old || !old.chestLid || old.seals.Length != 3) throw new InvalidOperationException("Saved MP1a cabin is incomplete.");
            var source = old.seals[0];
            coinMesh = source.GetComponent<MeshFilter>().sharedMesh;
            coinMaterial = source.GetComponent<Renderer>().sharedMaterial;
            MakeMaterials();
            root = Group("MP1b - Captain's Orders").transform;
            room = root.gameObject.AddComponent<CabinRoom>();
            room.sound=Sound("Cabin lock feedback",root,new Vector3(0,1,5.8f),"Seal");
            room.socketSound=Resources.Load<AudioClip>("Pirate/Audio/Seal");
            room.chestSound=Resources.Load<AudioClip>("Pirate/Audio/Treasure");
            room.exitSound=Resources.Load<AudioClip>("Pirate/Audio/Click");
            old.enabled = false;
            foreach (var seal in old.seals) Object.DestroyImmediate(seal);
            old.seals = Array.Empty<GameObject>();
            old.chestTreasure.SetActive(false);
            old.chestLid.localRotation = Quaternion.identity;
            room.chestLid = old.chestLid;
            room.chestOpenEuler = new Vector3(105, 0, 0);
            var lidCollision = old.chestLid.gameObject.AddComponent<BoxCollider>();
            lidCollision.center = new Vector3(0, .18f, -.62f);
            lidCollision.size = new Vector3(2.5f, .34f, 1.32f);
            Box("MP1b lid inner lining",old.chestLid,new Vector3(0,.025f,-.62f),new Vector3(2.46f,.10f,1.27f),walnut);
            RemoveLegacyInteractions();
            MakePuzzles();
            MakeChest(old.chestLid.parent);
            MakeExit();
            MakeBoards();
            MakeCollectibles();
            MakeProps();
            ConfigureHands(old.player);
            var desktop = old.player.gameObject.AddComponent<CabinDesktop>();
            desktop.player = old.player;
            var spawn = Group("Spawn - FromPassage", root, new Vector3(2.6f, .05f, -5.8f)).AddComponent<CabinSpawn>();
            spawn.spawnId = "FromPassage";
            spawn.transform.rotation = Quaternion.Euler(0, -20, 0);
            var existingStatus = old.status.transform;
            existingStatus.parent.gameObject.SetActive(false);
            PlayerSettings.productName = "mp1b";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "edu.illinois.mp1.blacktide.mp1b");
            // Allow a linked headset in Play Mode; desktop controls still work when no XR display is running.
            var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (xr) { xr.InitManagerOnStart = true; EditorUtility.SetDirty(xr); }
            Persist(root.gameObject);
            Persist(old.player.gameObject);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), CabinPath);
            MakePassage();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CabinPath, true), new EditorBuildSettingsScene(PassagePath, true) };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(CabinPath);
            Validate();
            Debug.Log("MP1B_AUTHORING_COMPLETE: saved the upgraded cabin and connected passage.");
        }

        public static void PolishExisting()
        {
            if(Path.GetFullPath(Application.dataPath).Replace('\\','/')!="C:/UnityProjects/mp1b/Assets")
                throw new InvalidOperationException("Unexpected project path.");
            EditorSceneManager.OpenScene(CabinPath);
            room=Object.FindAnyObjectByType<CabinRoom>();
            if(!room)throw new InvalidOperationException("Expected already-authored cabin.");
            // Snapshot the exact current scene before changing only identified MP1b objects.
            string snapshot="Assets/MP1B/Original/BeforePolish_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+".unity";
            if(!AssetDatabase.CopyAsset(CabinPath,snapshot))throw new IOException("Cannot snapshot current cabin.");
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Generated}))
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(m.name.EndsWith("MP1b parchment")) {m.shader=Shader.Find("Universal Render Pipeline/Unlit");m.SetColor("_BaseColor",new Color(.78f,.67f,.47f));}
                else if(m.name.EndsWith("MP1b ink")) {m.shader=Shader.Find("Universal Render Pipeline/Unlit");m.SetColor("_BaseColor",new Color(.045f,.055f,.05f));}
                else if(m.name.EndsWith("MP1b aged brass")) {m.SetColor("_BaseColor",new Color(.73f,.49f,.23f));m.SetFloat("_Metallic",.35f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.73f,.49f,.23f)*.28f);}
                else if(m.name.EndsWith("MP1b unlit indicator")) {m.shader=Shader.Find("Universal Render Pipeline/Unlit");m.SetColor("_BaseColor",new Color(.045f,.055f,.037f));}
                EditorUtility.SetDirty(m);
            }
            var log=room.puzzles.Single(p=>p.kind==CabinPuzzleKind.Log);
            log.transform.position=new Vector3(-6.05f,1.95f,-.65f);
            log.reward.transform.SetPositionAndRotation(log.rewardSpawn.position,log.rewardSpawn.rotation);
            foreach(var bottle in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if(bottle.name=="Sea glass bottle"&&Vector3.Distance(bottle.position,new Vector3(-5.4f,.93f,-.5f))<.2f)
                    bottle.position=new Vector3(-4.5f,.93f,-.90f);
            room.goldKeySpawn.localPosition=new Vector3(0,.75f,0);
            room.goldKey.transform.position=room.goldKeySpawn.position;
            room.lampIntensity=.22f;
            foreach(var lamp in room.coinLights)if(lamp)lamp.range=.30f;
            var controls=Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.text.Contains("<b>HOW TO PLAY</b>"));
            if(controls)controls.text="<b>HOW TO PLAY</b>\nWASD - walk\nHold right mouse - look\nE / click - press or grab\nE / click again - release\nG - drop held item\n\nVR: grip - grab\nTrigger - press\n\nThree orders > three coins\nChest > gold key > exit";
            var bells=room.puzzles.Single(p=>p.kind==CabinPuzzleKind.Bell);
            foreach(var b in bells.buttons)
            {
                if(b.visual.Find("Bell shell"))continue;
                var block=b.visual.Find("Brass");var bellMaterial=block.GetComponent<Renderer>().sharedMaterial;
                block.gameObject.SetActive(false);
                var body=Shape("Bell shell",PrimitiveType.Cylinder,b.visual,new Vector3(0,.015f,.02f),new Vector3(.36f,.18f,.36f),bellMaterial);
                Ring("Bell rim",b.visual,new Vector3(0,-.17f,.02f),.205f,.035f,bellMaterial);
                Shape("Bell clapper",PrimitiveType.Sphere,b.visual,new Vector3(0,-.225f,.02f),Vector3.one*.065f,bellMaterial);
                b.indicator=body.GetComponent<Renderer>();
                foreach(Transform child in b.visual)
                {
                    if(child.name.StartsWith("Symbol - ")) {child.localPosition=new Vector3(0,.015f,-.167f);child.localScale=Vector3.one*.22f;}
                    if(child.GetComponent<Canvas>())child.localPosition=new Vector3(0,-.29f,-.19f);
                }
                b.GetComponent<BoxCollider>().size=new Vector3(.46f,.52f,.43f);
            }
            Persist(bells.gameObject);
            if(!room.chestLid.Find("MP1b lid inner lining"))
            {
                var mat=room.chestLid.GetComponentInChildren<Renderer>().sharedMaterial;
                Box("MP1b lid inner lining",room.chestLid,new Vector3(0,.025f,-.62f),new Vector3(2.46f,.10f,1.27f),mat);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("MP1B_POLISH_COMPLETE. Current scene snapshot: "+snapshot);
        }

        static void MakeMaterials()
        {
            walnut = Lit("MP1b dark walnut", new Color(.12f, .055f, .026f), .05f, .24f);
            brass = Lit("MP1b aged brass", new Color(.73f, .49f, .23f), .35f, .4f);
            brass.EnableKeyword("_EMISSION"); brass.SetColor("_EmissionColor",new Color(.73f,.49f,.23f)*.28f);
            paper = Lit("MP1b parchment", new Color(.83f, .74f, .54f), 0, .22f);
            ink = Lit("MP1b ink", new Color(.045f, .067f, .063f), .1f, .2f);
            paper.shader=Shader.Find("Universal Render Pipeline/Unlit");paper.SetColor("_BaseColor",new Color(.78f,.67f,.47f));
            ink.shader=Shader.Find("Universal Render Pipeline/Unlit");ink.SetColor("_BaseColor",new Color(.045f,.055f,.05f));
            ruby = Lit("MP1b ruby", new Color(.65f, .02f, .075f), .32f, .8f, glow:true);
            darkLamp = Lit("MP1b unlit indicator", new Color(.045f, .055f, .037f), .35f, .5f);
            darkLamp.EnableKeyword("_EMISSION"); darkLamp.SetColor("_EmissionColor",Color.black);
        }

        static void RemoveLegacyInteractions()
        {
            foreach (var item in Object.FindObjectsByType<PirateInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item.outline) Object.DestroyImmediate(item.outline);
                var simple = item.GetComponent<XRSimpleInteractable>();
                if (simple) Object.DestroyImmediate(simple);
                // Decorative MP1a objects remain, but their old navigation-seal hints are removed.
                Object.DestroyImmediate(item);
            }
            string[] obsolete = { "Captain's orders", "VR control board", "Desktop controls", "Material comparison explanation", "Orrery instructions", "RESET VOYAGE", "RESET VOYAGE label", "EXTERIOR LOOKOUT", "EXTERIOR LOOKOUT label", "CHANGE LANTERN", "CHANGE LANTERN label", "Chest lock - interact to open", "Ship's bell", "FIRE CANNON brass control" };
            foreach (string name in obsolete)
            {
                var found = GameObject.Find(name);
                if (found) found.SetActive(false);
            }
            // No simulator is required for the desktop workflow.
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb && mb.GetType().Name == "XRDeviceSimulator") mb.gameObject.SetActive(false);
        }

        static TMP_Text Label(string name, Transform parent, Vector3 at, Vector2 size, string text, int font = 32, bool dark = false)
        {
            var t = Panel(name, parent, at, size, text, font);
            t.color = dark ? new Color(.075f, .055f, .03f) : new Color(.97f, .88f, .64f);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            return t;
        }

        static Transform Board(string name, Vector3 at, Vector2 size, float yaw = 0, Transform parent = null)
        {
            var g = Group(name, parent ? parent : root, at).transform;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Box("Walnut frame", g, new Vector3(0, 0, .065f), new Vector3(size.x + .12f, size.y + .12f, .10f), walnut, true);
            Box("Parchment face", g, Vector3.zero, new Vector3(size.x, size.y, .035f), paper);
            return g;
        }

        static CabinButton Button(Transform parent, string title, Vector3 at, Vector2 size, CabinButtonOperation operation, CabinPuzzle puzzle = null, int index = 0, string symbol = null)
        {
            var g = Group(title, parent, at);
            var hit = g.AddComponent<BoxCollider>(); hit.size = new Vector3(size.x, size.y, .13f);
            var face = Group("Pressable face",g.transform);
            var metal = Box("Brass",face.transform,Vector3.zero,new Vector3(size.x,size.y,.11f),brass);
            var b = g.AddComponent<CabinButton>(); b.operation = operation; b.room = room; b.puzzle = puzzle; b.index = index;
            b.prompt = title; b.visual = face.transform; b.indicator = metal.GetComponent<Renderer>();
            b.sound=Sound("Button click",g.transform,Vector3.zero,"Click");
            var simple = g.AddComponent<XRSimpleInteractable>(); simple.colliders.Add(hit);
            if (symbol == null) Label(title + " text", face.transform, new Vector3(0, 0, -.063f), size, title, 24, true);
            else
            {
                Icon(symbol, face.transform, new Vector3(0, .025f, -.071f), Mathf.Min(size.x, size.y) * .52f, ink);
                Label(title + " text", face.transform, new Vector3(0, -size.y*.34f, -.076f), new Vector2(size.x, .20f), title, 17, true);
            }
            return b;
        }

        static void Tray(Transform parent, Vector3 at, float width = .65f)
        {
            Box("Reward tray floor", parent, at, new Vector3(width, .06f, .48f), walnut, true);
            Box("Tray front rim", parent, at + new Vector3(0, .08f, -.24f), new Vector3(width, .13f, .045f), brass, true);
            Box("Tray back rim", parent, at + new Vector3(0, .08f, .24f), new Vector3(width, .13f, .045f), brass, true);
            foreach (int x in new[]{-1,1}) Box("Tray side rim", parent, at + new Vector3(x*(width*.5f-.02f), .08f, 0), new Vector3(.04f,.13f,.48f), brass, true);
        }

        static CabinItem Coin(string id, string symbol, Transform spawn)
        {
            var g = Group(id, root, spawn.position);
            g.transform.rotation = spawn.rotation;
            var face = Group("Original MP1a coin model", g.transform);
            face.AddComponent<MeshFilter>().sharedMesh = coinMesh;
            face.AddComponent<MeshRenderer>().sharedMaterial = coinMaterial;
            face.transform.localScale = new Vector3(.38f,.045f,.38f);
            face.transform.localRotation = Quaternion.Euler(90,0,0);
            Icon(symbol, g.transform, new Vector3(0,0,-.05f), .22f, ink);
            Icon(symbol, g.transform, new Vector3(0,0,.05f), .22f, ink);
            var c = g.AddComponent<BoxCollider>(); c.size = new Vector3(.39f,.39f,.10f);
            return AddItem(g, id, .25f);
        }

        static CabinItem AddItem(GameObject g, string id, float mass)
        {
            var rb = g.AddComponent<Rigidbody>(); rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var grab = g.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.useDynamicAttach = true; grab.throwOnDetach = false;
            grab.colliders.Clear();
            foreach (var c in g.GetComponentsInChildren<Collider>()) if (c.enabled && !c.isTrigger) grab.colliders.Add(c);
            var item = g.AddComponent<CabinItem>(); item.itemId = id; item.ownerId = "captain"; item.grab = grab;
            item.displayName=g.name.Replace("Loose prop - ","").Replace("captain.coin.bell","Bell coin").Replace("captain.coin.log","Quill coin").Replace("captain.coin.seal","Seal coin");
            return item;
        }

        static void MakePuzzles()
        {
            room.puzzles = new CabinPuzzle[3];
            var bell = MakeStation(0, "01  SHIP'S SIGNAL", new Vector3(-6.6f, 1.65f, 1.80f), -90, CabinPuzzleKind.Bell);
            bell.clueTitle = "SHIP'S SIGNAL";
            bell.clueBody = "Replay the signal and listen.\nTry each bell to learn its tone, then repeat the signal.\nTake your time: only the order matters.";
            Label("Bell directions", bell.transform, new Vector3(0,.42f,-.13f), new Vector2(1.85f,.44f), "Replay the signal, then ring the bells.", 28, true);
            bell.buttons = new CabinButton[3];
            string[] names = { "ANCHOR", "WHEEL", "SKULL" };
            string[] icons = { "anchor", "wheel", "skull" };
            for (int i=0;i<3;i++)
            {
                bell.buttons[i] = Button(bell.transform, names[i], new Vector3((i-1)*.6f,0,-.14f), new Vector2(.5f,.52f), CabinButtonOperation.PuzzleInput, bell, i, icons[i]);
                Shape("Bell body", PrimitiveType.Cylinder, bell.transform, new Vector3((i-1)*.6f,.18f,.14f),new Vector3(.32f,.19f,.32f),brass);
            }
            Button(bell.transform,"REPLAY SIGNAL",new Vector3(0,-.47f,-.13f),new Vector2(1.45f,.27f),CabinButtonOperation.Replay,bell);
            ConfigureReward(bell, "captain.coin.bell", "bell", new Vector3(0,-1.10f,-.37f));

            var log = MakeStation(1,"02  CAPTAIN'S LOG",new Vector3(-6.05f,1.95f,-.65f),-90,CabinPuzzleKind.Log);
            log.clueTitle = "CAPTAIN'S LOG";
            log.clueBody = "I signed the log only after locking the chest.\nBefore locking the chest, I counted the gold.\n\nPress the symbols in the order of these events.";
            Label("Log extract",log.transform,new Vector3(0,.38f,-.14f),new Vector2(1.85f,.57f),"I signed the log only after locking the chest.\nBefore locking the chest, I counted the gold.",25,true);
            log.buttons = new CabinButton[3];
            names = new[]{"GOLD","CHEST","QUILL"}; icons = new[]{"coin","chest","quill"};
            for(int i=0;i<3;i++)log.buttons[i]=Button(log.transform,names[i],new Vector3((i-1)*.6f,-.1f,-.14f),new Vector2(.5f,.45f),CabinButtonOperation.PuzzleInput,log,i,icons[i]);
            Button(log.transform,"READ CLUE",new Vector3(0,-.47f,-.13f),new Vector2(1.45f,.27f),CabinButtonOperation.ReadClue,log);
            // Open parchment book on the existing quartermaster's desk.
            foreach(int side in new[]{-1,1}) Box("Open log page",log.transform,new Vector3(side*.25f,-.61f,.05f),new Vector3(.48f,.035f,.38f),paper,false,new Vector3(0,0,side*5));
            var drawer=Group("Sliding reward drawer",log.transform,new Vector3(0,-.84f,.02f)).transform;
            Tray(drawer,Vector3.zero);
            log.revealTransform=drawer; log.revealOpenOffset=new Vector3(0,0,-.50f);
            ConfigureReward(log,"captain.coin.log","quill",new Vector3(0,-.72f,-.49f),false);
            CabinPanelLayoutUpdate.RaisePanel(log);
            foreach(var bottle in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if(bottle.name=="Sea glass bottle"&&Vector3.Distance(bottle.position,new Vector3(-5.4f,.93f,-.5f))<.2f)
                    bottle.position=new Vector3(-4.5f,.93f,-.90f);

            var seal=MakeStation(2,"03  CAPTAIN'S SEAL",new Vector3(6.65f,1.7f,2.1f),90,CabinPuzzleKind.Seal);
            seal.clueTitle="CAPTAIN'S SEAL";
            seal.clueBody="First, leave the captain's mark.\nChoose what keeps the ship still and what guides her course.\nSign again to confirm.";
            Label("Seal verse",seal.transform,new Vector3(0,.35f,-.14f),new Vector2(1.85f,.58f),seal.clueBody,24,true);
            seal.buttons=new CabinButton[4]; names=new[]{"QUILL","ANCHOR","WHEEL","SKULL"};icons=new[]{"quill","anchor","wheel","skull"};
            for(int i=0;i<4;i++)seal.buttons[i]=Button(seal.transform,names[i],new Vector3((i-1.5f)*.47f,-.15f,-.14f),new Vector2(.41f,.44f),CabinButtonOperation.PuzzleInput,seal,i,icons[i]);
            Button(seal.transform,"READ CLUE",new Vector3(0,-.50f,-.13f),new Vector2(1.45f,.25f),CabinButtonOperation.ReadClue,seal);
            var hatch=Group("Reward seal cover",seal.transform,new Vector3(0,-.81f,-.14f));
            Box("Brass seal cover",hatch.transform,Vector3.zero,new Vector3(.6f,.27f,.08f),brass,true);
            Icon("seal",hatch.transform,new Vector3(0,0,-.051f),.21f,ink);
            seal.revealTransform=hatch.transform; seal.revealOpenOffset=new Vector3(.63f,0,0);
            ConfigureReward(seal,"captain.coin.seal","seal",new Vector3(0,-1.03f,-.4f));
            CabinPanelLayoutUpdate.RaisePanel(seal);
        }

        static CabinPuzzle MakeStation(int index,string title,Vector3 at,float yaw,CabinPuzzleKind kind)
        {
            var panel=Board(title,at,new Vector2(2.05f,1.6f),yaw);
            var p=panel.gameObject.AddComponent<CabinPuzzle>(); p.room=room; p.kind=kind;
            p.puzzleName=title;
            p.sound=Sound("Puzzle sound",panel,Vector3.zero,"Bell");
            p.bellTones=new[]{Resources.Load<AudioClip>("Pirate/Audio/Bell"),Resources.Load<AudioClip>("Pirate/Audio/Bell"),Resources.Load<AudioClip>("Pirate/Audio/Bell")};
            p.successSound=Resources.Load<AudioClip>("Pirate/Audio/Seal"); p.errorSound=Resources.Load<AudioClip>("Pirate/Audio/Click");
            CabinPuzzlePolish.ConfigureAudio(p);
            p.feedbackText=Label("Puzzle feedback",panel,new Vector3(0,-.68f,-.14f),new Vector2(1.75f,.26f),"",17,true);
            room.puzzles[index]=p;
            Label("Station title",panel,new Vector3(0,.7f,-.04f),new Vector2(2,.24f),"<b>"+title+"</b>",27,true);
            Label("Press signifier",panel,new Vector3(.77f,-.7f,-.04f),new Vector2(.46f,.19f),"PRESS",17,true);
            return p;
        }

        static void ConfigureReward(CabinPuzzle p,string id,string icon,Vector3 at,bool tray=true)
        {
            var spawn=Group("Earned coin return point",p.transform,at+Vector3.up*.23f).transform;
            p.rewardSpawn=spawn; p.reward=Coin(id,icon,spawn);
            p.reward.gameObject.SetActive(false);
            if(tray)Tray(p.transform,at);
            Label("Reward tray label",p.transform,at+new Vector3(0,-.08f,-.27f),new Vector2(.8f,.24f),"GRAB YOUR COIN",18);
        }

        static void MakeChest(Transform chest)
        {
            room.coinSockets=new CabinSocket[3]; room.coinLightRenderers=new Renderer[3]; room.coinLights=new Light[3];
            string[] icons={"bell","quill","seal"};
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*.73f;
                var s=Group("Coin lock "+(i+1)+" - "+icons[i],chest,new Vector3(x,.69f,-.79f));
                var trigger=s.AddComponent<SphereCollider>();trigger.radius=.255f;trigger.isTrigger=true;
                var socket=s.AddComponent<CabinSocket>(); socket.requiredId=room.puzzles[i].reward.itemId;socket.retainItem=true;
                socket.snapPoint=Group("Locked coin pose",s.transform,new Vector3(0,0,-.035f)).transform;
                Ring("Brass coin recess",s.transform,Vector3.zero,.218f,.026f,brass,new Vector3(90,0,0));
                Shape("Dark socket face",PrimitiveType.Cylinder,s.transform,new Vector3(0,0,.025f),new Vector3(.38f,.015f,.38f),ink,false,new Vector3(90,0,0));
                Icon(icons[i],s.transform,new Vector3(0,0,-.014f),.24f,paper);
                var lamp=Shape("Indicator "+(i+1)+" - visible and unlit",PrimitiveType.Sphere,chest,new Vector3(x,1.07f,-.82f),Vector3.one*.11f,darkLamp);
                room.coinSockets[i]=socket;room.coinLightRenderers[i]=lamp.GetComponent<Renderer>(); var light=lamp.AddComponent<Light>(); light.type=LightType.Point; light.range=.7f; light.intensity=0; room.coinLights[i]=light;
            }
            Label("Chest instruction",chest,new Vector3(0,.23f,-.74f),new Vector2(2.25f,.24f),"MATCH EACH COIN  /  RELEASE INTO ITS SLOT",21);
            var keySpawn=Group("Gold key return point",chest,new Vector3(0,.75f,0)).transform;
            room.goldKeySpawn=keySpawn;
            var g=Group("Captain's gold key",root,keySpawn.position);
            Ring("Key bow",g.transform,new Vector3(0,.16f,0),.10f,.035f,brass,new Vector3(90,0,0));
            Box("Key shaft",g.transform,new Vector3(0,-.07f,0),new Vector3(.055f,.32f,.065f),brass);
            Box("Key bit",g.transform,new Vector3(.055f,-.17f,0),new Vector3(.12f,.08f,.065f),brass);
            var c=g.AddComponent<BoxCollider>();c.size=new Vector3(.29f,.58f,.12f);
            room.goldKey=AddItem(g,"captain.key.gold",.35f); g.SetActive(false);
            var cushion=Box("Gold key cushion",chest,new Vector3(0,.37f,0),new Vector3(.9f,.10f,.7f),paper,true);
        }

        static void MakeExit()
        {
            var door=Board("Captain's cabin exit",new Vector3(2.7f,1.38f,-7.03f),new Vector2(1.8f,2.65f),180);
            Box("Solid cabin door",door,new Vector3(0,0,-.01f),new Vector3(1.6f,2.45f,.14f),walnut,true);
            Label("Door heading",door,new Vector3(0,.76f,-.09f),new Vector2(1.6f,.8f),"<b>TO THE SHIP</b>\nFind the gold key\ninside the captain's chest.",30);
            var lockObject=Group("Gold keyhole",door,new Vector3(-.43f,-.10f,-.19f));
            Shape("Gold lock plate",PrimitiveType.Cylinder,lockObject.transform,Vector3.zero,new Vector3(.35f,.03f,.35f),brass,false,new Vector3(90,0,0));
            Box("Keyhole slit",lockObject.transform,new Vector3(0,0,-.05f),new Vector3(.04f,.15f,.018f),ink);
            var c=lockObject.AddComponent<SphereCollider>();c.radius=.25f;c.isTrigger=true;
            room.exitSocket=lockObject.AddComponent<CabinSocket>();room.exitSocket.requiredId="captain.key.gold";room.exitSocket.retainItem=false;
            room.exitSocket.snapPoint=Group("Key insertion pose",lockObject.transform,new Vector3(0,0,-.07f)).transform;
            room.exitLockVisual=lockObject.transform;
            room.exitUnlockEuler=new Vector3(0,0,90);
            CabinDoorAudioUpdate.Configure(room);
            var b=Button(door,"OPEN DOOR",new Vector3(.30f,-.10f,-.20f),new Vector2(.76f,.35f),CabinButtonOperation.Travel);
            var travel=b.gameObject.AddComponent<CabinTransition>();travel.targetScene="TransitionCabin";travel.targetSpawn="FromCaptain";travel.requireCabinClear=true;b.travel=travel;
            Label("Door key signifier",door,new Vector3(0,-.62f,-.09f),new Vector2(1.65f,.43f),"GOLD KEY → KEYHOLE\nTHEN PRESS THE HANDLE",23);
            Tray(door,new Vector3(-.43f,-.49f,-.27f),.65f);
        }

        static void MakeBoards()
        {
            var b=Board("Voyage progress",new Vector3(-2.9f,1.85f,-6.97f),new Vector2(2.65f,2.3f),180);
            Label("Progress title",b,new Vector3(0,.96f,-.04f),new Vector2(2.6f,.35f),"<b>CAPTAIN'S ORDERS</b>",37,true);
            room.progressText=Label("Live progress",b,new Vector3(0,.1f,-.045f),new Vector2(2.5f,1.28f),"",29,true);
            Button(b,"RECOVER ITEMS",new Vector3(-.65f,-.88f,-.11f),new Vector2(1.17f,.3f),CabinButtonOperation.Recover);
            Button(b,"RESET VOYAGE",new Vector3(.65f,-.88f,-.11f),new Vector2(1.17f,.3f),CabinButtonOperation.Reset);
            var status=Board("Cabin feedback",new Vector3(0,2.1f,6.95f),new Vector2(2.5f,1.25f));
            room.statusText=Label("Feedback text",status,new Vector3(0,0,-.045f),new Vector2(2.45f,1.2f),"Solve the three orders.\nBring their coins to the chest.",32,true);
            var guide=Board("Keyboard and mouse",new Vector3(-5.2f,2.45f,-6.97f),new Vector2(1.6f,1.45f),180);
            Label("Controls",guide,new Vector3(0,0,-.04f),new Vector2(1.55f,1.4f),"<b>HOW TO PLAY</b>\nWASD · walk\nHold right mouse · look\nE / click · press or grab\nE / click again · release\n\nThree orders → three coins\nChest → gold key → exit",25,true);
            var clue=Board("Expanded clue",new Vector3(0,2.0f,3.25f),new Vector2(2.4f,1.6f));
            room.cluePanel=clue.gameObject;
            room.clueText=Label("Readable clue",clue,new Vector3(0,.1f,-.04f),new Vector2(2.25f,1.26f),"",33,true);
            Button(clue,"CLOSE",new Vector3(0,-.62f,-.1f),new Vector2(.8f,.28f),CabinButtonOperation.CloseClue);
            clue.gameObject.SetActive(false);
            // Update the former MP1a subtitle without moving the title sign.
            foreach(var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(t.text.Contains("A VOYAGE AMONG STARS"))t.text="<b>THE BLACK TIDE</b>\nCAPTAIN'S ORDERS  /  ESCAPE THE CABIN";
        }

        static void MakeCollectibles()
        {
            Vector3[] points={new Vector3(-4.2f,1.4f,3.5f),new Vector3(-6.2f,1.48f,-3.3f),new Vector3(5.3f,1.60f,-2),new Vector3(5,1.3f,.1f),new Vector3(4.6f,1.06f,5.7f),new Vector3(-4.5f,1.03f,-4.8f)};
            room.gems=new CabinCollectible[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                var g=Group("Optional ruby "+(i+1),root,points[i]);
                var shape=Shape("Ruby crystal",PrimitiveType.Cube,g.transform,Vector3.zero,Vector3.one*.19f,ruby,false,new Vector3(35,45,25));
                var c=g.AddComponent<SphereCollider>();c.radius=.18f;
                var xr=g.AddComponent<XRSimpleInteractable>();xr.colliders.Add(c);
                var gem=g.AddComponent<CabinCollectible>();gem.room=room;gem.collectibleId="captain.ruby."+i;gem.visual=shape;room.gems[i]=gem;
                gem.sound=Sound("Ruby chime",g.transform,Vector3.zero,"Seal");
            }
        }

        static void MakeProps()
        {
            // Eight distinct silhouettes; mass span 0.25–2kg remains well below 20:1.
            Vector3[] points={new Vector3(-5.9f,1.48f,3.4f),new Vector3(-4.8f,1.48f,4.1f),new Vector3(-3.9f,1.48f,3.6f),new Vector3(-5.7f,1.48f,4.2f),new Vector3(6.4f,1.69f,-.7f),new Vector3(5.3f,1.64f,-2),new Vector3(-5,1.03f,-4.8f),new Vector3(5,1.31f,.1f)};
            string[] names={"Glass bottle","Brass compass","Captain's cup","Closed book","Coiled rope","Pocket lantern","Wooden mallet","Spyglass"};
            float[] masses={.4f,.7f,.5f,.8f,.6f,1.2f,2f,.9f};
            room.looseProps=new CabinItem[8];
            for(int i=0;i<8;i++)
            {
                var g=Group("Loose prop - "+names[i],root,points[i]);var p=g.transform;
                switch(i)
                {
                    case 0:
                        Shape("Bottle body",PrimitiveType.Cylinder,p,Vector3.zero,new Vector3(.18f,.16f,.18f),paper);
                        Shape("Bottle neck",PrimitiveType.Cylinder,p,new Vector3(0,.21f,0),new Vector3(.07f,.08f,.07f),brass);break;
                    case 1:
                        Shape("Compass case",PrimitiveType.Cylinder,p,Vector3.zero,new Vector3(.34f,.04f,.34f),brass);
                        Shape("Compass face",PrimitiveType.Cylinder,p,new Vector3(0,.045f,0),new Vector3(.29f,.01f,.29f),paper);
                        Box("Needle",p,new Vector3(0,.065f,0),new Vector3(.025f,.015f,.22f),ink);break;
                    case 2:
                        Shape("Cup body",PrimitiveType.Cylinder,p,Vector3.zero,new Vector3(.19f,.13f,.19f),brass);
                        Ring("Cup handle",p,new Vector3(.14f,0,0),.09f,.025f,brass,new Vector3(90,0,0));break;
                    case 3:
                        Box("Book pages",p,Vector3.zero,new Vector3(.30f,.09f,.4f),paper);
                        foreach(int side in new[]{-1,1})Box("Leather cover",p,new Vector3(0,side*.055f,0),new Vector3(.32f,.025f,.42f),walnut);break;
                    case 4:
                        for(int j=0;j<3;j++)Ring("Rope coil",p,new Vector3(0,j*.025f,0),.13f+j*.025f,.022f,brass);break;
                    case 5:
                        Box("Lantern glass",p,Vector3.zero,new Vector3(.16f,.23f,.16f),paper);
                        foreach(int side in new[]{-1,1})Box("Lantern cap",p,new Vector3(0,side*.14f,0),new Vector3(.23f,.05f,.23f),ink);
                        Ring("Lantern handle",p,new Vector3(0,.23f,0),.08f,.018f,brass,new Vector3(90,0,0));break;
                    case 6:
                        Box("Mallet handle",p,new Vector3(0,-.06f,0),new Vector3(.06f,.36f,.06f),walnut);
                        Box("Mallet head",p,new Vector3(0,.12f,0),new Vector3(.28f,.14f,.14f),brass);break;
                    case 7:
                        Shape("Spyglass barrel",PrimitiveType.Cylinder,p,Vector3.zero,new Vector3(.12f,.26f,.12f),brass,false,new Vector3(90,0,0));
                        Shape("Spyglass eyepiece",PrimitiveType.Cylinder,p,new Vector3(0,0,-.26f),new Vector3(.07f,.06f,.07f),ink,false,new Vector3(90,0,0));break;
                }
                var bounds=new Bounds(p.position,Vector3.zero);foreach(var r in g.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
                var c=g.AddComponent<BoxCollider>();c.center=bounds.center-p.position;c.size=bounds.size+Vector3.one*.015f;
                room.looseProps[i]=AddItem(g,"captain.prop."+i,masses[i]);
            }
        }

        static void ConfigureHands(PiratePlayer player)
        {
            foreach(var hand in new[]{player.leftHand,player.rightHand})
            {
                string side=hand==player.leftHand?"Left":"Right";
                var directObject=Group(side+" direct grip",hand);
                var volume=directObject.AddComponent<SphereCollider>();volume.radius=.095f;volume.isTrigger=true;
                var body=directObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
                var direct=directObject.AddComponent<XRDirectInteractor>();
                CabinXRSetup.ConfigureGrip(direct, side);
            }
        }

        static void MakePassage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            root=Group("Connected ship passage").transform;room=null;
            Box("Passage floor",root,new Vector3(0,-.15f,0),new Vector3(6,.3f,7),walnut,true);
            foreach(int side in new[]{-1,1})Box("Passage wall",root,new Vector3(side*3,1.8f,0),new Vector3(.15f,3.6f,7),walnut,true);
            Box("Passage back",root,new Vector3(0,1.8f,3.5f),new Vector3(6,3.6f,.15f),walnut,true);
            Box("Passage front",root,new Vector3(0,1.8f,-3.5f),new Vector3(6,3.6f,.15f),walnut,true);
            var light=Group("Passage warm light",root,new Vector3(0,3,0)).AddComponent<Light>();light.type=LightType.Point;light.range=12;light.intensity=9;light.color=new Color(1,.8f,.5f);
            var spawn=Group("Spawn - FromCaptain",root,new Vector3(0,.05f,-1.8f)).AddComponent<CabinSpawn>();spawn.spawnId="FromCaptain";
            var panel=Board("Return to captain",new Vector3(0,1.8f,3.32f),new Vector2(2.8f,1.7f));
            Label("Passage sign",panel,new Vector3(0,.24f,-.04f),new Vector2(2.7f,1.1f),"<b>THE SHIP'S PASSAGE</b>\nYour carried items travel with you.\nReturn to the captain's cabin.",35,true);
            var b=Button(panel,"RETURN TO CABIN",new Vector3(0,-.48f,-.12f),new Vector2(2,.34f),CabinButtonOperation.Travel);
            b.travel=b.gameObject.AddComponent<CabinTransition>();b.travel.targetScene="BlackTideCabin";b.travel.targetSpawn="FromPassage";b.travel.requireCabinClear=false;
            Persist(root.gameObject);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),PassagePath);
            PrefabUtility.SaveAsPrefabAsset(b.gameObject,"Assets/MP1B/Generated/SceneDoor.prefab");
        }

        static void Icon(string symbol,Transform parent,Vector3 at,float size,Material material)
        {
            var g=Group("Symbol - "+symbol,parent,at).transform;g.localScale=Vector3.one*size;
            void Stroke(float x,float y,float w,float h,float angle=0)=>Box("Ink",g,new Vector3(x,y,0),new Vector3(w,h,.035f),material,false,new Vector3(0,0,angle));
            void Circle(float x,float y,float r)=>Ring("Ink circle",g,new Vector3(x,y,0),r,.045f,material,new Vector3(90,0,0));
            switch(symbol)
            {
                case "anchor":Circle(0,.37f,.10f);Stroke(0,0,.075f,.64f);Stroke(0,.2f,.43f,.075f);Stroke(-.20f,-.25f,.075f,.35f,-50);Stroke(.20f,-.25f,.075f,.35f,50);break;
                case "wheel":Circle(0,0,.31f);for(int i=0;i<4;i++)Stroke(0,0,.065f,.94f,i*45);Circle(0,0,.085f);break;
                case "skull":Shape("Skull",PrimitiveType.Sphere,g,new Vector3(0,.08f,0),new Vector3(.60f,.55f,.07f),material);Stroke(0,-.25f,.34f,.19f);foreach(int x in new[]{-1,1})Shape("Eye",PrimitiveType.Sphere,g,new Vector3(x*.14f,.09f,-.05f),Vector3.one*.13f,paper);break;
                case "quill":Stroke(0,0,.065f,1,-28);for(int i=0;i<5;i++)Stroke(-.05f+i*.045f,-.12f+i*.11f,.32f,.055f,-10);break;
                case "bell":Shape("Bell crown",PrimitiveType.Sphere,g,new Vector3(0,.06f,0),new Vector3(.55f,.61f,.09f),material);Stroke(0,-.23f,.76f,.10f);Circle(0,.4f,.09f);Shape("Clapper",PrimitiveType.Sphere,g,new Vector3(0,-.36f,0),Vector3.one*.14f,material);break;
                case "coin":Circle(0,0,.38f);Stroke(0,0,.07f,.5f);Stroke(0,.18f,.30f,.065f);Stroke(0,-.18f,.30f,.065f);break;
                case "chest":Stroke(0,-.06f,.80f,.5f);Stroke(0,.28f,.82f,.12f);Box("Chest latch",g,new Vector3(0,-.01f,-.04f),new Vector3(.09f,.2f,.04f),paper);break;
                case "seal":Circle(0,.06f,.30f);Stroke(-.17f,-.32f,.16f,.35f,-20);Stroke(.17f,-.32f,.16f,.35f,20);Stroke(0,.06f,.27f,.06f,45);Stroke(0,.06f,.27f,.06f,-45);break;
            }
        }

        static void Persist(GameObject target)
        {
            Directory.CreateDirectory(Generated);AssetDatabase.Refresh();
            int n=0;
            void Save(Object o)
            {
                if(!o||AssetDatabase.Contains(o)||!(o is Mesh||o is Material||o is InputActionReference))return;
                string safe=string.Join("_",o.name.Split(Path.GetInvalidFileNameChars()));
                AssetDatabase.CreateAsset(o,AssetDatabase.GenerateUniqueAssetPath(Generated+"/"+(n++)+"_"+safe+".asset"));
            }
            foreach(var mf in target.GetComponentsInChildren<MeshFilter>(true))Save(mf.sharedMesh);
            foreach(var r in target.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)Save(m);
        }

        [MenuItem("MP1b/2. Validate authored scene")]
        public static void Validate()
        {
            var r=Object.FindAnyObjectByType<CabinRoom>();
            if(!r)throw new InvalidOperationException("Open the MP1b cabin first.");
            if(r.puzzles.Length!=3||r.coinSockets.Length!=3||r.coinLights.Length!=3||r.gems.Length!=6)throw new InvalidOperationException("Incomplete puzzle/lock/collectible counts.");
            foreach(var p in r.puzzles)if(!p.reward||!p.rewardSpawn||p.buttons.Length<3)throw new InvalidOperationException("Incomplete puzzle "+p.name);
            if(!r.goldKey||!r.exitSocket||!r.chestLid)throw new InvalidOperationException("Incomplete final key/exit chain.");
            var ids=new HashSet<string>();
            foreach(var item in Object.FindObjectsByType<CabinItem>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!ids.Add(item.itemId)||!item.GetComponent<Rigidbody>()||!item.GetComponent<XRGrabInteractable>())throw new InvalidOperationException("Invalid key/prop "+item.name);
            }
            if(Object.FindObjectsByType<XRDirectInteractor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=2)throw new InvalidOperationException("Both direct hands required.");
            Debug.Log("MP1B_STATIC_VALIDATION_PASS: 3 puzzles, 3 coin locks, 4 keys, 6 rubies, 8 props, 2 direct hands, 2 build scenes. Headset evidence is separate.");
        }

        [MenuItem("MP1b/3. Build desktop preview")]
        public static void BuildDesktop()=>BuildPlayer(BuildTarget.StandaloneWindows64,"Builds/Desktop/mp1b.exe");
        // Separate output permits a feedback update while the user is playing the previous build.
        public static void BuildDesktopBellFeedback()=>BuildPlayer(BuildTarget.StandaloneWindows64,"Builds/DesktopBellFeedback/mp1b.exe");
        [MenuItem("MP1b/4. Build Quest APK")]
        public static void BuildQuest()=>BuildPlayer(BuildTarget.Android,"Builds/Quest/mp1b.apk");
        static void BuildPlayer(BuildTarget target,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{CabinPath,PassagePath},locationPathName=path,target=target,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Build failed: "+report.summary.result);
            Debug.Log("MP1B_BUILD_PASS: "+path+" ("+report.summary.totalSize+" bytes)");
        }
    }
}
