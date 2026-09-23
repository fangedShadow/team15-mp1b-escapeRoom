using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace BlackTide.MP1B.Editor
{
    public static class CabinXRSetup
    {
        const string Controls = "Assets/Pirate/Resources/Pirate/PirateControls.inputactions";
        const string Cabin = "Assets/Pirate/Scenes/BlackTideCabin.unity";

        [MenuItem("MP1b/Configure Meta controller input")]
        public static void ConfigureCurrentProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before configuring XR.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var target in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(target);
                if (xr) { xr.InitManagerOnStart = true; EditorUtility.SetDirty(xr); }
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
                if (!settings) throw new InvalidOperationException("Missing OpenXR settings for " + target);
                foreach (var feature in settings.GetFeatures<OpenXRFeature>())
                {
                    if (!feature) continue;
                    string name = feature.GetType().Name;
                    if (name == "OculusTouchControllerProfile" || name == "MetaQuestTouchPlusControllerProfile" || name == "MetaQuestTouchProControllerProfile")
                    { feature.enabled = true; EditorUtility.SetDirty(feature); }
                }
                EditorUtility.SetDirty(settings);
            }
            var scene = EditorSceneManager.OpenScene(Cabin, OpenSceneMode.Single);
            var player = UnityEngine.Object.FindAnyObjectByType<BlackTide.PiratePlayer>();
            if (!player) throw new InvalidOperationException("Captain player is missing.");
            ConfigureGrip(player.leftHand.GetComponentInChildren<XRDirectInteractor>(true), "Left");
            ConfigureGrip(player.rightHand.GetComponentInChildren<XRDirectInteractor>(true), "Right");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Captain controller setup saved: Meta profiles enabled for PC/Android; grip uses shared input actions.");
        }

        internal static void ConfigureGrip(XRDirectInteractor direct, string side)
        {
            if (!direct) throw new InvalidOperationException("Missing " + side + " direct interactor.");
            direct.selectInput = new XRInputButtonReader("Grip", "Grip value", false, XRInputButtonReader.InputSourceMode.InputActionReference)
            {
                inputActionReferencePerformed = Reference(side, "Grip"),
                inputActionReferenceValue = Reference(side, "GripValue")
            };
            direct.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.StateChange;
            EditorUtility.SetDirty(direct);
        }

        static InputActionReference Reference(string side, string actionName)
        {
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Controls);
            var action = controls.FindAction(side + "/" + actionName, true);
            string path = "Assets/MP1B/Generated/" + side + actionName + ".asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (!reference)
            {
                reference = InputActionReference.Create(action);
                AssetDatabase.CreateAsset(reference, path);
            }
            else { reference.Set(action); EditorUtility.SetDirty(reference); }
            return reference;
        }
    }
}
