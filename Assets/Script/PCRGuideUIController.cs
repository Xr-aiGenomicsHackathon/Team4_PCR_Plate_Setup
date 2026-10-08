using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// PCR walkthrough UI. XR interactables can call CompleteCurrentAction after
/// validating a learner's real placement or handling action.
/// </summary>
public sealed class PCRGuideUIController : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private Animator plateAnimator;
    [SerializeField] private AudioSource narrationSource;
    [SerializeField] private AudioClip[] narrationClips = new AudioClip[8];

    private static readonly string[] StateNames = {
        "01_Meet_the_Plate", "02_Make_the_Map", "03_Master_Mix", "04_Controls",
        "05_Samples", "06_Cross_Check_Duplicates", "07_Check_Plate", "08_Seal"
    };
    private static readonly string[] Titles = {
        "Meet the plate", "Make the map", "Master mix", "Controls",
        "Samples", "Cross-check & duplicates", "Check the plate", "Seal the plate"
    };
    private static readonly string[] Summaries = {
        "This is our PCR plate. It has 96 tiny wells. Each well has a job.",
        "Practice the plate coordinates first: follow the prompts to locate wells A1 through D6. Apply the confirmed control and sample allocation after this coordinate check.",
        "Touch the prepared master mix tray with the pipette, then contact at least two different sample columns.",
        "Touch both the PTC and NTC columns with the pipette.",
        "Touch any DNA tube with the pipette, then contact at least three different sample columns.",
        "Use the pipette to contact at least three different sample-duplicate columns marked with “-D” in the template.",
        "Review the well, sample, volume and control assignments before the plate is sealed.",
        "Lower the sealing film evenly across the plate, then prepare it for handoff to Team 5."
    };
    private static readonly string[] Tasks = {
        "Hover over three columns", "Locate wells A1–D6", "Touch master mix and two sample columns", "Contact PTC and NTC",
        "Contact DNA tube and 3 sample columns", "Contact 3 sample-duplicate columns", "Run the final checklist", "Seal and hand off"
    };
    private static readonly string[] Instructions = {
        "Hover over any three different labeled columns with the ray. Each visited column stays highlighted; after the third, continue to the next stage.",
        "Follow the prompts and use the XR ray to select wells A1 through D6 in row order. Each correct well is marked green.",
        "Touch the prepared master mix tray with the pipette, then touch at least two different sample columns. The cones in each contacted column will rise.",
        "Contact the PTC column and the NTC column with the pipette. Both columns must be touched to complete this stage.",
        "Touch any DNA tube with the pipette, then touch at least three different sample columns. Each contacted column's cones will rise.",
        "Contact at least three different sample-duplicate columns marked with “-D” in the template using the pipette.",
        "Confirm every checklist item below against the plate before sealing it.",
        "Position the sealing film, smooth it across the plate and confirm the plate is ready for handoff."
    };
    private static readonly string[] NarrationText = {
        "“This is our PCR plate. It has 96 tiny wells. Each well has a job.”",
        "“Before we add anything, we make a map. This tells us where the controls and samples belong.”",
        "“First, we add 20 microlitres of prepared master mix to the required reaction wells.”",
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
        if (index < 0 || index > highestUnlockedStep) return;
        currentStep = index;
        if (narrationSource != null) narrationSource.Stop();
        Refresh();
    }

    private void NextStep()
    {
        if (currentStep >= 7 || !completed[currentStep]) return;
        currentStep++;
        highestUnlockedStep = Mathf.Max(highestUnlockedStep, currentStep);
        if (narrationSource != null) narrationSource.Stop();
        Refresh();
    }

    private void PreviousStep()
    {
        if (currentStep <= 0) return;
        currentStep--;
        if (narrationSource != null) narrationSource.Stop();
        Refresh();
    }

    private void ConfirmAction()
    {
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
            confirm.SetEnabled(currentStep != 6 || AllChecksComplete());
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
        confirm.SetEnabled(!completed[currentStep] && (currentStep != 6 || AllChecksComplete()));
        previous.SetEnabled(currentStep > 0);
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
            stepButtons[i].SetEnabled(i <= highestUnlockedStep);
            stepButtons[i].EnableInClassList("current", i == currentStep);
            var mark = root.Q<Label>($"step-mark-{i + 1}");
            if (mark != null) mark.text = completed[i] ? "✓" : (i == currentStep ? "●" : "");
        }
    }
}