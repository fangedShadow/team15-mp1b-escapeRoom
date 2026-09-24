using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace BlackTide.MP1B
{
    /// <summary>A mouse-controlled XRI hand, preserving the normal select and activate events.</summary>
    public sealed class CabinDesktopInteractor : XRBaseInteractor, IXRActivateInteractor
    {
        XRBaseInteractable aimedTarget;
        XRBaseInteractable selectedTarget;
        XRBaseInteractable activatedTarget;

        public override bool isSelectActive => selectedTarget && base.isSelectActive;
        public bool shouldActivate => false;
        public bool shouldDeactivate => false;
        public override XRBaseInteractable.MovementType? selectedInteractableMovementTypeOverride =>
            XRBaseInteractable.MovementType.Instantaneous;

        public void SetAim(XRBaseInteractable target) => aimedTarget = target;

        public override void GetValidTargets(List<IXRInteractable> targets)
        {
            targets.Clear();
            if (selectedTarget && selectedTarget.isActiveAndEnabled) targets.Add(selectedTarget);
            if (aimedTarget && aimedTarget.isActiveAndEnabled && aimedTarget != selectedTarget)
                targets.Add(aimedTarget);
        }

        // The manager may maintain a requested selection, but must not automatically grab a hovered item.
        public override bool CanSelect(IXRSelectInteractable interactable) =>
            selectedTarget && ReferenceEquals(selectedTarget, interactable);

        public bool BeginSelection(XRBaseInteractable target)
        {
            if (!target || !target.isActiveAndEnabled || target.isSelected || !interactionManager) return false;
            EndSelection();
            selectedTarget = target;
            if (!interactionManager.IsSelectPossible(this, target))
            {
                selectedTarget = null;
                return false;
            }
            interactionManager.SelectEnter((IXRSelectInteractor)this, target);
            return target && IsSelecting(target);
        }

        public void EndSelection()
        {
            Deactivate();
            var target = selectedTarget;
            selectedTarget = null;
            if (target && interactionManager && IsSelecting(target))
                interactionManager.SelectExit((IXRSelectInteractor)this, target);
        }

        public void Activate(XRBaseInteractable target)
        {
            if (!target || !target.isActiveAndEnabled || activatedTarget == target) return;
            Deactivate();
            activatedTarget = target;
            ((IXRActivateInteractable)target).OnActivated(new ActivateEventArgs
            {
                interactorObject = this,
                interactableObject = target
            });
        }

        public void Deactivate()
        {
            var target = activatedTarget;
            activatedTarget = null;
            if (target)
                ((IXRActivateInteractable)target).OnDeactivated(new DeactivateEventArgs
                {
                    interactorObject = this,
                    interactableObject = target
                });
        }

        public void GetActivateTargets(List<IXRActivateInteractable> targets)
        {
            targets.Clear();
            if (selectedTarget) targets.Add(selectedTarget);
            else if (aimedTarget) targets.Add(aimedTarget);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if (ReferenceEquals(selectedTarget, args.interactableObject))
            {
                Deactivate();
                selectedTarget = null;
            }
        }

        protected override void OnDisable()
        {
            aimedTarget = null;
            EndSelection();
            base.OnDisable();
        }
    }
}
