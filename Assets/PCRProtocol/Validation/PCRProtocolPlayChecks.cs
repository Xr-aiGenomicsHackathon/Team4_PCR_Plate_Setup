#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class PCRProtocolPlayChecks : MonoBehaviour
{
    public string Status="idle";
    public List<string> Passed=new List<string>();
    public int throughStep=3;
    PCRProtocolController flow;
    PCRProtocolTestHand hand;
    XRInteractionManager manager;
    public void Run(int through){throughStep=through;StartCoroutine(Safe(Scenario()));}
    IEnumerator Safe(IEnumerator routine)
    {
        Status="running";
        while(true){object current;try{if(!routine.MoveNext())break;current=routine.Current;}catch(Exception e){Status="FAILED: "+e.Message;Debug.LogError(Status);yield break;}yield return current;}
        if(!Status.StartsWith("FAILED"))Status="PASS through step "+throughStep;
    }
    void Check(bool value,string label){if(!value)throw new Exception(label);Passed.Add(label);}
    void Select(IXRSelectInteractor interactor,XRGrabInteractable item)
    {
        foreach(var previous in item.interactorsSelecting.ToArray())manager.SelectExit(previous,item);
        if(interactor==hand)hand.target=item;
        manager.SelectEnter(interactor,item);
    }
    void FreshTip()
    {
        flow.tipSocket.socketActive=true;
        var tip=flow.materialGroups[0].GetComponentsInChildren<PCRProtocolItem>().First(t=>!t.consumed&&t.identity==""&&t!=flow.AttachedTip);
        Select(flow.tipSocket,tip.Grab);
    }
    IEnumerator WaitStep(int step)
    {
        float deadline=Time.time+8;
        while(flow.Step!=step&&Time.time<deadline)yield return null;
        if(flow.Step!=step){Status="FAILED: expected step "+step+" got "+flow.Step;Debug.LogError(Status);StopAllCoroutines();}
    }
    IEnumerator MoveTip(Vector3 point)
    {
        for(int i=0;i<4;i++){hand.transform.position+=point-flow.TipPosition;yield return new WaitForSeconds(.08f);}
    }
    PCRProtocolTarget Source(string id)=>flow.targets.First(t=>t.kind==PCRProtocolTarget.TargetKind.Source&&t.identity==id);
    PCRProtocolTarget Well(int index)=>flow.targets.First(t=>t.kind==PCRProtocolTarget.TargetKind.Well&&t.index==index);
    IEnumerator Scenario()
    {
        flow=FindFirstObjectByType<PCRProtocolController>();manager=FindFirstObjectByType<XRInteractionManager>();
        var clean=FindFirstObjectByType<Stage1CleanBenchController>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        foreach(var name in new[]{"coatDone","glovesDone","cleaningDone"})clean.GetType().GetField(name,flags).SetValue(clean,true);
        clean.GetType().GetMethod("RefreshChecklist",flags).Invoke(clean,null);
        yield return WaitStep(1);yield return null;
        Check(flow.panel.gameObject.activeInHierarchy,"Clean-bench auto-handoff opens protocol panel");
        for(int i=0;i<4;i++)flow.panel.choices[i].isOn=true;
        Check(!flow.panel.confirm.interactable,"Confirm locked until all five materials selected");flow.ConfirmMaterials();Check(!flow.Busy,"Incomplete material selection rejected");
        flow.panel.choices[4].isOn=true;Check(flow.panel.confirm.interactable,"Fifth tick unlocks confirm");flow.panel.confirm.onClick.Invoke();
        Check(flow.materialGroups.All(g=>g.activeInHierarchy),"All five material groups appear");
        yield return WaitStep(2);
        var h=new GameObject("Temporary Validation Hand");h.transform.SetPositionAndRotation(flow.pipette.transform.position,flow.pipette.transform.rotation);hand=h.AddComponent<PCRProtocolTestHand>();hand.keepSelectedTargetValid=true;hand.attachTransform=h.transform;hand.interactionManager=manager;
        flow.pipette.useDynamicAttach=false;Select(hand,flow.pipette);yield return new WaitForSeconds(.2f);
        flow.SetVolume(9);Check(!flow.Busy&&flow.LastWarning.Contains("Wrong volume"),"Wrong setup volume blocks advancement");FreshTip();yield return new WaitForSeconds(.2f);flow.SetVolume(10);
        yield return WaitStep(3);Check(flow.AttachedTip!=null,"Sterile tip fitted in XR socket");
        Check(!flow.Accepts(flow.vortexSocket,flow.AttachedTip),"Vortex rejects wrong object");Select(flow.vortexSocket,flow.masterMix.Grab);yield return WaitStep(4);
        Check(flow.Mixed&&!flow.vortexSocket.hasSelection,"Vortex timer completes and releases tube");
        if(throughStep<=3)yield break;
        yield return MoveTip(Well(0).transform.position);Check(!flow.Transfer(Well(0)),"Empty pipette cannot dispense");
        yield return MoveTip(Source("S1").transform.position);Check(!flow.Transfer(Source("S1")),"Wrong source rejected for master mix");
        for(int i=0;i<12;i++)
        {
            yield return MoveTip(Source("MM").transform.position);Check(flow.Transfer(Source("MM")),"Aspirate MM "+i);
            if(i==0){yield return MoveTip(Well(1).transform.position);Check(!flow.Transfer(Well(1))&&flow.LoadedVolume==10,"Wrong well rejected without losing liquid");}
            yield return MoveTip(Well(i).transform.position);Check(flow.Transfer(Well(i)),"Dispense MM "+i);
        }
        yield return WaitStep(5);Check(flow.MasterVolumes.All(v=>v==10),"All twelve master-mix volumes are 10 µL");
        var old=flow.AttachedTip;flow.EjectTip();Check(flow.AttachedTip==old,"Tip cannot be ejected away from waste");yield return MoveTip(flow.wasteOpening.position);flow.EjectTip();yield return new WaitForSeconds(.4f);FreshTip();yield return WaitStep(6);
        flow.SetVolume(10);Check(!flow.Busy,"Wrong DNA volume rejected");flow.SetVolume(5);yield return WaitStep(7);
        for(int i=0;i<10;i++)
        {
            var source=Source(flow.identities[i]);yield return MoveTip(source.transform.position);Check(flow.Transfer(source),"Aspirate DNA "+i);
            yield return MoveTip(Well(i).transform.position);Check(flow.Transfer(Well(i)),"Dispense DNA "+i);
            yield return MoveTip(source.transform.position);Check(!flow.Transfer(source)&&flow.NeedsTipChange,"Used DNA tip blocked "+i);
            yield return MoveTip(flow.wasteOpening.position);flow.EjectTip();yield return new WaitForSeconds(.4f);FreshTip();yield return new WaitForSeconds(.2f);
        }
        yield return WaitStep(8);
        flow.SetVolume(10);yield return MoveTip(Source("PTC").transform.position);Check(!flow.Transfer(Source("PTC")),"Controls reject wrong volume");flow.SetVolume(5);
        for(int i=0;i<2;i++)
        {
            var src=Source(i==0?"PTC":"NTC");yield return MoveTip(src.transform.position);Check(flow.Transfer(src),"Aspirate control "+i);yield return MoveTip(Well(10+i).transform.position);Check(flow.Transfer(Well(10+i)),"Dispense control "+i);
            if(i==0){yield return MoveTip(Source("NTC").transform.position);Check(!flow.Transfer(Source("NTC")),"PTC tip cannot contaminate NTC");yield return MoveTip(flow.wasteOpening.position);flow.EjectTip();yield return new WaitForSeconds(.4f);FreshTip();yield return new WaitForSeconds(.2f);}
        }
        yield return WaitStep(9);Check(flow.SampleVolumes.All(v=>v==5),"All sample/control additions are 5 µL");
        if(throughStep<=8)yield break;
        hand.target=null;manager.SelectExit(hand,(IXRSelectInteractable)flow.pipette);
        flow.MasterVolumes[0]=0;flow.Inspect(1);Check(!flow.Inspected[1],"Plate check rejects an underfilled well");flow.MasterVolumes[0]=10;
        for(int i=0;i<3;i++)flow.Inspect(i);yield return WaitStep(10);
        flow.PressEdge(0,1);Check(flow.EdgeProgress[0]==0,"Cannot press seal edges before placing seal");Select(flow.sealSocket,flow.seal.Grab);yield return new WaitForSeconds(.3f);
        Check(flow.SealPlaced,"Optical seal snaps into plate socket");
        for(int i=0;i<4;i++)
        {
            var edge=flow.targets.First(t=>t.kind==PCRProtocolTarget.TargetKind.SealEdge&&t.index==i).GetComponent<XRSimpleInteractable>();hand.target=edge;manager.SelectEnter(hand,(IXRSelectInteractable)edge);yield return new WaitForSeconds(flow.edgePressSeconds+.1f);hand.target=null;manager.SelectExit(hand,(IXRSelectInteractable)edge);
        }
        yield return WaitStep(11);Check(flow.EdgeProgress.All(v=>v>=1),"All four physical edge holds completed");Check(flow.seal.transform.IsChildOf(flow.plate.transform),"Seal travels with plate");
        Select(flow.handoverSocket,flow.plate.Grab);yield return new WaitForSeconds(flow.successSeconds+.2f);Check(flow.Finished,"Sealed plate handover finishes sequence");
    }
}
#endif
