using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using System;

public class OrderCart : MonoBehaviour
{
    [Header("Order View")]
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform content;
    [SerializeField] OrderLineUI linePrefab;
    [SerializeField] TMP_Text totalValueText;

    // Layout tuning
    [Header("Compact Layout")]
    [SerializeField] float verticalSpacing = 2f;          
    [SerializeField] float rowPreferredHeight = 28f;      
    [SerializeField] float innerItemSpacing = 4f;         
    [SerializeField] RectOffset padding = null;           

    readonly List<OrderLineUI> lines = new();
    public float CurrentTotal { get; private set; }
    public int ItemCount => lines.Count;

    // Event that fires when cart contents change
    public event Action<float,int> OnCartChanged;

    void Awake()
    {
        if (!scrollRect) Debug.LogError("OrderCart: 'ScrollRect' is NOT assigned.", this);
        if (!content) Debug.LogError("OrderCart: 'Content' is NOT assigned.", this);
        if (!linePrefab) Debug.LogError("OrderCart: 'Line Prefab' is NOT assigned.", this);

        if (scrollRect)
        {
            if (scrollRect.content != content) scrollRect.content = content;
            if (!scrollRect.viewport)
                Debug.LogWarning("OrderCart: ScrollRect.viewport is not set (should be the Viewport).", this);
        }

        EnsureLayoutPipelineIsValid();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        RecalculateTotal();
    }

    void EnsureLayoutPipelineIsValid()
    {
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (!vlg) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = verticalSpacing;
        vlg.childControlHeight = true;
        vlg.childControlWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth  = true;
        if (padding != null) vlg.padding = padding;

        var csf = content.GetComponent<ContentSizeFitter>();
        if (!csf) csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot     = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0, content.sizeDelta.y);

        vlg.spacing = verticalSpacing;
    }

    public void AddOrUpdateLine(string description, int qty, float unitPrice)
    {
        if (qty <= 0) return;

        var existing = lines.Find(l => l.Description == description);
        if (existing != null)
        {
            existing.AddQuantity(qty);
        }
        else
        {
            var ui = Instantiate(linePrefab, content);
            TightenRow(ui);
            ui.transform.SetAsLastSibling();
            ui.Init(this, description, qty, unitPrice);
            lines.Add(ui);
        }

        RecalculateTotal();
        RebuildNow();
        if (scrollRect) scrollRect.verticalNormalizedPosition = 0f;
    }

    void TightenRow(OrderLineUI ui)
    {
        var le = ui.GetComponent<LayoutElement>();
        if (!le) le = ui.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = rowPreferredHeight;
        le.minHeight = rowPreferredHeight;

        foreach (var hg in ui.GetComponentsInChildren<HorizontalLayoutGroup>(true))
            hg.spacing = innerItemSpacing;
    }

    public void RemoveLine(OrderLineUI line)
    {
        if (lines.Remove(line))
        {
            Destroy(line.gameObject);
            RecalculateTotal();
            RebuildNow();
        }
    }

    public void ClearCart()
    {
        foreach (var l in lines) if (l) Destroy(l.gameObject);
        lines.Clear();
        RecalculateTotal();
        RebuildNow();
    }

    void RecalculateTotal()
    {
        float total = 0f;
        foreach (var l in lines) total += l.LineTotal;
        CurrentTotal = total;

        if (totalValueText) totalValueText.text = $"Total: {total:0.##}$";
        OnCartChanged?.Invoke(CurrentTotal, lines.Count);
    }

    void RebuildNow()
    {
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg) vlg.spacing = verticalSpacing;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
    }
}
