using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// PCR walkthrough UI. XR interactables can call CompleteCurrentAction after
/// validating a learner's real placement or handling action.
/// </summary>
public sealed class PCRGuideUIController : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private PCRWellMapStep plateLayout;
    [SerializeField] private GameObject[] plateLayoutHiddenPanels = new GameObject[0];
    private bool plateLayoutSession;
    private PanelSettings plateLayoutRuntimeSettings;
    public void BeginPlateLayout()
    {
        plateLayoutSession = true;
        if (document == null) document = GetComponent<UIDocument>();
        if (document != null)
        {
            document.enabled = false;
            if (plateLayoutRuntimeSettings == null && document.panelSettings != null)
            {
                plateLayoutRuntimeSettings = Instantiate(document.panelSettings);
                plateLayoutRuntimeSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
                plateLayoutRuntimeSettings.scale = 1f;
                plateLayoutRuntimeSettings.clearColor = true;
                plateLayoutRuntimeSettings.colorClearValue = Color.clear;
            }
            if (plateLayoutRuntimeSettings != null) document.panelSettings = plateLayoutRuntimeSettings;
            uiReady = false;
            document.enabled = true;
        }
        if (plateLayout != null) plateLayout.gameObject.SetActive(true);
        foreach (var panel in plateLayoutHiddenPanels) if (panel != null) panel.SetActive(false);
        completed[0] = true;
        currentStep = 1;
        highestUnlockedStep = 1;
        Refresh();
    }
    public void SelectLayoutIdentity(string identity) { if (plateLayout != null) plateLayout.SelectIdentity(identity); }
    public void SetPlateLayoutFeedback(string message, bool warning)
    {
        SetStepActionFeedback(message);
        if (statusDetail != null) statusDetail.style.color = warning
            ? new Color(.95f,.25f,.25f) : StyleKeyword.Null;
    }
    [SerializeField] private Animator plateAnimator;
    [SerializeField] private AudioSource narrationSource;
    [SerializeField] private AudioClip[] narrationClips = new AudioClip[8];

    private static readonly string[] StateNames = {
        "01_Meet_the_Plate", "02_Make_the_Map", "03_Master_Mix", "04_Controls",
        "05_Samples", "06_Cross_Check_Duplicates", "07_Check_Plate", "08_Seal"
    };
    private static readonly string[] Titles = {
        "Meet the plate", "Plate layout — 5 DNA samples", "Master mix", "Controls",
        "Samples", "Cross-check & duplicates", "Check the plate", "Seal the plate"
    };
    private static readonly string[] Summaries = {
        "This is our PCR plate. It has 96 tiny wells. Each well has a job.",
        "Plan five DNA samples in duplicate, plus a positive control (PTC) and a no-template control (NTC). This stage assigns identities; no liquid is transferred.",
        "Repeat at least three times: contact Master Mix, then one different sample column. Recontact Master Mix before the next column.",
        "Touch both the PTC and NTC columns with the pipette.",
        "For each of the three sample columns completed with Master Mix, contact the DNA Tube, then that same column.",
        "For each Master Mix sample column, contact the DNA Tube, then its matching duplicate column marked with “-D”.",
        "Review the well, sample, volume and control assignments before the plate is sealed.",
        "Lower the sealing film evenly across the plate, then prepare it for handoff to Team 5."
    };
    private static readonly string[] Tasks = {
        "Hover over three columns", "Assign five DNA samples and controls", "Complete at least 3 Master Mix-to-sample cycles", "Contact PTC and NTC",
        "Transfer DNA to the same 3 sample columns", "Transfer DNA to their 3 matching duplicate columns", "Run the final checklist", "Seal and hand off"
    };
    private static readonly string[] Instructions = {
        "Hover over any three different labeled columns with the ray. Each visited column stays highlighted; after the third, continue to the next stage.",
        "Choose an identity below, then select its well. A1/A2: S1 · A3/A4: S2 · A5/A6: S3 · A7/A8: S4 · A9/A10: S5 · A11: PTC · A12: NTC. Rows B–H remain unassigned.",
        "For each of at least three different sample columns, contact Master Mix first, then touch one column. Contact Master Mix again before the next column.",
        "Contact the PTC column and the NTC column with the pipette. Both columns must be touched to complete this stage.",
        "For each sample column completed with Master Mix, contact the DNA Tube first, then touch that same column. Repeat for all three columns.",
        "For each sample column completed with Master Mix, contact the DNA Tube first, then touch its matching “-D” duplicate. Repeat for all three columns.",
        "Confirm every checklist item below against the plate before sealing it.",
        "Position the sealing film, smooth it across the plate and confirm the plate is ready for handoff."
    };
    private static readonly string[] NarrationText = {
        "“This is our PCR plate. It has 96 tiny wells. Each well has a job.”",
        "“Before we add anything, we make a map. This tells us where the controls and samples belong.”",
        "“First, we add 15 microlitres of prepared master mix to the required reaction wells.”",
        "“Next, we add our controls. Controls help us check that the PCR experiment is working properly.”",
        "“Now we add 5 microlitres of each sample to its assigned well.”",
        "“Some samples are checked in duplicate wells so we can compare the reactions.”",
        "Review the checklist as you inspect the plate.",
        "“The plate is sealed and ready for the PCR machine.”"
    };
    private static readonly string[] Categories = {
        "ORIENTATION", "WELL MAP", "MASTER MIX", "CONTROLS",
        "SAMPLES", "DUPLICATES", "QUALITY CHECK", "SEAL & HANDOFF"
    };

    private VisualElement root, progressFill, actionStatus, volumeChip, checklist;
    private Label kicker, title, summary, badge, task, instruction, volume, statusTitle,
        statusDetail, audioCaption, audioState, progressCount; //footerHint
    private Button confirm, previous, listen, demo; //next
    private Button[] stepButtons;
    private Toggle[] checks;
    private readonly bool[] completed = new bool[8];
    private int currentStep;
    private int highestUnlockedStep;
    private bool uiReady;

    private void Awake()
    {
        if (document == null) document = GetComponent<UIDocument>();
        if (plateAnimator == null)
        {
            var plate = GameObject.Find("pcr_plate_setup_animation");
            if (plate != null) plateAnimator = plate.GetComponentInChildren<Animator>(true);
        }
        if (narrationSource == null) narrationSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (EnsureUIReady()) Refresh();
    }

    private void Update()
    {
        if (document == null) document = GetComponent<UIDocument>();
        if (document != null && uiReady && document.rootVisualElement != root)
            uiReady = false;
        if (!uiReady && EnsureUIReady()) Refresh();
    }

    private bool EnsureUIReady()
    {
        if (uiReady) return true;
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null || !document.enabled) return false;
        var candidate = document.rootVisualElement;
        if (candidate == null || candidate.panel == null) return false;
        root = candidate;
        CacheUI();
        if (kicker == null || title == null || summary == null || badge == null || task == null || instruction == null || audioCaption == null || progressCount == null || progressFill == null || volumeChip == null || volume == null || checklist == null || actionStatus == null || statusTitle == null || statusDetail == null || confirm == null || previous == null || audioState == null ) return false; //|| footerHint == null next == null ||
        BindUI();
        uiReady = true;
        return true;
    }

    private void OnDestroy()
    {
        if (plateLayoutRuntimeSettings != null) Destroy(plateLayoutRuntimeSettings);
    }

    private void OnDisable()
    {
        if (narrationSource != null) narrationSource.Stop();
    }

    private void CacheUI()
    {
        progressFill = root.Q<VisualElement>("progress-fill");
        actionStatus = root.Q<VisualElement>("action-status");
        volumeChip = root.Q<VisualElement>("volume-chip");
        checklist = root.Q<VisualElement>("checklist");
        kicker = root.Q<Label>("step-kicker");
        title = root.Q<Label>("step-title");
        summary = root.Q<Label>("step-summary");
        badge = root.Q<Label>("badge-number");
        task = root.Q<Label>("task-title");
        instruction = root.Q<Label>("task-copy");
        volume = root.Q<Label>("volume-label");
        statusTitle = root.Q<Label>("status-title");
        statusDetail = root.Q<Label>("status-detail");
        audioCaption = root.Q<Label>("audio-caption");
        audioState = root.Q<Label>("audio-state");
        progressCount = root.Q<Label>("progress-count");
        // footerHint = root.Q<Label>("footer-hint");
        confirm = root.Q<Button>("action-done");
        // next = root.Q<Button>("next-step");
        previous = root.Q<Button>("previous-step");
        listen = root.Q<Button>("replay-audio");
        demo = root.Q<Button>("play-demo");
        var palette = root.Q<VisualElement>("layout-palette");
        if (palette == null)
        {
            palette = new VisualElement { name = "layout-palette" };
            palette.style.flexDirection = FlexDirection.Row;
            palette.style.flexWrap = Wrap.Wrap;
            foreach (var identity in new[] { "S1", "S2", "S3", "S4", "S5", "PTC", "NTC" })
            {
                var value = identity;
                var button = new Button(() => SelectLayoutIdentity(value)) { name = "layout-" + value, text = value };
                button.AddToClassList("listen");
                button.style.fontSize = 20;
                button.style.minWidth = 90;
                button.style.minHeight = 56;
                palette.Add(button);
            }
            instruction.parent.Insert(instruction.parent.IndexOf(instruction) + 1, palette);
        }
        stepButtons = new Button[8];
        for (int i = 0; i < 8; i++)
        {
            int index = i;
            stepButtons[i] = root.Q<Button>($"step-{i + 1}");
            if (stepButtons[i] != null) stepButtons[i].clicked += () => SelectStep(index);
        }
        checks = new[] {
            root.Q<Toggle>("check-well"), root.Q<Toggle>("check-sample"),
            root.Q<Toggle>("check-volume"), root.Q<Toggle>("check-controls")
        };
    }

    private void BindUI()
    {
        if (confirm != null) confirm.clicked += ConfirmAction;
        // if (next != null) next.clicked += NextStep;
        if (previous != null) previous.clicked += PreviousStep;
        if (listen != null) listen.clicked += PlayNarration;
        if (demo != null) demo.clicked += PlayDemonstration;
        foreach (var check in checks)
            if (check != null) check.RegisterValueChangedCallback(_ => UpdateConfirmAvailability());
    }

    public void ShowStep(int oneBasedStep) => SelectStep(oneBasedStep - 1);
    public void ConfirmGuideAction() => ConfirmAction();
    public void AdvanceGuideStep() => NextStep();
    public void ReturnToPreviousStep() => PreviousStep();
    public void ReplayGuideNarration() => PlayNarration();
    public void PlayGuideDemonstration() => PlayDemonstration();
    public int CurrentStepNumber => currentStep + 1;
    public void SetStepActionFeedback(string message) { if (statusDetail != null) statusDetail.text = message; }
    public void ToggleChecklistItem(int index) { if (checks != null && index >= 0 && index < checks.Length && checks[index] != null) checks[index].value = !checks[index].value; }

    private void SelectStep(int index)
    {
        if (index < 0 || index > highestUnlockedStep || (plateLayoutSession && index != 1)) return;
        currentStep = index;
        if (narrationSource != null) narrationSource.Stop();
        Refresh();
    }

    private void NextStep()
    {
        if (plateLayoutSession && currentStep == 1)
        {
            SetPlateLayoutFeedback(completed[1] ? "Stage 2 complete: five DNA samples in duplicate plus PTC and NTC. Your plate layout is ready." : "Complete all 12 sample/control assignments first.", false);
            return;
        }
        if (currentStep >= 7 || !completed[currentStep]) return;
        currentStep++;
        highestUnlockedStep = Mathf.Max(highestUnlockedStep, currentStep);
        if (narrationSource != null) narrationSource.Stop();
        Refresh();
    }

    private void PreviousStep()
    {
        if (plateLayoutSession || currentStep <= 0) return;
        currentStep--;
        if (narrationSource != null) narrationSource.Stop();
        Refresh();
    }

    private void ConfirmAction()
    {
        if (currentStep == 1)
        {
            if (plateLayout == null || !plateLayout.IsComplete)
            { SetPlateLayoutFeedback("Assign all 12 sample/control wells before confirming.", true); return; }
            NextStep();
            return;
        }
        if (currentStep == 6 && !AllChecksComplete())
        {
            if (statusDetail != null) statusDetail.text = "Check all four checklist items before confirming.";
            return;
        }
        CompleteCurrentAction();
        if (currentStep < 8)
            NextStep();
    }


    /// <summary>Call this from an XR interaction after validating the learner's action.</summary>
    public void CompleteCurrentAction()
    {
        if (currentStep == 1 && (plateLayout == null || !plateLayout.IsComplete)) return;
        completed[currentStep] = true;
        highestUnlockedStep = Mathf.Max(highestUnlockedStep, currentStep);
        Refresh();
    }

    private void PlayNarration()
    {
        if (narrationSource == null || narrationClips == null ||
            narrationClips.Length <= currentStep || narrationClips[currentStep] == null)
        {
            if (audioState != null) audioState.text = "Narration clip not assigned";
            return;
        }
        narrationSource.Stop();
        narrationSource.clip = narrationClips[currentStep];
        narrationSource.Play();
        if (audioState != null) audioState.text = $"Now playing · {narrationSource.clip.length:0} sec";
    }

    private void PlayDemonstration()
    {
        if (plateLayoutSession) return;
        if (plateAnimator != null)
            plateAnimator.Play("Base Layer." + StateNames[currentStep], 0, 0f);
        else if (audioState != null)
            audioState.text = "Plate Animator is not assigned";
    }

    private bool AllChecksComplete()
    {
        if (checks == null || checks.Length != 4) return false;
        foreach (var check in checks)
            if (check == null || !check.value) return false;
        return true;
    }

    private void UpdateConfirmAvailability()
    {
        if (confirm != null && !completed[currentStep])
            confirm.SetEnabled(currentStep == 1 ? plateLayout != null && plateLayout.IsComplete : currentStep != 6 || AllChecksComplete());
    }

    private void Refresh()
    {
        if (document == null) document = GetComponent<UIDocument>();
        if (document != null && uiReady && document.rootVisualElement != root)
            uiReady = false;
        if (!EnsureUIReady()) return;
        int n = currentStep + 1;
        kicker.text = $"STAGE {n:00}  /  {Categories[currentStep]}";
        title.text = Titles[currentStep];
        if (plateLayoutSession)
        {
            root.Q<VisualElement>(className: "rail").style.display = DisplayStyle.None;
            var card = root.Q<VisualElement>(className: "card");
            card.style.width = Length.Percent(100);
            title.style.fontSize = 30;
            title.style.whiteSpace = WhiteSpace.Normal;
            summary.style.fontSize = 18;
            task.style.fontSize = 22;
            task.style.whiteSpace = WhiteSpace.Normal;
            instruction.style.fontSize = 18;
            statusDetail.style.fontSize = 18;
            statusTitle.style.fontSize = 16;
            root.Q<VisualElement>(className: "status-copy").style.flexShrink = 1;
            foreach (var className in new[] { "audio", "demo-row", "footer" })
            {
                var item = root.Q<VisualElement>(className: className);
                if (item != null) item.style.display = DisplayStyle.None;
            }
        }
        var palette = root.Q<VisualElement>("layout-palette");
        if (palette != null) palette.style.display = currentStep == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        summary.text = Summaries[currentStep];
        badge.text = n.ToString("00");
        task.text = Tasks[currentStep];
        instruction.text = Instructions[currentStep];
        audioCaption.text = NarrationText[currentStep];
        progressCount.text = $"{n:00} / 08";
        progressFill.style.width = Length.Percent(n * 12.5f);
        volumeChip.EnableInClassList("hidden", currentStep != 2 && currentStep != 4);
        volume.text = currentStep == 2 ? "20 µL" : "5 µL";
        checklist.EnableInClassList("hidden", currentStep != 6);
        actionStatus.EnableInClassList("complete", completed[currentStep]);
        statusTitle.text = completed[currentStep] ? (currentStep == 7 ? "Guide complete" : "Task confirmed") : "Ready when you are";
        statusDetail.text = completed[currentStep]
            ? "This stage is complete. Continue when you’re ready."
            : (currentStep == 6 ? "Check all four checklist items, then confirm to continue." : "Perform the task, then confirm it to unlock the next stage.");
        confirm.text = completed[currentStep] ? (currentStep == 7 ? "Finished ✓" : "Completed ✓") : "Confirm task";
        confirm.SetEnabled(currentStep == 1 ? plateLayout != null && plateLayout.IsComplete : !completed[currentStep] && (currentStep != 6 || AllChecksComplete()));
        previous.SetEnabled(!plateLayoutSession && currentStep > 0);
        if (demo != null) demo.SetEnabled(!plateLayoutSession);
        // next.SetEnabled(currentStep < 7 && completed[currentStep]);
        // footerHint.text = currentStep == 7 ? (completed[currentStep] ? "Guide complete · Plate ready for handoff" : "Final stage · confirm to finish")
        //     : (completed[currentStep] ? "Stage complete" : "Confirm the task to continue");
        audioState.text = narrationClips != null && narrationClips.Length > currentStep &&
            narrationClips[currentStep] != null
            ? $"Narration ready · {narrationClips[currentStep].length:0} sec"
            : "Narration clip not assigned";

        for (int i = 0; i < stepButtons.Length; i++)
        {
            if (stepButtons[i] == null) continue;
            stepButtons[i].SetEnabled(i <= highestUnlockedStep && (!plateLayoutSession || i == 1));
            stepButtons[i].style.display = plateLayoutSession && i != 1 ? DisplayStyle.None : DisplayStyle.Flex;
            stepButtons[i].EnableInClassList("current", i == currentStep);
            var mark = root.Q<Label>($"step-mark-{i + 1}");
            if (mark != null) mark.text = completed[i] ? "✓" : (i == currentStep ? "●" : "");
        }
    }
}