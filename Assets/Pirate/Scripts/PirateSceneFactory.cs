using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using Unity.XR.CoreUtils;
using static BlackTide.SceneKit;

namespace BlackTide
{
    public static class PirateSceneFactory
    {
        static Material wood, darkWood, brass, iron, navy, cream, aqua, red, goldGlow, rope, particle, outline;
        static PirateGame game;
        static Transform world, fx;
        static InputActionAsset actions;
        static readonly List<GameObject> seals = new List<GameObject>();

        public static GameObject Build()
        {
            seals.Clear();
            var root = Group("BLACK TIDE - Captain's Cabin");
            root.SetActive(false); world = root.transform;
            game = Group("Game - treasure quest and controller actions", world).AddComponent<PirateGame>();
            fx = Group("Spatial sound and particle feedback", world).transform;
            actions = Resources.Load<InputActionAsset>("Pirate/PirateControls");
            if (!actions) throw new System.InvalidOperationException("PirateControls.inputactions is missing or has not imported yet.");
            game.inputs = actions;
            MakeMaterials();
            MakeRoom();
            MakeLighting();
            MakeOrrery();
            MakeFurniture();
            MakeChest();
            MakeSigns();
            MakeExterior();
            MakeSignal();
            MakeRig();
            game.seals = seals.ToArray();
            root.SetActive(true);
            return root;
        }

        static void MakeMaterials()
        {
            wood = Lit("Walnut - plank base and normal", Color.white, .02f, .24f, "Wood_Base", new Vector2(2f, 2f), "Wood_Normal");
            darkWood = Lit("Dark walnut beams", new Color(.19f,.095f,.048f), .05f, .28f);
            brass = Lit("Aged nautical brass", new Color(.63f,.4f,.11f), .78f, .7f);
            iron = Lit("Forged iron", new Color(.095f,.12f,.14f), .72f, .42f);
            navy = Lit("Flat navy - no texture maps", new Color(.025f,.09f,.13f), .0f, .2f);
            cream = Lit("Canvas and parchment", new Color(.82f,.71f,.46f), 0f, .15f);
            aqua = Lit("Sea glass", new Color(.06f,.64f,.67f), .3f, .75f, glow:true);
            red = Lit("Crimson signal glass", new Color(.86f,.1f,.13f), .2f, .6f, glow:true);
            goldGlow = Lit("Amber lantern glass", new Color(1f,.57f,.12f), .15f, .6f, glow:true);
            rope = Lit("Hemp rope", new Color(.46f,.34f,.18f), 0f, .18f);
            var ps = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!ps) ps = Shader.Find("Sprites/Default");
            particle = new Material(ps) { name = "Golden feedback particles" };
            particle.SetColor("_BaseColor", new Color(1f,.75f,.2f));
            particle.SetColor("_Color", new Color(1f,.75f,.2f));
            outline = new Material(Shader.Find("BlackTide/InteractionOutline")) { name = "Brass interaction outline" };
            // Keep the provided sky13 faces from the assignment; outside the room they are clearly visible.
            var sky = new Material(Shader.Find("Skybox/6 Sided")) { name = "Course sky13 - maritime lookout sky" };
            string[] faces = { "FR", "BK", "LF", "RT", "UP", "DN" };
            string[] slots = { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex" };
            for (int i=0;i<faces.Length;i++) sky.SetTexture(slots[i],Resources.Load<Texture2D>("Pirate/Textures/sky13_"+faces[i]));
            sky.SetFloat("_Exposure", .85f); sky.SetFloat("_Rotation", 35f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.22f,.27f,.31f);
            RenderSettings.fog = false;
        }

