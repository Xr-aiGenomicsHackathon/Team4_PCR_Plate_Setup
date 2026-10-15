#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
public sealed class PCRProtocolTestHand : XRBaseInteractor
{
    public IXRInteractable target;
    public override bool isSelectActive => target!=null;
    public override void GetValidTargets(List<IXRInteractable> targets){targets.Clear();if(target!=null)targets.Add(target);}
}
#endif
