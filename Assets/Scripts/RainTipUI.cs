using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RainTipUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] CanvasGroup group;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Button goToSheltersButton;

    [TextArea]
    [SerializeField] string message =
        @"
        It’s raining! Quickly, press the button below to start the Shelter Navigation GPS! 
        or
        Place a shed in each tent!
        ";

    [TextArea]
    [SerializeField] string enteredShelterMessage =
        "You entered a shelter. You must stay inside until the rain passes.";

    [TextArea]
    [SerializeField] string rainPassedMessage =
        "The rain has passed — you can get out of the shelter now.";

    [TextArea]
    [SerializeField] string alreadyInsideDuringRainMessage =
        "You are already in a shelter. You must stay inside until it is safe out there.";

    [Header("Panels to open when user accepts")]
    [SerializeField] GameObject canvas_MapControlCenter;
    [SerializeField] GameObject tentControlCenter_Panel;
    [SerializeField] GameObject canvas_Shelter;
    [SerializeField] GameObject shelter_Panel;

    [Header("Shelter navigation controller")]
    [SerializeField] ShelterRoutesPanel routesPanel;  // has OpenPanel() / StopNavigation()

    Coroutine fadeRoutine;

    void Reset()
    {
        group = GetComponent<CanvasGroup>();
        label = GetComponentInChildren<TextMeshProUGUI>(true);
        goToSheltersButton = GetComponentInChildren<Button>(true);
    }

    void Awake()
    {
        if (goToSheltersButton)
        {
            goToSheltersButton.onClick.RemoveAllListeners();
            goToSheltersButton.onClick.AddListener(OnGoToSheltersClicked);
        }
        SetVisible(false);
    }

    // ===== WeatherManager hooks =====
    public void ShowRainMessage()
    {
        StopFade();

    if (ShelterEntranceController.IsPlayerInsideAnyShelter)
    {
        // NEW BEHAVIOR: lock edges solid, user cannot leave until rain stops
        ShelterEntranceController.LockInsideAll();

        if (label) label.text = alreadyInsideDuringRainMessage;
        SetVisible(true);
        SetInteractable(true);
        if (goToSheltersButton) goToSheltersButton.gameObject.SetActive(false);
        return;
    }

        // If nav panel is already open and you're NOT inside, suppress prompt
        if (routesPanel && routesPanel.IsOpen)
            return;

        // Normal "start navigation" prompt
        if (label) label.text = message;
        SetVisible(true);
        SetInteractable(true);
        if (goToSheltersButton) goToSheltersButton.gameObject.SetActive(true);
    }

    /// Called right after the player crosses the entrance box.
    public void ShowShelterEnteredMessage()
    {
        StopFade();
        if (label) label.text = enteredShelterMessage;
        SetVisible(true);
        SetInteractable(true);
        if (goToSheltersButton) goToSheltersButton.gameObject.SetActive(false);
    }

    /// Called when rain stops: informs player they can leave now; hides the navigation button.
    public void ShowRainPassedMessage()
    {
        StopFade();
        if (label) label.text = rainPassedMessage;
        SetVisible(true);
        SetInteractable(true);
        if (goToSheltersButton) goToSheltersButton.gameObject.SetActive(false);
    }

    public void HideRainMessage()
    {
        StopFade();
        SetVisible(false);
        SetInteractable(false);
    }

    // WeatherManager convenience
    public void NotifyRainStopped()
    {
        // 1) Ask all shelters to process rain-stop logic.
        ShelterEntranceController.RainStoppedForAll();

        // 2) If user is inside any shelter, show the “rain passed” info; otherwise keep UI hidden.
        if (ShelterEntranceController.IsPlayerInsideAnyShelter)
            ShowRainPassedMessage();
        else
            HideRainMessage();
    }

    // ===== Button: Start Navigation =====
    void OnGoToSheltersClicked()
    {
        // Hide the entire message + button (inactive/ not visible)
        HideRainMessage();
        if (goToSheltersButton) goToSheltersButton.gameObject.SetActive(false);

        // continue with the usual flow
        InteractionLock.BlockInteractions = true;

        ActivateUpToCanvas(canvas_MapControlCenter);
        ActivateUpToCanvas(tentControlCenter_Panel);
        ActivateUpToCanvas(canvas_Shelter);
        ActivateUpToCanvas(shelter_Panel);

        if (tentControlCenter_Panel) tentControlCenter_Panel.SetActive(true);
        if (shelter_Panel) shelter_Panel.SetActive(true);

        if (routesPanel) routesPanel.OpenPanel();

        // Arm all shelters (enables BOX trigger, etc.)
        ShelterEntranceController.StartNavigationForAll();
    }

    public void HideShelterUIAndStopNavigation()
    {
        if (tentControlCenter_Panel) tentControlCenter_Panel.SetActive(false);
        if (shelter_Panel)           shelter_Panel.SetActive(false);
        if (canvas_MapControlCenter) canvas_MapControlCenter.SetActive(false);
        if (canvas_Shelter)          canvas_Shelter.SetActive(false);

        if (routesPanel) routesPanel.StopNavigation();

        InteractionLock.BlockInteractions = false;
        HideRainMessage();
    }

    static void ActivateUpToCanvas(GameObject go)
    {
        if (!go) return;
        var t = go.transform;
        while (t != null)
        {
            var g = t.gameObject;
            if (!g.activeSelf) g.SetActive(true);
            if (g.GetComponent<Canvas>()) break;
            t = t.parent;
        }
    }

    void SetVisible(bool on)
    {
        if (!group) return;
        gameObject.SetActive(true);
        group.alpha = on ? 1f : 0f;
        if (!on) gameObject.SetActive(false);
    }

    void SetInteractable(bool on)
    {
        if (!group) return;
        group.interactable = on;
        group.blocksRaycasts = on;
    }

    void StopFade()
    {
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
    }

    public void OnShelterClosed()
    {
        InteractionLock.BlockInteractions = false;
    }
}
