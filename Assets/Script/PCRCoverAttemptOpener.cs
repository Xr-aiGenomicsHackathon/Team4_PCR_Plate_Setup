using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public sealed class PCRCoverAttemptOpener : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable coverInteractable;
    [SerializeField] private Animator plateAnimator;

    private bool animationStarted;

    private void Awake()
    {
        if (coverInteractable == null)
            coverInteractable = GetComponent<XRGrabInteractable>();
        if (plateAnimator == null)
            plateAnimator = GetComponentInParent<Animator>();
    }

    private void OnEnable()
    {
        if (coverInteractable != null)
            coverInteractable.selectEntered.AddListener(OnCoverGrabAttempted);
    }

    private void OnDisable()
    {
        if (coverInteractable != null)
            coverInteractable.selectEntered.RemoveListener(OnCoverGrabAttempted);
    }

    private void OnCoverGrabAttempted(SelectEnterEventArgs args)
    {
        if (animationStarted || plateAnimator == null)
            return;

        animationStarted = true;

  
        if (coverInteractable != null)
            coverInteractable.enabled = false;

        plateAnimator.Play("Base Layer.01_Meet_the_Plate", 0, 0f);
    }
}