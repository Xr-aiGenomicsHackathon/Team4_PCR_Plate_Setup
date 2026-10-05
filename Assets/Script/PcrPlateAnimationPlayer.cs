using UnityEngine;

public class PcrPlateAnimationPlayer : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private static readonly string[] States =
    {
        "01_Meet_the_Plate",
        "02_Make_the_Map",
        "03_Master_Mix",
        "04_Controls",
        "05_Samples",
        "06_Cross_Check_Duplicates",
        "07_Check_Plate",
        "08_Seal"
    };

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    // Pass 1–8 to play the corresponding step from its beginning.
    public void PlayStep(int stepNumber)
    {
        int index = stepNumber - 1;

        if (animator == null || index < 0 || index >= States.Length)
        {
            Debug.LogWarning($"Can't play PCR animation step {stepNumber}.");
            return;
        }

        animator.Play($"Base Layer.{States[index]}", 0, 0f);
    }
}