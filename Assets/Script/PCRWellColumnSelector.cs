using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Adds ray-selectable inspection targets to the labeled PCR plate columns.
/// Hover previews the group name; selecting a column highlights it and keeps
/// its name visible until another column is selected or step 1 ends.
/// </summary>
public sealed class PCRWellColumnSelector : MonoBehaviour
{
    [SerializeField] private PCRGuideUIController guide;
    [SerializeField] private Transform plateWellRoot;
    [SerializeField] private Color hoverColor = new Color(0.22f, 0.72f, 1f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.2f, 1f, 0.55f, 1f);
    [SerializeField] private Color masterMixShadeColor = new Color(0.906f, 0.610f, 0f, 1f);
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.055f, 0f);

    private sealed class ColumnTarget
    {
        public string label;
        public Transform group;
        public XRSimpleInteractable interactable;
        public readonly List<Renderer> renderers = new List<Renderer>();
        public readonly List<MaterialPropertyBlock> originalBlocks = new List<MaterialPropertyBlock>();
        public readonly List<int> colorProperties = new List<int>();
        public readonly HashSet<Renderer> masterMixWellRenderers = new HashSet<Renderer>();
        public bool masterMixShaded;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private readonly List<ColumnTarget> targets = new List<ColumnTarget>();
    private readonly HashSet<ColumnTarget> selectedTargets = new HashSet<ColumnTarget>();
    private readonly HashSet<string> visitedColumnLabels = new HashSet<string>();
    private ColumnTarget hovered;
    private ColumnTarget selected;
    [SerializeField] private Canvas popupCanvas;
    [SerializeField] private RectTransform popupRect;
    [SerializeField] private Text popupText;
    private Camera viewCamera;
    private bool wasStepActive;
    private int lastGuideStepNumber;
    private bool completionPending;

    private void Awake()
    {
        Debug.Log("wake up");
        if (guide == null) guide = FindFirstObjectByType<PCRGuideUIController>();
        if (plateWellRoot == null)
        {
            var plate = GameObject.Find("pcr_plate_setup_animation");
            if (plate != null) plateWellRoot = plate.transform.Find("Plate_Root");
        }
        if (viewCamera == null) viewCamera = Camera.main;
        BuildTargets();
        BuildPopup();
    }

    private void Update()
    {
        int stepNumber = guide != null ? guide.CurrentStepNumber : 0;
        bool active = guide != null && stepNumber == 1;

        if (active != wasStepActive)
        {
            wasStepActive = active;
            if (!active)
            {
                hovered = null;
                selected = null;
                selectedTargets.Clear();
                visitedColumnLabels.Clear();
                completionPending = false;
                RefreshHighlights();
                HidePopup();
            }
            else if (stepNumber == 1)
            {
                guide.SetStepActionFeedback("Hover over any three different labeled columns.");
            }

            foreach (var target in targets)
                if (target.interactable != null) target.interactable.enabled = active;
        }

        if (stepNumber != lastGuideStepNumber)
        {
            if (lastGuideStepNumber == 1 && stepNumber != 1)
            {
                visitedColumnLabels.Clear();
                selectedTargets.Clear();
                selected = null;
                completionPending = false;
                RefreshHighlights();
            }
            lastGuideStepNumber = stepNumber;
            if (stepNumber == 1 && guide != null)
                guide.SetStepActionFeedback("Hover over any three different labeled columns.");
        }

        if (popupCanvas != null && popupCanvas.enabled && viewCamera != null)
            popupCanvas.transform.rotation = Quaternion.LookRotation(popupCanvas.transform.position - viewCamera.transform.position);
    }
    private void BuildTargets()
    {
        if (plateWellRoot == null) return;
        // The labeled group colliders are named "Collider" beneath groups such
        // as PTC, NTC, S1, and S1-D. Ignore the individual well colliders.
        var colliders = plateWellRoot.GetComponentsInChildren<BoxCollider>(true);
        Debug.Log(colliders.Length + "are the available colliders");
        foreach (var box in colliders)
        {
            if (box.gameObject.name != "Collider" || box.transform.parent == null) continue;

            var group = box.transform.parent;
            var interactable = box.GetComponent<XRSimpleInteractable>();
            if (interactable == null) interactable = box.gameObject.AddComponent<XRSimpleInteractable>();

            var target = new ColumnTarget
            {
                label = group.name,
                group = group,
                interactable = interactable
            };

            foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var material = renderer.sharedMaterial;
                if (material == null) continue;

                int property = material.HasProperty(BaseColorId) ? BaseColorId :
                    (material.HasProperty(ColorId) ? ColorId : 0);
                if (property == 0) continue;

                var original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original);
                target.renderers.Add(renderer);
                if (renderer.gameObject.name.StartsWith("Cone."))
                    target.masterMixWellRenderers.Add(renderer);
                target.originalBlocks.Add(original);
                target.colorProperties.Add(property);
            }

            interactable.hoverEntered.AddListener(args => OnHoverEntered(target));
            interactable.hoverExited.AddListener(args => OnHoverExited(target));
            targets.Add(target);
            interactable.enabled = false;
        }
    }

    public bool HighlightMasterMixColumn(string columnLabel)
    {
        foreach (var target in targets)
        {
            if (!string.Equals(target.label, columnLabel, System.StringComparison.OrdinalIgnoreCase))
                continue;

            target.masterMixShaded = true;
            RefreshHighlights();
            return true;
        }
        return false;
    }

    private void OnHoverEntered(ColumnTarget target)
    {
        if (!IsStepActive() || target == null) return;
        hovered = target;
        selected = target;
        selectedTargets.Add(target);
        // RefreshHighlights();
        ShowPopup(target);
        RegisterVisitedColumn(target);
    }

    private void RegisterVisitedColumn(ColumnTarget target)
    {
        if (!IsStepActive() || completionPending) return;
        bool isNewColumn = visitedColumnLabels.Add(target.label);
        if (!isNewColumn)
        {
            guide.SetStepActionFeedback(target.label + " already visited. Hover over a different column. " +
                visitedColumnLabels.Count + " of 3 columns visited.");
            return;
        }

        if (visitedColumnLabels.Count >= 3)
        {
            completionPending = true;
            guide.SetStepActionFeedback("Three different columns visited. Moving to the next step...");
            guide.CompleteCurrentAction();
            guide.AdvanceGuideStep();
            return;
        }

        guide.SetStepActionFeedback("Visited " + visitedColumnLabels.Count + " of 3 columns. Hover over any different column.");
    }

    private void OnHoverExited(ColumnTarget target)
    {
        if (hovered != target) return;
        hovered = null;
        RefreshHighlights();

        if (selected != null) ShowPopup(selected);
        else HidePopup();

        if (guide != null && IsStepActive())
            guide.SetStepActionFeedback(selected != null
                ? "Selected column: " + selected.label + ". Point to another column to inspect it."
                : "Hover over a labeled column to inspect it.");
    }

    private void AdvanceAfterSelection()
    {
        if (completionPending && guide != null && guide.CurrentStepNumber == 1)
            guide.AdvanceGuideStep();
    }
    private bool IsStepActive() => guide != null && guide.CurrentStepNumber == 1;

    private void RefreshHighlights()
    {
        foreach (var target in targets)
        {
            Color? tint = target == hovered ? hoverColor : (selectedTargets.Contains(target) ? selectedColor : (Color?)null);
            for (int i = 0; i < target.renderers.Count; i++)
            {
                var renderer = target.renderers[i];
                if (renderer == null) continue;

                if (target.masterMixWellRenderers.Contains(renderer))
                {
                    if (target.masterMixShaded)
                    {
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block);
                        block.SetColor(target.colorProperties[i], masterMixShadeColor);
                        renderer.SetPropertyBlock(block);
                    }
                    else
                    {
                        // Column-ray hover colors apply to the group, never to well meshes.
                        renderer.SetPropertyBlock(target.originalBlocks[i]);
                    }
                    continue;
                }

                if (!tint.HasValue)
                {
                    renderer.SetPropertyBlock(target.originalBlocks[i]);
                    continue;
                }

                var highlight = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(highlight);
                highlight.SetColor(target.colorProperties[i], tint.Value);
                renderer.SetPropertyBlock(highlight);
            }
        }
    }

    private void BuildPopup()
    {
        var popupObject = transform.Find("PCR Column Name Popup");
        if (popupCanvas == null && popupObject != null)
            popupCanvas = popupObject.GetComponent<Canvas>();
        if (popupCanvas == null)
        {
            Debug.LogWarning("PCR column popup Canvas is not assigned or present under Plate_Root.", this);
            return;
        }

        if (popupRect == null)
            popupRect = popupCanvas.GetComponent<RectTransform>();
        if (popupText == null)
            popupText = popupCanvas.GetComponentInChildren<Text>(true);

        if (popupRect == null || popupText == null)
        {
            Debug.LogWarning("PCR column popup needs a RectTransform and Text child.", this);
            popupCanvas.enabled = false;
            return;
        }

        popupCanvas.enabled = false;
    }

    private void ShowPopup(ColumnTarget target)
    {
        if (popupCanvas == null || target == null) return;
        popupText.text = target.label;
        popupCanvas.transform.position = GetGroupPosition(target) + popupOffset;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null)
            popupCanvas.transform.rotation = Quaternion.LookRotation(popupCanvas.transform.position - viewCamera.transform.position);
        popupCanvas.enabled = true;
    }

    private Vector3 GetGroupPosition(ColumnTarget target)
    {
        var renderers = target.group.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return target.group.position;
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }

    private void HidePopup()
    {
        if (popupCanvas != null) popupCanvas.enabled = false;
    }
}
