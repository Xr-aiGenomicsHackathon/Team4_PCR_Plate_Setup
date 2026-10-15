using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public sealed class PCRGuideWorldPanelControls : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private PCRGuideUIController guide;
    [SerializeField] private RectTransform hitTargetRoot;

    private readonly Dictionary<string, RectTransform> targets = new Dictionary<string, RectTransform>();

    private void Start() { StartCoroutine(RefreshTargets()); }

    private IEnumerator RefreshTargets()
    {
        while (document == null || guide == null || hitTargetRoot == null)
            yield return null;

        while (true)
        {
            var root = document.rootVisualElement;
            if (root != null && root.panel != null && root.layout.width > 10f && root.layout.height > 10f)
                RefreshHitTargets(root);
            yield return new WaitForSecondsRealtime(0.15f);
        }
    }

    private void RefreshHitTargets(VisualElement root)
    {
        foreach (var identity in new[] { "S1", "S2", "S3", "S4", "S5", "PTC", "NTC" })
        {
            var value = identity;
            AddOrUpdate(root, "layout-" + value, () => guide.SelectLayoutIdentity(value));
        }
        AddOrUpdate(root, "step-1", () => guide.ShowStep(1));
        AddOrUpdate(root, "step-2", () => guide.ShowStep(2));
        AddOrUpdate(root, "step-3", () => guide.ShowStep(3));
        AddOrUpdate(root, "step-4", () => guide.ShowStep(4));
        AddOrUpdate(root, "step-5", () => guide.ShowStep(5));
        AddOrUpdate(root, "step-6", () => guide.ShowStep(6));
        AddOrUpdate(root, "step-7", () => guide.ShowStep(7));
        AddOrUpdate(root, "step-8", () => guide.ShowStep(8));
        AddOrUpdate(root, "action-done", guide.ConfirmGuideAction);
        AddOrUpdate(root, "replay-audio", guide.ReplayGuideNarration);
        AddOrUpdate(root, "play-demo", guide.PlayGuideDemonstration);
        AddOrUpdate(root, "previous-step", guide.ReturnToPreviousStep);
        AddOrUpdate(root, "next-step", guide.AdvanceGuideStep);
        AddOrUpdate(root, "check-well", () => guide.ToggleChecklistItem(0));
        AddOrUpdate(root, "check-sample", () => guide.ToggleChecklistItem(1));
        AddOrUpdate(root, "check-volume", () => guide.ToggleChecklistItem(2));
        AddOrUpdate(root, "check-controls", () => guide.ToggleChecklistItem(3));
    }

    private void AddOrUpdate(VisualElement root, string elementName, UnityEngine.Events.UnityAction action)
    {
        var element = root.Q<VisualElement>(elementName);
        if (element == null) return;
        var bounds = element.worldBound;

        if (!targets.TryGetValue(elementName, out var rect))
        {
            if (bounds.width <= 1f || bounds.height <= 1f) return;

            var go = new GameObject("Hit " + elementName, typeof(RectTransform), typeof(CanvasRenderer),
                                    typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(hitTargetRoot, false);
            rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            image.raycastTarget = true;

            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.onClick.AddListener(action);

            AddHoverForwarding(go, element);
            targets[elementName] = rect;
        }

        bool visible = bounds.width > 1f && bounds.height > 1f;
        rect.gameObject.SetActive(visible);
        if (!visible)
        {
            element.RemoveFromClassList("ray-focus");
            return;
        }

        rect.sizeDelta = bounds.size;
        rect.anchoredPosition = new Vector2(bounds.center.x - root.layout.width * 0.5f,
                                            root.layout.height * 0.5f - bounds.center.y);
    }

    private static void AddHoverForwarding(GameObject target, VisualElement element)
    {
        var trigger = target.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => element.AddToClassList("ray-focus"));
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => element.RemoveFromClassList("ray-focus"));
        trigger.triggers.Add(exit);
    }
}