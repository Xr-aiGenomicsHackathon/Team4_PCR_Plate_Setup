using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Each rack position accepts only its assigned pipette.
public sealed class PipetteRackSocket : XRSocketInteractor
{
    [SerializeField] private XRGrabInteractable assignedPipette;
    public XRGrabInteractable AssignedPipette { get => assignedPipette; set => assignedPipette = value; }

    public override bool CanHover(IXRHoverInteractable interactable)
    {
        return ReferenceEquals(interactable, assignedPipette) && base.CanHover(interactable);
    }

    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        return ReferenceEquals(interactable, assignedPipette) && base.CanSelect(interactable);
    }
}
