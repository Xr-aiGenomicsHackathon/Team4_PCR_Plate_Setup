using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Ensures each labeled plate-column interactable explicitly registers its trigger box.
/// Runs after the selector creates the interactables in Awake.
/// </summary>
public sealed class PCRWellColumnColliderBinder : MonoBehaviour
{
    private void Start()
    {
        foreach (var box in GetComponentsInChildren<BoxCollider>(true))
        {
            if (box.gameObject.name != "Collider") continue;

            var interactable = box.GetComponent<XRSimpleInteractable>();
            if (interactable == null) continue;

            interactable.colliders.Clear();
            interactable.colliders.Add(box);
        }
    }
}