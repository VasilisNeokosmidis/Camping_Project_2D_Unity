using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InstructionWizardSimple : MonoBehaviour
{
    [System.Serializable]
    public class Page
    {
        public GameObject panel;
        public Button prevBtn;
        public Button nextBtn;
        public Button closeBtn;   // the top-right "X"
        public Button skipBtn;    // ← NEW: Skip Instructions
    }

    [Header("Root & Pages")]
    [SerializeField] GameObject root;      // WizardRoot
    [SerializeField] List<Page> pages = new();

    [Header("After Finish / Skip (opens the Map Placement UI)")]
    [SerializeField] MapPanelController mapPanel;

    [Header("Wizard/Canvas to hide on Close/Finish/Skip")]
    [SerializeField] GameObject canvas_MapPlacementInstructions;  // Canvas_MapPlacementInstructions

    [Header("Other Panels")]
    [SerializeField] GameObject mainMenuPanel;  // enable on Close (X), not on Skip/Finish

    // (Kept for compatibility; not used on Close anymore)
    [SerializeField] GameObject canvas_MapControlCenter;          // (unused on Close)
    [SerializeField] GameObject tentControlCenter_Panel;          // (unused on Close)

    [Header("Options")]
    [SerializeField] bool pauseGameWhileOpen = true;

    int index;
    bool reopenMapOnClose;  // unused in hard close path but kept for Show(bool)
    bool _inited;

    void Awake()
    {
        if (!_inited) EnsureInit();
        if (root) root.SetActive(false);
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

    void EnsureInit()
    {
        if (_inited) return;

        if (!root) root = gameObject;

        for (int i = 0; i < pages.Count; i++)
        {
            int ci = i;
            var p = pages[i];

            if (p.prevBtn)
            {
                p.prevBtn.onClick.RemoveAllListeners();
                p.prevBtn.onClick.AddListener(() => GoTo(ci - 1));
            }
            if (p.nextBtn)
            {
                p.nextBtn.onClick.RemoveAllListeners();
                p.nextBtn.onClick.AddListener(() =>
                {
                    if (ci == pages.Count - 1) Finish();
                    else GoTo(ci + 1);
                });
            }
            if (p.closeBtn)
            {
                p.closeBtn.onClick.RemoveAllListeners();
                p.closeBtn.onClick.AddListener(CloseEverythingHard);
            }
            if (p.skipBtn)
            {
                p.skipBtn.onClick.RemoveAllListeners();
                p.skipBtn.onClick.AddListener(SkipToMapNow); // ← wire skip
            }
        }

        _inited = true;
    }

    void Update()
    {
        if (!root || !root.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.LeftArrow))  GoTo(index - 1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) { if (index == pages.Count - 1) Finish(); else GoTo(index + 1); }
        if (Input.GetKeyDown(KeyCode.Escape)) CloseEverythingHard();
    }

    public void Show() => Show(false);

    public void Show(bool returnToMapOnClose)
    {
        EnsureInit();

        var parentCanvas = (root ? root : gameObject).GetComponentInParent<Canvas>(true);
        if (parentCanvas && !parentCanvas.gameObject.activeSelf)
            parentCanvas.gameObject.SetActive(true);

        ActivateHierarchy(root ? root : gameObject);

        reopenMapOnClose = returnToMapOnClose;
        index = 0;

        if (pauseGameWhileOpen) Time.timeScale = 0f;

        root.SetActive(true);
        Refresh();

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    void GoTo(int newIndex)
    {
        index = Mathf.Clamp(newIndex, 0, pages.Count - 1);
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            if (p.panel) p.panel.SetActive(i == index);

            if (p.prevBtn) p.prevBtn.interactable = i > 0;

            if (p.nextBtn)
            {
                string label = (i == pages.Count - 1) ? "Finish" : "Next";
                var tmp = p.nextBtn.GetComponentInChildren<TMP_Text>(true);
                if (tmp) tmp.text = label;
                else
                {
                    var legacy = p.nextBtn.GetComponentInChildren<UnityEngine.UI.Text>(true);
                    if (legacy) legacy.text = label;
                }
            }
        }
    }

    void Finish() => OpenMapPlacementAndHideWizard();

    // ✅ NEW: Skip behaves like Finish, immediately opening Map Placement
    void SkipToMapNow() => OpenMapPlacementAndHideWizard();

    // Centralized “open Map Placement & hide wizard” logic for Finish/Skip
    void OpenMapPlacementAndHideWizard()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;

        // Hide current page defensively
        if (index >= 0 && index < pages.Count && pages[index].panel)
            pages[index].panel.SetActive(false);

        // Hide the wizard
        if (root) root.SetActive(false);

        // ✅ Ensure the Map Placement canvas is ON (as requested)
        if (canvas_MapPlacementInstructions)
        {
            ActivateHierarchy(canvas_MapPlacementInstructions);
            canvas_MapPlacementInstructions.SetActive(true);
        }

        // Find / use the MapPanelController and open its panel
        var target = mapPanel;
#if UNITY_2023_1_OR_NEWER
        if (!target) target = Object.FindFirstObjectByType<MapPanelController>(FindObjectsInactive.Include);
#else
        if (!target) target = Object.FindObjectOfType<MapPanelController>(true);
#endif
        if (!target)
        {
            Debug.LogError("[InstructionWizardSimple] Finish/Skip pressed but no MapPanelController found.");
            return;
        }

        // Make sure its Canvas is alive, then open (this enables the Map Placement panel)
        var canvas = target.GetComponentInParent<Canvas>(true);
        if (canvas && !canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);

        target.Open(); // internally ensures its panel is active

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    // Old close – kept in case something else calls it
    public void OnCloseButton() => CloseEverythingHard();

    // Same behavior as ShelterInstructionWizardSimple.CloseEverythingHard
    void CloseEverythingHard()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;

        // Hide current page defensively
        if (index >= 0 && index < pages.Count && pages[index].panel)
            pages[index].panel.SetActive(false);

        // Hide wizard & its canvas
        if (root) root.SetActive(false);
        if (canvas_MapPlacementInstructions) canvas_MapPlacementInstructions.SetActive(false);

        // Do not disable MapControlCenter/TentControlCenter here
        // Enable Main Menu panel on hard close
        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        // Do not auto-reopen Map on hard close
        reopenMapOnClose = false;

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }
}
