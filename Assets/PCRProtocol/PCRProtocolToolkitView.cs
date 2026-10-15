using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using UnityEngine.Video;
using UIButton=UnityEngine.UIElements.Button;
using UIImage=UnityEngine.UI.Image;

// UI Toolkit presentation only; PCRProtocolController remains the sole protocol state machine.
[DefaultExecutionOrder(100)]
public sealed class PCRProtocolToolkitView : MonoBehaviour
{
    public PCRProtocolPanel legacy;
    public ThemeStyleSheet theme;
    public StyleSheet stylesheet;
    public VideoPlayer tutorial;
    public UIDocument document;
    public RawImage surface;
    RenderTexture texture;
    PanelSettings settings;
    VisualElement root,content,progress,feedback;
    Label heading,step,detail,status;
    string view="Protocol";
    int lastStep=-1;
    readonly Dictionary<string,UIButton> buttons=new Dictionary<string,UIButton>();
    readonly Dictionary<string,RectTransform> hits=new Dictionary<string,RectTransform>();
    readonly List<Action> sync=new List<Action>();
    PCRProtocolController Flow=>legacy.flow;
    public string CurrentView=>view;
    static readonly string[] names={"Materials","Pipette setup","Mix master mix","Dispense master mix","Change tip","Set 5 µL","DNA samples","Controls","Plate check","Seal","Handover"};
    void Start()
    {
        // Retain the old panel's serialized bindings as a compatibility model, but render only Toolkit.
        foreach(var g in legacy.GetComponentsInChildren<Graphic>(true))g.enabled=false;
        texture=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32){name="PCR Toolkit UI",antiAliasing=1};texture.Create();
        settings=ScriptableObject.CreateInstance<PanelSettings>();settings.themeStyleSheet=theme;settings.scaleMode=PanelScaleMode.ConstantPixelSize;settings.scale=1;settings.targetTexture=texture;settings.clearColor=true;settings.colorClearValue=Color.clear;
        var doc=new GameObject("PCR Protocol UI Toolkit");doc.transform.SetParent(transform,false);document=doc.AddComponent<UIDocument>();document.panelSettings=settings;
        var go=new GameObject("UI Toolkit Surface",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));go.transform.SetParent(transform,false);surface=go.GetComponent<RawImage>();surface.texture=texture;surface.raycastTarget=false;
        var rect=surface.rectTransform;rect.sizeDelta=new Vector2(1100,786);rect.anchoredPosition=Vector2.zero;
        root=document.rootVisualElement;root.styleSheets.Add(stylesheet);
        var card=Element(root,"shell");var header=Element(card,"header");
        var copy=Element(header,"copy");Text(copy,"PCR / GUIDED PRACTICAL","eyebrow");heading=Text(copy,"Prepare your plate","title");step=Text(header,"Step 1 of 11","pill");
        var track=Element(card,"track");progress=Element(track,"fill");
        var tabs=Element(card,"tabs");foreach(var tab in new[]{"Protocol","Guide","Plate map","Tutorial"}){var value=tab;Button(tabs,"tab-"+tab,tab,()=>Open(value),"tab");}
        detail=Text(card,"","summary");content=Element(card,"content");
        feedback=Element(card,"feedback");status=Text(feedback,"","feedback-text");
        var footer=Element(card,"footer");Button(footer,"listen","Listen",()=>legacy.listen.onClick.Invoke());Button(footer,"sound","Sound on",()=>legacy.sound.onClick.Invoke());Button(footer,"recenter","Recenter",legacy.Place);
        Open("Protocol");
    }
    VisualElement Element(VisualElement parent,string cls){var e=new VisualElement();e.AddToClassList(cls);parent.Add(e);return e;}
    Label Text(VisualElement parent,string value,string cls){var e=new Label(value);e.AddToClassList(cls);parent.Add(e);return e;}
    UIButton Button(VisualElement parent,string id,string value,Action action,string cls="button")
    {
        var b=new UIButton(action){name=id,text=value};b.AddToClassList(cls);parent.Add(b);buttons[id]=b;
        var g=new GameObject("Toolkit Hit "+id,typeof(RectTransform),typeof(CanvasRenderer),typeof(UIImage),typeof(UnityEngine.UI.Button));g.transform.SetParent(surface.transform,false);
        var img=g.GetComponent<UIImage>();img.color=new Color(1,1,1,0);img.raycastTarget=true;var btn=g.GetComponent<UnityEngine.UI.Button>();btn.transition=Selectable.Transition.None;btn.onClick.AddListener(()=>{if(b.enabledInHierarchy)action();});
        var evt=g.AddComponent<EventTrigger>();var enter=new EventTrigger.Entry{eventID=EventTriggerType.PointerEnter};enter.callback.AddListener(_=>b.AddToClassList("ray-focus"));var exit=new EventTrigger.Entry{eventID=EventTriggerType.PointerExit};exit.callback.AddListener(_=>b.RemoveFromClassList("ray-focus"));evt.triggers.Add(enter);evt.triggers.Add(exit);
        hits[id]=g.GetComponent<RectTransform>();return b;
    }
    public void Open(string name)
    {
        if(content==null)return;
        if(name!="Tutorial"&&tutorial&&tutorial.isPlaying)tutorial.Pause();
        foreach(var id in new List<string>(buttons.Keys))if(!id.StartsWith("tab-")&&id!="listen"&&id!="sound"&&id!="recenter"){Destroy(hits[id].gameObject);hits.Remove(id);buttons.Remove(id);}
        content.Clear();sync.Clear();view=name;
        foreach(var pair in buttons)if(pair.Key.StartsWith("tab-"))pair.Value.EnableInClassList("selected",pair.Key=="tab-"+name);
        if(name=="Protocol")BuildProtocol();else if(name=="Guide")BuildGuide();else if(name=="Plate map")BuildMap();else BuildVideo();
    }
    void BuildProtocol()
    {
        if(Flow.Step==1){var list=Element(content,"checklist");for(int i=0;i<5;i++){int n=i;var b=Button(list,"material-"+i,"",()=>{legacy.choices[n].isOn=!legacy.choices[n].isOn;},"check-row");sync.Add(()=>{b.text=(legacy.choices[n].isOn?"✓   ":"□   ")+new[]{"Pipette tips","PCR plate","Optical seal","Master mix","DNA samples"}[n];b.EnableInClassList("checked",legacy.choices[n].isOn);b.SetEnabled(!Flow.Busy);});}var confirm=Button(content,"confirm","Confirm materials",()=>legacy.confirm.onClick.Invoke(),"primary");sync.Add(()=>confirm.SetEnabled(legacy.confirm.interactable&&!Flow.Busy));return;}
        var metrics=Element(content,"metrics");var volume=Text(metrics,"","metric");sync.Add(()=>volume.text=Flow.Step<=8?Flow.Volume+" µL":"QUALITY / HANDOVER");
        if(Flow.Step==2||Flow.Step==6||Flow.Step==8){var row=Element(content,"actions");Button(row,"minus","− 1 µL",()=>legacy.minus.onClick.Invoke());Button(row,"plus","+ 1 µL",()=>legacy.plus.onClick.Invoke());}
        if(Flow.Step==4||Flow.Step==7||Flow.Step==8){var fill=new ProgressBar{title="Loaded volume",lowValue=0,highValue=1};fill.AddToClassList("meter");content.Add(fill);sync.Add(()=>{fill.value=Flow.LoadedVolume/Mathf.Max(1,Flow.Volume);fill.title="Loaded "+Flow.LoadedVolume+" / "+Flow.Volume+" µL";});}
        if(Flow.Step==3){var fill=new ProgressBar{lowValue=0,highValue=1};fill.AddToClassList("meter");content.Add(fill);sync.Add(()=>{fill.value=legacy.vortexRing.fillAmount;fill.title="Mixing · "+Mathf.RoundToInt(fill.value*100)+"%";});}
        if(Flow.Step==5||Flow.Step==7||Flow.Step==8)Button(content,"eject","Eject tip over waste",()=>legacy.eject.onClick.Invoke(),"primary");
        if(Flow.Step==9)for(int i=0;i<3;i++){int n=i;var b=Button(content,"inspect-"+i,new[]{"Check for bubbles","Verify all fill volumes","Verify well positions"}[i],()=>Flow.Inspect(n),"check-row");sync.Add(()=>b.EnableInClassList("checked",Flow.Inspected[n]));}
        if(Flow.Step==10)for(int i=0;i<4;i++){int n=i;var bar=new ProgressBar{lowValue=0,highValue=1};bar.AddToClassList("meter");content.Add(bar);sync.Add(()=>{bar.value=Flow.EdgeProgress[n];bar.title="Seal edge "+(n+1)+" · "+Mathf.RoundToInt(bar.value*100)+"%";});}
        Text(content,"Follow the highlighted object on your workbench.","hint");
    }
    void BuildGuide(){var list=Element(content,"guide-grid");for(int i=0;i<11;i++){var row=Text(list,(i+1).ToString("00")+"   "+names[i],"guide-row");row.EnableInClassList("current",i+1==Flow.Step);row.EnableInClassList("done",i+1<Flow.Step);}Text(content,"The practical advances automatically after each successful action. Use Protocol to continue.","hint");}
    void BuildMap(){Text(content,"ROW A · FIVE SAMPLES IN DUPLICATE + TWO CONTROLS","eyebrow");var grid=Element(content,"map-grid");for(int i=0;i<12;i++){int n=i;var cell=Element(grid,"well");Text(cell,"A"+(i+1),"well-coordinate");Text(cell,Flow.identities[i],"well-id");var v=Text(cell,"","well-volume");sync.Add(()=>v.text=(Flow.MasterVolumes[n]+Flow.SampleVolumes[n])+" / 15 µL");}Text(content,"Rows B–H are unused in this run. PTC: positive control. NTC: water control.","hint");}
    void BuildVideo(){if(!tutorial||(!tutorial.clip&&string.IsNullOrEmpty(tutorial.url))){Text(content,"No tutorial video is assigned to this scene.","hint");return;}var picture=new UnityEngine.UIElements.Image();picture.AddToClassList("video");content.Add(picture);sync.Add(()=>picture.image=tutorial.targetTexture);Button(content,"play-video","Play / pause tutorial",()=>{tutorial.gameObject.SetActive(true);var c=tutorial.GetComponent<Canvas>();if(c)c.enabled=false;if(tutorial.isPlaying)tutorial.Pause();else tutorial.Play();},"primary");}
    void LateUpdate()
    {
        if(root==null)return;
        if(lastStep!=Flow.Step){lastStep=Flow.Step;Open(view);}
        heading.text=view=="Protocol"?legacy.instruction.text:view=="Guide"?"Your PCR workflow":view=="Plate map"?"Plate allocation":"PCR tutorial";
        step.text="Step "+Flow.Step+" of 11";detail.text=view=="Protocol"?legacy.detail.text:"Your practical progress is preserved while you review this screen.";
        progress.style.width=Length.Percent(Flow.Finished?100:Mathf.Clamp01((Flow.Step-1)/11f)*100);
        var message=legacy.warning.text.Length>0?legacy.warning.text:legacy.banner.text;status.text=message.Length>0?message:Flow.Finished?"Plate handed over. Practical complete.":"Use your controller ray to select a control.";
        feedback.EnableInClassList("warning",legacy.warning.text.Length>0);feedback.EnableInClassList("success",legacy.banner.text.Length>0||Flow.Finished);
        buttons["listen"].SetEnabled(Flow.HasInstructionAudio);buttons["sound"].text=Flow.SoundEnabled?"Sound on":"Sound off";
        foreach(var action in sync)action();
        foreach(var pair in buttons){var b=pair.Value;var hit=hits[pair.Key];var bounds=b.worldBound;bool visible=b.resolvedStyle.display!=DisplayStyle.None&&bounds.width>1&&bounds.height>1;hit.gameObject.SetActive(visible);if(!visible)continue;var r=surface.rectTransform.rect;hit.sizeDelta=new Vector2(bounds.width/1400*r.width,bounds.height/1000*r.height);hit.anchoredPosition=new Vector2((bounds.center.x/1400-.5f)*r.width,(.5f-bounds.center.y/1000)*r.height);hit.GetComponent<UnityEngine.UI.Button>().interactable=b.enabledInHierarchy;}
    }
    void OnDisable(){if(tutorial&&tutorial.isPlaying)tutorial.Pause();}
    void OnDestroy(){if(texture){texture.Release();Destroy(texture);}if(settings)Destroy(settings);}
}