        static void RoomPlane(string name, Vector3 at, Vector3 rotation, Material material)
        {
            var p = Shape(name, PrimitiveType.Plane, world, at, new Vector3(1.5f,1f,1.5f), material, false, rotation);
            // The six inward-facing 15 m planes also receive two-sided box collision.
            var c = p.AddComponent<BoxCollider>(); c.size = new Vector3(10f,.16f,10f);
        }
        static void MakeRoom()
        {
            bool course = Resources.Load<Texture2D>("Pirate/Textures/tile") && Resources.Load<Texture2D>("Pirate/Textures/tile-normal");
            string texture = course ? "tile" : "Wood_Base", normal = course ? "tile-normal" : "Wood_Normal";
            var wall1 = Lit("Wall 1 - base normal - tiling 1 - matte", Color.white, .05f,.12f,texture,Vector2.one,normal);
            var wall2 = Lit("Wall 2 - base normal - tiling 4 - polished metal", Color.white,.7f,.86f,texture,new Vector2(4f,4f),normal);
            RoomPlane("Room Floor - 15 x 15 m", Vector3.zero, Vector3.zero, wood);
            RoomPlane("Room Ceiling - height 15 m", new Vector3(0,15,0), new Vector3(180,0,0), darkWood);
            RoomPlane("Wall 1 NORTH - same base and normal maps", new Vector3(0,7.5f,7.5f), new Vector3(-90,0,0), wall1);
            RoomPlane("Wall 2 EAST - distinct tiling and material properties", new Vector3(7.5f,7.5f,0),new Vector3(0,0,90),wall2);
            RoomPlane("Wall 3 SOUTH - flat navy material", new Vector3(0,7.5f,-7.5f),new Vector3(90,0,0),navy);
            RoomPlane("Wall 4 WEST - walnut", new Vector3(-7.5f,7.5f,0),new Vector3(0,0,-90),wood);
            var structure = Group("Timber framing", world).transform;
            for (int i = -3; i <= 3; i++)
            {
                float z = i * 2.35f;
                Box("Port rib " + i, structure,new Vector3(-7.24f,7.5f,z),new Vector3(.32f,15f,.3f),darkWood);
                Box("Starboard rib " + i,structure,new Vector3(7.24f,7.5f,z),new Vector3(.32f,15f,.3f),darkWood);
                Box("Roof beam " + i,structure,new Vector3(0,14.7f,z),new Vector3(14.7f,.4f,.4f),darkWood);
                Box("Upper crossbeam " + i,structure,new Vector3(0,8.3f,z),new Vector3(14.7f,.28f,.25f),darkWood);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Box("Lower side rail",structure,new Vector3(s*7.18f,1f,0),new Vector3(.28f,.25f,15),brass);
                Box("Upper side rail",structure,new Vector3(s*7.18f,3.5f,0),new Vector3(.28f,.25f,15),darkWood);
                Box("Aft / fore skirting",structure,new Vector3(0,.2f,s*7.2f),new Vector3(14.5f,.4f,.2f),darkWood);
            }
            // Nautical rug leaves a clear path around the orrery's solid plinth.
            Box("Navy navigation rug",structure,new Vector3(0,.015f,0),new Vector3(7f,.025f,7f),navy);
            Ring("Compass rose rim",structure,new Vector3(0,.043f,0),3.3f,.025f,brass);
            for (int i = 0; i < 8; i++)
            {
                float a = i*45f*Mathf.Deg2Rad;
                Box("Compass tick",structure,new Vector3(Mathf.Sin(a)*3f,.065f,Mathf.Cos(a)*3f),new Vector3(.08f,.03f,.45f),brass,false,new Vector3(0,i*45,0));
            }
        }

        static void MakeLighting()
        {
            var lamp = Group("Ceiling point light - rubric",world,new Vector3(0,14.8f,0)).AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 38f; lamp.intensity = 80f;
            lamp.color = game.lightColors[0]; lamp.shadows = LightShadows.Soft;
            game.ceilingLight = lamp;
            var probe = Group("Cabin reflection probe",world,new Vector3(0,4f,0)).AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128; probe.size = new Vector3(15f,15f,15f);
            probe.boxProjection = true; probe.nearClipPlane = .3f; probe.farClipPlane = 35f;
            var chandelier = Group("Hanging brass chandelier",world,new Vector3(0,6.9f,0)).transform;
            Ring("Brass chandelier hoop",chandelier,Vector3.zero,1.6f,.08f,brass);
            Shape("Suspension chain",PrimitiveType.Cylinder,chandelier,new Vector3(0,3.7f,0),new Vector3(.07f,3.7f,.07f),iron);
            for (int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3f;
                Lantern(new Vector3(Mathf.Cos(a)*1.6f,6.7f,Mathf.Sin(a)*1.6f),"Chandelier lantern",false);
            }
            for (int s=-1;s<=1;s+=2)
            {
                Lantern(new Vector3(s*6.8f,2.5f,-3.8f),"Entry lantern",true);
                Lantern(new Vector3(s*6.8f,2.5f,4.8f),"Cargo lantern",true);
            }
            var crystal = Shape("Color indicator on orrery",PrimitiveType.Sphere,world,new Vector3(0,1.35f,0),Vector3.one*.4f,goldGlow);
            game.lightCrystal = crystal.GetComponent<Renderer>();
            game.lightMaterials = new[] { goldGlow,aqua,red };
        }
        static void Lantern(Vector3 at, string name, bool addLight)
        {
            var g=Group(name,world,at).transform;
            Shape("Warm lantern glass",PrimitiveType.Cylinder,g,Vector3.zero,new Vector3(.25f,.27f,.25f),goldGlow);
            Shape("Lantern cap",PrimitiveType.Cylinder,g,new Vector3(0,.31f,0),new Vector3(.38f,.06f,.38f),iron);
            Shape("Lantern foot",PrimitiveType.Cylinder,g,new Vector3(0,-.31f,0),new Vector3(.38f,.06f,.38f),iron);
            Ring("Lantern handle",g,new Vector3(0,.48f,0),.15f,.025f,brass,new Vector3(90,0,0));
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI/2;
                Box("Iron cage bar",g,new Vector3(Mathf.Cos(a)*.14f,0,Mathf.Sin(a)*.14f),new Vector3(.025f,.58f,.025f),iron);
            }
            if(addLight)
            {
                var l=g.gameObject.AddComponent<Light>();l.type=LightType.Point;l.range=7f;l.intensity=4f;
                l.color=new Color(1f,.7f,.36f);l.shadows=LightShadows.None;
            }
        }

