using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using TMPro;
using BlackTide;

namespace BlackTide.Editor
{
    public static class PirateProjectSetup
    {
        public const string ScenePath = "Assets/Pirate/Scenes/BlackTideCabin.unity";
        const string Generated = "Assets/Pirate/Generated";
        const string Ready = Generated + "/SceneReady.txt";

        [InitializeOnLoadMethod]
        static void QueueFirstImport()
        {
            if (!File.Exists(Ready)) EditorApplication.delayCall += FirstImport;
        }
        static void FirstImport()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Ready)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += FirstImport; return; }
            var current = SceneManager.GetActiveScene();
            if (current.isDirty && current.path != ScenePath)
            { Debug.Log("Black Tide: save your current scene, then choose Pirate MP1 > 1. Build or Rebuild Cabin."); return; }
            try { BuildCabin(false); }
            catch (Exception e) { Debug.LogException(e); Debug.LogError("Black Tide setup stopped. Resolve the first error, then run Pirate MP1 > 1. Build or Rebuild Cabin."); }
        }

        [MenuItem("Pirate MP1/1. Build or Rebuild Cabin")]
        public static void BuildCabinMenu() => BuildCabin(true);

        static void BuildCabin(bool askBeforeReplace)
        {
            if (askBeforeReplace && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (askBeforeReplace && File.Exists(Ready) && !EditorUtility.DisplayDialog("Rebuild generated cabin?",
                "This regenerates BlackTideCabin.unity from PirateSceneFactory.cs. Save a copy first if you edited that scene manually.", "Rebuild", "Cancel")) return;
            Directory.CreateDirectory(Generated); Directory.CreateDirectory("Assets/Pirate/Scenes"); AssetDatabase.Refresh();
            EnsureTMPResources();
            ImportNormals();
            ConfigureRenderPipeline();
            ConfigurePlayer();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = PirateSceneFactory.Build();
            PersistGeneratedAssets(root);
            var game = root.GetComponentInChildren<PirateGame>();
            var template = game.signalPrefab.gameObject;
            string prefabPath = Generated + "/SignalProjectile.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(template, prefabPath);
            game.signalPrefab = prefab.GetComponent<SignalProjectile>();
            UnityEngine.Object.DestroyImmediate(template);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            // Loader/profile setup is explicit and reports errors instead of silently claiming VR readiness.
            ConfigureXR();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Ready, "Generated from PirateSceneFactory. Keep this marker to preserve manual scene edits.\n");
            AssetDatabase.Refresh();
            if (SceneView.lastActiveSceneView)
            {
                SceneView.lastActiveSceneView.LookAt(new Vector3(0,3,1), Quaternion.Euler(20,-15,0), 16f);
            }
            Selection.activeGameObject = game.gameObject;
            Debug.Log("Black Tide cabin generated. Press Play for desktop preview, or connect an OpenXR headset. Run Pirate MP1 > 3. Validate Scene before building.");
        }

        static void EnsureTMPResources()
        {
            if (Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")) return;
            var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(p => p.name == "com.unity.ugui");
            string resources = package == null ? null : Directory.GetFiles(package.resolvedPath,
                "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
            if (resources == null) throw new InvalidOperationException("TMP Essential Resources not found. Use Window > TextMeshPro > Import TMP Essential Resources, then rebuild the cabin.");
            AssetDatabase.ImportPackage(resources, false); AssetDatabase.Refresh();
            if (!Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"))
                throw new InvalidOperationException("Wait for TMP Essential Resources to finish importing, then run Pirate MP1 > 1. Build or Rebuild Cabin.");
        }

        static void ImportNormals()
        {
            foreach (string path in Directory.GetFiles("Assets/Pirate/Resources/Pirate/Textures")
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) continue;
                bool normal = Path.GetFileName(path).IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0;
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal;
                importer.mipmapEnabled = true; importer.maxTextureSize = path.Contains("NightSky") || path.Contains("sky13_") ? 2048 : 1024;
                importer.wrapMode = path.Contains("sky13_") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat; importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureRenderPipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Generated + "/CabinRenderer.asset");
            if (!renderer)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, Generated + "/CabinRenderer.asset");
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "/CabinURP.asset");
            if (!pipeline)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, Generated + "/CabinURP.asset");
            }
            pipeline.msaaSampleCount = 4; pipeline.renderScale = 1f; pipeline.shadowDistance = 25f;
            var so = new SerializedObject(pipeline);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_AdditionalLightsRenderingMode", 1);
            SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 1024);
            so.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i=0;i<QualitySettings.names.Length;i++)
            {
                int previous = QualitySettings.GetQualityLevel();
                QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline;
                QualitySettings.SetQualityLevel(previous, false);
            }
            EditorUtility.SetDirty(pipeline);
        }
        static void SetBool(SerializedObject so,string name,bool value)
        { var p=so.FindProperty(name); if(p!=null)p.boolValue=value; }
        static void SetInt(SerializedObject so,string name,int value)
        { var p=so.FindProperty(name); if(p!=null)p.intValue=value; }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "MP1 Team";
            PlayerSettings.productName = "The Black Tide - Captain's Cabin";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "edu.illinois.mp1.blacktide");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            // All runtime controls use the new Input System, including the desktop fallback.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SetInt(settings, "activeInputHandler", 1); settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        [MenuItem("Pirate MP1/2. Configure OpenXR for Quest and PC")]
        public static void ConfigureXR()
        {
            XRGeneralSettingsPerBuildTarget store;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out store) || !store)
            {
                store = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                string path = Generated + "/XRGeneralSettingsPerBuildTarget.asset";
                Directory.CreateDirectory(Generated);
                AssetDatabase.CreateAsset(store, path);
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, store, true);
            }
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                if (!store.HasSettingsForBuildTarget(group)) store.CreateDefaultSettingsForBuildTarget(group);
                if (!store.HasManagerSettingsForBuildTarget(group)) store.CreateDefaultManagerSettingsForBuildTarget(group);
                var settings = store.SettingsForBuildTarget(group); settings.InitManagerOnStart = true;
                if (!XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group))
                    throw new InvalidOperationException("OpenXR loader assignment failed for " + group + ". Check installed build modules and XR Plug-in Management.");
                FeatureHelpers.RefreshFeatures(group);
                var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (!openxr) throw new InvalidOperationException("OpenXR settings could not be created for " + group);
                openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                foreach (var feature in openxr.GetFeatures<OpenXRFeature>())
                {
                    if (!feature) continue;
                    string type = feature.GetType().Name;
                    if (type == "OculusTouchControllerProfile" || type == "MetaQuestTouchPlusControllerProfile")
                        feature.enabled = true;
                    if (group == BuildTargetGroup.Android && type == "MetaQuestFeature") feature.enabled = true;
                    if (group == BuildTargetGroup.Android && type == "OculusQuestFeature") feature.enabled = false;
                }
                EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.Manager); EditorUtility.SetDirty(openxr);
            }
            EditorUtility.SetDirty(store); AssetDatabase.SaveAssets();
            Debug.Log("OpenXR configured for Android and desktop. Confirm Oculus Touch / Meta Quest Touch Plus and Meta Quest Support in Project Settings > XR Plug-in Management > OpenXR.");
        }

        static void PersistGeneratedAssets(GameObject root)
        {
            string assets = Generated + "/SceneAssets";
            Directory.CreateDirectory(assets); AssetDatabase.Refresh();
            // Each regeneration keeps old assets intact; a fresh folder avoids invalidating other scene copies.
            string batch = assets + "/Build_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
            Directory.CreateDirectory(batch); AssetDatabase.Refresh();
            var written = new HashSet<UnityEngine.Object>();
            int index = 0;
            void Save(UnityEngine.Object obj)
            {
                if (!obj || AssetDatabase.Contains(obj) || !written.Add(obj)) return;
                if (!(obj is Material || obj is Mesh || obj is InputActionReference)) return;
                string name = string.Join("_", obj.name.Split(Path.GetInvalidFileNameChars()));
                AssetDatabase.CreateAsset(obj, batch + "/" + (index++) + "_" + name + ".asset");
            }
            foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true)) Save(mesh.sharedMesh);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials) Save(material);
            Save(RenderSettings.skybox);
            // Serialize the action references created by the factory and the non-visible color variants.
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (!component) continue;
                var so = new SerializedObject(component); var p = so.GetIterator();
                while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference) Save(p.objectReferenceValue);
            }
        }

        [MenuItem("Pirate MP1/3. Validate Scene")]
        public static void ValidateScene()
        {
            var errors = new List<string>();
            var game = UnityEngine.Object.FindFirstObjectByType<PirateGame>();
            if (!game) errors.Add("Missing PirateGame: build or open BlackTideCabin.unity first.");
            else
            {
                if (!game.player || !game.player.view || !game.player.leftHand || !game.player.rightHand) errors.Add("XR rig references are incomplete.");
                if (!game.ceilingLight || !game.status || !game.chestLid || !game.chestTreasure) errors.Add("Scene references are incomplete.");
                if (game.seals.Length != 3) errors.Add("Exactly three navigation seals are required.");
                if (!game.signalPrefab || !PrefabUtility.IsPartOfPrefabAsset(game.signalPrefab)) errors.Add("Signal projectile must reference a saved prefab asset.");
                if (!game.spawnSound || !game.spawnSound.clip || !game.spawnParticles) errors.Add("Signal feedback is incomplete.");
                if (!game.inputs || game.inputs.FindAction("Gameplay/Light") == null) errors.Add("Input action asset is missing or invalid.");
                if (!RenderSettings.skybox) errors.Add("Missing skybox.");
                var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cameras.Length != 1) errors.Add("Expected exactly one active game camera.");
                foreach (var audio in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    if (!audio.clip || audio.spatialBlend < .99f) errors.Add("Audio missing clip / 3D setting: " + audio.name);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (canvas.renderMode != RenderMode.WorldSpace) errors.Add("UI must use world space: " + canvas.name);
            }
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            bool course = Resources.Load<Texture2D>("Pirate/Textures/tile") && Resources.Load<Texture2D>("Pirate/Textures/tile-normal");
            if (!course) Debug.LogWarning("Course texture check: custom maps are active. Add tile.png and tile-normal.png from the PDF's provided materials, then use Pirate MP1 > Apply Course Wall Textures.");
            Debug.Log("Scene reference validation PASSED. This is not a compile/build or headset playtest result. Now test controls, walls, treasure reset, audio and exterior return in Play Mode.");
        }

        [MenuItem("Pirate MP1/Apply Course Wall Textures")]
        public static void ApplyCourseTextures()
        {
            ImportNormals();
            var texture = Resources.Load<Texture2D>("Pirate/Textures/tile");
            var normal = Resources.Load<Texture2D>("Pirate/Textures/tile-normal");
            if (!texture || !normal) throw new InvalidOperationException("Copy tile.png and tile-normal.png into Assets/Pirate/Resources/Pirate/Textures first.");
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!(r.name.StartsWith("Wall 1 NORTH") || r.name.StartsWith("Wall 2 EAST"))) continue;
                var m = r.sharedMaterial; m.SetTexture("_BaseMap", texture); m.SetTexture("_MainTex", texture);
                m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        [MenuItem("Pirate MP1/4. Build Quest APK")]
        public static void BuildQuest()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            { EditorUtility.DisplayDialog("Install Android Build Support", "In Unity Hub, add Android Build Support, Android SDK & NDK Tools, and OpenJDK to Unity 6000.5.6f1.", "OK"); return; }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                Debug.Log("Switched to Android. After imports finish, choose Pirate MP1 > 4. Build Quest APK again."); return;
            }
            ConfigurePlayer(); ConfigureXR();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath); ValidateScene();
            Directory.CreateDirectory("Builds/Quest");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = "Builds/Quest/BlackTideVR.apk", options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("APK build failed. Read the first Console error.");
            Debug.Log("APK build succeeded: Builds/Quest/BlackTideVR.apk. Install and test it on the target headset.");
        }

        [MenuItem("Pirate MP1/5. Capture Gameplay Screenshot")]
        public static void CaptureScreenshot()
        {
            if (!EditorApplication.isPlaying)
            { Debug.Log("Enter Play Mode and frame the Game view before capturing a screenshot."); return; }
            Directory.CreateDirectory("Screenshots");
            string path = Path.GetFullPath("Screenshots/BlackTide_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(path, 2);
            Debug.Log("Screenshot scheduled at the end of this frame: " + path);
        }
    }
}
