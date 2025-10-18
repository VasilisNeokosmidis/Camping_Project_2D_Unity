using UnityEngine;
using UnityEngine.UI;

public class RestaurantPanelUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject restaurantPanel;
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;

    void Awake()
    {
        if (closeButton)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    void OnCloseClicked()
    {
        if (restaurantPanel) restaurantPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
    }
}
