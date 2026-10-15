using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Validates the configured, approved five-sample plate map without transferring liquid.</summary>
public sealed class PCRWellMapStep : MonoBehaviour
{
    [Serializable] public sealed class WellBinding
    {
        public string coordinate;
        public MeshRenderer renderer;
    }
    [SerializeField] private PCRGuideUIController guide;
    [SerializeField] private Transform plateWellRoot;
    [SerializeField] private WellBinding[] wellBindings = new WellBinding[0];
    [Serializable] public sealed class Assignment
    {
        public string coordinate;
        public string identity;
    }
    [SerializeField] private Assignment[] assignments = new Assignment[0];
    private string selectedIdentity;
    public string SelectedIdentity => selectedIdentity;
    public string[] Identities => new[] { "S1", "S2", "S3", "S4", "S5", "PTC", "NTC" };
    [SerializeField, Min(0)] private int correctSelectionPoints;
    [SerializeField, Min(0)] private int invalidSelectionPenalty;
    private sealed class Well
    {
        public WellBinding binding;
        public XRSimpleInteractable interaction;
        public MaterialPropertyBlock original;
        public UnityAction<SelectEnterEventArgs> select;
        public UnityAction<HoverEnterEventArgs> hoverEnter;
        public UnityAction<HoverExitEventArgs> hoverExit;
        public bool done;
    }
    private readonly Dictionary<string, Well> wells = new Dictionary<string, Well>();
    private bool ready, wasActive;
    private int nextTarget, invalidAttempts, score;
    public bool IsComplete => ready && nextTarget == assignments.Length;
    public int CompletedCount => nextTarget;
    public int TargetCount => assignments.Length;
    public int InvalidAttempts => invalidAttempts;
    public int Score => score;
    public string ExpectedCoordinate
    {
        get { foreach (var target in assignments) if (!wells.ContainsKey(target.coordinate) || !wells[target.coordinate].done) return target.coordinate; return ""; }
    }
    public bool ConfigurationReady => ready;
    private bool IsActive => ready && guide != null && guide.CurrentStepNumber == 2;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Start()
    {
        if (guide == null || plateWellRoot == null || wellBindings.Length != 96 ||
            assignments.Length == 0) { Fail("Assign all 96 wells and the target coordinates."); return; }
        var unique = new HashSet<string>();
        foreach (var binding in wellBindings)
        {
            if (binding == null || binding.renderer == null || string.IsNullOrEmpty(binding.coordinate) ||
                !unique.Add(binding.coordinate)) { Fail("Missing or duplicate well binding."); return; }
        }
        unique.Clear();
        foreach (var target in assignments)
        {
            if (!Array.Exists(wellBindings, b => b.coordinate == target.coordinate) || !unique.Add(target.coordinate) || !Array.Exists(Identities, id => id == target.identity))
            { Fail("Target coordinates must be unique and present on the plate."); return; }
        }
        foreach (var binding in wellBindings)
        {
            var t = binding.renderer.transform;
            float spacing = float.MaxValue;
            foreach (var other in wellBindings)
                if (other != binding) spacing = Mathf.Min(spacing,
                    Vector3.Distance(binding.renderer.bounds.center, other.renderer.bounds.center));
            var sphere = t.GetComponent<SphereCollider>();
            if (sphere == null) sphere = t.gameObject.AddComponent<SphereCollider>();
            sphere.center = t.InverseTransformPoint(binding.renderer.bounds.center);
            var scale = t.lossyScale;
            sphere.radius = spacing * .38f / Mathf.Max(.000001f,
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            var interaction = t.GetComponent<XRSimpleInteractable>();
            if (interaction == null) interaction = t.gameObject.AddComponent<XRSimpleInteractable>();
            interaction.colliders.Clear();
            interaction.colliders.Add(sphere);
            var original = new MaterialPropertyBlock();
            binding.renderer.GetPropertyBlock(original);
            var well = new Well { binding=binding, interaction=interaction, original=original };
            well.select = _ => SelectCoordinate(binding.coordinate);
            well.hoverEnter = _ =>
            {
                if (!IsActive) return;
                if (!well.done) Tint(well, new Color(.25f,.68f,.95f));
                guide.SetPlateLayoutFeedback("Pointing at " + binding.coordinate +
                    (well.done ? " (assigned). " : ". ") + "Selected identity: " +
                    (string.IsNullOrEmpty(selectedIdentity) ? "none" : selectedIdentity) +
                    ". " + nextTarget + "/" + TargetCount + " assigned.", false);
            };
            well.hoverExit = _ => RefreshColors();
            interaction.selectEntered.AddListener(well.select);
            interaction.firstHoverEntered.AddListener(well.hoverEnter);
            interaction.lastHoverExited.AddListener(well.hoverExit);
            interaction.enabled = false;
            wells.Add(binding.coordinate, well);
        }
        ready = true;
    }

    private void Fail(string message)
    {
        Debug.LogError("Stage 2 plate layout: " + message, this);
        if (guide != null) guide.SetPlateLayoutFeedback(message, true);
    }

    private void Update()
    {
        bool active = IsActive;
        if (active == wasActive) return;
        wasActive = active;
        foreach (var well in wells.Values) well.interaction.enabled = active;
        RefreshColors();
        if (active) ReportProgress();
    }

    public void SelectIdentity(string identity)
    {
        if (!IsActive || !Array.Exists(Identities, id => id == identity)) return;
        selectedIdentity = identity;
        ReportProgress();
    }

    public bool SelectCoordinate(string coordinate)
    {
        if (!IsActive || IsComplete) return false;
        if (coordinate != null && wells.TryGetValue(coordinate, out var previous) && previous.done)
        { ReportProgress(); return false; }
        if (string.IsNullOrEmpty(selectedIdentity))
        { guide.SetPlateLayoutFeedback("Choose a DNA sample, positive control (PTC), or no-template control (NTC) first.", false); return false; }
        var expected = Array.Find(assignments, target => target.coordinate == coordinate);
        if (expected == null || expected.identity != selectedIdentity)
        {
            invalidAttempts++;
            score = Mathf.Max(0, score - invalidSelectionPenalty);
            guide.SetPlateLayoutFeedback(expected == null
                ? coordinate + " is unassigned in this run. Use the approved row A map."
                : coordinate + " requires " + expected.identity + ". You selected " + selectedIdentity +
                  ". Check the map before assigning.", true);
            return false;
        }
        wells[coordinate].done = true;
        nextTarget++;
        score += correctSelectionPoints;
        RefreshColors();
        if (IsComplete) guide.CompleteCurrentAction();
        ReportProgress();
        return true;
    }

    private void ReportProgress()
    {
        string scoring = correctSelectionPoints == 0 && invalidSelectionPenalty == 0
            ? "Scoring rubric pending." : "Score: " + score + ".";
        guide.SetPlateLayoutFeedback(IsComplete
            ? "Plate layout complete: all " + TargetCount + " sample/control wells assigned. " + scoring
            : nextTarget + "/" + TargetCount + " assigned. Selected: " +
              (string.IsNullOrEmpty(selectedIdentity) ? "none" : selectedIdentity) + ". " + scoring, false);
    }

    private void RefreshColors()
    {
        foreach (var well in wells.Values)
        {
            if (!IsActive) well.binding.renderer.SetPropertyBlock(well.original);
            else if (well.done) Tint(well, new Color(.16f,.72f,.43f));
            else if (Array.Exists(assignments, a => a.coordinate == well.binding.coordinate))
                Tint(well, new Color(.95f,.7f,.15f));
            else well.binding.renderer.SetPropertyBlock(well.original);
        }
    }

    private static void Tint(Well well, Color color)
    {
        var block = new MaterialPropertyBlock();
        well.binding.renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        well.binding.renderer.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        foreach (var well in wells.Values)
        {
            if (well.interaction != null)
            {
                well.interaction.selectEntered.RemoveListener(well.select);
                well.interaction.firstHoverEntered.RemoveListener(well.hoverEnter);
                well.interaction.lastHoverExited.RemoveListener(well.hoverExit);
            }
            if (well.binding.renderer != null) well.binding.renderer.SetPropertyBlock(well.original);
        }
    }
}
