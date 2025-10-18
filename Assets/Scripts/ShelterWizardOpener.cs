using UnityEngine;

public class ShelterWizardOpener : MonoBehaviour
{
    [SerializeField] ShelterInstructionWizardSimple wizard;

    [Header("Panels")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject tentControlCenter_Panel;
    [SerializeField] GameObject canvas_MapControlCenter;

    public void OpenShelterInstructions()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);

        if (!wizard)
        {
#if UNITY_2023_1_OR_NEWER
            wizard = Object.FindFirstObjectByType<ShelterInstructionWizardSimple>(FindObjectsInactive.Include);
#else
            wizard = Object.FindObjectOfType<ShelterInstructionWizardSimple>(true);
#endif
        }

        if (wizard) wizard.Show();
        else Debug.LogError("[ShelterWizardOpener] No ShelterInstructionWizardSimple found/assigned.");
    }

    // ✅ Make Exit call the authoritative CloseAll()
    public void OnExitButton()
    {
        if (SignInteract.Instance)
        {
            SignInteract.Instance.CloseAll(); // handles unfreeze + unlock
        }
        else
        {
            // Fallback (in case SignInteract isn’t loaded)
            if (mainMenuPanel) mainMenuPanel.SetActive(false);
            if (tentControlCenter_Panel) tentControlCenter_Panel.SetActive(false);
            if (canvas_MapControlCenter) canvas_MapControlCenter.SetActive(false);
            InteractionLock.BlockInteractions = false; // ensure unlock
            Debug.LogWarning("[ShelterWizardOpener] SignInteract.Instance not found; closed panels and unlocked as fallback.");
        }
    }
}
