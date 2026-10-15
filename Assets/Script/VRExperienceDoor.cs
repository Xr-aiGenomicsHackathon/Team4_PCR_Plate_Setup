using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.UIElements;
using UIDocument = UnityEngine.UIElements.UIDocument;
using UITKButton = UnityEngine.UIElements.Button;
using CanvasButton = UnityEngine.UI.Button;
using CanvasImage = UnityEngine.UI.Image;

/// <summary>
/// Binds the doorway's UXML controls to the lab session and live camera preview.
/// Layout and styling live in VRExperienceDoor.uxml and VRExperienceDoor.uss.
/// </summary>
public sealed class VRExperienceDoor : MonoBehaviour
{
    [SerializeField] private UIDocument entryDocument;
    [SerializeField] private UIDocument experienceGuide;
    [SerializeField] private Transform previewTarget;
    [SerializeField] private Vector3 previewTargetPosition = new Vector3(0f, 1.35f, 0.2f);
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture previewTexture;
    [SerializeField] private Canvas hitCanvas;
    [SerializeField] private RectTransform hitTargetRoot;
    [SerializeField] private GameObject Door;

    [Header("Introduction and entry sequence")]
    [SerializeField] private AudioSource introSource;
    [SerializeField] private AudioClip introAudio;
    [SerializeField, Range(0f, 1f)] private float narrationVolume = 0.65f;
    [SerializeField] private GameObject equipmentWorldPanel;
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private AnimationClip doorOpenClip;
    [SerializeField] private AnimationClip doorCloseClip;
    [SerializeField] private XROrigin playerOrigin;
    [SerializeField] private Transform equipmentArrival;
    [SerializeField] private Behaviour[] locomotionProviders;
    [SerializeField, Min(0.1f)] private float uiExitSeconds = 0.75f;
    [SerializeField] private float uiExitDistance = 2f;
    private readonly List<Behaviour> suspendedLocomotion = new List<Behaviour>();
    private UITKButton startButton;
    private Label introStatus;
    private bool introComplete;
    public bool IntroComplete => introComplete;
    public bool HasStarted => started;

    private readonly Dictionary<string, RectTransform> targets = new Dictionary<string, RectTransform>();
    private VisualElement root;
    private VisualElement previewScreen;
    private Label previewHint;
    private UITKButton previewToggle;
    private bool previewPlaying = true;
    private bool started;
    private bool ownsPreviewTexture;

    [SerializeField] private float orbitDegreesPerSecond = 10f;
    [SerializeField] private float orbitRadius = 4.7f;
    [SerializeField] private float orbitHeight = 0.95f;
    [SerializeField] private float orbitDrift = 0.16f;
    private float orbitStartAngle;
    private float orbitElapsed;




    private void Awake()
    {
        if (entryDocument == null) entryDocument = GetComponent<UIDocument>();
        if (experienceGuide == null)
        {
            foreach (var candidate in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if (candidate != entryDocument) { experienceGuide = candidate; break; }
        }
        if (hitCanvas == null)
        {
            var canvasObject = GameObject.Find("Door Entry World Panel");
            if (canvasObject != null) hitCanvas = canvasObject.GetComponent<Canvas>();
        }
        if (hitTargetRoot == null && hitCanvas != null)
        {
            var hitRootObject = hitCanvas.transform.Find("Hit Targets");
            if (hitRootObject != null) hitTargetRoot = hitRootObject.GetComponent<RectTransform>();
        }
        if (previewCamera == null) CreatePreviewCamera();
        if (doorAnimator != null && doorAnimator.HasState(0, Animator.StringToHash("DoorClosed")))
        {
            doorAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            doorAnimator.Play("DoorClosed", 0, 0f);
            doorAnimator.Update(0f);
        }
    }

    private void Start()
    {
        if (experienceGuide != null) experienceGuide.enabled = false;
        if (equipmentWorldPanel != null) equipmentWorldPanel.SetActive(false);
        SuspendLocomotion();
        if (entryDocument == null) return;

        root = entryDocument.rootVisualElement;
        previewScreen = root.Q<VisualElement>("preview-screen");
        previewHint = root.Q<Label>("preview-hint");
        previewToggle = root.Q<UITKButton>("toggle-preview");
        if (previewTexture != null && previewScreen != null)
            previewScreen.style.backgroundImage = Background.FromRenderTexture(previewTexture);

        startButton = root.Q<UITKButton>("start-session");
        introStatus = new Label { name = "intro-status" };
        introStatus.AddToClassList("control-hint");
        root.Q<VisualElement>(className: "intro-column")?.Add(introStatus);
        SetIntroReady(false);
        if (startButton != null) startButton.clicked += StartExperience;
        if (previewToggle != null) previewToggle.clicked += TogglePreview;
        StartCoroutineRefresh();
        StartCoroutine(PlayIntroduction());
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(RefreshHitTargets));
        StopAllCoroutines();
        if (introSource != null) introSource.Stop();
        RestoreLocomotion();
        if (startButton != null) startButton.clicked -= StartExperience;
        if (previewToggle != null) previewToggle.clicked -= TogglePreview;
        if (previewCamera != null) previewCamera.enabled = false;
    }

