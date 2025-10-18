using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ShelterInstructionWizardSimple : MonoBehaviour
{
    [System.Serializable]
    public class Page
    {
        public GameObject panel;
        public Button prevBtn;
        public Button nextBtn;
        public Button closeBtn;   // top-right "X"
        public Button startNowBtn; // per-page “Start Now”
    }

    [Header("Root & Pages")]
    [SerializeField] GameObject root;                 
    [SerializeField] List<Page> pages = new();

    [Header("Shelter UI Targets (fallback if routesPanel not set)")]
    [SerializeField] GameObject canvas_Shelter;       
    [SerializeField] GameObject shelter_Panel;        

    [Header("What to enable when user presses the Shelter button")]
    [SerializeField] GameObject canvas_ShelterInstructions; 

    [Header("What to disable when any Close (X) is pressed")]
    [SerializeField] GameObject canvas_MapControlCenter;    
    [SerializeField] GameObject tentControlCenter_Panel;    

    [Header("Other Panels")]
    [SerializeField] GameObject mainMenuPanel;  // ✅ NEW: assign your MainMenu_Panel here

    [Header("Routes Panel (preferred)")]
    [SerializeField] ShelterRoutesPanel routesPanel;  

    [Header("Options")]
    [SerializeField] bool pauseGameWhileOpen = true;

    int index;
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
            if (go.GetComponent<Canvas>()) break;
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
            if (p.startNowBtn)
            {
                p.startNowBtn.onClick.RemoveAllListeners();
                p.startNowBtn.onClick.AddListener(StartShelterNow);
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

    public void Show()
    {
        EnsureInit();

        if (canvas_ShelterInstructions)
        {
            ActivateHierarchy(canvas_ShelterInstructions);
            canvas_ShelterInstructions.SetActive(true);
        }

        ActivateHierarchy(root ? root : gameObject);

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

    void StartShelterNow()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;

        if (index >= 0 && index < pages.Count && pages[index].panel)
            pages[index].panel.SetActive(false);

        if (root) root.SetActive(false);
        if (canvas_ShelterInstructions) canvas_ShelterInstructions.SetActive(false);

        var target = routesPanel;
#if UNITY_2023_1_OR_NEWER
        if (!target) target = Object.FindFirstObjectByType<ShelterRoutesPanel>(FindObjectsInactive.Include);
#else
        if (!target) target = Object.FindObjectOfType<ShelterRoutesPanel>(true);
#endif
        if (target)
        {
            target.OpenPanel();
            Debug.Log("[ShelterWizard] StartNow → routesPanel.OpenPanel()");
        }
        else
        {
            if (canvas_Shelter) ActivateHierarchy(canvas_Shelter);
            if (canvas_Shelter) canvas_Shelter.SetActive(true);
            if (shelter_Panel) shelter_Panel.SetActive(true);
        }

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    void Finish()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;

        if (root) root.SetActive(false);
        if (canvas_ShelterInstructions) canvas_ShelterInstructions.SetActive(false);

        var target = routesPanel;
#if UNITY_2023_1_OR_NEWER
        if (!target) target = Object.FindFirstObjectByType<ShelterRoutesPanel>(FindObjectsInactive.Include);
#else
        if (!target) target = Object.FindObjectOfType<ShelterRoutesPanel>(true);
#endif
        if (target)
        {
            target.OpenPanel();
        }
        else
        {
            if (canvas_Shelter) ActivateHierarchy(canvas_Shelter);
            if (canvas_Shelter) canvas_Shelter.SetActive(true);
            if (shelter_Panel) shelter_Panel.SetActive(true);
        }

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    public void OnCloseButton() => CloseEverythingHard();

    void CloseEverythingHard()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;

        if (index >= 0 && index < pages.Count && pages[index].panel)
            pages[index].panel.SetActive(false);

        if (root) root.SetActive(false);
        if (canvas_ShelterInstructions) canvas_ShelterInstructions.SetActive(false);

        // ❌ removed: tentControlCenter_Panel.SetActive(false);
        // ❌ removed: canvas_MapControlCenter.SetActive(false);

        // ✅ Enable Main Menu panel
        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }
}
