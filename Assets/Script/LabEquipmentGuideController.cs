using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;

public sealed class LabEquipmentGuideController : MonoBehaviour
{
    [Serializable]
    public sealed class EquipmentSlide
    {
        public string title, category;
        [TextArea] public string function, useInPhase, reminder;
        public Texture2D image;
    }
    [SerializeField] private UIDocument document;
    [SerializeField] private RectTransform hitTargetRoot;
    [SerializeField] private EquipmentSlide[] slides;
    [SerializeField] private string nextScene = "Assets/Scenes/StarterScene.unity";
    private VisualElement root;
    private Button boundNext;
    private int currentIndex;
    private bool complete, loading;
    private float nextRefresh;
    private readonly Dictionary<Button, Action> callbacks = new Dictionary<Button, Action>();
    private readonly Dictionary<string, RectTransform> targets = new Dictionary<string, RectTransform>();
    public int CurrentSlideIndex => currentIndex;
    public int SlideCount => slides == null ? 0 : slides.Length;
    public bool IsComplete => complete;
    public EquipmentSlide[] Slides => slides;

    private void Awake()
    {
        if (document == null) document = GetComponent<UIDocument>();
        // Earlier versions could leave generated ray targets serialized in the scene.
        if (hitTargetRoot != null)
            foreach (Transform child in hitTargetRoot)
                if (child.name.StartsWith("Equipment Hit ")) child.gameObject.SetActive(false);
    }
    private void Update()
    {
        if (document == null || !document.enabled || document.rootVisualElement?.panel == null)
        { HideTargets(); return; }
        var candidate = document.rootVisualElement;
        if (root != candidate || boundNext != candidate.Q<Button>("next-equipment")) BindView(candidate);
        if (Time.unscaledTime >= nextRefresh)
        { nextRefresh = Time.unscaledTime + 0.1f; RefreshTargets(); }
    }
    public void BindView(VisualElement view)
    {
        UnbindView(); root = view;
        boundNext = root.Q<Button>("next-equipment");
        Bind("previous-equipment", Previous);
        Bind("next-equipment", Next);
        Bind("retake-learning", Retake);
        Bind("proceed-next-step", Proceed);
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        Refresh();
    }
    private void Bind(string name, Action action)
    {
        var button = root.Q<Button>(name);
        if (button == null) return;
        callbacks.Add(button, action); button.clicked += action;
    }
    public void UnbindView()
    {
        foreach (var pair in callbacks) pair.Key.clicked -= pair.Value;
        callbacks.Clear();
        if (root != null) root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        root = null; boundNext = null; HideTargets();
    }
    private void OnDisable() { UnbindView(); }
    private void OnKeyDown(KeyDownEvent evt)
    {
        if (complete || loading) return;
        if (evt.keyCode == KeyCode.LeftArrow) { Previous(); evt.StopPropagation(); }
        else if (evt.keyCode == KeyCode.RightArrow) { Next(); evt.StopPropagation(); }
    }
    public void Previous()
    { if (!complete && !loading && currentIndex > 0) { currentIndex--; Refresh(); } }
    public void Next()
    {
        if (loading || complete || SlideCount == 0) return;
        if (currentIndex < SlideCount - 1) currentIndex++;
        else complete = true;
        Refresh();
        if (complete) root?.Q<Button>("retake-learning")?.Focus();
    }
    public void Retake()
    {
        if (loading) return;
        currentIndex = 0; complete = false; Refresh();
        root?.Q<Button>("next-equipment")?.Focus();
    }
    public void Proceed()
    {
        if (!complete || loading) return;
        if (!Application.isPlaying) { SetStatus("In Play Mode, this opens PCR plate setup (StarterScene)."); return; }
        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        { SetStatus("PCR plate setup is unavailable. Enable StarterScene in the build scene list."); return; }
        loading = true; Refresh(); SetStatus("Opening PCR plate setup…");
        try
        {
            if (SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single) == null)
                throw new InvalidOperationException("Scene loading did not start.");
        }
        catch (Exception exception)
        { loading = false; Refresh(); SetStatus("Could not open PCR plate setup. Please try again."); Debug.LogException(exception, this); }
    }
    private void SetStatus(string text)
    { var label = root?.Q<Label>("navigation-status"); if (label != null) label.text = text; }
    private void Refresh()
    {
        if (root == null || SlideCount == 0) return;
        currentIndex = Mathf.Clamp(currentIndex, 0, SlideCount - 1);
        var slide = slides[currentIndex];
        root.Q<VisualElement>("learning-view").EnableInClassList("hidden", complete);
        root.Q<VisualElement>("completion-view").EnableInClassList("hidden", !complete);
        root.Q<Label>("equipment-title").text = slide.title;
        root.Q<Label>("equipment-function").text = slide.function;
        root.Q<Label>("slide-position").text = (currentIndex + 1) + " / " + SlideCount;
        var picture = root.Q<Image>("equipment-image");
        picture.image = slide.image; picture.scaleMode = ScaleMode.ScaleToFit;
        root.Q<Button>("previous-equipment").SetEnabled(currentIndex > 0 && !loading);
        root.Q<Button>("next-equipment").SetEnabled(!loading);
        root.Q<Button>("next-equipment").tooltip = currentIndex == SlideCount - 1 ? "Finish learning" : "Next equipment";
        root.Q<Button>("retake-learning").SetEnabled(!loading);
        root.Q<Button>("proceed-next-step").SetEnabled(!loading);
        SetStatus(""); RefreshTargets();
    }
    private void HideTargets()
    { foreach (var target in targets.Values) if (target != null) target.gameObject.SetActive(false); }
    private void RefreshTargets()
    {
        if (!Application.isPlaying || root?.panel == null || hitTargetRoot == null) return;
        if (float.IsNaN(root.layout.width) || root.layout.width < 1 || root.layout.height < 1) return;
        foreach (var pair in callbacks)
        {
            var element = pair.Key;
            var bounds = element.worldBound;
            bool visible = true;
            for (VisualElement parent = element; parent != null; parent = parent.parent)
                if (parent.resolvedStyle.display == DisplayStyle.None || parent.resolvedStyle.visibility == Visibility.Hidden) { visible = false; break; }
            visible &= bounds.width > 1 && bounds.height > 1 && !float.IsInfinity(bounds.width);
            RectTransform rect;
            if (!targets.TryGetValue(element.name, out rect) || rect == null)
            {
                var existing = hitTargetRoot.Find("Equipment Hit " + element.name);
                if (existing != null) rect = existing.GetComponent<RectTransform>();
                if (rect == null)
                {
                    var go = new GameObject("Equipment Hit " + element.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UIImage), typeof(UIButton));
                    rect = go.GetComponent<RectTransform>(); rect.SetParent(hitTargetRoot, false);
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                    go.layer = hitTargetRoot.gameObject.layer;
                }
                targets[element.name] = rect;
            }
            rect.gameObject.SetActive(visible);
            var image = rect.GetComponent<UIImage>(); image.color = new Color(1, 1, 1, 0.01f); image.raycastTarget = visible;
            var button = rect.GetComponent<UIButton>(); button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.interactable = visible && element.enabledInHierarchy;
            button.onClick.RemoveAllListeners();
            Action action = pair.Value;
            button.onClick.AddListener(() => { if (element.enabledInHierarchy && button.interactable) action(); });
            var trigger = rect.GetComponent<EventTrigger>() ?? rect.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (element.enabledInHierarchy) element.AddToClassList("ray-focus"); });
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => element.RemoveFromClassList("ray-focus"));
            trigger.triggers.Add(enter); trigger.triggers.Add(exit);
            if (!visible || !element.enabledInHierarchy) element.RemoveFromClassList("ray-focus");
            var origin = root.worldBound;
            float sx = hitTargetRoot.rect.width / root.layout.width, sy = hitTargetRoot.rect.height / root.layout.height;
            rect.sizeDelta = new Vector2(bounds.width * sx, bounds.height * sy);
            rect.anchoredPosition = new Vector2((bounds.center.x - origin.x - root.layout.width / 2) * sx, (root.layout.height / 2 - bounds.center.y + origin.y) * sy);
        }
    }
}
