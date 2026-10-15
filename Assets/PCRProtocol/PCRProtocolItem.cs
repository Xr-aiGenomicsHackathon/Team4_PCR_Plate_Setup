using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public sealed class PCRProtocolItem : MonoBehaviour
{
    public enum ItemKind { Tip, MasterMix, Sample, Plate, Seal }
    public ItemKind kind;
    public string identity;
    public bool consumed;
    public Transform liquidPoint;
    public XRGrabInteractable Grab => GetComponent<XRGrabInteractable>();
}