        static void MakeOrrery()
        {
            var group=Group("NAVIGATOR'S ORRERY - planet moon and gravity comet",world).transform;
            Shape("Orrery plinth",PrimitiveType.Cylinder,group,new Vector3(0,.45f,0),new Vector3(2.5f,.45f,2.5f),darkWood,true);
            Shape("Orrery gold rim",PrimitiveType.Cylinder,group,new Vector3(0,.93f,0),new Vector3(2.6f,.035f,2.6f),brass);
            Shape("Brass meridian stand",PrimitiveType.Cylinder,group,new Vector3(0,1.5f,0),new Vector3(.16f,.55f,.16f),brass);
            Material earth=Lit("Cartographer's planet",Color.white,.3f,.48f,"Planet",Vector2.one);
            var planet=Shape("Planet - rotates with delta time",PrimitiveType.Sphere,group,new Vector3(0,3.5f,0),Vector3.one*2f,earth);
            planet.AddComponent<OrbitalMotion>();
            Shape("Moon - child of planet at local X 2",PrimitiveType.Sphere,planet.transform,new Vector3(2,0,0),Vector3.one*.22f,cream);
            Ring("Brass meridian",group,new Vector3(0,3.5f,0),1.3f,.045f,brass,new Vector3(75,0,18));
            Ring("Moon orbit guide",group,new Vector3(0,3.5f,0),4f,.015f,brass);
            var comet=Shape("Comet - velocity and gravitational acceleration",PrimitiveType.Sphere,group,new Vector3(2.8f,3.5f,0),Vector3.one*.2f,aqua);
            var gravity=comet.AddComponent<GravityComet>();gravity.attractor=planet.transform;game.comet=gravity;
            var tr=comet.AddComponent<TrailRenderer>();tr.time=2.5f;tr.startWidth=.07f;tr.endWidth=0f;
            tr.sharedMaterial=particle;tr.startColor=new Color(.3f,1f,1f);tr.endColor=new Color(.1f,.4f,.6f,0);
        }

