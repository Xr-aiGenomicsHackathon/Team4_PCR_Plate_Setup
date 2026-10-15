using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// An installed tip leaves through the eject control, not by being pulled off by another hand.
public sealed class PCRProtocolTipFilter : MonoBehaviour, IXRSelectFilter
{
    public PCRProtocolController flow;
    public bool canProcess => isActiveAndEnabled;
    XRGrabInteractable grab;
    void OnEnable(){grab=GetComponent<XRGrabInteractable>();if(grab)grab.selectFilters.Add(this);}
    void OnDisable(){if(grab)grab.selectFilters.Remove(this);}
    public bool Process(IXRSelectInteractor interactor,IXRSelectInteractable interactable)
    {return flow==null||flow.AttachedTip==null||flow.AttachedTip.gameObject!=gameObject||interactor is XRSocketInteractor;}
}