    private void CreatePreviewCamera()
    {
        if (previewTexture == null)
        {
            previewTexture = new RenderTexture(1024, 576, 16, RenderTextureFormat.ARGB32)
            {
                name = "Doorway live lab preview",
                useMipMap = false,
                autoGenerateMips = false
            };
            previewTexture.Create();
            ownsPreviewTexture = true;
        }
        var cameraObject = new GameObject("Doorway Preview Camera");
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.targetTexture = previewTexture;
        previewCamera.fieldOfView = 54f;
        previewCamera.nearClipPlane = 0.1f;
        previewCamera.farClipPlane = 45f;
        previewCamera.depth = -5f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.71f, 0.82f, 0.80f);
        cameraObject.transform.position = new Vector3(2.55f, 1.75f, -3.45f);
        Vector3 initialOffset = cameraObject.transform.position - (previewTarget != null ? previewTarget.position : previewTargetPosition);
        orbitStartAngle = Mathf.Atan2(initialOffset.x, initialOffset.z) * Mathf.Rad2Deg;
        cameraObject.transform.LookAt(previewTarget != null ? previewTarget.position : previewTargetPosition);
    }

    private void StartCoroutineRefresh()
    {
        InvokeRepeating(nameof(RefreshHitTargets), 0.15f, 0.15f);
    }

    private void LateUpdate()
    {
        // Create the transparent hit targets as soon as the UI Toolkit layout is ready.
        if (!started) RefreshHitTargets();
        UpdatePreviewOrbit();
    }

    private void UpdatePreviewOrbit()
    {
        if (!previewPlaying || previewCamera == null || !previewCamera.enabled) return;

        orbitElapsed += Time.deltaTime;
        Vector3 center = previewTarget != null ? previewTarget.position : previewTargetPosition;
        float angle = (orbitStartAngle + orbitDegreesPerSecond * orbitElapsed) * Mathf.Deg2Rad;
        float radius = orbitRadius + Mathf.Sin(orbitElapsed * 0.22f) * orbitDrift;
        float height = orbitHeight + Mathf.Sin(orbitElapsed * 0.16f) * orbitDrift * 0.45f;
        previewCamera.transform.position = center + new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
        previewCamera.transform.LookAt(center + Vector3.up * 0.12f);
    }

    private void RefreshHitTargets()
    {
        if (started || root == null || hitTargetRoot == null || hitCanvas == null) return;
        if (root.panel == null || float.IsNaN(root.layout.width) || float.IsNaN(root.layout.height) || float.IsInfinity(root.layout.width) || float.IsInfinity(root.layout.height) || root.layout.width < 10f || root.layout.height < 10f) return;
        AddOrUpdateTarget("start-session", StartExperience);
        AddOrUpdateTarget("toggle-preview", TogglePreview);
    }

    private void AddOrUpdateTarget(string elementName, UnityEngine.Events.UnityAction action)
    {
        var element = root.Q<VisualElement>(elementName);
        if (element == null) return;
        var bounds = element.worldBound;
        if (float.IsNaN(bounds.width) || float.IsNaN(bounds.height) || float.IsInfinity(bounds.width) || float.IsInfinity(bounds.height)) return;
        if (!targets.TryGetValue(elementName, out var rect))
        {
            if (bounds.width <= 1f || bounds.height <= 1f) return;
            var go = new GameObject("Hit " + elementName, typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasImage), typeof(CanvasButton));
            go.transform.SetParent(hitTargetRoot, false);
            rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<CanvasImage>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            image.raycastTarget = true;
            var button = go.GetComponent<CanvasButton>();
            button.transition = Selectable.Transition.None;
            targets[elementName] = rect;
        }

        var button2 = rect.GetComponent<CanvasButton>();
        if (button2 != null) { button2.onClick.RemoveListener(action); button2.onClick.AddListener(action); button2.interactable = element.enabledInHierarchy && !started; }

        var visible = bounds.width > 1f && bounds.height > 1f;
        rect.gameObject.SetActive(visible);
        if (!visible) return;
        rect.sizeDelta = bounds.size;
        rect.anchoredPosition = new Vector2(bounds.center.x - root.layout.width * 0.5f,
                                            root.layout.height * 0.5f - bounds.center.y);
    }

    private void TogglePreview()
    {
        previewPlaying = !previewPlaying;
        if (previewCamera != null) previewCamera.enabled = previewPlaying;
        if (previewHint != null)
            previewHint.text = previewPlaying ? "See where your team will learn together." : "Preview paused. Play it again when you’re ready.";
        if (previewToggle != null) previewToggle.text = previewPlaying ? "Ⅱ  PAUSE" : "▶  PLAY";
    }

    private IEnumerator PlayIntroduction()
    {
        if (introAudio == null)
        {
            SetIntroReady(true);
            yield break;
        }
        if (introSource == null) introSource = gameObject.AddComponent<AudioSource>();
        introSource.playOnAwake = false;
        introSource.loop = false;
        introSource.spatialBlend = 0f;
        introSource.pitch = 1f;
        introSource.volume = narrationVolume;
        introSource.dopplerLevel = 0f;
        introSource.reverbZoneMix = 0f;
        var speechFilter = introSource.GetComponent<AudioHighPassFilter>();
        if (speechFilter == null) speechFilter = introSource.gameObject.AddComponent<AudioHighPassFilter>();
        speechFilter.cutoffFrequency = 120f;
        speechFilter.highpassResonanceQ = 1f;
        introSource.clip = introAudio;
        introStatus.text = "Listen to the introduction…";
        if (introAudio.loadState == AudioDataLoadState.Unloaded) introAudio.LoadAudioData();
        while (introAudio.loadState == AudioDataLoadState.Loading) yield return null;
        if (introAudio.loadState == AudioDataLoadState.Failed)
        {
            introStatus.text = "The introduction could not load. Please restart the experience.";
            yield break;
        }
        introSource.Play();
        yield return null;
        while (introSource.isPlaying || AudioListener.pause) yield return null;
        SetIntroReady(true);
    }

    private void SetIntroReady(bool ready)
    {
        introComplete = ready;
        startButton?.SetEnabled(!started);
        if (introStatus != null)
            introStatus.text = ready ? "Ready when you are. Select Start Experience." : "Listen to the introduction, or select Start Experience to continue.";
        RefreshHitTargets();
    }

    private void SuspendLocomotion()
    {
        if (locomotionProviders == null) return;
        foreach (var provider in locomotionProviders)
            if (provider != null && provider.enabled)
            { suspendedLocomotion.Add(provider); provider.enabled = false; }
    }

    private void RestoreLocomotion()
    {
        foreach (var provider in suspendedLocomotion)
            if (provider != null) provider.enabled = true;
        suspendedLocomotion.Clear();
    }

    private void StartExperience()
    {
        if (started) return;
        if (doorAnimator == null || doorOpenClip == null || doorCloseClip == null || playerOrigin == null || equipmentArrival == null)
        {
            if (introStatus != null) introStatus.text = "Entry setup is incomplete. Check the scene references.";
            Debug.LogError("Entry sequence needs the door animation, XR Origin, and equipment arrival marker.", this);
            return;
        }
        if (!doorAnimator.HasState(0, Animator.StringToHash("DoorOpen")) ||
            !doorAnimator.HasState(0, Animator.StringToHash("DoorClosing")))
        {
            Debug.LogError("The entry door needs the LabEntryDoor Animator controller.", this);
            return;
        }
        started = true;
        if (introSource != null) introSource.Stop();
        startButton?.SetEnabled(false);
        CancelInvoke(nameof(RefreshHitTargets));
        foreach (var target in targets.Values)
            if (target != null) target.gameObject.SetActive(false);
        StartCoroutine(EnterEquipmentArea());
    }

    private IEnumerator EnterEquipmentArea()
    {
        // Move and fade the world-space panel, while its texture remains visible.
        if (hitCanvas != null)
        {
            var panel = hitCanvas.transform;
            var from = panel.position;
            var to = from + panel.up * uiExitDistance + panel.forward * 0.5f;
            var group = hitCanvas.GetComponent<CanvasGroup>();
            if (group == null) group = hitCanvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            float elapsed = 0f;
            while (elapsed < uiExitSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / uiExitSeconds));
                panel.position = Vector3.Lerp(from, to, fraction);
                group.alpha = 1f - fraction;
                yield return null;
            }
            hitCanvas.gameObject.SetActive(false);
        }
        if (entryDocument != null) entryDocument.enabled = false;
        previewPlaying = false;
        if (previewCamera != null) previewCamera.enabled = false;

        doorAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        doorAnimator.Play("DoorOpen", 0, 0f);
        yield return null;
        // Hold the final pose before moving the player.
        while (doorAnimator.GetCurrentAnimatorStateInfo(0).IsName("DoorOpen") &&
               doorAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        var character = playerOrigin.GetComponent<CharacterController>();
        bool characterEnabled = character != null && character.enabled;
        if (characterEnabled) character.enabled = false;
        playerOrigin.MatchOriginUpCameraForward(Vector3.up, equipmentArrival.forward);
        var head = playerOrigin.Camera.transform;
        var arrival = equipmentArrival.position;
        arrival.y += head.position.y - playerOrigin.transform.position.y;
        playerOrigin.MoveCameraToWorldLocation(arrival);
        if (characterEnabled) character.enabled = true;

        // Close only after the XR player has arrived safely inside.
        doorAnimator.Play("DoorClosing", 0, 0f);
        yield return null;
        while (doorAnimator.GetCurrentAnimatorStateInfo(0).IsName("DoorClosing") &&
               doorAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;
        doorAnimator.Play("DoorClosed", 0, 0f);

        if (equipmentWorldPanel != null) equipmentWorldPanel.SetActive(true);
        if (experienceGuide != null) experienceGuide.enabled = true;
        RestoreLocomotion();
    }

    private void OnDestroy()
    {
        if (previewCamera != null) Destroy(previewCamera.gameObject);
        if (previewTexture != null && ownsPreviewTexture)
        {
            previewTexture.Release();
            Destroy(previewTexture);
        }
    }
}