        static void MakeFurniture()
        {
            var chart=Table(new Vector3(-4.8f,0,3.7f),"Captain's chart table",new Vector3(3.4f,1.15f,2f));
            var mapMat=Lit("Captain's chart - original illustrated texture",Color.white,0f,.1f,"TreasureMap",Vector2.one);
            var map=Box("Treasure map",chart,new Vector3(0,1.24f,0),new Vector3(2.2f,.035f,1.5f),mapMat,true);
            AddInteraction(map,PirateAction.Map);
            // A separate quad gives the map a readable top projection instead of cube side UVs.
            Shape("Readable chart surface",PrimitiveType.Quad,chart,new Vector3(0,1.262f,0),new Vector3(2.2f,1.5f,1),mapMat,false,new Vector3(90,0,0));
            Seal(new Vector3(-5.3f,1.65f,3.4f),0);
            var compass=Group("Brass desk compass",chart,new Vector3(.95f,1.3f,.45f)).transform;
            Shape("Compass case",PrimitiveType.Cylinder,compass,Vector3.zero,new Vector3(.5f,.05f,.5f),brass);
            Shape("Compass face",PrimitiveType.Cylinder,compass,new Vector3(0,.055f,0),new Vector3(.42f,.01f,.42f),cream);
            Box("Compass needle",compass,new Vector3(0,.08f,0),new Vector3(.04f,.02f,.3f),red,false,new Vector3(0,25,0));
            for(int i=0;i<3;i++)Box("Captain's log book "+i,chart,new Vector3(-1.3f,1.27f+i*.12f,.45f),new Vector3(.4f,.1f,.55f),i%2==0?navy:red);
            Bottle(new Vector3(-6f,1.25f,3.1f));
            Chair(new Vector3(-4.7f,0,5.3f),180);
            Chair(new Vector3(-6.65f,0,3.7f),90);

            Barrel(new Vector3(5.3f,0,-2f),1f);
            Barrel(new Vector3(6.4f,0,-.7f),1.05f);
            Barrel(new Vector3(5f,0,.1f),.8f);
            Barrel(new Vector3(-6.25f,0,-3.3f),.9f);
            Seal(new Vector3(5.3f,1.8f,-2f),1);
            Crate(new Vector3(-5f,.5f,-4.8f),Vector3.one);
            Crate(new Vector3(-6.1f,.6f,-5.2f),Vector3.one*1.2f);
            Crate(new Vector3(-6.1f,1.65f,-5.2f),Vector3.one*.85f);
            var mess=Table(new Vector3(-5.3f,0,-.4f),"Quartermaster's desk",new Vector3(2.5f,.85f,1.25f));
            Bottle(new Vector3(-5.4f,.93f,-.5f));
            Chair(new Vector3(-5.3f,0,-1.7f),0);
            for(int i=0;i<3;i++)Ring("Coiled rope "+i,world,new Vector3(6f,.06f+i*.07f,2f),.55f,.055f,rope);

            var helm=Group("Ship's wheel",world,new Vector3(5.1f,1.55f,5.6f)).transform;
            Box("Helm support",helm,new Vector3(0,-.75f,.3f),new Vector3(.45f,1.6f,.45f),darkWood,true);
            Ring("Helm rim",helm,Vector3.zero,.8f,.085f,wood,new Vector3(90,0,0));
            for(int i=0;i<8;i++)Box("Wheel spoke "+i,helm,Vector3.zero,new Vector3(.065f,2f,.08f),brass,false,new Vector3(0,0,i*45));
            Shape("Helm axle",PrimitiveType.Sphere,helm,Vector3.zero,Vector3.one*.24f,brass);
            Seal(new Vector3(5.1f,1.55f,4.6f),2);

            var cannon=Group("Decorative signal cannon",world,new Vector3(5.1f,.8f,-5.4f)).transform;
            cannon.localEulerAngles=new Vector3(0,-35,0);
            Shape("Iron cannon tube",PrimitiveType.Cylinder,cannon,Vector3.zero,new Vector3(.5f,.85f,.5f),iron,false,new Vector3(90,0,0));
            Box("Cannon carriage",cannon,new Vector3(0,-.4f,0),new Vector3(.9f,.4f,1.2f),wood,true);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Shape("Cannon wheel",PrimitiveType.Cylinder,cannon,new Vector3(x*.55f,-.45f,z*.4f),new Vector3(.45f,.1f,.45f),darkWood,false,new Vector3(0,0,90));
            var cannonTarget=Box("FIRE CANNON brass control",cannon,new Vector3(0,.36f,0),new Vector3(.65f,.12f,.65f),brass,true);
            AddInteraction(cannonTarget,PirateAction.Cannon);

            var bell=Group("Ship's bell",world,new Vector3(-6.7f,1.7f,.7f)).transform;
            Shape("Bronze bell",PrimitiveType.Cylinder,bell,Vector3.zero,new Vector3(.42f,.27f,.42f),brass,true);
            Ring("Bell lip",bell,new Vector3(0,-.27f,0),.26f,.05f,brass);
            Shape("Bell clapper",PrimitiveType.Sphere,bell,new Vector3(0,-.38f,0),Vector3.one*.12f,iron);
            AddInteraction(bell.gameObject,PirateAction.Bell,"Bell");
            Sound("Ocean beyond port hull",fx,new Vector3(-8f,2f,0),"Ocean",true);
            Sound("Ocean beyond starboard hull",fx,new Vector3(8f,2f,0),"Ocean",true);
        }

