using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackTide.MP1B.Editor
{
    public static class CabinPanelLayoutUpdate
    {
        const float PanelLift = .28f;
        static readonly string[] PanelParts = {
            "Walnut frame", "Parchment face", "Puzzle feedback", "Station title",
            "Press signifier", "Log extract", "Seal verse"
        };

        // Idempotent and shared by initial authoring and the in-place update.
        public static void RaisePanel(CabinPuzzle puzzle)
        {
            if (puzzle.kind != CabinPuzzleKind.Log && puzzle.kind != CabinPuzzleKind.Seal)
                throw new InvalidOperationException("Only log and seal panels may be raised.");
            var face = puzzle.transform.Find("Parchment face");
            if (!face) throw new InvalidOperationException("Panel face is missing.");
            float delta = PanelLift - face.localPosition.y;
            foreach (Transform child in puzzle.transform)
                if (PanelParts.Contains(child.name) || child.GetComponent<CabinButton>())
                {
                    // Canvas coordinates serialize through anchoredPosition, not localPosition.
                    if (child is RectTransform rect)
                    {
                        float originalY = child.name switch {
                            "Puzzle feedback" => -.68f, "Station title" => .7f,
                            "Press signifier" => -.7f, "Log extract" => .38f,
                            "Seal verse" => .35f,
                            _ => throw new InvalidOperationException("Unexpected panel label: " + child.name)
                        };
                        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, originalY + PanelLift);
                    }
                    else
                    child.localPosition += Vector3.up * delta;
                    EditorUtility.SetDirty(child);
                }
        }

        public static void Apply()
        {
            if (Path.GetFullPath(Application.dataPath).Replace('\\', '/') != "C:/UnityProjects/mp1b/Assets")
                throw new InvalidOperationException("Unexpected project path.");
            const string path = "Assets/Pirate/Scenes/BlackTideCabin.unity";
            EditorSceneManager.OpenScene(path);
            var puzzles = UnityEngine.Object.FindObjectsByType<CabinPuzzle>(FindObjectsSortMode.None);
            var log = puzzles.Single(p => p.kind == CabinPuzzleKind.Log);
            var seal = puzzles.Single(p => p.kind == CabinPuzzleKind.Seal);
            string snapshot = "Assets/MP1B/Original/BeforePanelLift_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".unity";
            if (!AssetDatabase.CopyAsset(path, snapshot)) throw new IOException("Scene snapshot failed.");
            RaisePanel(log);
            RaisePanel(seal);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            CabinAuthoring.Validate();
            CapturePanel(log, "layout_lock2");
            CapturePanel(seal, "layout_lock3");
            Debug.Log("MP1B_PANEL_LAYOUT_PASS: log and seal panels raised 0.28m; reward assemblies unchanged; snapshot=" + snapshot);
        }

        public static void CapturePanel(CabinPuzzle puzzle, string name)
        {
            var player = UnityEngine.Object.FindFirstObjectByType<BlackTide.PiratePlayer>();
            var cameraObject = new GameObject("Layout preview camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(player.view);
            camera.enabled = false;
            var origin = puzzle.transform.TransformPoint(new Vector3(0f, -.05f, -2.35f));
            var target = puzzle.transform.TransformPoint(new Vector3(0f, -.03f, 0f));
            camera.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(target - origin));
            var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            string previousText = puzzle.feedbackText.text;
            try
            {
                // Show the actual initial runtime feedback in this edit-mode preview.
                puzzle.feedbackText.text = puzzle.kind == CabinPuzzleKind.Bell ?
                    "Replay the signal, then ring the bells in order." : "Read the clue, then enter your answer.";
                puzzle.feedbackText.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Verification");
                File.WriteAllBytes("Verification/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                puzzle.feedbackText.text = previousText;
                RenderTexture.active = previous;
                camera.targetTexture = null;
                texture.Release();
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
