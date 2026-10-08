using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles pipette contact feedback for the master mix and DNA tube, lifts every
/// cone in a contacted plate column, and validates the master-mix stage.
/// </summary>
public sealed class PippetteController : MonoBehaviour
{
    [Header("Contact labels")]
    [SerializeField] private GameObject masterMix;
    [SerializeField] private GameObject dnaTube;
    [SerializeField] private Color selectedBackground = Color.red;
    [SerializeField] private Color selectedText = Color.white;

    [Header("Column lift")]
    [SerializeField, Min(0f)] private float columnLiftHeight = 0.025f;
    [SerializeField, Min(0.01f)] private float columnLiftDuration = 0.35f;

    [Header("Stage validation")]
    [SerializeField] private PCRGuideUIController guide;
    [SerializeField, Min(1)] private int requiredSampleColumns = 2;
    [SerializeField, Min(1)] private int requiredStageFiveSampleColumns = 3;
    [SerializeField, Min(1)] private int requiredDuplicateColumns = 3;

    private const int MasterMixStageNumber = 3;
    private const int ControlsStageNumber = 4;
    private const int SamplesStageNumber = 5;
    private const int DuplicateStageNumber = 6;
    private Transform plateRoot;
    private TargetVisual masterMixVisual;
    private TargetVisual dnaTubeVisual;
    private TargetVisual selectedVisual;
    private readonly HashSet<Collider> masterMixContacts = new HashSet<Collider>();
    private readonly HashSet<Collider> dnaTubeContacts = new HashSet<Collider>();
    private readonly HashSet<string> contactedSampleColumns = new HashSet<string>();
    private readonly HashSet<Transform> liftedCones = new HashSet<Transform>();
    private bool masterMixContacted;
    private bool stageCompleted;
    private bool controlsStageCompleted;
    private bool ptcContacted;
    private bool ntcContacted;
    private bool dnaTubeContacted;
    private bool samplesStageCompleted;
    private readonly HashSet<string> stageFiveSampleColumns = new HashSet<string>();
    private readonly HashSet<string> contactedDuplicateColumns = new HashSet<string>();
    private bool duplicateStageCompleted;

