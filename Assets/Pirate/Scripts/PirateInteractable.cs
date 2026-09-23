using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlackTide
{
    public enum PirateAction { Seal, Chest, Map, Bell, Cannon, Light, Spawn, Teleport, Reset }

    public sealed class PirateInteractable : MonoBehaviour
    {
        public PirateGame game;
        public PirateAction action;
        public int sealIndex;
        public GameObject outline;
        public ParticleSystem feedback;
        public AudioSource sound;
        XRSimpleInteractable xr;
        float lastActivation = -10f;

        void Start()
        {
            xr = GetComponent<XRSimpleInteractable>();
            if (!xr) return;
            xr.selectEntered.AddListener(OnSelected);
            xr.hoverEntered.AddListener(OnHoverEntered);
            xr.hoverExited.AddListener(OnHoverExited);
        }
        void OnDestroy()
        {
            if (!xr) return;
            xr.selectEntered.RemoveListener(OnSelected);
            xr.hoverEntered.RemoveListener(OnHoverEntered);
            xr.hoverExited.RemoveListener(OnHoverExited);
        }
        void OnSelected(SelectEnterEventArgs _) => Activate();
        void OnHoverEntered(HoverEnterEventArgs _) => Highlight(true);
        void OnHoverExited(HoverExitEventArgs _) => Highlight(xr && xr.isHovered);
        public void Highlight(bool value) { if (outline) outline.SetActive(value); }

        public void Activate()
        {
            if (!game || Time.unscaledTime - lastActivation < 0.3f) return;
            lastActivation = Time.unscaledTime;
            if (feedback) feedback.Play();
            if (sound) sound.Play();
            switch (action)
            {
                case PirateAction.Seal: game.Collect(sealIndex, gameObject); break;
                case PirateAction.Chest: game.OpenChest(); break;
                case PirateAction.Map: game.ShowMessage("THE CAPTAIN'S ROUTE\nFind three seals: chart table, cargo barrels, ship's wheel."); break;
                case PirateAction.Bell: game.ShowMessage("Ship's bell rung. Listen as you move around it."); break;
                case PirateAction.Cannon: game.FireCannon(transform); break;
                case PirateAction.Light: game.CycleLight(); break;
                case PirateAction.Spawn: game.SpawnSignal(); break;
                case PirateAction.Teleport: game.BreakOut(); break;
                case PirateAction.Reset: game.ResetVoyage(); break;
            }
        }
    }
}
