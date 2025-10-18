using UnityEngine; 
using UnityEngine.UI;

public class MarkController : MonoBehaviour
{
    [Header("Marks")]
    [SerializeField] private GameObject questionMark;  // ❓ clickable mark

    [Header("UI (Shelter Overview)")]
    [SerializeField] private GameObject canvasShelterOverview;  // Canvas_ShelterOverview
    [SerializeField] private GameObject panelShelterOverview;   // Panel_ShelterOverview
    [SerializeField] private Button closeButton;                // Close button inside panel

    //private bool _panelOpen = false;

    void Awake()
    {
        // Make sure everything starts clean
        if (canvasShelterOverview)
            canvasShelterOverview.SetActive(false);

        if (panelShelterOverview)
            panelShelterOverview.SetActive(false);

        if (questionMark)
            questionMark.SetActive(true);

        if (closeButton)
            closeButton.onClick.AddListener(ClosePanel);
    }

    public void OpenPanel()
    {
        if (canvasShelterOverview)
            canvasShelterOverview.SetActive(true);

        if (panelShelterOverview)
            panelShelterOverview.SetActive(true);

        //_panelOpen = true;
    }

    public void ClosePanel()
    {
        if (canvasShelterOverview)
            canvasShelterOverview.SetActive(false);

        if (panelShelterOverview)
            panelShelterOverview.SetActive(false);

        //_panelOpen = false;

        // ❓ Hide the question mark — no checkmark replacement
        if (questionMark)
            questionMark.SetActive(false);
    }
}
