using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BlackTide.Editor
{
    public static class PirateSkyboxRepair
    {
        const string Version = "Skybox repair v2";
        const string TextureFolder = "Assets/Pirate/Resources/Pirate/Textures/";
        const string MaterialFolder = "Assets/Pirate/Skybox";
        const string MaterialPath = MaterialFolder + "/CourseSky13.mat";
        static readonly string[] Faces = { "FR", "BK", "LF", "RT", "UP", "DN" };
        static readonly string[] Slots =
        {
            "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex"
        };

        [MenuItem("Pirate MP1/5. Repair Skybox")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Stop Play Mode before repairing the skybox.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Open the saved BlackTideCabin scene first.");

            // Use the camera referenced by the game's player, not a Scene view camera.
            var cameras = new HashSet<Camera>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var player in root.GetComponentsInChildren<PiratePlayer>(true))
                    if (player.view && player.view.gameObject.scene == scene)
                        cameras.Add(player.view);
            if (cameras.Count == 0)
                throw new InvalidOperationException("No PiratePlayer camera in the active scene. Open the generated cabin scene first.");

            var shader = Shader.Find("Skybox/6 Sided");
            if (!shader || !shader.isSupported)
                throw new InvalidOperationException("Skybox/6 Sided shader is missing or unsupported. Check the Console shader errors.");

            // File existence and an imported Texture2D are separate checks. A JPG can
            // exist while Unity has imported it as a Cubemap or has stale import data.
            Debug.Log(Version + " - CURRENT project Assets folder: " + Application.dataPath);
            var textures = new Texture2D[Faces.Length];
            var importers = new TextureImporter[Faces.Length];
            for (int i = 0; i < Faces.Length; i++)
            {
                string path = TextureFolder + "sky13_" + Faces[i] + ".jpg";
                string absolutePath = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
                if (!File.Exists(absolutePath))
                    throw new InvalidOperationException(Version + ": File is absent from the CURRENT project:\n" +
                        absolutePath + "\nUse Unity Project > Assets > Show in Explorer to check the open project, not another copy.");
            }

            // Register and synchronously import each file before asking for its importer.
            for (int i = 0; i < Faces.Length; i++)
            {
                string path = TextureFolder + "sky13_" + Faces[i] + ".jpg";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                importers[i] = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importers[i])
                    throw new InvalidOperationException(Version + ": File exists but Unity could not create a TextureImporter: " +
                        path + "\nCheck the preceding import errors in Console and try opening this JPG in Windows Photos.");
            }

            for (int i = 0; i < Faces.Length; i++)
            {
                var importer = importers[i];
                string path = TextureFolder + "sky13_" + Faces[i] + ".jpg";
                Debug.Log(Version + ": " + Path.GetFileName(path) +
                    " previous type=" + importer.textureType + ", shape=" + importer.textureShape);
                // Normalize BEFORE loading Texture2D. Cubemap -> Texture2D requires
                // changing textureShape as well as textureType; never delete .meta files.
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!textures[i])
                {
                    var actual = AssetDatabase.LoadMainAssetAtPath(path);
                    string actualType = actual ? actual.GetType().FullName : "no imported asset";
                    throw new InvalidOperationException(Version + ": File exists, but Texture2D import failed: " +
                        path + "\nImported object type: " + actualType +
                        "\nCheck preceding import errors; the file was not deleted.");
                }
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Pirate", "Skybox");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!sky)
            {
                sky = new Material(shader) { name = "CourseSky13" };
                AssetDatabase.CreateAsset(sky, MaterialPath);
            }
            else
            {
                Undo.RecordObject(sky, "Repair skybox material");
                sky.shader = shader;
            }
            for (int i = 0; i < Faces.Length; i++) sky.SetTexture(Slots[i], textures[i]);
            sky.SetColor("_Tint", new Color(.5f, .5f, .5f, .5f));
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 35f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            foreach (var camera in cameras)
            {
                Undo.RecordObject(camera, "Enable camera skybox");
                camera.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(camera);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);

                // An explicit camera reference keeps the saved material in APK builds too.
                var cameraSky = camera.GetComponent<Skybox>();
                if (!cameraSky) cameraSky = Undo.AddComponent<Skybox>(camera.gameObject);
                Undo.RecordObject(cameraSky, "Assign camera skybox");
                cameraSky.enabled = true;
                cameraSky.material = sky;
                EditorUtility.SetDirty(cameraSky);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cameraSky);

                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data)
                {
                    Undo.RecordObject(data, "Set game camera to Base");
                    data.renderType = CameraRenderType.Base;
                    EditorUtility.SetDirty(data);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(data);
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = sky;
            EditorGUIUtility.PingObject(sky);
            SceneView.RepaintAll();
            Debug.Log(Version + " APPLIED: six sky13 textures, saved CourseSky13 material, and game camera skybox assigned. " +
                "Press Ctrl+S to save the scene, then Play and teleport outside to verify clouds. Rebuild the APK after checking.", sky);
        }
    }
}
