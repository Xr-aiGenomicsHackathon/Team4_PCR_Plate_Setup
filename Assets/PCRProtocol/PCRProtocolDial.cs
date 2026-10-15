using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class PCRProtocolDial : MonoBehaviour
{
    public PCRProtocolController flow;
    Transform hand;
    Quaternion start;
    int initial;
    void OnEnable() { GetComponent<XRSimpleInteractable>().selectEntered.AddListener(Begin); GetComponent<XRSimpleInteractable>().selectExited.AddListener(End); }
    void OnDisable() { GetComponent<XRSimpleInteractable>().selectEntered.RemoveListener(Begin); GetComponent<XRSimpleInteractable>().selectExited.RemoveListener(End); }
    void Begin(SelectEnterEventArgs a) { hand=a.interactorObject.transform; start=hand.rotation; initial=flow.Volume; }
    void End(SelectExitEventArgs a) { hand=null; }
    void Update()
    {
        if(!hand) return;
        (hand.rotation*Quaternion.Inverse(start)).ToAngleAxis(out float angle,out Vector3 axis);
        if(angle>180) angle-=360;
        flow.SetVolume(initial+Mathf.RoundToInt(angle*Mathf.Sign(Vector3.Dot(axis,transform.up))/12f));
    }
}
