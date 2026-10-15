using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public sealed class PCRProtocolTarget : MonoBehaviour
{
    public enum TargetKind { Source, Well, Inspection, SealEdge }
    public TargetKind kind;
    public PCRProtocolController flow;
    public string identity;
    public int index;
    public float radius=.025f;
    bool pressing;
    void OnEnable()
    {
        var i=GetComponent<XRSimpleInteractable>();
        if(i){i.selectEntered.AddListener(Selected);i.selectExited.AddListener(Released);}
    }
    void OnDisable()
    {
        pressing=false;
        var i=GetComponent<XRSimpleInteractable>();
        if(i){i.selectEntered.RemoveListener(Selected);i.selectExited.RemoveListener(Released);}
    }
    void Selected(SelectEnterEventArgs a)
    {
        if(kind==TargetKind.Inspection) flow.Inspect(index);
        if(kind==TargetKind.SealEdge) pressing=true;
    }
    void Released(SelectExitEventArgs a) { pressing=false; }
    void Update() { if(pressing&&kind==TargetKind.SealEdge) flow.PressEdge(index,Time.deltaTime); }
}
