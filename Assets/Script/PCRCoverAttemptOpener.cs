using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Plays the original first-grab plate animation. If the cover is grabbed while
/// away from the PCR plate, it animates back to its saved home pose.
/// </summary>
public sealed class PCRCoverAttemptOpener : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable coverInteractable;
    [SerializeField] private Animator plateAnimator;
    [SerializeField] private Collider coverCollider;
    [SerializeField] private Collider plateCollider;
    [SerializeField, Min(0.01f)] private float returnDuration = 0.45f;
    [SerializeField, Min(0f)] private float plateEdgeTolerance = 0.01f;
    [SerializeField, Min(0f)] private float heightToleranceBelowPlate = 0.08f;
    [SerializeField, Min(0f)] private float heightToleranceAbovePlate = 0.18f;

    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private Vector3 returnStartPosition;
    private Quaternion returnStartRotation;
    private float returnProgress;
    private bool initialAnimationStarted;
    private bool initialAnimationPlaying;
    private bool returningToPlate;
    private bool holdAtHome;

    private void Awake()
    {
        if (coverInteractable == null)
            coverInteractable = GetComponent<XRGrabInteractable>();
        if (coverCollider == null)
            coverCollider = GetComponent<Collider>();
        if (plateAnimator == null)
            plateAnimator = GetComponentInParent<Animator>();
        if (plateCollider == null)
        {
            var rack = transform.parent != null ? transform.parent.Find("PCR_Plate_Rack") : null;
            if (rack != null)
                plateCollider = rack.GetComponentInChildren<Collider>(true);
        }

        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        if (coverInteractable != null)
            coverInteractable.selectEntered.AddListener(OnCoverGrabbed);
    }

    private void OnDisable()
    {
        if (coverInteractable != null)
            coverInteractable.selectEntered.RemoveListener(OnCoverGrabbed);
        StopAllCoroutines();
        initialAnimationPlaying = false;
        returningToPlate = false;
        holdAtHome = false;
        if (coverInteractable != null && !coverInteractable.enabled)
            coverInteractable.enabled = true;
    }

    private void LateUpdate()
    {
        // The parent Animator animates this transform, so apply the return pose
        // after Animator evaluation while the cover is being returned or held.
        if (returningToPlate)
        {
            float eased = Mathf.SmoothStep(0f, 1f, returnProgress);
            transform.localPosition = Vector3.Lerp(returnStartPosition, homeLocalPosition, eased);
            transform.localRotation = Quaternion.Slerp(returnStartRotation, homeLocalRotation, eased);
        }
        else if (holdAtHome && (coverInteractable == null || !coverInteractable.isSelected))
        {
            ApplyHomePose();
        }
    }

    private void OnCoverGrabbed(SelectEnterEventArgs args)
    {
        if (initialAnimationPlaying || returningToPlate)
            return;

        holdAtHome = false;
        if (!IsDirectlyOverPlate())
        {
            StartCoroutine(ReturnToPlate());
            return;
        }

        if (!initialAnimationStarted && plateAnimator != null)
            StartCoroutine(PlayInitialAnimation());
    }

    private IEnumerator PlayInitialAnimation()
    {
        initialAnimationStarted = true;
        initialAnimationPlaying = true;
        if (coverInteractable != null)
            coverInteractable.enabled = false;

        plateAnimator.Play("Base Layer.01_Meet_the_Plate", 0, 0f);
        float duration = 1.96f;
        var introClip = plateAnimator.runtimeAnimatorController != null
            ? plateAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.Contains("01_Meet_the_Plate"))
            : null;
        if (introClip != null)
            duration = introClip.length;

        yield return new WaitForSecondsRealtime(duration);

        initialAnimationPlaying = false;
        if (coverInteractable != null)
            coverInteractable.enabled = true;
    }

    private IEnumerator ReturnToPlate()
    {
        if (coverCollider == null || plateCollider == null)
            yield break;

        if (coverInteractable != null)
            coverInteractable.enabled = false;

        returnStartPosition = transform.localPosition;
        returnStartRotation = transform.localRotation;
        returnProgress = 0f;
        returningToPlate = true;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, returnDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            returnProgress = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        returnProgress = 1f;
        ApplyHomePose();
        returningToPlate = false;
        holdAtHome = true;

        if (coverInteractable != null)
            coverInteractable.enabled = true;
    }

    private bool IsDirectlyOverPlate()
    {
        if (coverCollider == null || plateCollider == null)
            return false;

        var coverCenter = coverCollider.bounds.center;
        var plateBounds = plateCollider.bounds;
        float tolerance = plateEdgeTolerance;

        bool insideFootprint =
            coverCenter.x >= plateBounds.min.x - tolerance &&
            coverCenter.x <= plateBounds.max.x + tolerance &&
            coverCenter.z >= plateBounds.min.z - tolerance &&
            coverCenter.z <= plateBounds.max.z + tolerance;

        float heightAbovePlate = coverCenter.y - plateBounds.max.y;
        bool atPlateHeight =
            heightAbovePlate >= -heightToleranceBelowPlate &&
            heightAbovePlate <= heightToleranceAbovePlate;

        return insideFootprint && atPlateHeight;
    }

    private void ApplyHomePose()
    {
        transform.localPosition = homeLocalPosition;
        transform.localRotation = homeLocalRotation;
    }
}