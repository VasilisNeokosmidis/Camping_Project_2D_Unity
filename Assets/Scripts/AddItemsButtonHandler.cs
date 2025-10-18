using UnityEngine;

public class AddItemsButtonHandler : MonoBehaviour
{
    [SerializeField] OrderCart cart;
    [SerializeField] MenuPanelCollector[] allMenuPanels;

    public void AddVisibleMenuSelections()
    {
        Debug.Log("[AddItems] Click");
        foreach (var panel in allMenuPanels)
        {
            if (panel && panel.gameObject.activeInHierarchy)
            {
                Debug.Log($"[AddItems] Active menu: {panel.name}");
                panel.AddSelectedToCart(cart, resetQuantitiesAfter: true);
                return;
            }
        }
        Debug.LogWarning("[AddItems] No active menu panel found!");
    }
}
