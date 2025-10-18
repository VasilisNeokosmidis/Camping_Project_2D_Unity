using UnityEngine; 
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class ControlCenterUI : MonoBehaviour
{
    public event Action Exited;

    [Header("Canvas Root (Canvas_ControlCenter)")]
    [SerializeField] GameObject canvasRoot;

    [Header("Panels (inside ControlCenter)")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject energyPanel;
    [SerializeField] GameObject colorPanel;
    [SerializeField] GameObject weatherPanel;
    [SerializeField] GameObject restaurantPanel;         // NEW

    [Header("Main Menu Buttons (optional – auto-found if left empty)")]
    [SerializeField] Button colorMenuButton;
    [SerializeField] Button energyMenuButton;
    [SerializeField] Button weatherMenuButton;
    [SerializeField] Button restaurantMenuButton;        // NEW

    [Header("Color customizer (inside Color_Panel)")]
    [SerializeField] TentCustomizerPanel2D colorCustomizer;

    [Header("Other UI to keep/force hidden on Exit")]
    [SerializeField] GameObject instructionsCanvasRoot;   // Canvas_ControlCenterInstructions
    [SerializeField] GameObject instructionsWizardRoot;   // ControlCenterInstructions_WizardRoot

    [Header("Tent UI (names you care about)")]
    [SerializeField] GameObject canvas_TentControlCenter; // KEEP ENABLED ON EXIT
    [SerializeField] GameObject tentControlCenter_Panel;  // DISABLE ON EXIT
    [SerializeField] GameObject mainMenuPanel_Tent;       // DISABLE ON EXIT

    [Header("Player (fallback unfreeze)")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] bool unfreezePlayerOnExit = true;

    [Header("Extra targets to disable on Exit")]
    [SerializeField] List<GameObject> disableOnExit = new();   // assign any GO(s) here
    [SerializeField] string[] alsoDisableByName;               // optional: auto-find by name

    private GameObject currentTentRoot;

    void Awake()
    {
        if (!mainMenuPanel)  mainMenuPanel  = FindDeep(transform, "MainMenu_Panel")?.gameObject;
        if (!energyPanel)    energyPanel    = FindDeep(transform, "Energy_Panel")?.gameObject;
        if (!colorPanel)     colorPanel     = FindDeep(transform, "Color_Panel")?.gameObject;
        if (!weatherPanel)   weatherPanel   = FindDeep(transform, "Weather_Panel")?.gameObject;
        if (!restaurantPanel)restaurantPanel= FindDeep(transform, "Restaurant_Panel")?.gameObject; // NEW

        if (!energyMenuButton)    energyMenuButton    = FindDeep(transform, "BTN_Energy")     ?.GetComponent<Button>();
        if (!colorMenuButton)     colorMenuButton     = FindDeep(transform, "BTN_Color")      ?.GetComponent<Button>();
        if (!weatherMenuButton)   weatherMenuButton   = FindDeep(transform, "BTN_Weather")    ?.GetComponent<Button>();
        if (!restaurantMenuButton)restaurantMenuButton= FindDeep(transform, "BTN_Restaurant") ?.GetComponent<Button>(); // NEW

        if (energyMenuButton)     { energyMenuButton.onClick.RemoveAllListeners();     energyMenuButton.onClick.AddListener(OnClickEnergy); }
        if (colorMenuButton)      { colorMenuButton.onClick.RemoveAllListeners();      colorMenuButton.onClick.AddListener(OnClickColor);  }
        if (weatherMenuButton)    { weatherMenuButton.onClick.RemoveAllListeners();    weatherMenuButton.onClick.AddListener(OnClickWeather); }
        if (restaurantMenuButton) { restaurantMenuButton.onClick.RemoveAllListeners(); restaurantMenuButton.onClick.AddListener(OnClickRestaurant); } // NEW

        // Best-effort auto-find
        if (!instructionsCanvasRoot)   instructionsCanvasRoot   = GameObject.Find("Canvas_ControlCenterInstructions");
        if (!instructionsWizardRoot)   instructionsWizardRoot   = GameObject.Find("ControlCenterInstructions_WizardRoot");
        if (!mainMenuPanel_Tent)       mainMenuPanel_Tent       = GameObject.Find("MainMenu_Panel_Tent");
        if (!tentControlCenter_Panel)  tentControlCenter_Panel  = GameObject.Find("TentControlCenter_Panel");
        if (!canvas_TentControlCenter) canvas_TentControlCenter = GameObject.Find("Canvas_TentControlCenter");
    }

    public void Open(GameObject tentRoot)
    {
        currentTentRoot = tentRoot;
        if (canvasRoot) canvasRoot.SetActive(true);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowMain();
    }

    public void OnClickEnergy()
    {
        if (!energyPanel)   energyPanel   = FindDeep(transform, "Energy_Panel")   ?.gameObject;
        if (!mainMenuPanel) mainMenuPanel = FindDeep(transform, "MainMenu_Panel") ?.gameObject;
        if (!colorPanel)    colorPanel    = FindDeep(transform, "Color_Panel")    ?.gameObject;
        if (!weatherPanel)  weatherPanel  = FindDeep(transform, "Weather_Panel")  ?.gameObject;
        if (!restaurantPanel) restaurantPanel = FindDeep(transform, "Restaurant_Panel")?.gameObject;

        SetOnlyActive(energyPanel);
        if (energyPanel) { energyPanel.SetActive(true); energyPanel.transform.SetAsLastSibling(); }
    }

    public void OnClickColor()
    {
        TentCustomizerPanel2D.TogglePreviewClone(true);
        if (!colorPanel) colorPanel = FindDeep(transform, "Color_Panel")?.gameObject;
        SetOnlyActive(colorPanel);

        if (!colorCustomizer && colorPanel)
            colorCustomizer = colorPanel.GetComponentInChildren<TentCustomizerPanel2D>(true);

        if (colorCustomizer && currentTentRoot)
        {
            colorCustomizer.gameObject.SetActive(true);
            colorCustomizer.OpenForTent(currentTentRoot);
        }
    }

    public void OnClickWeather()
    {
        if (!weatherPanel) weatherPanel = FindDeep(transform, "Weather_Panel")?.gameObject;
        SetOnlyActive(weatherPanel);
        if (weatherPanel) { weatherPanel.SetActive(true); weatherPanel.transform.SetAsLastSibling(); }
    }

    // NEW: Restaurant handler
    public void OnClickRestaurant()
    {
        if (!restaurantPanel) restaurantPanel = FindDeep(transform, "Restaurant_Panel")?.gameObject;
        SetOnlyActive(restaurantPanel);
        if (restaurantPanel) { restaurantPanel.SetActive(true); restaurantPanel.transform.SetAsLastSibling(); }
    }

    

    public void OnClickBack() => ShowMain();

    private void ShowMain()
    {
        if (!mainMenuPanel)  mainMenuPanel  = FindDeep(transform, "MainMenu_Panel")?.gameObject;
        if (!energyPanel)    energyPanel    = FindDeep(transform, "Energy_Panel")?.gameObject;
        if (!colorPanel)     colorPanel     = FindDeep(transform, "Color_Panel")?.gameObject;
        if (!weatherPanel)   weatherPanel   = FindDeep(transform, "Weather_Panel")?.gameObject;
        if (!restaurantPanel)restaurantPanel= FindDeep(transform, "Restaurant_Panel")?.gameObject;

        if (mainMenuPanel) { mainMenuPanel.SetActive(true); mainMenuPanel.transform.SetAsLastSibling(); }
        if (energyPanel)    energyPanel.SetActive(false);
        if (colorPanel)     colorPanel.SetActive(false);
        if (weatherPanel)   weatherPanel.SetActive(false);
        if (restaurantPanel)restaurantPanel.SetActive(false); // NEW
    }

    private void SetOnlyActive(GameObject target)
    {
        if (mainMenuPanel)  mainMenuPanel.SetActive(target == mainMenuPanel);
        if (energyPanel)    energyPanel.SetActive(target == energyPanel);
        if (colorPanel)     colorPanel.SetActive(target == colorPanel);
        if (weatherPanel)   weatherPanel.SetActive(target == weatherPanel);
        if (restaurantPanel)restaurantPanel.SetActive(target == restaurantPanel); // NEW
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (!root) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // === The rule you asked for ===
    private void CloseTentPanelsKeepCanvas()
    {
        // Disable panels
        if (mainMenuPanel_Tent)       mainMenuPanel_Tent.SetActive(false);
        if (tentControlCenter_Panel)  tentControlCenter_Panel.SetActive(false);

        // Keep parent canvas enabled
        if (canvas_TentControlCenter) canvas_TentControlCenter.SetActive(true);
    }
    
     public void OnClickExit()
    {
        // A) Disable whatever you listed, *first*
        DisableExtraTargets();

        // 1) Always disable ONLY the two panels (keep the canvas enabled)
        CloseTentPanelsKeepCanvas();

        // 2) Hide any instructions defensively (never re-enable)
        EnsureInstructionsHidden();

        // 3) Close this Control Center
        if (canvasRoot) canvasRoot.SetActive(false);
        gameObject.SetActive(false);

        // 4) Notify listeners
        Exited?.Invoke();

        // 5) Re-apply the same rule in case a listener re-opened something
        CloseTentPanelsKeepCanvas();
        EnsureInstructionsHidden();

        // 6) Safety-net: unfreeze player
        if (unfreezePlayerOnExit) UnfreezePlayer();
    }

    void DisableExtraTargets()
    {
        // Explicit assignments in Inspector
        if (disableOnExit != null)
        {
            for (int i = 0; i < disableOnExit.Count; i++)
                if (disableOnExit[i]) disableOnExit[i].SetActive(false);
        }

        // Optional: disable by name (anywhere in scene)
        if (alsoDisableByName != null)
        {
            foreach (var n in alsoDisableByName)
            {
                if (string.IsNullOrEmpty(n)) continue;
                var go = GameObject.Find(n);
                if (go) go.SetActive(false);
            }
        }
    }

    private void EnsureInstructionsHidden()
    {
        if (instructionsCanvasRoot) instructionsCanvasRoot.SetActive(false);
        if (instructionsWizardRoot) instructionsWizardRoot.SetActive(false);

        string[] canvasNames =
        {
            "Canvas_ControlCenterInstructions",
            "Canvas_ControlCenter_Instructions",
            "Canvas_CC_Instructions"
        };
        string[] wizardNames =
        {
            "ControlCenterInstructions_WizardRoot",
            "ControlCenterIntroWizardRoot",
            "CC_Instructions_WizardRoot"
        };

        foreach (var n in canvasNames) { var go = GameObject.Find(n); if (go) go.SetActive(false); }
        foreach (var n in wizardNames) { var go = GameObject.Find(n); if (go) go.SetActive(false); }

#if UNITY_2023_1_OR_NEWER
        var wizard = UnityEngine.Object.FindFirstObjectByType<ControlCenterIntroWizard>(FindObjectsInactive.Include);
#else
        var wizard = UnityEngine.Object.FindObjectOfType<ControlCenterIntroWizard>(true);
#endif
        if (wizard)
        {
            wizard.gameObject.SetActive(false);
            var canvas = wizard.GetComponentInParent<Canvas>(true);
            if (canvas) canvas.gameObject.SetActive(false);
        }

        var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (var c in allCanvases)
        {
            if (!c) continue;
            if (c.gameObject.hideFlags != HideFlags.None) continue;
            var nm = c.gameObject.name;
            if (nm.IndexOf("ControlCenterInstructions", StringComparison.OrdinalIgnoreCase) >= 0 ||
                nm.IndexOf("CC_Instructions", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                c.gameObject.SetActive(false);
            }
        }
    }

    private void UnfreezePlayer()
    {
        var player = GameObject.FindWithTag(playerTag);
        if (!player) return;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        var move = FindByTypeName(player, "MoveScript");
        if (move) move.enabled = true;
    }

    private static MonoBehaviour FindByTypeName(GameObject go, string typeName)
    {
        var all = go.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in all)
        {
            if (mb == null) continue;
            if (mb.GetType().Name == typeName) return mb;
        }
        return null;
    }
}
