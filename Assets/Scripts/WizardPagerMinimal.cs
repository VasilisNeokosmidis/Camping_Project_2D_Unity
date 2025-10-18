using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class WizardPagerMinimal : MonoBehaviour
{
    [System.Serializable]
    public class Page
    {
        public GameObject panel;
        public Button prevBtn;
        public Button nextBtn;   // GoNext()
        public Button closeBtn;  // CloseWizard()
        public Button skipBtn;   // NEW: SkipInstructions()
    }

    [Header("Root & Pages")]
    [SerializeField] GameObject root;       // leave null => this.gameObject
    [SerializeField] List<Page> pages = new();

    [Header("Behavior")]
    [SerializeField] bool alwaysStartFromPage1 = true;
    [SerializeField] bool pauseGameWhileOpen = false;
    [SerializeField] bool verboseLogs = false;

    [SerializeField] bool autoWireButtons = false;
    [SerializeField] bool autoDiscoverPages = false;

    WizardCloseOpensPegPanel closer;
    int index = 0;
    bool inited;

    // Expose current page
    public GameObject CurrentPageGO
        => (pages != null && pages.Count > 0 && index >= 0 && index < pages.Count)
            ? pages[index].panel
            : null;

    void Awake()
    {
        EnsureInit();
        if (root) root.SetActive(false);
    }

    // ---------- PUBLIC: wire to buttons ----------
    public void GoNext()
    {
        EnsureInit();
        if (index >= pages.Count - 1) Finish();
        else GoTo(index + 1);
    }

    public void GoPrev()
    {
        EnsureInit();
        GoTo(index - 1);
    }

    public void CloseWizard()
    {
        EnsureInit();
        Finish();
    }

    // NEW: SKIP
    public void SkipInstructions()
    {
        EnsureInit();
        if (!closer)
        {
            Debug.LogWarning("[WizardPager] Skip: no WizardCloseOpensPegPanel found.", this);
            return;
        }

        var current = CurrentPageGO;
        closer.SkipInstructionsOpenPegPanel(current);
        if (root) root.SetActive(false); // ασφαλές
    }
    // ---------------------------------------------------------

    void EnsureInit()
    {
        if (inited) return;

        if (!root) root = gameObject;
        closer = root.GetComponent<WizardCloseOpensPegPanel>();

        if (autoDiscoverPages)
        {
            pages = root
                .GetComponentsInChildren<Transform>(true)
                .Where(t => t.parent == root.transform && t.name.StartsWith("Page_"))
                .OrderBy(t => t.name, System.StringComparer.Ordinal)
                .Select(t => CollectPage(t.gameObject))
                .ToList();
        }

        if (autoWireButtons)
        {
            foreach (var p in pages)
            {
                if (p.prevBtn)
                {
                    p.prevBtn.onClick.RemoveAllListeners();
                    p.prevBtn.onClick.AddListener(GoPrev);
                }
                if (p.nextBtn)
                {
                    p.nextBtn.onClick.RemoveAllListeners();
                    p.nextBtn.onClick.AddListener(GoNext);
                }
                if (p.closeBtn)
                {
                    p.closeBtn.onClick.RemoveAllListeners();
                    p.closeBtn.onClick.AddListener(CloseWizard);
                }
                if (p.skipBtn)
                {
                    p.skipBtn.onClick.RemoveAllListeners();
                    p.skipBtn.onClick.AddListener(SkipInstructions);
                }
            }
        }

        if (pages.Count > 0 && pages[^1].nextBtn) SetButtonLabelAllTexts(pages[^1].nextBtn, "Finish");

        inited = true;
        if (verboseLogs) Debug.Log($"[WizardPager] Inited. Pages: {pages.Count}", this);
    }

    Page CollectPage(GameObject panelGO)
    {
        var p = new Page { panel = panelGO };
        var buttons = panelGO.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            var n = b.name.ToLowerInvariant();
            if (p.prevBtn  == null && (n.Contains("prev")   || n == "previous")) p.prevBtn  = b;
            else if (p.nextBtn  == null && (n.Contains("next") || n == "finish")) p.nextBtn  = b;
            else if (p.closeBtn == null && (n.Contains("close") || n == "x" || n.Contains("cancel"))) p.closeBtn = b;
            else if (p.skipBtn  == null && n.Contains("skip")) p.skipBtn = b; // <--- SKIP
        }
        return p;
    }

    static void ActivateHierarchy(GameObject node)
    {
        if (!node) return;
        var t = node.transform;
        while (t != null)
        {
            var go = t.gameObject;
            if (!go.activeSelf) go.SetActive(true);
            if (go.GetComponent<Canvas>()) break; // stop at Canvas
            t = t.parent;
        }
    }

    public void Show()
    {
        EnsureInit();

        var cv = (root ? root : gameObject).GetComponentInParent<Canvas>(true);
        if (cv && !cv.gameObject.activeSelf) cv.gameObject.SetActive(true);

        ActivateHierarchy(root ? root : gameObject);

        if (alwaysStartFromPage1 || index < 0 || index >= pages.Count) index = 0;

        if (pauseGameWhileOpen) Time.timeScale = 0f;

        root.SetActive(true);
        Refresh();

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    void Finish()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;

        if (!closer) closer = root.GetComponent<WizardCloseOpensPegPanel>();
        if (!closer) closer = root.GetComponentInParent<WizardCloseOpensPegPanel>(true);

        if (closer) closer.OpenPegPanelFromWizardFinish();
        else Debug.LogWarning("[WizardPager] Finish: no WizardCloseOpensPegPanel found.", this);

        if (root) root.SetActive(false);
    }

    void GoTo(int newIndex)
    {
        if (pages == null || pages.Count == 0) return;
        index = Mathf.Clamp(newIndex, 0, pages.Count - 1);
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];

            if (p.panel) p.panel.SetActive(i == index);

            if (p.prevBtn) p.prevBtn.interactable = (i == index && index > 0);
            if (p.nextBtn) p.nextBtn.interactable = (i == index);
            if (p.skipBtn) p.skipBtn.interactable = (i == index);

            if (p.nextBtn) SetButtonLabelAllTexts(p.nextBtn, (i == pages.Count - 1) ? "Finish" : "Next");
        }

        if (verboseLogs) Debug.Log($"[WizardPager] Page {index + 1}/{pages.Count}", this);
    }

    void SetButtonLabelAllTexts(Button btn, string label)
    {
        foreach (var t in btn.GetComponentsInChildren<TMP_Text>(true)) t.text = label;
        foreach (var t in btn.GetComponentsInChildren<Text>(true))     t.text = label;
    }
}
