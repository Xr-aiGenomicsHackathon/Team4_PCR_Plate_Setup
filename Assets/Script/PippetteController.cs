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
    [SerializeField, Min(1)] private int requiredSampleColumns = 3;
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
    private readonly HashSet<string> contactedSampleColumns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Transform> liftedCones = new HashSet<Transform>();
    private bool masterMixContactReady;
    private bool stageCompleted;
    private bool controlsStageCompleted;
    private bool ptcContacted;
    private bool ntcContacted;
    private bool dnaTubeContactReady;
    private bool samplesStageCompleted;
    private readonly HashSet<string> stageFiveSampleColumns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> contactedDuplicateColumns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
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

            // Each fresh trigger-entry event is a source contact. Do not require
            // every collider under the source root to have exited first; a missed
            // exit can otherwise block all later source-to-column cycles.
            if (target == dnaTubeVisual &&
                (IsSamplesStageActive() || IsDuplicateStageActive()))
            {
                dnaTubeContactReady = true;
                if (guide != null)
                    guide.SetStepActionFeedback(IsSamplesStageActive()
                        ? "DNA Tube contacted. Touch one different sample column, then contact the DNA Tube again."
                        : "DNA Tube contacted. Touch one different duplicate column, then contact the DNA Tube again.");
            }
            if (target == masterMixVisual && IsMasterMixStageActive())
            {
                masterMixContactReady = true;
                if (guide != null)
                    guide.SetStepActionFeedback("Master Mix contacted. Touch one different sample column, then contact Master Mix again.");
            }
        }

        var column = ResolveColumn(other);
        if (column == null) return;
        if ((IsMasterMixStageActive() || IsSamplesStageActive() || IsDuplicateStageActive()) &&
            !IsNearestOverlappingColumn(column))
            return;

        if (IsMasterMixStageActive())
        {
            if (!masterMixContactReady)
            {
                if (guide != null)
                    guide.SetStepActionFeedback("Contact Master Mix before each sample column.");
                return;
            }

            if (!IsRegularSampleColumn(column.name))
            {
                if (guide != null)
                    guide.SetStepActionFeedback("This step needs a regular sample column. Contact Master Mix again before another column.");
                return;
            }

            // Only a valid regular sample column consumes this Master Mix contact.
            masterMixContactReady = false;
            LiftColumnCones(column);
            bool isNewColumn = contactedSampleColumns.Add(column.name);
            if (guide != null)
                guide.SetStepActionFeedback(isNewColumn
                    ? "Master Mix cycles · " + contactedSampleColumns.Count + "/" + requiredSampleColumns + " distinct sample columns. Contact Master Mix again before the next column."
                    : column.name + " was already counted. Contact Master Mix before another different sample column.");

            TryCompleteMasterMixStage();
            return;
        }

        if (IsSamplesStageActive())
        {
            if (!dnaTubeContactReady)
            {
                if (guide != null)
                    guide.SetStepActionFeedback("Contact the DNA Tube before each sample column.");
                return;
            }

            if (!IsRegularSampleColumn(column.name) || !contactedSampleColumns.Contains(column.name))
            {
                if (guide != null)
                    guide.SetStepActionFeedback("Touch one of the sample columns completed with Master Mix. Contact the DNA Tube again before another column.");
                return;
            }

            dnaTubeContactReady = false;
            LiftColumnCones(column);
            bool isNewColumn = stageFiveSampleColumns.Add(column.name);
            if (guide != null)
                guide.SetStepActionFeedback(isNewColumn
                    ? "DNA Tube cycles · " + stageFiveSampleColumns.Count + "/" + contactedSampleColumns.Count + " selected sample columns. Contact the DNA Tube again before the next column."
                    : column.name + " was already counted. Contact the DNA Tube before another different sample column.");
            TryCompleteSamplesStage();
            return;
        }

        if (IsDuplicateStageActive())
        {
            if (!dnaTubeContactReady)
            {
                if (guide != null)
                    guide.SetStepActionFeedback("Contact the DNA Tube before each duplicate column.");
                return;
            }

            if (!IsSelectedDuplicateColumn(column.name))
            {
                if (guide != null)
                    guide.SetStepActionFeedback("Touch the matching -D duplicate of a Master Mix sample column. Contact the DNA Tube again before another column.");
                return;
            }

            dnaTubeContactReady = false;
            LiftColumnCones(column);
            bool isNewColumn = contactedDuplicateColumns.Add(column.name);
            if (guide != null)
                guide.SetStepActionFeedback(isNewColumn
                    ? "DNA Tube cycles · " + contactedDuplicateColumns.Count + "/" + contactedSampleColumns.Count + " matching duplicate columns. Contact the DNA Tube again before the next column."
                    : column.name + " was already counted. Contact the DNA Tube before another different duplicate column.");
            TryCompleteDuplicateStage();
            return;
        }

        LiftColumnCones(column);
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
        return IsSampleColumn(columnName) && columnName.EndsWith("-D", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRegularSampleColumn(string columnName)
    {
        return IsSampleColumn(columnName) && !IsDuplicateColumn(columnName);
    }

    private bool IsSelectedDuplicateColumn(string columnName)
    {
        if (!IsDuplicateColumn(columnName)) return false;
        string sourceColumn = columnName.Substring(0, columnName.Length - 2);
        return contactedSampleColumns.Contains(sourceColumn);
    }

    private void TryCompleteDuplicateStage()
    {
        if (!IsDuplicateStageActive() ||
            contactedSampleColumns.Count < requiredDuplicateColumns ||
            !HasEverySelectedDuplicateColumn()) return;
        duplicateStageCompleted = true;
        guide.SetStepActionFeedback("Matching duplicate columns for all Master Mix sample columns contacted");
        guide.CompleteCurrentAction();
        guide.AdvanceGuideStep();
    }

    private bool IsSamplesStageActive()
    {
        return guide != null && guide.CurrentStepNumber == SamplesStageNumber && !samplesStageCompleted;
    }

    private void TryCompleteSamplesStage()
    {
        if (!IsSamplesStageActive() ||
            contactedSampleColumns.Count < requiredStageFiveSampleColumns ||
            !stageFiveSampleColumns.IsSupersetOf(contactedSampleColumns)) return;
        samplesStageCompleted = true;
        guide.SetStepActionFeedback("DNA Tube contacted for all Master Mix sample columns");
        guide.CompleteCurrentAction();
        guide.AdvanceGuideStep();
    }

    private bool HasEverySelectedDuplicateColumn()
    {
        foreach (string sampleColumn in contactedSampleColumns)
            if (!contactedDuplicateColumns.Contains(sampleColumn + "-D")) return false;
        return true;
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
        if (!IsMasterMixStageActive() ||
            contactedSampleColumns.Count < requiredSampleColumns)
            return;

        stageCompleted = true;
        guide.SetStepActionFeedback("Three distinct sample columns completed through repeated Master Mix contacts");
        guide.CompleteCurrentAction();
        guide.AdvanceGuideStep();
    }

    private bool IsNearestOverlappingColumn(Transform contactedColumn)
    {
        var pipetteCollider = GetComponent<Collider>();
        if (plateRoot == null || contactedColumn == null || pipetteCollider == null) return true;

        var tipBounds = pipetteCollider.bounds;
        var contactedBox = GetColumnGroupCollider(contactedColumn);
        if (contactedBox == null || !contactedBox.bounds.Intersects(tipBounds)) return true;

        Transform nearestColumn = null;
        float nearestSurfaceDistance = float.PositiveInfinity;
        float nearestCenterDistance = float.PositiveInfinity;
        for (int i = 0; i < plateRoot.childCount; i++)
        {
            var candidate = plateRoot.GetChild(i);
            var candidateBox = GetColumnGroupCollider(candidate);
            if (candidateBox == null || !candidateBox.enabled || !candidateBox.gameObject.activeInHierarchy ||
                !candidateBox.bounds.Intersects(tipBounds))
                continue;

            Vector3 closestPoint = candidateBox.bounds.ClosestPoint(tipBounds.center);
            float surfaceDistance = (closestPoint - tipBounds.center).sqrMagnitude;
            float centerDistance = (candidateBox.bounds.center - tipBounds.center).sqrMagnitude;
            bool isCloser = surfaceDistance < nearestSurfaceDistance ||
                (Mathf.Approximately(surfaceDistance, nearestSurfaceDistance) && centerDistance < nearestCenterDistance);
            if (!isCloser) continue;

            nearestSurfaceDistance = surfaceDistance;
            nearestCenterDistance = centerDistance;
            nearestColumn = candidate;
        }

        return nearestColumn == null || nearestColumn == contactedColumn;
    }

    private static BoxCollider GetColumnGroupCollider(Transform column)
    {
        var colliderTransform = column != null ? column.Find("Collider") : null;
        return colliderTransform != null ? colliderTransform.GetComponent<BoxCollider>() : null;
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