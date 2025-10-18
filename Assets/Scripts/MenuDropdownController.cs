using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MenuDropdownController : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] TMP_Dropdown dropdown;

    [SerializeField] GameObject defaultPanel;
    [SerializeField] GameObject breakfastPanel;
    [SerializeField] GameObject snackPanel;
    [SerializeField] GameObject lunchPanel;
    [SerializeField] GameObject eveningPanel;

    [SerializeField] Button addItemsButton; // optional; leave null if you don’t want to toggle it

    int dashIndex = -1;
    bool dashRemoved = false;
    bool suppress = false;

    void OnEnable()
    {
        // Always show "-" when the Restaurant panel first appears
        EnsureDashPresent();
        SelectDash();
    }

    void Start()
    {
        dropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void EnsureDashPresent()
    {
        if (dashRemoved) return; // once removed, don't re-add
        dashIndex = dropdown.options.FindIndex(o => o.text.Trim() == "-");
        if (dashIndex < 0)
        {
            dropdown.options.Insert(0, new TMP_Dropdown.OptionData("-"));
            dashIndex = 0;
            dropdown.RefreshShownValue();
        }
    }

    void RemoveDash()
    {
        if (dashRemoved) return;
        dashIndex = dropdown.options.FindIndex(o => o.text.Trim() == "-");
        if (dashIndex >= 0)
        {
            suppress = true; // avoid firing OnValueChanged while we edit options
            dropdown.options.RemoveAt(dashIndex);
            dropdown.RefreshShownValue();
            suppress = false;
        }
        dashRemoved = true;
        dashIndex = -1;
    }

    void SelectDash()
    {
        if (dashIndex < 0) return;
        suppress = true;
        dropdown.value = dashIndex;
        dropdown.RefreshShownValue();
        suppress = false;
        ShowDefault();
    }

    void OnDropdownChanged(int index)
    {
        if (suppress) return;

        string choice = dropdown.options[index].text.Trim().ToLowerInvariant();

        // If user picked a real option for the first time → remove "-"
        if (!dashRemoved && index == dashIndex)
        {
            ShowDefault();
            return; // still on "-"
        }
        else if (!dashRemoved && index != dashIndex)
        {
            string pickedText = dropdown.options[index].text; // remember selection text
            RemoveDash();

            // re-find index of the same text after list shrinks
            int newIdx = dropdown.options.FindIndex(o => o.text == pickedText);
            if (newIdx >= 0)
            {
                suppress = true;
                dropdown.value = newIdx;
                dropdown.RefreshShownValue();
                suppress = false;
                choice = dropdown.options[newIdx].text.Trim().ToLowerInvariant();
            }
        }

        // Show the selected menu
        HideAllMenus();
        defaultPanel.SetActive(false);
        if (addItemsButton) addItemsButton.interactable = true;

        switch (choice)
        {
            case "breakfast": breakfastPanel.SetActive(true); break;
            case "snack":     snackPanel.SetActive(true);     break;
            case "lunch":     lunchPanel.SetActive(true);     break;
            case "evening":   eveningPanel.SetActive(true);   break;
            default:          ShowDefault();                  break;
        }
    }

    void HideAllMenus()
    {
        breakfastPanel.SetActive(false);
        snackPanel.SetActive(false);
        lunchPanel.SetActive(false);
        eveningPanel.SetActive(false);
    }

    void ShowDefault()
    {
        HideAllMenus();
        defaultPanel.SetActive(true);
        if (addItemsButton) addItemsButton.interactable = false;
    }
}