        static Transform Table(Vector3 at,string name,Vector3 size)
        {
            var g=Group(name,world,at).transform;
            Box("Solid walnut top",g,new Vector3(0,size.y,0),new Vector3(size.x,.18f,size.z),wood,true);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box("Table leg",g,new Vector3(x*(size.x*.5f-.2f),size.y*.5f,z*(size.z*.5f-.2f)),new Vector3(.15f,size.y,.15f),darkWood,true);
            return g;
        }
        static void Chair(Vector3 at,float yaw)
        {
            var g=Group("Walnut cabin chair",world,at).transform;g.localEulerAngles=new Vector3(0,yaw,0);
            Box("Seat",g,new Vector3(0,.5f,0),new Vector3(.7f,.12f,.7f),wood,true);
            Box("Back",g,new Vector3(0,.9f,.3f),new Vector3(.7f,.7f,.12f),wood,true);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box("Chair leg",g,new Vector3(x*.27f,.25f,z*.27f),new Vector3(.09f,.5f,.09f),darkWood,true);
        }
        static void Barrel(Vector3 at,float scale)
        {
            var b=Group("Cargo barrel",world,at).transform;b.localScale=Vector3.one*scale;
            Shape("Wooden staves",PrimitiveType.Cylinder,b,new Vector3(0,.7f,0),new Vector3(1.05f,.7f,1.05f),wood,true);
            Shape("Barrel lid",PrimitiveType.Cylinder,b,new Vector3(0,1.42f,0),new Vector3(.94f,.02f,.94f),darkWood);
            foreach(float y in new[]{.12f,.42f,1f,1.3f})Ring("Iron barrel hoop",b,new Vector3(0,y,0),.535f,.04f,iron);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6f;
                Box("Barrel stave seam",b,new Vector3(Mathf.Sin(a)*.526f,.7f,Mathf.Cos(a)*.526f),new Vector3(.014f,1.26f,.014f),darkWood);
            }
        }
        static void Crate(Vector3 at,Vector3 scale)
        {
            var c=Group("Cargo crate",world,at).transform;c.localScale=scale;
            Box("Crate body",c,Vector3.zero,Vector3.one,wood,true);
            for(int i=-1;i<=1;i+=2)
            {
                Box("Crate edge",c,new Vector3(i*.45f,0,-.51f),new Vector3(.1f,1f,.05f),darkWood);
                Box("Crate band",c,new Vector3(0,i*.45f,-.51f),new Vector3(1f,.1f,.05f),darkWood);
            }
            Box("Crate diagonal brace",c,new Vector3(0,0,-.54f),new Vector3(.12f,1.2f,.055f),darkWood,false,new Vector3(0,0,45));
        }
        static void Bottle(Vector3 at)
        {
            var b=Group("Sea glass bottle",world,at).transform;
            Shape("Bottle body",PrimitiveType.Cylinder,b,new Vector3(0,.15f,0),new Vector3(.18f,.15f,.18f),aqua);
            Shape("Bottle neck",PrimitiveType.Cylinder,b,new Vector3(0,.36f,0),new Vector3(.075f,.08f,.075f),aqua);
            Shape("Cork",PrimitiveType.Cylinder,b,new Vector3(0,.455f,0),new Vector3(.08f,.025f,.08f),rope);
        }

        static void MakeChest()
        {
            var chest=Group("Captain's treasure chest",world,new Vector3(0,0,5.8f)).transform;
            Box("Chest bottom",chest,new Vector3(0,.18f,0),new Vector3(2.5f,.26f,1.3f),darkWood,true);
            Box("Chest front",chest,new Vector3(0,.63f,-.6f),new Vector3(2.5f,.75f,.15f),wood,true);
            Box("Chest back",chest,new Vector3(0,.63f,.6f),new Vector3(2.5f,.75f,.15f),wood,true);
            for(int x=-1;x<=1;x+=2)
            {
                Box("Chest side",chest,new Vector3(x*1.2f,.63f,0),new Vector3(.15f,.75f,1.3f),wood,true);
                Box("Chest gold strap",chest,new Vector3(x*.83f,.6f,-.69f),new Vector3(.13f,.8f,.04f),brass);
            }
            var lid=Group("Hinged lid - opens when all seals collected",chest,new Vector3(0,1.05f,.62f)).transform;
            for(int i=0;i<7;i++)
            {
                float z=-.54f+i*.18f;
                float y=.1f+.3f*Mathf.Sqrt(Mathf.Max(0f,1f-z*z/.36f));
                Box("Curved lid stave",lid,new Vector3(0,y,z-.62f),new Vector3(2.5f,.12f,.21f),wood,false,new Vector3(-z*50,0,0));
            }
            game.chestLid=lid;
            var latch=Box("Chest lock - interact to open",chest,new Vector3(0,.95f,-.73f),new Vector3(.5f,.55f,.1f),brass,true);
            AddInteraction(latch,PirateAction.Chest);
            game.chestTreasure=Group("Revealed coins and gemstones",chest,new Vector3(0,.47f,0));
            for(int i=0;i<25;i++)
            {
                float x=Mathf.Sin(i*2.39996f)*.95f,z=Mathf.Cos(i*2.39996f)*.4f;
                Shape("Gold coin "+i,PrimitiveType.Cylinder,game.chestTreasure.transform,new Vector3(x,.04f*(i%5),z),new Vector3(.23f,.025f,.23f),brass);
            }
            for(int i=0;i<4;i++)Shape("Gemstone "+i,PrimitiveType.Cube,game.chestTreasure.transform,
                new Vector3(-.6f+i*.4f,.2f,.1f),Vector3.one*.2f,i%2==0?aqua:red,false,new Vector3(35,45,25));
            game.chestTreasure.SetActive(false);
            game.treasureParticles=Burst("Treasure celebration",fx,new Vector3(0,1.35f,5.8f),particle);
            game.treasureSound=Sound("Treasure fanfare",fx,new Vector3(0,1.35f,5.8f),"Treasure");
        }

        static void Seal(Vector3 at,int index)
        {
            var seal=Shape("Navigation seal "+(index+1),PrimitiveType.Cylinder,world,at,new Vector3(.48f,.075f,.48f),brass,true,new Vector3(90,0,0));
            seal.AddComponent<HoverBob>();
            var interaction=AddInteraction(seal,PirateAction.Seal,"Seal");interaction.sealIndex=index;
            seals.Add(seal);
        }
        static PirateInteractable AddInteraction(GameObject target,PirateAction action,string clip="Click")
        {
            var p=target.AddComponent<PirateInteractable>();p.game=game;p.action=action;
            var xr=target.AddComponent<XRSimpleInteractable>();
            foreach(var c in target.GetComponentsInChildren<Collider>())if(c.enabled)xr.colliders.Add(c);
            if(xr.colliders.Count==0)
            {
                var c=target.AddComponent<BoxCollider>();c.size=Vector3.one*.6f;xr.colliders.Add(c);
            }
            var mf=target.GetComponent<MeshFilter>();
            if(mf)
            {
                var o=Group("Hover outline",target.transform);o.AddComponent<MeshFilter>().sharedMesh=mf.sharedMesh;
                o.AddComponent<MeshRenderer>().sharedMaterial=outline;o.SetActive(false);p.outline=o;
            }
            p.feedback=Burst(action+" particles",fx,target.transform.position,particle);
            p.sound=Sound(action+" spatial sound",fx,target.transform.position,clip);
            return p;
        }

        static void Sign(string name,Vector3 at,Vector2 size,string text,Vector3 rot=default,int font=44)
        {
            var sign=Group(name,world,at).transform;sign.localEulerAngles=rot;
            Box("Wooden sign back",sign,new Vector3(0,0,.07f),new Vector3(size.x+.1f,size.y+.1f,.1f),darkWood);
            Box("Navy sign face",sign,new Vector3(0,0,.008f),new Vector3(size.x,size.y,.02f),navy);
            Panel(name+" text",sign,new Vector3(0,0,-.014f),size,text,font);
        }
        static void Marker(string name,Vector3 at,PirateAction action)
        {
            var g=Box(name,world,at,new Vector3(1.35f,.65f,.15f),brass,true);
            AddInteraction(g,action);
            Panel(name+" label",world,at+Vector3.back*.09f,new Vector2(1.3f,.6f),name,35);
        }
        static void MakeSigns()
        {
            Sign("THE BLACK TIDE title",new Vector3(0,4.2f,7.1f),new Vector2(7,1.6f),"<b>THE BLACK TIDE</b>\nCAPTAIN'S CABIN  /  A VOYAGE AMONG STARS",font:66);
            Sign("Captain's orders",new Vector3(-3f,2.15f,6.9f),new Vector2(3.5f,1.5f),"<b>CAPTAIN'S ORDERS</b>\nFind 3 navigation seals.\nOpen the chest. Claim the treasure.",font:44);
            Sign("VR control board",new Vector3(3f,2.5f,6.9f),new Vector2(3.5f,2.2f),
                "<b>VR CONTROLS</b>\nTrigger - interact with brass markers\nA - lantern color    B - exterior lookout\nX - fire signal    Hold Y - exit (1.2 s)\nLeft stick - move    Right stick - snap turn",font:36);
            Sign("Desktop controls",new Vector3(-3.5f,2.2f,-7.1f),new Vector2(4.6f,2f),
                "<b>DESKTOP PREVIEW</b>\nW A S D - walk    Hold right mouse - look\nE / left click - interact at crosshair\nL - light    T - lookout    F - signal\nR - restart    Hold Q - exit",new Vector3(0,180,0),40);
            Sign("Material comparison explanation",new Vector3(3.5f,2.2f,-7.1f),new Vector2(4.6f,2f),
                "<b>THE SHIP'S MATERIALS</b>\nNorth wall: base + normal / tile 1 / matte\nEast wall: same maps / tile 4 / polished\nSouth wall: flat navy with no texture map\nLook closely at the surfaces and reflections.",new Vector3(0,180,0),37);
            Sign("Orrery instructions",new Vector3(0,1.8f,-2.8f),new Vector2(3.5f,.95f),
                "<b>NAVIGATOR'S ORRERY</b>\nMoon: parent rotation  /  Comet: gravitational acceleration",font:35);
            var statusBack=Group("Voyage status board",world,new Vector3(0,2.15f,6.95f)).transform;
            Box("Status board",statusBack,new Vector3(0,0,.06f),new Vector3(2.7f,1.3f,.1f),navy);
            game.status=Panel("Voyage status - world space",statusBack,Vector3.zero,new Vector2(2.65f,1.3f),"CAPTAIN'S ORDERS",37);
            Marker("CHANGE LANTERN",new Vector3(6.4f,1.4f,3.3f),PirateAction.Light);
            Marker("EXTERIOR LOOKOUT",new Vector3(2.5f,1.4f,-5.8f),PirateAction.Teleport);
            Marker("RESET VOYAGE",new Vector3(-2.5f,1.4f,-5.8f),PirateAction.Reset);
        }

        static void MakeExterior()
        {
            var ext=Group("Exterior ship silhouette and lookout",world).transform;
            var ocean=Lit("Deep ocean",new Color(.015f,.075f,.12f),.4f,.86f,"Ocean",new Vector2(20,20),"Wood_Normal");
            Shape("Ocean horizon",PrimitiveType.Plane,ext,new Vector3(0,-2,0),new Vector3(100,1,100),ocean);
            Box("External lower hull",ext,new Vector3(0,-1,0),new Vector3(16,1.9f,19),darkWood);
            for(int i=0;i<4;i++)Box("Tapered bow",ext,new Vector3(0,-.9f,10f+i),new Vector3(14f-i*3f,1.6f,1.5f),darkWood);
            // Outward hull panels sit behind the six closed interior room planes.
            Box("Exterior port hull",ext,new Vector3(-7.65f,7.5f,0),new Vector3(.15f,15f,15.4f),darkWood);
            Box("Exterior starboard hull",ext,new Vector3(7.65f,7.5f,0),new Vector3(.15f,15f,15.4f),darkWood);
            Box("Exterior stern",ext,new Vector3(0,7.5f,-7.65f),new Vector3(15.4f,15f,.15f),darkWood);
            Box("Exterior bow wall",ext,new Vector3(0,7.5f,7.65f),new Vector3(15.4f,15f,.15f),darkWood);
            for(int i=0;i<4;i++)
            {
                Box("Hull brass stripe",ext,new Vector3(7.75f,2f+i*3.5f,0),new Vector3(.025f,.08f,15.3f),brass);
                Box("Stern brass stripe",ext,new Vector3(0,2f+i*3.5f,-7.75f),new Vector3(15.3f,.08f,.025f),brass);
            }
            Shape("Main mast",PrimitiveType.Cylinder,ext,new Vector3(0,17.4f,0),new Vector3(.45f,6.2f,.45f),darkWood);
            Box("Main yard",ext,new Vector3(0,21f,0),new Vector3(12,.2f,.2f),darkWood);
            Box("Canvas sail",ext,new Vector3(0,18.5f,.15f),new Vector3(10,4.6f,.06f),cream);
            var flag=Lit("Jolly Roger navy",Color.white,0f,.1f,"PirateFlag",Vector2.one);
            Shape("Pirate banner",PrimitiveType.Quad,ext,new Vector3(1.9f,23.2f,0),new Vector3(3.5f,2,1),flag);
            var lookout=Group("External observation platform",ext,new Vector3(22,.5f,-20)).transform;
            Box("Lookout floor",lookout,new Vector3(0,-.12f,0),new Vector3(8,.25f,8),wood,true);
            for(int s=-1;s<=1;s+=2)
            {
                Box("Lookout X safety rail",lookout,new Vector3(s*3.9f,.6f,0),new Vector3(.15f,1.2f,8),brass,true);
                Box("Lookout Z safety rail",lookout,new Vector3(0,.6f,s*3.9f),new Vector3(8,1.2f,.15f),brass,true);
            }
            Sign("Lookout sign",new Vector3(22,1.9f,-16.4f),new Vector2(3.3f,1),"<b>EXTERIOR LOOKOUT</b>\nB / T - return to the captain's cabin",font:38);
            Sound("Sea wind at lookout",fx,new Vector3(22,1,-20),"Ocean",true);
        }

        static void MakeSignal()
        {
            var template=Shape("Signal projectile template",PrimitiveType.Sphere,world,new Vector3(0,-20,0),Vector3.one*.14f,goldGlow);
            template.layer=2;
            game.signalPrefab=template.AddComponent<SignalProjectile>();
            var trail=template.AddComponent<TrailRenderer>();trail.time=.22f;trail.startWidth=.06f;trail.endWidth=0f;trail.sharedMaterial=particle;
            template.SetActive(false);
            game.spawnParticles=Burst("Signal launch and impact",fx,Vector3.zero,particle);
            game.spawnSound=Sound("Signal launch sound",fx,Vector3.zero,"Signal");
        }
        static InputActionReference Ref(string action) => InputActionReference.Create(actions.FindAction(action,true));
        static void Pose(TrackedPoseDriver driver,string map)
        {
            driver.positionInput=new InputActionProperty(Ref(map+"/Position"));
            driver.rotationInput=new InputActionProperty(Ref(map+"/Rotation"));
            driver.trackingStateInput=new InputActionProperty(Ref(map+"/TrackingState"));
            driver.updateType=TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        }
        static void MakeRig()
        {
            Group("XR Interaction Manager",world).AddComponent<XRInteractionManager>();
            var input=Group("Input action manager",world).AddComponent<InputActionManager>();
            input.actionAssets=new List<InputActionAsset>{actions};
            var rig=Group("XR Origin - tracked head and both controllers",world,new Vector3(0,0,-5.1f));
            var xr=rig.AddComponent<XROrigin>();xr.Origin=rig;
            var offset=Group("Camera Floor Offset",rig.transform);xr.CameraFloorOffsetObject=offset;
            xr.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor;xr.CameraYOffset=1.65f;
            var cameraGo=Group("Main Camera",offset.transform,new Vector3(0,1.65f,0));cameraGo.tag="MainCamera";
            var cam=cameraGo.AddComponent<Camera>();cam.nearClipPlane=.05f;cam.farClipPlane=650f;
            cam.clearFlags=CameraClearFlags.Skybox;cam.fieldOfView=75f;cameraGo.AddComponent<AudioListener>();
            xr.Camera=cam;var head=cameraGo.AddComponent<TrackedPoseDriver>();Pose(head,"Head");
            var left=Hand("Left",offset.transform,aqua);var right=Hand("Right",offset.transform,goldGlow);
            var body=rig.AddComponent<CharacterController>();body.radius=.24f;body.height=1.7f;
            body.center=new Vector3(0,.87f,0);body.skinWidth=.025f;body.stepOffset=.2f;
            var p=rig.AddComponent<PiratePlayer>();p.origin=xr;p.view=cam;p.body=body;p.headTracking=head;
            p.inputs=actions;p.leftHand=left;p.rightHand=right;
            p.interiorAnchor=Group("Cabin start and return anchor",world,new Vector3(0,.05f,-5.1f)).transform;
            p.exteriorAnchor=Group("External viewpoint anchor",world,new Vector3(22,.55f,-20)).transform;
            p.exteriorAnchor.rotation=Quaternion.Euler(0,-48f,0);game.player=p;
        }
        static Transform Hand(string side,Transform parent,Material material)
        {
            var hand=Group(side+" Controller - tracked aim pose",parent).transform;
            Pose(hand.gameObject.AddComponent<TrackedPoseDriver>(),side);
            Shape(side+" controller model",PrimitiveType.Capsule,hand,new Vector3(0,0,-.04f),new Vector3(.065f,.07f,.065f),material,false,new Vector3(70,0,0));
            var ray=hand.gameObject.AddComponent<XRRayInteractor>();ray.maxRaycastDistance=9f;
            ray.enableUIInteraction=false;ray.raycastMask=~(1<<2);
            ray.selectInput=new XRInputButtonReader("Select",null,false,XRInputButtonReader.InputSourceMode.InputActionReference)
            { inputActionReferencePerformed=Ref(side+"/Select"),inputActionReferenceValue=Ref(side+"/SelectValue") };
            ray.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.StateChange;
            var line=hand.gameObject.AddComponent<LineRenderer>();line.sharedMaterial=particle;
            line.startWidth=.006f;line.endWidth=.003f;
            var visual=hand.gameObject.AddComponent<XRInteractorLineVisual>();visual.lineWidth=.006f;
            return hand;
        }
    }
}
