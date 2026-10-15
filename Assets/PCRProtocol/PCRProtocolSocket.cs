using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class PCRProtocolSocket : XRSocketInteractor
{
    public enum SocketRole { Tip, Vortex, Seal, Handover }
    public PCRProtocolController flow;
    public SocketRole role;
    public override bool CanHover(IXRHoverInteractable item) =>
        flow != null && flow.Accepts(this, item.transform.GetComponent<PCRProtocolItem>()) && base.CanHover(item);
    public override bool CanSelect(IXRSelectInteractable item) =>
        flow != null && flow.Accepts(this, item.transform.GetComponent<PCRProtocolItem>()) && base.CanSelect(item);
    protected override void OnEnable()
    {
        base.OnEnable();
        selectEntered.AddListener(Entered);
        selectExited.AddListener(Exited);
    }
    protected override void OnDisable()
    {
        selectEntered.RemoveListener(Entered);
        selectExited.RemoveListener(Exited);
        base.OnDisable();
    }
    void Entered(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs a) => flow?.Docked(this, a.interactableObject.transform.GetComponent<PCRProtocolItem>());
    void Exited(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs a) => flow?.Undocked(this, a.interactableObject.transform.GetComponent<PCRProtocolItem>());
}
