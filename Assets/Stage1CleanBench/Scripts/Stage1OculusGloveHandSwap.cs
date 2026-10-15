using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public sealed class Stage1OculusGloveHandSwap : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable gloves;
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;
    [SerializeField] private GameObject leftControllerVisual;
    [SerializeField] private GameObject rightControllerVisual;
    [SerializeField] private GameObject leftHandVisual;
    [SerializeField] private GameObject rightHandVisual;

    private Animator leftHandAnimator;
    private Animator rightHandAnimator;
    private InputAction leftGripAction;
    private InputAction leftGripButtonAction;
    private InputAction leftTriggerAction;
    private InputAction leftTriggerButtonAction;
    private InputAction rightGripAction;
    private InputAction rightGripButtonAction;
    private InputAction rightTriggerAction;
    private InputAction rightTriggerButtonAction;

    private void Awake()
    {
        if (gloves == null)
        {
            var gloveObject = GameObject.Find("PLACEHOLDER_ProtectiveGloves");
            if (gloveObject != null) gloves = gloveObject.GetComponent<XRGrabInteractable>();
        }

        ResolveController(ref leftController, "Left Controller");
        ResolveController(ref rightController, "Right Controller");
        if (leftController != null && leftControllerVisual == null)
            leftControllerVisual = FindChild(leftController, "Left Controller Visual");
        if (rightController != null && rightControllerVisual == null)
            rightControllerVisual = FindChild(rightController, "Right Controller Visual");
        if (leftController != null && leftHandVisual == null)
            leftHandVisual = FindChild(leftController, "Oculus Gloved Hand Visual");
        if (rightController != null && rightHandVisual == null)
            rightHandVisual = FindChild(rightController, "Oculus Gloved Hand Visual");

        leftHandAnimator = leftHandVisual != null ? leftHandVisual.GetComponentInChildren<Animator>(true) : null;
        rightHandAnimator = rightHandVisual != null ? rightHandVisual.GetComponentInChildren<Animator>(true) : null;

        ResolveControllerActions();
        PrepareAnimator(leftHandAnimator);
        PrepareAnimator(rightHandAnimator);

        if (gloves != null)
            gloves.selectEntered.AddListener(OnGlovesGrabbed);
        else
            Debug.LogWarning("Stage 1 glove hand swap could not find PLACEHOLDER_ProtectiveGloves.");
    }

    private void OnDestroy()
    {
        if (gloves != null)
            gloves.selectEntered.RemoveListener(OnGlovesGrabbed);
    }

    private void Update()
    {
        UpdateHandPose(XRNode.LeftHand, leftHandVisual, leftHandAnimator);
        UpdateHandPose(XRNode.RightHand, rightHandVisual, rightHandAnimator);
    }

    private void ResolveController(ref Transform controller, string objectName)
    {
        if (controller == null)
        {
            var found = GameObject.Find(objectName);
            if (found != null) controller = found.transform;
        }
    }

    private GameObject FindChild(Transform parent, string childName)
    {
        var found = parent.Find(childName);
        return found != null ? found.gameObject : null;
    }

    private void ResolveControllerActions()
    {
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            var leftGrip = asset.FindAction("XRI Left Interaction/Select Value", false);
            var leftGripButton = asset.FindAction("XRI Left Interaction/Select", false);
            var leftTrigger = asset.FindAction("XRI Left Interaction/Activate Value", false);
            var leftTriggerButton = asset.FindAction("XRI Left Interaction/Activate", false);
            var rightGrip = asset.FindAction("XRI Right Interaction/Select Value", false);
            var rightGripButton = asset.FindAction("XRI Right Interaction/Select", false);
            var rightTrigger = asset.FindAction("XRI Right Interaction/Activate Value", false);
            var rightTriggerButton = asset.FindAction("XRI Right Interaction/Activate", false);

            if (leftGrip == null || leftGripButton == null || leftTrigger == null || leftTriggerButton == null ||
                rightGrip == null || rightGripButton == null || rightTrigger == null || rightTriggerButton == null)
                continue;

            leftGripAction = leftGrip;
            leftGripButtonAction = leftGripButton;
            leftTriggerAction = leftTrigger;
            leftTriggerButtonAction = leftTriggerButton;
            rightGripAction = rightGrip;
            rightGripButtonAction = rightGripButton;
            rightTriggerAction = rightTrigger;
            rightTriggerButtonAction = rightTriggerButton;
            return;
        }

        Debug.LogWarning("Stage 1 glove hand swap could not find the XRI controller input actions.");
    }

    private void OnGlovesGrabbed(SelectEnterEventArgs args)
    {
        var interactorTransform = args.interactorObject.transform;
        if (BelongsTo(interactorTransform, leftController))
        {
            ActivateHand(leftControllerVisual, leftHandVisual, leftHandAnimator, XRNode.LeftHand);
            return;
        }

        if (BelongsTo(interactorTransform, rightController))
            ActivateHand(rightControllerVisual, rightHandVisual, rightHandAnimator, XRNode.RightHand);
    }

    private bool BelongsTo(Transform interactor, Transform controller)
    {
        return interactor != null && controller != null &&
               (interactor == controller || interactor.IsChildOf(controller));
    }

    private void ActivateHand(GameObject controllerVisual, GameObject handVisual, Animator handAnimator, XRNode handNode)
    {
        if (handVisual == null)
        {
            Debug.LogWarning("Stage 1 could not find the Oculus hand-physics visual for the glove hand.");
            return;
        }

        if (controllerVisual != null) controllerVisual.SetActive(false);
        handVisual.SetActive(true);
        PrepareAnimator(handAnimator);
        UpdateHandPose(handNode, handVisual, handAnimator);
    }

    private void PrepareAnimator(Animator animator)
    {
        if (animator == null) return;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.SetFloat("Grip", 0f);
        animator.SetFloat("Trigger", 0f);
    }

    private void UpdateHandPose(XRNode handNode, GameObject handVisual, Animator animator)
    {
        if (handVisual == null || !handVisual.activeInHierarchy || animator == null) return;

        bool left = handNode == XRNode.LeftHand;
        var gripValueAction = left ? leftGripAction : rightGripAction;
        var gripButtonAction = left ? leftGripButtonAction : rightGripButtonAction;
        var triggerValueAction = left ? leftTriggerAction : rightTriggerAction;
        var triggerButtonAction = left ? leftTriggerButtonAction : rightTriggerButtonAction;

        float grip = gripValueAction != null && gripValueAction.enabled
            ? gripValueAction.ReadValue<float>()
            : 0f;
        float trigger = triggerValueAction != null && triggerValueAction.enabled
            ? triggerValueAction.ReadValue<float>()
            : 0f;

        // Keep analog pressure when available, and use the paired controller button action
        // when a simulator or device only reports a digital press.
        if (gripButtonAction != null && gripButtonAction.enabled && gripButtonAction.IsPressed())
            grip = Mathf.Max(grip, 1f);
        if (triggerButtonAction != null && triggerButtonAction.enabled && triggerButtonAction.IsPressed())
            trigger = Mathf.Max(trigger, 1f);

        animator.SetFloat("Grip", grip);
        animator.SetFloat("Trigger", trigger);
    }
}