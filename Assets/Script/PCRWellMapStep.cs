using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public sealed class PCRWellMapStep : MonoBehaviour
{
    [SerializeField] private PCRGuideUIController guide;
    [SerializeField] private Transform plateWellRoot;
    [SerializeField] private float interactionRadius = 0.011f;

    private sealed class Well
    {
        public string coordinate;
        public MeshRenderer renderer;
        public XRSimpleInteractable interactable;
        public bool completed;
    }

    private readonly List<Well> wells = new List<Well>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private readonly Color focusColor = new Color(0.16f, 0.78f, 0.55f);
    private readonly Color hoverColor = new Color(0.28f, 0.68f, 0.92f);
    private readonly Color completedColor = new Color(0.16f, 0.72f, 0.43f);
    private int nextTarget;
    private bool wasActive;

    private IEnumerator Start()
    {
        if (guide == null)
            guide = FindFirstObjectByType<PCRGuideUIController>();

        if (plateWellRoot == null)
        {
            var plate = GameObject.Find("pcr_plate_setup_animation");
            if (plate != null)
                plateWellRoot = plate.transform.Find("Plate_Root");
        }

        // Let the XR Interaction Manager finish initializing before registering wells.
        yield return null;
        InitializeWells();
    }

    private void Update()
    {
        if (guide == null || wells.Count == 0) return;

        bool active = guide.CurrentStepNumber == 2;
        if (active == wasActive) return;

        wasActive = active;
        if (active)
        {
            guide.SetStepActionFeedback("Select each well from A1 through D6 in row order. 0 / 24 mapped.");
            RefreshWellColors();
        }
        else
        {
            foreach (var well in wells)
                SetWellColor(well, Color.white, false);
        }
    }

    private void InitializeWells()
    {
        if (plateWellRoot == null) return;

        for (int index = 1; index <= 96; index++)
        {
            string objectName = "Cone." + index.ToString("000");
            var wellTransform = plateWellRoot.Find(objectName);
            if (wellTransform == null) continue;

            var renderer = wellTransform.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.bounds.size.sqrMagnitude < 0.000001f) continue;

            var sphere = wellTransform.GetComponent<SphereCollider>();
            if (sphere == null) sphere = wellTransform.gameObject.AddComponent<SphereCollider>();
            sphere.radius = interactionRadius / Mathf.Max(0.001f, MaxAbs(wellTransform.lossyScale));
            sphere.center = Vector3.zero;

            var interactable = wellTransform.GetComponent<XRSimpleInteractable>();
            if (interactable == null) interactable = wellTransform.gameObject.AddComponent<XRSimpleInteractable>();
            if (!interactable.colliders.Contains(sphere))
                interactable.colliders.Add(sphere);

            int zeroBased = index - 1;
            int row = zeroBased / 12;
            int column = zeroBased % 12 + 1;
            var well = new Well
            {
                coordinate = ((char)('A' + row)).ToString() + column,
                renderer = renderer,
                interactable = interactable
            };
            wells.Add(well);

            interactable.firstHoverEntered.AddListener(_ => OnWellHover(well, true));
            interactable.lastHoverExited.AddListener(_ => OnWellHover(well, false));
            interactable.selectEntered.AddListener(_ => OnWellSelected(well));
        }

        if (wells.Count < 96 && guide != null)
            guide.SetStepActionFeedback("Well map setup incomplete: expected 96 wells, found " + wells.Count + ".");
        else if (guide != null && guide.CurrentStepNumber == 2)
            guide.SetStepActionFeedback("Select each well from A1 through D6 in row order. 0 / 24 mapped.");

        RefreshWellColors();
    }

    private void OnWellHover(Well well, bool entered)
    {
        if (!wasActive || well.completed || guide.CurrentStepNumber != 2) return;
        if (entered)
            SetWellColor(well, IsExpected(well) ? focusColor : hoverColor, true);
        else
            RefreshWellColors();
    }

    private void OnWellSelected(Well well)
    {
        if (!wasActive || guide == null || guide.CurrentStepNumber != 2 || nextTarget >= 24) return;

        if (!IsExpected(well))
        {
            guide.SetStepActionFeedback("That is " + well.coordinate + ". Locate " + ExpectedCoordinate() + " next.");
            return;
        }

        well.completed = true;
        nextTarget++;
        SetWellColor(well, completedColor, true);

        if (nextTarget >= 24)
        {
            guide.SetStepActionFeedback("A1–D6 mapped. The coordinate practice is complete.");
            guide.CompleteCurrentAction();
            return;
        }

        guide.SetStepActionFeedback("Correct. Now select " + ExpectedCoordinate() + ". " + nextTarget + " / 24 mapped.");
        RefreshWellColors();
    }

    private bool IsExpected(Well well)
    {
        if (nextTarget >= 24) return false;
        int targetRow = nextTarget / 6;
        int targetColumn = nextTarget % 6 + 1;
        return well.coordinate == ((char)('A' + targetRow)).ToString() + targetColumn;
    }

    private string ExpectedCoordinate()
    {
        int targetRow = nextTarget / 6;
        int targetColumn = nextTarget % 6 + 1;
        return ((char)('A' + targetRow)).ToString() + targetColumn;
    }

    private void RefreshWellColors()
    {
        foreach (var well in wells)
        {
            if (well.completed)
                SetWellColor(well, completedColor, true);
            else if (wasActive && guide != null && guide.CurrentStepNumber == 2 && IsExpected(well))
                SetWellColor(well, focusColor, true);
            else
                SetWellColor(well, Color.white, false);
        }
    }

    private static void SetWellColor(Well well, Color color, bool enabled)
    {
        if (well.renderer == null) return;
        if (!enabled)
        {
            well.renderer.SetPropertyBlock(null);
            return;
        }

        var block = new MaterialPropertyBlock();
        well.renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        well.renderer.SetPropertyBlock(block);
    }

    private static float MaxAbs(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }
}