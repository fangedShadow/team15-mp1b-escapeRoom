using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackTide.MP1B.Editor
{
    public static class CabinDoorAudioUpdate
    {
        public static void Configure(CabinRoom room)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MP1B/Audio/DoorUnlock.wav");
            if (!clip || !room.exitSocket) throw new InvalidOperationException("Exit lock or audio is missing.");
            var source = room.exitSocket.GetComponent<AudioSource>();
            if (!source) source = room.exitSocket.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.clip = clip;
            source.volume = .85f;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = 12f;
            source.dopplerLevel = 0f;
            if (room.sound) source.outputAudioMixerGroup = room.sound.outputAudioMixerGroup;
            room.exitSound = clip;
            room.exitUnlockAudio = source;
            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(room);
        }

        public static void Apply()
        {
            if (Path.GetFullPath(Application.dataPath).Replace('\\', '/') != "C:/UnityProjects/mp1b/Assets")
                throw new InvalidOperationException("Unexpected project path.");
            const string path = "Assets/Pirate/Scenes/BlackTideCabin.unity";
            EditorSceneManager.OpenScene(path);
            var room = UnityEngine.Object.FindFirstObjectByType<CabinRoom>();
            if (!room) throw new InvalidOperationException("MP1b room is missing.");
            string snapshot = "Assets/MP1B/Original/BeforeDoorAudio_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".unity";
            if (!AssetDatabase.CopyAsset(path, snapshot)) throw new IOException("Scene snapshot failed.");
            Configure(room);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            CabinAuthoring.Validate();
            Debug.Log("MP1B_DOOR_AUDIO_PASS: dedicated key-turn/latch sound placed at the exit lock; snapshot=" + snapshot);
        }
    }
}
