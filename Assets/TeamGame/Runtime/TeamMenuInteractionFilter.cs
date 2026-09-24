using System.Collections.Generic;
using BlackTide;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team15
{
    /// <summary>Keeps tracked hands and menu rays working while blocking new room interactions.</summary>
    [DisallowMultipleComponent]
    public sealed class TeamMenuInteractionFilter : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
    {
        readonly List<XRBaseInteractor> installed = new List<XRBaseInteractor>();
        readonly Dictionary<XRGrabInteractable, GrabSettings> pausedGrabs =
            new Dictionary<XRGrabInteractable, GrabSettings>();
        readonly Dictionary<XRRayInteractor, LayerMask> rayMasks = new Dictionary<XRRayInteractor, LayerMask>();
        readonly Dictionary<GameObject, int> handLayers = new Dictionary<GameObject, int>();
        Camera menuCamera;
        int cameraMask;
        CameraClearFlags cameraClear;
        Color cameraBackground;
        TeamSession session;
        PiratePlayer player;
        bool paused;

        struct GrabSettings
        {
            public bool position;
            public bool rotation;
            public bool scale;
            public bool throwing;
        }

        public bool canProcess => isActiveAndEnabled;

        public void Bind(TeamSession owner, PiratePlayer rig)
        {
            SetPaused(false);
            RemoveFilters();
            session = owner;
            player = rig;
            if (isActiveAndEnabled) InstallFilters();
        }

        void OnEnable() => InstallFilters();
        void OnDisable() { SetPaused(false); RemoveFilters(); }
        void OnDestroy() { SetPaused(false); RemoveFilters(); }
        void Update() => SetMenuPresentation(paused && player && player.IsXR);

        public void SetPaused(bool value)
        {
            if (value == paused) return;
            paused = value;
            SetMenuPresentation(value && player && player.IsXR);
            if (!value)
            {
                foreach (var entry in rayMasks)
                    if (entry.Key) entry.Key.raycastMask = entry.Value;
                rayMasks.Clear();
                foreach (var entry in pausedGrabs)
                {
                    var grab = entry.Key;
                    if (!grab) continue;
                    grab.trackPosition = entry.Value.position;
                    grab.trackRotation = entry.Value.rotation;
                    grab.trackScale = entry.Value.scale;
                    grab.throwOnDetach = entry.Value.throwing;
                }
                pausedGrabs.Clear();
                return;
            }
            if (!player) return;
            // Menu rays must remain usable even when a wall lies between the head and menu.
            foreach (var ray in player.GetComponentsInChildren<XRRayInteractor>(true))
            {
                rayMasks[ray] = ray.raycastMask;
                ray.raycastMask = 1 << TeamSession.MenuLayer;
            }
            // Include a desktop hand created after Bind and deduplicate objects held with two hands.
            foreach (var interactor in player.GetComponentsInChildren<XRBaseInteractor>(true))
                foreach (var selected in interactor.interactablesSelected)
                {
                    if (!(selected is XRGrabInteractable grab) || pausedGrabs.ContainsKey(grab)) continue;
                    pausedGrabs.Add(grab, new GrabSettings
                    {
                        position = grab.trackPosition,
                        rotation = grab.trackRotation,
                        scale = grab.trackScale,
                        throwing = grab.throwOnDetach
                    });
                    grab.trackPosition = false;
                    grab.trackRotation = false;
                    grab.trackScale = false;
                    grab.throwOnDetach = false;
                }
        }

        void SetMenuPresentation(bool visible)
        {
            if (!visible)
            {
                if (menuCamera)
                {
                    menuCamera.cullingMask = cameraMask;
                    menuCamera.clearFlags = cameraClear;
                    menuCamera.backgroundColor = cameraBackground;
                }
                menuCamera = null;
                foreach (var entry in handLayers)
                    if (entry.Key) entry.Key.layer = entry.Value;
                handLayers.Clear();
                return;
            }
            if (menuCamera || !player || !player.view) return;
            menuCamera = player.view;
            cameraMask = menuCamera.cullingMask;
            cameraClear = menuCamera.clearFlags;
            cameraBackground = menuCamera.backgroundColor;
            menuCamera.cullingMask = 1 << TeamSession.MenuLayer;
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = Color.black;
            // Keep tracked controller models and rays visible against the isolated menu.
            SetHandLayer(player.leftHand);
            SetHandLayer(player.rightHand);
        }

        void SetHandLayer(Transform hand)
        {
            if (!hand) return;
            foreach (var renderer in hand.GetComponentsInChildren<Renderer>(true))
            {
                var target = renderer.gameObject;
                if (handLayers.ContainsKey(target)) continue;
                handLayers.Add(target, target.layer);
                target.layer = TeamSession.MenuLayer;
            }
        }

        void InstallFilters()
        {
            if (!player || !session) return;
            foreach (var interactor in player.GetComponentsInChildren<XRBaseInteractor>(true))
            {
                if (installed.Contains(interactor)) continue;
                interactor.hoverFilters.Add(this);
                interactor.selectFilters.Add(this);
                installed.Add(interactor);
            }
        }

        void RemoveFilters()
        {
            foreach (var interactor in installed)
            {
                if (!interactor) continue;
                interactor.hoverFilters.Remove(this);
                interactor.selectFilters.Remove(this);
            }
            installed.Clear();
        }

        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
        {
            if (!session || !TeamSession.GameplayBlocked) return true;
            return interactable != null && session.AllowsMenuTarget(interactable.transform);
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (!session || !TeamSession.GameplayBlocked) return true;
            if (interactable == null) return false;
            if (session.AllowsMenuTarget(interactable.transform)) return true;
            // Do not cancel an existing grab merely because a pause menu was opened.
            // The player's normal release gesture remains available to XRI.
            return interactor != null && interactor.IsSelecting(interactable);
        }
    }
}
