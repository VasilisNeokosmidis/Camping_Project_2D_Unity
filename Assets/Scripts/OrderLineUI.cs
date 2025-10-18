using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class OrderLineUI : MonoBehaviour
{
    [SerializeField] TMP_Text descText;
    [SerializeField] TMP_Text qtyText;
    [SerializeField] TMP_Text amountText;
    [SerializeField] UnityEngine.UI.Button removeBtn;

    public string Description { get; private set; }
    public int Qty { get; private set; }
    public float UnitPrice { get; private set; }
    OrderCart cart;

    public void Init(OrderCart owner, string description, int qty, float unitPrice)
    {
        cart = owner;
        Description = description;
        Qty = qty;
        UnitPrice = unitPrice;

        if (removeBtn) removeBtn.onClick.AddListener(RemoveSelf);

        Debug.Log($"[Line] Init desc='{Description}' qty={Qty} unit={UnitPrice}");

        Refresh();
    }

    public void AddQuantity(int delta)
    {
        Qty += delta;
        Debug.Log($"[Line] '{Description}' new Qty={Qty}");
        Refresh();
    }

  void Refresh()
{
    if (descText)   { descText.color = Color.black;   descText.text   = Description; }
    if (qtyText)    { qtyText.color  = Color.black;   qtyText.text    = Qty.ToString(); }
    if (amountText) { amountText.color = Color.black; amountText.text = (Qty * UnitPrice).ToString("0.##") + "$"; }

    LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    var rt = (RectTransform)transform;
    Debug.Log($"[Line] Refresh '{Description}' rowSize={rt.rect.size} texts=({descText?.text},{qtyText?.text},{amountText?.text})");
}


    void RemoveSelf()
    {
        cart.RemoveLine(this);
    }

    public float LineTotal => Qty * UnitPrice;
}
