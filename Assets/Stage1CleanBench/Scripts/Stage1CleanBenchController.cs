using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
public sealed class Stage1CleanBenchController : MonoBehaviour
{
[SerializeField] private XRGrabInteractable labCoat, protectiveGloves, cleaningSpray, cleaningWipe;
[SerializeField] private Stage1CleaningReadiness cleaningReadiness;
[SerializeField] private PCRGuideUIController plateLayoutGuide;
[SerializeField] private GameObject plateLayoutUIRoot;
[SerializeField] private GameObject plateLayoutWorldPanel;
[SerializeField] private int coatPoints, glovePoints, cleaningPoints;
private bool coatDone, glovesDone, cleaningDone;
private Label coatStatus, gloveStatus, cleaningStatus, feedbackTitle, feedbackDetail, scoreLabel, stageState;
private UnityEngine.UIElements.Button nextButton;
private UnityEngine.UI.Button nextProxy;
private int score;
private RenderTexture panelTexture;
private PanelSettings runtimePanelSettings;
private void Awake()
{
if (!labCoat) labCoat=FindGrab("PLACEHOLDER_LabCoat"); if (!protectiveGloves) protectiveGloves=FindGrab("PLACEHOLDER_ProtectiveGloves");
if (!cleaningSpray) cleaningSpray=FindGrab("CleaningSpray_ExistingVisual"); if (!cleaningWipe) cleaningWipe=FindGrab("PLACEHOLDER_CleaningWipe");
if (!cleaningReadiness) cleaningReadiness=GetComponent<Stage1CleaningReadiness>();
if (cleaningReadiness) cleaningReadiness.Completed += OnCleaningReady;
Subscribe(labCoat); Subscribe(protectiveGloves);
CreateToolkitWorldPanel(); RefreshChecklist();
}
private void OnDestroy()
{
Unsubscribe(labCoat); Unsubscribe(protectiveGloves);
if (cleaningReadiness) cleaningReadiness.Completed -= OnCleaningReady;
if(panelTexture!=null) panelTexture.Release(); if(runtimePanelSettings!=null) Destroy(runtimePanelSettings);
}
private XRGrabInteractable FindGrab(string n){var g=GameObject.Find(n);return g?g.GetComponent<XRGrabInteractable>():null;}
private void Subscribe(XRGrabInteractable i){if(i!=null)i.selectExited.AddListener(OnItemReleased);}
private void Unsubscribe(XRGrabInteractable i){if(i!=null)i.selectExited.RemoveListener(OnItemReleased);}
private void OnItemReleased(SelectExitEventArgs args)
{
var go=args.interactableObject.transform.gameObject;
if(labCoat&&go==labCoat.gameObject&&!coatDone) StartCoroutine(WearItem(go,true));
else if(protectiveGloves&&go==protectiveGloves.gameObject&&!glovesDone) StartCoroutine(WearItem(go,false));

RefreshChecklist();
}
private IEnumerator WearItem(GameObject item,bool isCoat)
{
var camera=Camera.main;
if(camera){Vector3 start=item.transform.position;Quaternion startRotation=item.transform.rotation;Vector3 target=camera.transform.position+camera.transform.forward*.35f+Vector3.down*.25f;Quaternion targetRotation=camera.transform.rotation;float elapsed=0;
while(elapsed<.35f){elapsed+=Time.deltaTime;float t=Mathf.Clamp01(elapsed/.35f);item.transform.position=Vector3.Lerp(start,target,t);item.transform.rotation=Quaternion.Slerp(startRotation,targetRotation,t);yield return null;}}
foreach(var r in item.GetComponentsInChildren<Renderer>())r.enabled=false;
foreach(var c in item.GetComponentsInChildren<Collider>())c.enabled=false;
var grab=item.GetComponent<XRGrabInteractable>();if(grab)grab.enabled=false;
if(isCoat&&!coatDone){coatDone=true;AddScore(coatPoints);SetFeedback("Laboratory coat equipped.","Protective clothing is ready for the clean-bench task.",false);}
if(!isCoat&&!glovesDone){glovesDone=true;AddScore(glovePoints);SetFeedback("Protective gloves equipped.","You are ready to continue preparing the bench.",false);}
RefreshChecklist();
}
private void OnCleaningReady()
{
if (cleaningDone) return;
cleaningDone=true; AddScore(cleaningPoints);
SetFeedback("Cleaning setup validated.","LaminarSlide is open; cleaner and spray are held in opposite hands.",false);
RefreshChecklist();
}
private void AddScore(int points){if(points>0)score+=points;}
private void SetFeedback(string title,string detail,bool warning)
{
if(feedbackTitle!=null)feedbackTitle.text=title;if(feedbackDetail!=null)feedbackDetail.text=detail;
var card=feedbackTitle!=null&&feedbackTitle.parent!=null?feedbackTitle.parent.parent:null;
if(card!=null){card.EnableInClassList("warning",warning);card.EnableInClassList("complete",!warning&&coatDone&&glovesDone&&cleaningDone);}
}
private void RefreshChecklist()
{
bool complete=coatDone&&glovesDone&&cleaningDone;
SetItem(coatStatus,coatDone,"PPE","Wear laboratory coat");SetItem(gloveStatus,glovesDone,"PPE","Put on protective gloves");
SetItem(cleaningStatus,cleaningDone,"SURFACE","Open slide; hold cleaner and spray");
if(scoreLabel!=null)scoreLabel.text=coatPoints+glovePoints+cleaningPoints>0?"SCORE  "+score:"SCORING  RUBRIC PENDING";
if(stageState!=null)stageState.text=complete?"READY TO CONTINUE":"IN PROGRESS";if(nextButton!=null)nextButton.SetEnabled(complete);if(nextProxy!=null)nextProxy.interactable=complete;
if(complete)SetFeedback("Stage 1 complete.","Your workstation is prepared and Stage 2 is unlocked.",false);
}
private void SetItem(Label label,bool done,string tag,string title)
{
if(label==null)return;label.text=(done?"✓   ":"○   ")+title;label.parent.parent.EnableInClassList("done",done);
var eyebrow=label.parent.Q<Label>(className:"item-tag");if(eyebrow!=null)eyebrow.text=tag;
}
private void OnNextStageClicked()
{
if (!coatDone || !glovesDone || !cleaningDone) return;
if (!plateLayoutGuide || !plateLayoutUIRoot || !plateLayoutWorldPanel)
{ SetFeedback("Stage 2 configuration missing.","Assign the existing plate-layout guide and world panel.",true); return; }
var panel=transform.Find("Stage1_CleanBench_WorldPanel");
if (!panel)
{ SetFeedback("Current panel is missing.","The next step needs the current panel position.",true); return; }
// Inherit the outgoing panel's live world pose before showing the next step.
// Keep the incoming panel's authored scale and layout.
plateLayoutWorldPanel.transform.SetPositionAndRotation(panel.position,panel.rotation);
plateLayoutUIRoot.SetActive(true);
plateLayoutWorldPanel.SetActive(true);
plateLayoutGuide.BeginPlateLayout();
panel.gameObject.SetActive(false);
}
private void CreateToolkitWorldPanel()
{
var visualTree=Resources.Load<VisualTreeAsset>("Stage1CleanBench/Stage1CleanBench");var styleSheet=Resources.Load<StyleSheet>("Stage1CleanBench/Stage1CleanBench");
if(visualTree==null||styleSheet==null){Debug.LogError("Stage 1 UI Toolkit assets were not found in Resources/Stage1CleanBench.");return;}
var panelRoot=transform.Find("Stage1_CleanBench_UI_Toolkit")?.gameObject;var existingDocument=panelRoot?panelRoot.GetComponent<UIDocument>():null;var theme=existingDocument&&existingDocument.panelSettings?existingDocument.panelSettings.themeStyleSheet:null;
runtimePanelSettings=ScriptableObject.CreateInstance<PanelSettings>();runtimePanelSettings.name="Stage1CleanBench_RuntimePanelSettings";runtimePanelSettings.themeStyleSheet=theme;
runtimePanelSettings.scaleMode=PanelScaleMode.ScaleWithScreenSize;runtimePanelSettings.referenceResolution=new Vector2Int(1200,800);runtimePanelSettings.clearColor=true;runtimePanelSettings.colorClearValue=new Color(0,0,0,0);
panelTexture=new RenderTexture(1200,800,24,RenderTextureFormat.ARGB32);panelTexture.name="Stage1CleanBench_UITexture";panelTexture.Create();runtimePanelSettings.targetTexture=panelTexture;
if(panelRoot==null){panelRoot=new GameObject("Stage1_CleanBench_UI_Toolkit",typeof(UIDocument));panelRoot.transform.SetParent(transform,false);}
var document=panelRoot.GetComponent<UIDocument>();document.panelSettings=runtimePanelSettings;document.visualTreeAsset=visualTree;
var root=document.rootVisualElement;coatStatus=root.Q<Label>("coat-status");gloveStatus=root.Q<Label>("glove-status");cleaningStatus=root.Q<Label>("cleaning-status");
feedbackTitle=root.Q<Label>("feedback-title");feedbackDetail=root.Q<Label>("feedback-detail");scoreLabel=root.Q<Label>("score-label");stageState=root.Q<Label>("stage-state");nextButton=root.Q<UnityEngine.UIElements.Button>("next-stage");
if(nextButton!=null)nextButton.clicked+=OnNextStageClicked;
var existingPanel=transform.Find("Stage1_CleanBench_WorldPanel");var panelObject=existingPanel?existingPanel.gameObject:new GameObject("Stage1_CleanBench_WorldPanel",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(TrackedDeviceGraphicRaycaster));if(existingPanel==null)panelObject.transform.SetParent(transform,false);
var graphicRaycaster=panelObject.GetComponent<GraphicRaycaster>();if(graphicRaycaster!=null)Destroy(graphicRaycaster);if(panelObject.GetComponent<TrackedDeviceGraphicRaycaster>()==null)panelObject.AddComponent<TrackedDeviceGraphicRaycaster>();
var canvas=panelObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Camera.main;canvas.sortingOrder=25;
var rect=panelObject.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1200,800);rect.localScale=Vector3.one*.00078f;
var surfaceTransform=panelObject.transform.Find("UI Toolkit Surface");var surface=surfaceTransform?surfaceTransform.gameObject:new GameObject("UI Toolkit Surface",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));if(surfaceTransform==null)surface.transform.SetParent(panelObject.transform,false);
var surfaceRect=surface.GetComponent<RectTransform>();surfaceRect.anchorMin=Vector2.zero;surfaceRect.anchorMax=Vector2.one;surfaceRect.offsetMin=Vector2.zero;surfaceRect.offsetMax=Vector2.zero;
var raw=surface.GetComponent<RawImage>();raw.texture=panelTexture;raw.raycastTarget=false;raw.uvRect=new Rect(1,0,-1,1);
var proxyTransform=panelObject.transform.Find("Next Stage XR Hit Target");if(proxyTransform==null){var proxyObject=new GameObject("Next Stage XR Hit Target",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));proxyObject.transform.SetParent(panelObject.transform,false);proxyTransform=proxyObject.transform;}
nextSurface=raw;
proxyTransform.SetParent(surface.transform,false);
var proxyRect=proxyTransform.GetComponent<RectTransform>();proxyRect.pivot=new Vector2(.5f,.5f);proxyRect.anchorMin=Vector2.zero;proxyRect.anchorMax=Vector2.zero;proxyRect.sizeDelta=Vector2.zero;
var proxyImage=proxyTransform.GetComponent<UnityEngine.UI.Image>();proxyImage.color=new Color(1,1,1,.001f);proxyImage.raycastTarget=true;nextProxy=proxyTransform.GetComponent<UnityEngine.UI.Button>();if(nextProxy!=null)nextProxy.onClick.AddListener(OnNextStageClicked);
}
private RawImage nextSurface;
private void LateUpdate()
{
    if (nextProxy == null || nextSurface == null || nextButton == null || nextButton.panel == null) return;
    // Map the rendered UI Toolkit button through the RawImage UVs, including mirroring.
    Rect viewport = nextButton.panel.visualTree.worldBound;
    Rect button = nextButton.worldBound;
    Rect uv = nextSurface.uvRect;
    if (viewport.width <= 0 || viewport.height <= 0 || button.width <= 0 || button.height <= 0 ||
        float.IsNaN(button.x) || float.IsNaN(button.y) || Mathf.Approximately(uv.width,0) || Mathf.Approximately(uv.height,0)) return;
    float x0 = ((button.xMin-viewport.xMin)/viewport.width-uv.x)/uv.width;
    float x1 = ((button.xMax-viewport.xMin)/viewport.width-uv.x)/uv.width;
    float y0 = (1f-(button.yMax-viewport.yMin)/viewport.height-uv.y)/uv.height;
    float y1 = (1f-(button.yMin-viewport.yMin)/viewport.height-uv.y)/uv.height;
    var rect = (RectTransform)nextProxy.transform;
    rect.anchorMin = new Vector2(Mathf.Min(x0,x1),Mathf.Min(y0,y1));
    rect.anchorMax = new Vector2(Mathf.Max(x0,x1),Mathf.Max(y0,y1));
    rect.offsetMin = Vector2.zero;
    rect.offsetMax = Vector2.zero;
}
}