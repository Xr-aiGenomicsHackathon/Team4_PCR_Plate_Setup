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
[SerializeField] private Collider cleaningWorkArea;
[SerializeField] private int coatPoints, glovePoints, cleaningPoints;
private bool coatDone, glovesDone, sprayUsedInZone, wipeUsedInZone, cleaningDone;
private Label coatStatus, gloveStatus, cleaningStatus, feedbackTitle, feedbackDetail, scoreLabel, stageState;
private UnityEngine.UIElements.Button nextButton;
private UnityEngine.UI.Button nextProxy;
private int score;
private RenderTexture panelTexture;
private PanelSettings runtimePanelSettings;
private void Awake()
{
labCoat=FindGrab("PLACEHOLDER_LabCoat"); protectiveGloves=FindGrab("PLACEHOLDER_ProtectiveGloves");
cleaningSpray=FindGrab("CleaningSpray_ExistingVisual"); cleaningWipe=FindGrab("PLACEHOLDER_CleaningWipe");
cleaningWorkArea=FindCollider("ZONE_CleaningWorkArea");
Subscribe(labCoat); Subscribe(protectiveGloves); Subscribe(cleaningSpray); Subscribe(cleaningWipe);
CreateToolkitWorldPanel(); RefreshChecklist();
}
private void OnDestroy()
{
Unsubscribe(labCoat); Unsubscribe(protectiveGloves); Unsubscribe(cleaningSpray); Unsubscribe(cleaningWipe);
if(panelTexture!=null) panelTexture.Release(); if(runtimePanelSettings!=null) Destroy(runtimePanelSettings);
}
private XRGrabInteractable FindGrab(string n){var g=GameObject.Find(n);return g?g.GetComponent<XRGrabInteractable>():null;}
private Collider FindCollider(string n){var g=GameObject.Find(n);return g?g.GetComponent<Collider>():null;}
private void Subscribe(XRGrabInteractable i){if(i!=null)i.selectExited.AddListener(OnItemReleased);}
private void Unsubscribe(XRGrabInteractable i){if(i!=null)i.selectExited.RemoveListener(OnItemReleased);}
private void OnItemReleased(SelectExitEventArgs args)
{
var go=args.interactableObject.transform.gameObject;
if(labCoat&&go==labCoat.gameObject&&!coatDone) StartCoroutine(WearItem(go,true));
else if(protectiveGloves&&go==protectiveGloves.gameObject&&!glovesDone) StartCoroutine(WearItem(go,false));
else if(cleaningSpray&&go==cleaningSpray.gameObject){sprayUsedInZone=IsInside(cleaningWorkArea,go.transform.position);if(sprayUsedInZone)TryCompleteCleaning();else SetFeedback("Place the cleaning spray at the workstation.","Carry the spray into the marked workstation area and release it there.",true);}
else if(cleaningWipe&&go==cleaningWipe.gameObject){wipeUsedInZone=IsInside(cleaningWorkArea,go.transform.position);if(wipeUsedInZone)TryCompleteCleaning();else SetFeedback("Place the cleaning wipe at the workstation.","Carry the wipe into the marked workstation area and release it there.",true);}

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
private void TryCompleteCleaning()
{
if(sprayUsedInZone&&wipeUsedInZone&&cleaningSpray&&cleaningWipe&&IsInside(cleaningWorkArea,cleaningSpray.transform.position)&&IsInside(cleaningWorkArea,cleaningWipe.transform.position)&&!cleaningDone){cleaningDone=true;AddScore(cleaningPoints);SetFeedback("Cleaning items placed at workstation.","The spray and wipe are both inside the marked workstation area.",false);}
}
private bool IsInside(Collider area,Vector3 point){return area&&area.bounds.Contains(point);}
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
SetItem(cleaningStatus,cleaningDone,"SURFACE","Place spray and wipe at workstation");
if(scoreLabel!=null)scoreLabel.text=coatPoints+glovePoints+cleaningPoints>0?"SCORE  "+score:"SCORING  RUBRIC PENDING";
if(stageState!=null)stageState.text=complete?"READY TO CONTINUE":"IN PROGRESS";if(nextButton!=null)nextButton.SetEnabled(complete);if(nextProxy!=null)nextProxy.interactable=complete;
if(complete)SetFeedback("Stage 1 complete.","Your workstation is prepared and Stage 2 is unlocked.",false);
}
private void SetItem(Label label,bool done,string tag,string title)
{
if(label==null)return;label.text=(done?"✓   ":"○   ")+title;label.parent.parent.EnableInClassList("done",done);
var eyebrow=label.parent.Q<Label>(className:"item-tag");if(eyebrow!=null)eyebrow.text=tag;
}
private void OnNextStageClicked(){SetFeedback("Stage 2 is unlocked.","Add the verified Stage 2 scene transition when that stage is implemented.",false);}
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
var proxyRect=proxyTransform.GetComponent<RectTransform>();proxyRect.anchorMin=Vector2.one;proxyRect.anchorMax=Vector2.one;proxyRect.pivot=Vector2.one;proxyRect.anchoredPosition=new Vector2(-55,-38);proxyRect.sizeDelta=new Vector2(220,68);var proxyImage=proxyTransform.GetComponent<UnityEngine.UI.Image>();proxyImage.color=new Color(1,1,1,.001f);proxyImage.raycastTarget=true;nextProxy=proxyTransform.GetComponent<UnityEngine.UI.Button>();if(nextProxy!=null)nextProxy.onClick.AddListener(OnNextStageClicked);
}
}