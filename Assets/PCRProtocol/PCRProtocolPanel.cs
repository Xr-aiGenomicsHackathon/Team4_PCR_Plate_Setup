using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public sealed class PCRProtocolPanel : MonoBehaviour
{
    public PCRProtocolController flow;
    public TMP_FontAsset font;
    public Sprite ringSprite;
    public TMP_Text stepLabel,instruction,detail,warning,banner,volumeLabel;
    public Image progress,vortexRing,liquidMeter;
    public GameObject liquidMeterTrack;
    public Toggle[] choices;
    public Button confirm,minus,plus,eject,listen,sound,recenter;
    public GameObject materialChoices;
    public void Build()
    {
        var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
        gameObject.AddComponent<CanvasScaler>();
        var ray=gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();ray.ignoreReversedGraphics=false;ray.checkFor3DOcclusion=false;
        var rect=(RectTransform)transform;rect.sizeDelta=new Vector2(1000,840);rect.localScale=Vector3.one*.001f;
        Box("Background",transform,new Vector2(0,0),new Vector2(1000,840),new Color(.035f,.075f,.10f,.98f));
        stepLabel=Text("Step",transform,"PCR PLATING",new Vector2(-455,350),new Vector2(910,48),30);
        instruction=Text("Instruction",transform,"Complete clean-bench setup",new Vector2(-455,285),new Vector2(910,110),35);
        detail=Text("Detail",transform,"",new Vector2(-455,160),new Vector2(910,100),25);
        var track=Box("Progress Track",transform,new Vector2(0,-330),new Vector2(910,12),new Color(.14f,.24f,.28f));
        progress=Box("Progress",track.transform,Vector2.zero,new Vector2(0,12),new Color(.1f,.85f,.65f));
        progress.rectTransform.pivot=new Vector2(0,.5f);progress.rectTransform.anchoredPosition=new Vector2(-455,0);
        warning=Text("Warning",transform,"",new Vector2(-455,-190),new Vector2(910,60),27);warning.color=new Color(1,.3f,.3f);
        banner=Text("Success",transform,"",new Vector2(-455,-255),new Vector2(910,55),29);banner.color=new Color(.3f,1,.66f);
        materialChoices=new GameObject("Material Checklist",typeof(RectTransform));materialChoices.transform.SetParent(transform,false);
        choices=new Toggle[5];string[] names={"Pipette tips","PCR plate","Optical seal","Master mix","DNA samples"};
        for(int i=0;i<5;i++)
        {
            var bg=Box(names[i],materialChoices.transform,new Vector2(-190,30-i*49),new Vector2(530,43),new Color(.10f,.20f,.25f));
            bg.raycastTarget=true;var toggle=bg.gameObject.AddComponent<Toggle>();toggle.targetGraphic=bg;
            var outline=Box("Checkbox outline",bg.transform,new Vector2(-235,0),new Vector2(32,32),new Color(.45f,.65f,.7f));outline.transform.SetAsFirstSibling();Box("Checkbox empty",outline.transform,Vector2.zero,new Vector2(25,25),new Color(.05f,.12f,.16f));
            var mark=Box("Check",bg.transform,new Vector2(-235,0),new Vector2(28,28),new Color(.1f,.85f,.65f));toggle.graphic=mark;
            Text("Label",bg.transform,names[i],new Vector2(-205,18),new Vector2(440,40),25);
            choices[i]=toggle;
        }
        confirm=Button("Confirm materials",new Vector2(290,-100),new Vector2(300,70));
        minus=Button("− 1 µL",new Vector2(-340,10),new Vector2(210,65));
        plus=Button("+ 1 µL",new Vector2(340,10),new Vector2(210,65));
        volumeLabel=Text("Volume",transform,"10 µL",new Vector2(-110,42),new Vector2(220,65),40);volumeLabel.alignment=TextAlignmentOptions.Center;
        eject=Button("Eject tip over waste",new Vector2(0,-85),new Vector2(500,70));
        vortexRing=Box("Vortex ring",transform,new Vector2(350,40),new Vector2(105,105),new Color(.2f,.85f,.65f));vortexRing.sprite=ringSprite;vortexRing.type=Image.Type.Filled;vortexRing.fillMethod=Image.FillMethod.Radial360;vortexRing.fillAmount=0;
        liquidMeterTrack=Box("Liquid Meter Track",transform,new Vector2(0,-147),new Vector2(320,14),new Color(.12f,.22f,.27f)).gameObject;
        liquidMeter=Box("Liquid Meter",liquidMeterTrack.transform,Vector2.zero,new Vector2(0,14),new Color(.2f,.6f,1));liquidMeter.rectTransform.pivot=new Vector2(0,.5f);liquidMeter.rectTransform.anchoredPosition=new Vector2(-160,0);
        listen=Button("Listen",new Vector2(-335,-380),new Vector2(210,55));
        sound=Button("Sound: on",new Vector2(-85,-380),new Vector2(220,55));
        recenter=Button("Recenter panel",new Vector2(265,-380),new Vector2(370,55));
    }
    void Start()
    {
        for(int i=0;i<choices.Length;i++){int index=i;choices[i].onValueChanged.AddListener(v=>flow.SelectMaterial(index,v));}
        confirm.onClick.AddListener(flow.ConfirmMaterials);minus.onClick.AddListener(()=>flow.SetVolume(flow.Volume-1));plus.onClick.AddListener(()=>flow.SetVolume(flow.Volume+1));
        eject.onClick.AddListener(flow.EjectTip);listen.onClick.AddListener(flow.PlayInstruction);sound.onClick.AddListener(()=>{flow.ToggleSound();sound.GetComponentInChildren<TMP_Text>().text=flow.SoundEnabled?"Sound: on":"Sound: off";});recenter.onClick.AddListener(Place);
        GetComponent<Canvas>().worldCamera=Camera.main;
    }
    public void Place()
    {
        var cam=Camera.main;if(!cam)return;
        var forward=Vector3.ProjectOnPlane(cam.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
        transform.position=cam.transform.position+forward*1.5f+Vector3.down*.05f;
        transform.rotation=Quaternion.LookRotation(forward,Vector3.up);
    }
    public void Show(int step,string title,string body,float fraction)
    {
        stepLabel.text=$"Step {step} of 11  ·  PCR PLATING";instruction.text=title;detail.text=body;
        progress.rectTransform.sizeDelta=new Vector2(910*Mathf.Clamp01(fraction),12);
        materialChoices.SetActive(step==1);confirm.gameObject.SetActive(step==1);
        bool dial=step==2||step==6||step==8;minus.gameObject.SetActive(dial);plus.gameObject.SetActive(dial);volumeLabel.gameObject.SetActive(step>=2&&step<=8);
        eject.gameObject.SetActive(step==5||step==7||step==8);vortexRing.gameObject.SetActive(step==3);
        listen.interactable=flow.HasInstructionAudio;
        volumeLabel.text=flow.Volume+" µL";
        if(liquidMeterTrack){liquidMeterTrack.SetActive(step==4||step==7||step==8);liquidMeter.rectTransform.sizeDelta=new Vector2(320*Mathf.Clamp01(flow.LoadedVolume/Mathf.Max(1,flow.Volume)),14);}
    }
    public Image Box(string name,Transform parent,Vector2 pos,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform;r.anchoredPosition=pos;r.sizeDelta=size;var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
    }
    public TMP_Text Text(string name,Transform parent,string value,Vector2 pos,Vector2 size,float fontSize)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.pivot=new Vector2(0,1);rt.anchoredPosition=pos;rt.sizeDelta=size;
        var t=go.GetComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=fontSize;t.color=new Color(.9f,.97f,1);t.raycastTarget=false;t.enableWordWrapping=true;return t;
    }
    Button Button(string label,Vector2 pos,Vector2 size)
    {
        var bg=Box(label,transform,pos,size,new Color(.10f,.31f,.38f));bg.raycastTarget=true;var button=bg.gameObject.AddComponent<Button>();button.targetGraphic=bg;
        var colors=button.colors;colors.highlightedColor=new Color(.5f,1,.9f);colors.disabledColor=new Color(.25f,.30f,.32f);button.colors=colors;
        var t=Text("Text",bg.transform,label,new Vector2(-size.x/2,size.y/2),size,26);t.alignment=TextAlignmentOptions.Center;return button;
    }
}
