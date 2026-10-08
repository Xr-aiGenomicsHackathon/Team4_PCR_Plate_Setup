using UnityEngine;
using UnityEngine.UI;

/// <summary>Routes XR ray clicks from the world-space guide panel to the runtime guide UI.
/// </summary>
public sealed class PCRGuideWorldPanelButtonBridge : MonoBehaviour
{
    [SerializeField] private PCRGuideUIController guide;
    private bool bound;

    private void Start() => BindButtons();

    private void BindButtons()
    {
        if (bound) return;
        if (guide == null) guide = FindFirstObjectByType<PCRGuideUIController>();
        if (guide == null) return;

        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            switch (button.name)
            {
                case "Hit action-done": button.onClick.AddListener(guide.ConfirmGuideAction); break;
                case "Hit next-step": button.onClick.AddListener(guide.AdvanceGuideStep); break;
                case "Hit previous-step": button.onClick.AddListener(guide.ReturnToPreviousStep); break;
                case "Hit replay-audio": button.onClick.AddListener(guide.ReplayGuideNarration); break;
                case "Hit play-demo": button.onClick.AddListener(guide.PlayGuideDemonstration); break;
                default:
                    if (button.name.StartsWith("Hit step-") && int.TryParse(button.name.Substring(9), out var step))
                        button.onClick.AddListener(() => guide.ShowStep(step));
                    break;
            }
        }
        bound = true;
    }
}
