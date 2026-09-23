using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackTide.MP1B.Editor
{
    public static class CabinPuzzlePolish
    {
        public static void ConfigureAudio(CabinPuzzle puzzle)
        {
            var failure = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MP1B/Audio/PuzzleFailure.wav");
            var success = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MP1B/Audio/BellSuccess.wav");
            if (!failure || !success) throw new InvalidOperationException("Puzzle feedback audio is missing.");
            puzzle.errorSound = puzzle.kind == CabinPuzzleKind.Bell ? null : failure;
            if (puzzle.kind == CabinPuzzleKind.Bell) puzzle.successSound = success;
            puzzle.sound.volume = .85f;
            EditorUtility.SetDirty(puzzle);
            EditorUtility.SetDirty(puzzle.sound);
        }

        public static void Apply()
        {
            if (Path.GetFullPath(Application.dataPath).Replace('\\', '/') != "C:/UnityProjects/mp1b/Assets")
                throw new InvalidOperationException("Unexpected project path.");
            const string path = "Assets/Pirate/Scenes/BlackTideCabin.unity";
            EditorSceneManager.OpenScene(path);
            var puzzles = UnityEngine.Object.FindObjectsByType<CabinPuzzle>(FindObjectsSortMode.None);
            var bell = puzzles.Single(p => p.kind == CabinPuzzleKind.Bell);
            string snapshot = "Assets/MP1B/Original/BeforePuzzleFeedback_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".unity";
            if (!AssetDatabase.CopyAsset(path, snapshot)) throw new IOException("Scene snapshot failed.");
            foreach (var puzzle in puzzles) ConfigureAudio(puzzle);
            // Local panel-right points along world +Z. Move every station part with its root.
            bell.transform.position = new Vector3(-6.6f, 1.65f, 1.80f);
            bell.reward.transform.SetPositionAndRotation(bell.rewardSpawn.position, bell.rewardSpawn.rotation);
            EditorUtility.SetDirty(bell.transform);
            EditorUtility.SetDirty(bell.reward.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            CabinAuthoring.Validate();
            EditorSceneManager.OpenScene(path);
            bell = UnityEngine.Object.FindObjectsByType<CabinPuzzle>(FindObjectsSortMode.None).Single(p => p.kind == CabinPuzzleKind.Bell);
            CabinPanelLayoutUpdate.CapturePanel(bell, "layout_bell_spacing");
            Debug.Log("MP1B_PUZZLE_POLISH_PASS: bell moved 0.55m right; new failure cues on log/seal only; bell success chime; snapshot=" + snapshot);
        }
    }
}
