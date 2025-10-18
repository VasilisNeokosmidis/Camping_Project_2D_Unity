using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MenuItemUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_Text itemDescription;
    [SerializeField] TMP_Text priceText;   // “5$” etc. OR hide & use unitPrice directly
    [SerializeField] TMP_Text qtyText;
    [SerializeField] Button addBtn;
    [SerializeField] Button removeBtn;

    [Header("Data")]
    [SerializeField] string description;
    [SerializeField] float unitPrice = 5f;

    int qty;

void Awake()
{
    if (!string.IsNullOrWhiteSpace(itemDescription?.text)) description = itemDescription.text;
    if (!string.IsNullOrWhiteSpace(priceText?.text))
    {
        var raw = priceText.text.Replace("$","").Trim();
        if (float.TryParse(raw, out var p)) unitPrice = p;
    }
    Debug.Log($"[MenuItemUI:{name}] '{description}' unit={unitPrice}");

    qty = 0;
    Refresh();
    addBtn.onClick.AddListener(() => { qty++; Refresh(); });
    removeBtn.onClick.AddListener(() => { qty = Mathf.Max(0, qty - 1); Refresh(); });
}


    void Refresh() => qtyText.text = qty.ToString();

    public int Quantity => qty;
    public string Description => description;
    public float UnitPrice => unitPrice;
    public float LineTotal => qty * unitPrice;

    public void ResetQuantity()
    {
        qty = 0;
        Refresh();
    }
}
