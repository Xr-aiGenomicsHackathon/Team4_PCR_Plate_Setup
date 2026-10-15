using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class PCRProtocolController : MonoBehaviour
{
    [Header("Shared UI and equipment")]
    public PCRProtocolPanel panel;
    public XRGrabInteractable pipette;
    public Transform pipetteNozzle,wasteOpening;
    public TMP_Text pipetteDisplay;
    public PCRProtocolItem masterMix,plate,seal;
    public PCRProtocolSocket tipSocket,vortexSocket,sealSocket,handoverSocket;
    public GameObject[] materialGroups=new GameObject[5];
    public GameObject[] legacyPanels=new GameObject[0];
    public PCRProtocolTarget[] targets=new PCRProtocolTarget[0];
    public Transform[] liquidVisuals=new Transform[12];
    public GameObject inspectionRoot,edgesRoot;
    public Image[] edgeRings=new Image[4];
    [Header("Training protocol: confirm timings and well map with scientist")]
    public float successSeconds=2f,gentleMixSeconds=3f,edgePressSeconds=1f;
    public string[] wellNames={"A1","A2","A3","A4","A5","A6","A7","A8","A9","A10","A11","A12"};
    public string[] identities={"S1","S1","S2","S2","S3","S3","S4","S4","S5","S5","PTC","NTC"};
    public AudioClip[] instructionClips=new AudioClip[11];
    public AudioSource audioSource;
    public int Step {get;private set;}
    public int Volume {get;private set;}=1;
    public bool Busy {get;private set;}
    public bool Finished {get;private set;}
    public bool SoundEnabled {get;private set;}=true;
    public string LastWarning {get;private set;}="";
    public PCRProtocolItem AttachedTip {get;private set;}
    public bool Mixed {get;private set;}
    public int MasterCount {get;private set;}
    public int SampleCount {get;private set;}
    public int ControlCount {get;private set;}
    public float LoadedVolume {get;private set;}
    public bool NeedsTipChange {get;private set;}
    public bool SealPlaced {get;private set;}
    public float[] MasterVolumes {get;}=new float[12];
    public float[] SampleVolumes {get;}=new float[12];
    public string[] AddedIdentities {get;}=new string[12];
    public float[] EdgeProgress {get;}=new float[4];
    public bool[] Inspected {get;}=new bool[3];
    readonly bool[] selectedMaterials=new bool[5];
    readonly Dictionary<Renderer,MaterialPropertyBlock> originalColors=new Dictionary<Renderer,MaterialPropertyBlock>();
    string loadedIdentity="";
    float warningUntil,mixElapsed;
    bool vortexRunning;
    bool started;
    int tipSerial,lastUsedTipSerial;
    PCRProtocolItem lastUsedTip;
    public bool Held => pipette && pipette.interactorsSelecting.Any(i=>!(i is XRSocketInteractor));
    public bool HasInstructionAudio => Step>0&&Step<=instructionClips.Length&&instructionClips[Step-1]!=null;
    public Vector3 TipPosition => AttachedTip&&AttachedTip.liquidPoint?AttachedTip.liquidPoint.position:pipetteNozzle.position;

    void Start()
    {
        foreach(var root in materialGroups)if(root)root.SetActive(false);
        foreach(var root in legacyPanels)if(root)root.SetActive(false);
        panel.gameObject.SetActive(false);
        if(inspectionRoot)inspectionRoot.SetActive(false);if(edgesRoot)edgesRoot.SetActive(false);
        if(plate&&plate.Grab)plate.Grab.enabled=false;
        pipette.activated.AddListener(Activated);pipette.selectEntered.AddListener(PickedPipette);
        if(pipetteDisplay)pipetteDisplay.text="1 µL";
    }
    void OnDestroy(){if(pipette){pipette.activated.RemoveListener(Activated);pipette.selectEntered.RemoveListener(PickedPipette);}RestoreHighlights();}
    public void BeginProtocol()
    {
        if(started)return;started=true;Step=1;panel.gameObject.SetActive(true);panel.Place();EnterStep();
    }
    void PickedPipette(SelectEnterEventArgs a){if(Step==2)StartCoroutine(CheckSetupNextFrame());}
    IEnumerator CheckSetupNextFrame(){yield return null;CheckSetup();}
    public void SelectMaterial(int index,bool value){if(Step!=1||Busy||index<0||index>=5)return;selectedMaterials[index]=value;panel.confirm.interactable=selectedMaterials.All(v=>v);}
    public void ConfirmMaterials()
    {
        if(Step!=1||Busy)return;
        if(!selectedMaterials.All(v=>v)){Warn("Tick all five materials before confirming.");return;}
        foreach(var root in materialGroups)if(root)root.SetActive(true);
        Complete("Materials ready — all five items are on the bench.");
    }
    public void SetVolume(int value)
    {
        if(Busy||!(Step==2||Step==6||Step==8))return;
        if(!Held){Warn("Pick up the training pipette before adjusting its dial.");return;}
        Volume=Mathf.Clamp(value,1,20);if(pipetteDisplay)pipetteDisplay.text=Volume+" µL";
        panel.volumeLabel.text=Volume+" µL";
        int expected=Step==2?10:5;
        if(Volume!=expected){Warn("Wrong volume — set "+expected+" µL.");return;}
        ClearWarning();
        if(Step==2)CheckSetup();else if(Step==6)Complete("Pipette set to 5 µL for DNA samples.");
    }
    void CheckSetup(){if(Step==2&&!Busy&&Held&&AttachedTip&&!AttachedTip.consumed&&Volume==10)Complete("Sterile tip fitted. Pipette set to 10 µL.");}
    public bool Accepts(PCRProtocolSocket socket,PCRProtocolItem item)
    {
        if(!item||item.consumed||!started)return false;
        switch(socket.role)
        {
            case PCRProtocolSocket.SocketRole.Tip:return item.kind==PCRProtocolItem.ItemKind.Tip&&Step>=2&&Step<=8&&(!AttachedTip||AttachedTip==item);
            case PCRProtocolSocket.SocketRole.Vortex:return item==masterMix&&Step==3&&!Mixed;
            case PCRProtocolSocket.SocketRole.Seal:return item==seal&&Step>=10&&!Finished;
            case PCRProtocolSocket.SocketRole.Handover:return item==plate&&Step==11&&SealPlaced&&EdgeProgress.All(v=>v>=1);
        }
        return false;
    }
    public void Docked(PCRProtocolSocket socket,PCRProtocolItem item)
    {
        if(!item)return;
        if(socket==tipSocket)
        {
            AttachedTip=item;tipSerial++;ClearWarning();CheckSetup();
            if(Step==5&&!Busy&&tipSerial>lastUsedTipSerial&&lastUsedTip&&lastUsedTip.consumed)Complete("Fresh sterile tip fitted.");
            if((Step==7||Step==8)&&NeedsTipChange&&tipSerial>lastUsedTipSerial&&lastUsedTip&&lastUsedTip.consumed)
            {NeedsTipChange=false;if(Step==7&&SampleCount==10)Complete("All five DNA samples added to their duplicate wells.");else Refresh();}
        }
        else if(socket==vortexSocket&&Step==3&&!Busy){mixElapsed=0;vortexRunning=true;ClearWarning();}
        else if(socket==sealSocket&&Step==10&&!Busy){SealPlaced=true;ClearWarning();if(edgesRoot)edgesRoot.SetActive(true);Refresh();}
        else if(socket==handoverSocket&&Step==11&&!Busy)Complete("Sealed plate handed to the next team.");
    }
    public void Undocked(PCRProtocolSocket socket,PCRProtocolItem item)
    {
        if(socket==tipSocket&&item==AttachedTip)AttachedTip=null;
        if(socket==vortexSocket&&!Mixed){vortexRunning=false;mixElapsed=0;}
        if(socket==sealSocket&&Step==10&&!Busy){SealPlaced=false;Array.Clear(EdgeProgress,0,4);if(edgesRoot)edgesRoot.SetActive(false);}
    }
    void Update()
    {
        if(panel&&Time.time>warningUntil&&LastWarning.Length>0)ClearWarning();
        if(Step==3&&!Busy&&vortexRunning&&vortexSocket.IsSelecting(masterMix.Grab))
        {
            mixElapsed+=Time.deltaTime;panel.vortexRing.fillAmount=Mathf.Clamp01(mixElapsed/gentleMixSeconds);
            if(mixElapsed>=gentleMixSeconds)
            {
                Mixed=true;vortexRunning=false;vortexSocket.socketActive=false;
                vortexSocket.interactionManager.SelectExit((IXRSelectInteractor)vortexSocket,(IXRSelectInteractable)masterMix.Grab);
                Complete("Master mix gently mixed. Tube released.");
            }
        }
    }
    public void EjectTip()
    {
        if(Busy||Step<2||Step>8)return;
        if(!Held){Warn("Hold the pipette to eject its tip.");return;}
        if(!AttachedTip){Warn("No tip fitted — fit a sterile tip.");return;}
        if(Vector3.Distance(TipPosition,wasteOpening.position)>.14f){Warn("Move the pipette tip over the waste bin before ejecting.");return;}
        var old=AttachedTip;old.consumed=true;tipSocket.socketActive=false;
        tipSocket.interactionManager.SelectExit((IXRSelectInteractor)tipSocket,(IXRSelectInteractable)old.Grab);
        AttachedTip=null;LoadedVolume=0;loadedIdentity="";ClearWarning();
        StartCoroutine(DisposeTip(old));Refresh();
    }
    IEnumerator DisposeTip(PCRProtocolItem old){yield return null;if(old)old.gameObject.SetActive(false);yield return new WaitForSeconds(.3f);tipSocket.socketActive=true;}
    void Activated(ActivateEventArgs a)
    {
        if(Busy)return;
        if(!Held){Warn("Hold the training pipette.");return;}
        var near=targets.Where(t=>t&&t.gameObject.activeInHierarchy&&(t.kind==PCRProtocolTarget.TargetKind.Source||t.kind==PCRProtocolTarget.TargetKind.Well))
            .Where(t=>Vector3.Distance(TipPosition,t.transform.position)<=t.radius).OrderBy(t=>Vector3.Distance(TipPosition,t.transform.position)).FirstOrDefault();
        if(!near){Warn("Place the tip at the highlighted tube opening or well, then press Trigger.");return;}
        Transfer(near);
    }
    public bool Transfer(PCRProtocolTarget target)
    {
        if(Busy||!(Step==4||Step==7||Step==8))return false;
        if(!Held||!AttachedTip){Warn("Hold the pipette and fit a sterile tip.");return false;}
        if(Vector3.Distance(TipPosition,target.transform.position)>target.radius){Warn("Bring the pipette tip to the highlighted target.");return false;}
        int required=Step==4?10:5;if(Volume!=required){Warn("Wrong volume — set "+required+" µL.");return false;}
        if(NeedsTipChange){Warn("Change your tip before the next sample or control.");return false;}
        int next=Step==4?MasterCount:Step==7?SampleCount:10+ControlCount;
        if(next>=12)return false;
        string source=Step==4?"MM":identities[next];
        if(target.kind==PCRProtocolTarget.TargetKind.Source)
        {
            if(LoadedVolume>0){Warn("Pipette already contains liquid — dispense into "+wellNames[next]+".");return false;}
            if(target.identity!=source){Warn("Wrong tube — use "+source+".");return false;}
            if(Step==4&&!Mixed){Warn("Mix the master mix first.");return false;}
            if(AttachedTip.identity.Length>0&&(Step!=4||AttachedTip.identity!="MM")){Warn("Change your tip — this tip has already been used.");return false;}
            AttachedTip.identity=source;loadedIdentity=source;LoadedVolume=required;ClearWarning();Refresh();return true;
        }
        if(target.kind!=PCRProtocolTarget.TargetKind.Well)return false;
        if(target.index!=next){Warn("Wrong well — use "+wellNames[next]+".");return false;}
        if(LoadedVolume!=required||loadedIdentity!=source){Warn("Draw up "+required+" µL of "+source+" first.");return false;}
        if(Step!=4&&MasterVolumes[next]!=10){Warn("This well needs 10 µL master mix first.");return false;}
        if(Step==4)MasterVolumes[next]=required;else{SampleVolumes[next]=required;AddedIdentities[next]=source;}
        LoadedVolume=0;loadedIdentity="";lastUsedTipSerial=tipSerial;lastUsedTip=AttachedTip;ClearWarning();StartCoroutine(AnimateFill(next));
        if(Step==4){MasterCount++;if(MasterCount==12)Complete("10 µL master mix added to all 12 planned wells.");else Refresh();}
        else if(Step==7){SampleCount++;NeedsTipChange=true;Refresh();}
        else {ControlCount++;if(ControlCount==2)Complete("Positive control and NTC added with fresh tips.");else{NeedsTipChange=true;Refresh();}}
        return true;
    }
    IEnumerator AnimateFill(int i)
    {
        if(i>=liquidVisuals.Length||!liquidVisuals[i])yield break;
        var liquid=liquidVisuals[i];liquid.gameObject.SetActive(true);var final=liquid.localScale;float target=(MasterVolumes[i]+SampleVolumes[i])/15f;float t=0;
        while(t<.35f){t+=Time.deltaTime;liquid.localScale=new Vector3(final.x,Mathf.Max(.0001f,.002f*target*Mathf.Clamp01(t/.35f)),final.z);yield return null;}
    }
    public void Inspect(int index)
    {
        if(Step!=9||Busy||index<0||index>2)return;
        if(index==1&&(MasterVolumes.Any(v=>v!=10)||SampleVolumes.Any(v=>v!=5))){Warn("An empty or underfilled well remains. Plate check cannot pass.");return;}
        if(index==2&&Enumerable.Range(0,12).Any(i=>AddedIdentities[i]!=identities[i])){Warn("A well identity does not match the plate map.");return;}
        Inspected[index]=true;ClearWarning();Refresh();if(Inspected.All(v=>v))Complete("Plate inspection passed: no bubbles, empty wells, or misplaced samples.");
    }
    public void PressEdge(int index,float seconds)
    {
        if(Step!=10||Busy||index<0||index>=4)return;
        if(!SealPlaced){Warn("Place the optical seal on the plate first.");return;}
        EdgeProgress[index]=Mathf.Clamp01(EdgeProgress[index]+Mathf.Max(0,seconds)/edgePressSeconds);
        if(index<edgeRings.Length&&edgeRings[index])edgeRings[index].fillAmount=EdgeProgress[index];
        if(EdgeProgress.All(v=>v>=1))Complete("All four edges pressed. Plate sealed.");
    }
    void Complete(string message)
    {
        if(Busy)return;Busy=true;ClearWarning();panel.banner.text=message;RestoreHighlights();StartCoroutine(Advance());
    }
    IEnumerator Advance()
    {
        yield return new WaitForSeconds(successSeconds);panel.banner.text="";
        if(Step==11){Finished=true;Busy=false;panel.instruction.text="Plate handed over";panel.detail.text="The next team can now load the sealed plate.";panel.progress.rectTransform.sizeDelta=new Vector2(910,12);yield break;}
        Step++;Busy=false;EnterStep();
    }
    void EnterStep()
    {
        if(Step==1)panel.confirm.interactable=false;
        if(Step==5)lastUsedTipSerial=tipSerial;
        if(inspectionRoot)inspectionRoot.SetActive(Step==9);
        if(edgesRoot)edgesRoot.SetActive(Step==10&&SealPlaced);
        if(Step==11)
        {
            sealSocket.socketActive=false;
            if(sealSocket.IsSelecting(seal.Grab))sealSocket.interactionManager.SelectExit((IXRSelectInteractor)sealSocket,(IXRSelectInteractable)seal.Grab);
            seal.Grab.enabled=false;foreach(var collider in seal.GetComponentsInChildren<Collider>())collider.enabled=false;seal.transform.SetParent(plate.transform,true);seal.GetComponent<Rigidbody>().isKinematic=true;
            plate.Grab.enabled=true;
        }
        Refresh();
    }
    void Refresh()
    {
        if(Step==0)return;
        string[] titles={"Select your materials","Fit a sterile tip · set 10 µL","Gently mix the master mix","Dispense master mix","Change your tip","Set the pipette to 5 µL","Add DNA samples","Add the controls","Inspect the plate","Apply and press the optical seal","Plate ready for loading"};
        string body="";
        switch(Step)
        {
            case 1:body="Tick all five materials, then Confirm. They will appear at their bench positions.";break;
            case 2:body="Grab the highlighted pipette. Lower its nozzle onto a fresh tip. Grip and twist the dial, or use ± buttons, to set 10 µL.";break;
            case 3:body="Place the MM tube into the highlighted vortex socket. Keep it docked while the gentle-mix ring fills.";break;
            case 4:body=$"{MasterCount}/12 wells complete. "+TransferInstruction(MasterCount,"MM",10);break;
            case 5:body="Hold the pipette over the waste opening and press Eject tip. Fit a new sterile tip from the box.";break;
            case 6:body="Hold the pipette and turn its dial (or use ±) to 5 µL.";break;
            case 7:body=NeedsTipChange?"Change your tip: eject into waste and fit a new one before continuing.":TransferInstruction(SampleCount,identities[Mathf.Min(SampleCount,9)],5);break;
            case 8:body=NeedsTipChange?"Change your tip before the NTC. Use a fresh tip for each control.":"Check 5 µL. "+TransferInstruction(10+ControlCount,ControlCount==0?"PTC":"NTC",5);break;
            case 9:body=$"{Inspected.Count(v=>v)}/3 checks confirmed. Select the plate markers: bubbles, fill volumes, and well identities. No invalid transfer has been allowed.";break;
            case 10:body=SealPlaced?"Hold Trigger on each of the four highlighted edges until its ring fills.":"Pick up the optical film and release it over the plate. The socket aligns it. Then press all four edges.";break;
            case 11:body="Pick up the entire sealed plate and release it into the NEXT TEAM handover socket.";break;
        }
        panel.Show(Step,titles[Step-1],body,(Step-1)/11f);UpdateHighlights();
    }
    string TransferInstruction(int index,string source,int volume)=>index>=12?"All planned wells filled.":$"{(LoadedVolume>0?"Dispense":"Aspirate")} {volume} µL {(LoadedVolume>0?"into "+wellNames[index]:"from "+source)}. Put the tip at the target and press Trigger. Loaded: {LoadedVolume:0}/{volume} µL.";
    public void Warn(string text){LastWarning=text;warningUntil=Time.time+3f;if(panel)panel.warning.text=text;}
    void ClearWarning(){LastWarning="";if(panel)panel.warning.text="";}
    void UpdateHighlights()
    {
        RestoreHighlights();Transform cue=null;
        if(Step==2||Step==6)cue=pipette.transform;
        if(Step==3)cue=masterMix.transform;
        if(Step==5||NeedsTipChange)cue=AttachedTip?wasteOpening:materialGroups[0].transform;
        if(Step==4||Step==7||Step==8)
        {
            int index=Step==4?MasterCount:Step==7?SampleCount:10+ControlCount;
            if(!NeedsTipChange&&index<12){string id=Step==4?"MM":identities[index];var t=targets.FirstOrDefault(x=>x&&(LoadedVolume>0?x.kind==PCRProtocolTarget.TargetKind.Well&&x.index==index:x.kind==PCRProtocolTarget.TargetKind.Source&&x.identity==id));if(t)cue=t.transform;}
        }
        if(Step==10)cue=SealPlaced?edgesRoot.transform:seal.transform;if(Step==11)cue=plate.transform;
        if(!cue)return;
        foreach(var r in cue.GetComponentsInChildren<Renderer>(true))
        {var old=new MaterialPropertyBlock();r.GetPropertyBlock(old);originalColors[r]=old;var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);b.SetColor("_BaseColor",new Color(.3f,1,.7f));b.SetColor("_Color",new Color(.3f,1,.7f));b.SetColor("_EmissionColor",new Color(.12f,.55f,.3f));r.SetPropertyBlock(b);}
    }
    void RestoreHighlights(){foreach(var pair in originalColors)if(pair.Key)pair.Key.SetPropertyBlock(pair.Value);originalColors.Clear();}
    public void ToggleSound(){SoundEnabled=!SoundEnabled;if(audioSource){audioSource.mute=!SoundEnabled;if(!SoundEnabled)audioSource.Stop();}}
    public void PlayInstruction(){if(SoundEnabled&&HasInstructionAudio&&audioSource){audioSource.clip=instructionClips[Step-1];audioSource.Play();}}
}