    private void Awake()
    {
        if (masterMix == null) masterMix = GameObject.Find("low_poly_tray");
        if (dnaTube == null) dnaTube = GameObject.Find("microcentrifuge_tubes");
        if (guide == null) guide = FindFirstObjectByType<PCRGuideUIController>();

        var plate = GameObject.Find("pcr_plate_setup_animation/Plate_Root");
        if (plate != null) plateRoot = plate.transform;

        masterMixVisual = new TargetVisual(masterMix, "Master Mix");
        dnaTubeVisual = new TargetVisual(dnaTube, "DNA Tube");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        var target = ResolveTarget(other);
        if (target != null)
        {
            ContactsFor(target).Add(other);
            Select(target);
            if (target == dnaTubeVisual && IsSamplesStageActive())
            {
                dnaTubeContacted = true;
                TryCompleteSamplesStage();
            }
            if (target == masterMixVisual && IsMasterMixStageActive())
            {
                masterMixContacted = true;
                TryCompleteMasterMixStage();
            }
        }

        var column = ResolveColumn(other);
        if (column == null) return;

        LiftColumnCones(column);
        if (IsSampleColumn(column.name) && IsMasterMixStageActive())
        {
            contactedSampleColumns.Add(column.name);
            if (guide != null)
                guide.SetStepActionFeedback(
                    "Master mix contacted · " + contactedSampleColumns.Count + "/" +
                    requiredSampleColumns + " sample columns contacted");

            TryCompleteMasterMixStage();
        }

        if (IsSampleColumn(column.name) && IsSamplesStageActive())
        {
            stageFiveSampleColumns.Add(column.name);
            if (guide != null)
                guide.SetStepActionFeedback("DNA tube contacted · " + stageFiveSampleColumns.Count + "/" + requiredStageFiveSampleColumns + " sample columns contacted");
            TryCompleteSamplesStage();
        }

        if (IsDuplicateColumn(column.name) && IsDuplicateStageActive())
        {
            contactedDuplicateColumns.Add(column.name);
            if (guide != null)
                guide.SetStepActionFeedback("Duplicate columns contacted · " + contactedDuplicateColumns.Count + "/" + requiredDuplicateColumns);
            TryCompleteDuplicateStage();
        }

        if (IsControlColumn(column.name) && IsControlsStageActive())
        {
            if (column.name.Equals("PTC", System.StringComparison.OrdinalIgnoreCase)) ptcContacted = true;
            if (column.name.Equals("NTC", System.StringComparison.OrdinalIgnoreCase)) ntcContacted = true;
            if (guide != null)
                guide.SetStepActionFeedback("Control columns contacted · PTC " + (ptcContacted ? "✓" : "·") + " NTC " + (ntcContacted ? "✓" : "·"));
            TryCompleteControlsStage();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var target = ResolveTarget(other);
        if (target == null) return;

        var contacts = ContactsFor(target);
        contacts.Remove(other);
        if (selectedVisual != target || contacts.Count > 0) return;

        target.Restore();
        selectedVisual = null;

        if (dnaTubeContacts.Count > 0) Select(dnaTubeVisual);
        else if (masterMixContacts.Count > 0) Select(masterMixVisual);
    }

    private bool IsMasterMixStageActive()
    {
        return guide != null && guide.CurrentStepNumber == MasterMixStageNumber && !stageCompleted;
    }

    private bool IsDuplicateStageActive()
    {
        return guide != null && guide.CurrentStepNumber == DuplicateStageNumber && !duplicateStageCompleted;
    }

    private static bool IsDuplicateColumn(string columnName)
    {
        return IsSampleColumn(columnName) && columnName.IndexOf("-D", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void TryCompleteDuplicateStage()
    {
        if (!IsDuplicateStageActive() || contactedDuplicateColumns.Count < requiredDuplicateColumns) return;
        duplicateStageCompleted = true;
        guide.SetStepActionFeedback("Three sample-duplicate columns contacted");
        guide.CompleteCurrentAction();
        guide.AdvanceGuideStep();
    }

    private bool IsSamplesStageActive()
    {
        return guide != null && guide.CurrentStepNumber == SamplesStageNumber && !samplesStageCompleted;
    }

    private void TryCompleteSamplesStage()
    {
        if (!IsSamplesStageActive() || !dnaTubeContacted || stageFiveSampleColumns.Count < requiredStageFiveSampleColumns) return;
        samplesStageCompleted = true;
        guide.SetStepActionFeedback("DNA tube and sample column contacts complete");
        guide.CompleteCurrentAction();
        guide.AdvanceGuideStep();
    }

    private bool IsControlsStageActive()
    {
        return guide != null && guide.CurrentStepNumber == ControlsStageNumber && !controlsStageCompleted;
    }

    private static bool IsControlColumn(string columnName)
    {
        return columnName.Equals("PTC", System.StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("NTC", System.StringComparison.OrdinalIgnoreCase);
    }

    private void TryCompleteControlsStage()
    {
        if (!IsControlsStageActive() || !ptcContacted || !ntcContacted) return;
        controlsStageCompleted = true;
        guide.SetStepActionFeedback("PTC and NTC columns contacted");
        guide.CompleteCurrentAction();
        guide.AdvanceGuideStep();
    }

    private void TryCompleteMasterMixStage()
    {
        if (!IsMasterMixStageActive() || !masterMixContacted ||
            contactedSampleColumns.Count < requiredSampleColumns)
            return;

        stageCompleted = true;
        guide.SetStepActionFeedback("Master mix and sample column contacts complete");
        guide.CompleteCurrentAction();
        guide.AdvanceGuideStep();
    }

    private Transform ResolveColumn(Collider other)
    {
        if (plateRoot == null || other == null) return null;

        var current = other.transform;
        while (current != null && current.parent != plateRoot)
            current = current.parent;

        if (current == null || current.parent != plateRoot) return null;
        return current.GetComponentInChildren<Collider>(true) != null ? current : null;
    }

    private static bool IsSampleColumn(string columnName)
    {
        return !string.IsNullOrEmpty(columnName) &&
               columnName.StartsWith("S", System.StringComparison.OrdinalIgnoreCase);
    }

    private void LiftColumnCones(Transform column)
    {
        var cones = column.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("Cone", System.StringComparison.OrdinalIgnoreCase) &&
                        t.GetComponent<Renderer>() != null);

        foreach (var cone in cones)
        {
            if (!liftedCones.Add(cone)) continue;
            StartCoroutine(RaiseCone(cone));
        }
    }

    private IEnumerator RaiseCone(Transform cone)
    {
        if (cone == null || cone.parent == null) yield break;

        // The plate hierarchy is heavily scaled, so animate in world space to
        // keep the configured lift distance independent of the parent's scale.
        var start = cone.position;
        var end = start + Vector3.up * columnLiftHeight;
        var elapsed = 0f;
        var duration = Mathf.Max(0.01f, columnLiftDuration);

        while (elapsed < duration && cone != null)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            cone.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (cone != null) cone.position = end;
    }

    private TargetVisual ResolveTarget(Collider other)
    {
        if (other == null) return null;
        if (other.CompareTag("dnatube") || IsInside(other.transform, dnaTube)) return dnaTubeVisual;
        if (other.CompareTag("mastermix") || IsInside(other.transform, masterMix)) return masterMixVisual;
        return null;
    }

    private static bool IsInside(Transform item, GameObject root)
    {
        return root != null && item != root.transform && item.IsChildOf(root.transform);
    }

    private HashSet<Collider> ContactsFor(TargetVisual target)
    {
        return target == dnaTubeVisual ? dnaTubeContacts : masterMixContacts;
    }

    private void Select(TargetVisual target)
    {
        if (selectedVisual == target) return;
        if (selectedVisual != null) selectedVisual.Restore();
        selectedVisual = target;
        selectedVisual.Apply(selectedBackground, selectedText);
    }

    private sealed class TargetVisual
    {
        private readonly Image background;
        private readonly Text label;
        private readonly Color originalBackground;
        private readonly Color originalText;
        private readonly FontStyle originalFontStyle;
        private readonly string originalLabel;
        private readonly string displayName;

        public TargetVisual(GameObject targetRoot, string name)
        {
            displayName = name;
            if (targetRoot == null) return;

            background = targetRoot.GetComponentsInChildren<Image>(true).FirstOrDefault();
            label = targetRoot.GetComponentsInChildren<Text>(true).FirstOrDefault();
            if (background != null) originalBackground = background.color;
            if (label != null)
            {
                originalText = label.color;
                originalFontStyle = label.fontStyle;
                originalLabel = label.text;
                if (displayName == "DNA Tube" && originalLabel == "Master Mix")
                    originalLabel = displayName;
            }
        }

        public void Apply(Color backgroundColor, Color textColor)
        {
            if (background != null) background.color = backgroundColor;
            if (label != null)
            {
                label.text = displayName + " (Selected)";
                label.color = textColor;
                label.fontStyle = FontStyle.Bold;
            }
        }

        public void Restore()
        {
            if (background != null) background.color = originalBackground;
            if (label != null)
            {
                label.text = originalLabel;
                label.color = originalText;
                label.fontStyle = originalFontStyle;
            }
        }
    }
}