using UnityEngine;
using System.Collections.Generic;

public class MenuPanelCollector : MonoBehaviour
{
    [SerializeField] List<MenuItemUI> items = new();

    public void AddSelectedToCart(OrderCart cart, bool resetQuantitiesAfter = true)
    {
        int moved = 0;
        foreach (var it in items)
        {
            if (!it) continue;
            if (it.Quantity > 0)
            {
                Debug.Log($"[Collector:{name}] -> '{it.Description}' x{it.Quantity} @ {it.UnitPrice}");
                cart.AddOrUpdateLine(it.Description, it.Quantity, it.UnitPrice);
                if (resetQuantitiesAfter) it.ResetQuantity();
                moved++;
            }
        }
        Debug.Log($"[Collector:{name}] moved {moved} items.");
    }
}
