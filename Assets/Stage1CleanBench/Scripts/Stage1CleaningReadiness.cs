using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Validates the three-task Stage 1 interaction; this does not simulate decontamination.</summary>
public sealed class Stage1CleaningReadiness : MonoBehaviour
{
    [SerializeField] private XRSimpleInteractable slideInteraction;
    [SerializeField] private Animator slideAnimator;
    [SerializeField] private string openState = "open_laminar_animation";
    [SerializeField] private XRGrabInteractable cleaner;
    [SerializeField] private XRGrabInteractable spray;
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;
    private bool openingRequested;
    private bool completed;
    public event Action Completed;
    public bool IsComplete => completed;

    private void Start()
    {
        if (slideAnimator != null && slideAnimator.isActiveAndEnabled)
        {
            slideAnimator.SetBool("open", false);
            slideAnimator.Play("close_laminar_animaion", 0, 1f);
        }
    }

    private void OnEnable()
    {
        if (slideInteraction != null) slideInteraction.selectEntered.AddListener(OpenSlide);
    }

    private void OnDisable()
    {
        if (slideInteraction != null) slideInteraction.selectEntered.RemoveListener(OpenSlide);
    }

    private void OpenSlide(SelectEnterEventArgs args)
    {
        if (openingRequested || slideAnimator == null || !slideAnimator.isActiveAndEnabled) return;
        if (!slideAnimator.HasState(0, Animator.StringToHash(openState)))
        {
            Debug.LogWarning("Stage 1 opening animation state is missing.", this);
            return;
        }
        openingRequested = true;
        slideAnimator.SetBool("open", true);
        slideAnimator.Play(openState, 0, 0f);
    }

    private void Update()
    {
        if (completed || !openingRequested || slideAnimator == null ||
            !slideAnimator.isActiveAndEnabled || slideAnimator.IsInTransition(0)) return;
        var state = slideAnimator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName(openState) || state.normalizedTime < 1f) return;
        int cleanerHand = HeldHand(cleaner);
        int sprayHand = HeldHand(spray);
        if (!OppositeHands(cleanerHand, sprayHand)) return;
        completed = true;
        Completed?.Invoke();
    }

    // Reject ambiguous multi-hand grabs, untracked interactors, and missing references.
    private int HeldHand(XRGrabInteractable item)
    {
        if (item == null || item.interactorsSelecting.Count != 1 ||
            leftController == null || rightController == null || leftController == rightController) return 0;
        var hand = item.interactorsSelecting[0].transform;
        bool left = hand == leftController || hand.IsChildOf(leftController);
        bool right = hand == rightController || hand.IsChildOf(rightController);
        return left == right ? 0 : (left ? 1 : 2);
    }

    public static bool OppositeHands(int cleanerHand, int sprayHand)
    {
        return (cleanerHand == 1 && sprayHand == 2) || (cleanerHand == 2 && sprayHand == 1);
    }
}
