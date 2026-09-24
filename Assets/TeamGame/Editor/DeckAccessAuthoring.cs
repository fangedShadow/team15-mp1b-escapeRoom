using System;
using System.Linq;
using BlackTide;
using Team15.Deck;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Team15.Editor
{
    public static class DeckAccessAuthoring
    {
        public static void Apply()
        {
            if (!Application.dataPath.Replace('\\', '/').Equals("C:/UnityProjects/team15_mp1b/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Wrong integration project.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/DeckScene.unity");
            var room = Object.FindFirstObjectByType<TeamRoom>();
            var up = Object.FindObjectsByType<HoverTeleport>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(t => t.name == "StylShip_WireMid");
            var old = room.transform.Find("Deck route guidance");
            if (old) Object.DestroyImmediate(old.gameObject);
            var root = SceneKit.Group("Deck route guidance", room.transform).transform;
            var timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamGame/Generated/RouteSign.mat");

            // Keep the original net as the upward interaction, with a safe standing height.
            var arrival = up.teleportDestination;
            // The lookout floor is below its high rail; use the floor below the standing point.
            Physics.SyncTransforms();
            var floorRay = new Ray(new Vector3(arrival.position.x,24f,arrival.position.z), Vector3.down);
            if (!Physics.Raycast(floorRay, out var floor, 2f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Lookout standing point has no supporting floor.");
            arrival.position = new Vector3(arrival.position.x, floor.point.y + .05f, arrival.position.z);
            var returnSign = Sign(root, timber, "Return to deck", new Vector3(2.25f,24.55f,4.5f), 0,
                new Vector2(1.25f,.55f), "<b>RETURN TO DECK</b>\nE / click  |  VR trigger");
            TeleportButton(returnSign, new Vector2(1.25f,.55f), room.spawn.transform);

            var climb = Sign(root, timber, "Climb to crow's nest", new Vector3(.05f,6.25f,3.6f), 270,
                new Vector2(1.4f,.8f), "<b>CLIMB TO CROW'S NEST</b>\nE / click  |  VR trigger\nBring the cutlass from the bow.\nUse the sign upstairs to return.");
            TeleportButton(climb, new Vector2(1.4f,.8f), arrival);
            Sign(root, timber, "Deck objectives", new Vector3(4.6f,6.35f,-5.5f), 180,
                new Vector2(1.9f,1.05f), "<b>THREE KEYS TO THE EXIT</b>\nCANNON: hit the flying target.\nCOINS: find all 5 around the ship.\nBOX: take the bow's cutlass up the net.\nCut the hanging crate's rope; return\nto the deck to find the dropped key.");
            Sign(root, timber, "Rope instruction", new Vector3(4.0f,24.9f,1.7f), 180,
                new Vector2(1.25f,.6f), "<b>THE HANGING CRATE</b>\nTouch the rope with your cutlass.\nThe key will land on the main deck.");

            var key = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Box Key");
            key.position = new Vector3(key.position.x,5.1f,key.position.z);
            var sword = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Cutlass");
            sword.position = new Vector3(2.45f,sword.position.y,sword.position.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sword);
            PrefabUtility.RecordPrefabInstancePropertyModifications(key);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrival);
            DeckStairAuthoring.Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("DECK_ACCESS_AUTHORING_PASS");
        }

        static void TeleportButton(Transform sign, Vector2 size, Transform destination)
        {
            var box = sign.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0,0,.03f); box.size = new Vector3(size.x+.08f,size.y+.08f,.12f);
            var interactable = sign.gameObject.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Add(box);
            var teleport = sign.gameObject.AddComponent<HoverTeleport>();
            teleport.teleportDestination = destination;
            teleport.objectRenderer = sign.GetComponentInChildren<MeshRenderer>();
        }

        static Transform Sign(Transform parent, Material material, string name, Vector3 at, float yaw, Vector2 size, string text)
        {
            var sign = SceneKit.Group(name, parent).transform;
            sign.SetPositionAndRotation(at, Quaternion.Euler(0,yaw,0));
            SceneKit.Box("Timber backing", sign, new Vector3(0,0,.045f), new Vector3(size.x+.08f,size.y+.08f,.06f), material);
            SceneKit.Panel("Instruction", sign, Vector3.zero, size, text, 27);
            return sign;
        }
    }
}
