using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class WeatherPanelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI celsiusText;

    [Header("Icons (exactly one enabled)")]
    [SerializeField] private RawImage sunnyIcon;
    [SerializeField] private RawImage nightIcon;
    [SerializeField] private RawImage rainIcon;

    [Header("Temperature Range (°C)")]
    [SerializeField] private float minCelsius = 5f;
    [SerializeField] private float maxCelsius = 40f;

    [Header("Day/Night decision")]
    [Range(0f,1f)] [SerializeField] private float nightTintThreshold = 0.5f;
    [Range(0f,0.2f)] [SerializeField] private float hysteresis = 0.05f;

    [Header("Update / Smoothing")]
    [SerializeField] private bool liveUpdate = true;
    [Range(0f, 20f)] [SerializeField] private float smoothLerpSpeed = 8f;

    [Header("Color by Temperature (optional override)")]
    [SerializeField] private Gradient tempColorGradient;

    [Header("Auto-wiring")]
    [SerializeField] private DayNightLightMeter lightMeter;

    // ==== Static events for ALL WeatherPanels ====
    public static event Action RainStartedGlobal;
    public static event Action RainStoppedGlobal;

    private float _currentCelsius;
    private bool _isNightStable;
    private bool _forceRainIcon;

    // ========= NAV (όπως στο EnergyPanel, αλλά ΧΩΡΙΣ unfreeze) =========
    [Header("Navigation")]
    [SerializeField] private GameObject panelRoot;           // default: this.gameObject
    [SerializeField] private GameObject mainMenuPanel;       // auto-find "MainMenu_Panel" αν μείνει κενό
    [SerializeField] private GameObject controlCenterPanel;  // optional: ControlCenter_Panel
    [SerializeField] private Button backToMenuButton;        // optional; auto-find by name "BackToMainMenu"
    [SerializeField] private Button closeXButton;            // optional X button

    // ========= RAIN TENT (SHED) TOGGLE =========
    [Header("Rain Tent (Shed) Toggle")]
    [SerializeField] private Transform finishedTentRoot;         // optional: Finished_Tent root
    [SerializeField] private GameObject shed;                    // optional: Shed GO; auto-found if null
    [SerializeField] private Button deployTentButton;            // drag your button here
    [SerializeField] private TextMeshProUGUI deployTentLabel;    // TMP label inside the button

    [Header("Label Text")]
    [SerializeField] private string textDeploy = "Deploy Rain Tent";
    [SerializeField] private string textDrop   = "Drop the Rain Tent";

    [Header("Colors")]
    [SerializeField] private Color colorDeploy = new Color(0.18f, 0.62f, 0.24f); // green
    [SerializeField] private Color colorDrop   = new Color(0.80f, 0.15f, 0.15f); // red
    [SerializeField] private Color labelColor  = Color.white;

    // ========= HINT OUTLINE (red, thick) =========
    [Header("Deploy Hint Outline")]
    [Tooltip("If true, automatically adds a UI Outline to the Deploy button (if missing).")]
    [SerializeField] private bool autoAddOutline = true;

    [Tooltip("Red outline color while rain icon is shown and tent not deployed.")]
    [SerializeField] private Color outlineColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Tooltip("Thickness in pixels (Outline effectDistance).")]
    [SerializeField] private float outlineThickness = 6f;

    private Outline _deployOutline;   // cached outline component on the deploy button

    void Awake()
    {
        if (!panelRoot) panelRoot = gameObject;

        EnsureDefaultGradient();

        if (!lightMeter)
        {
            var go = GameObject.Find("DayNightManager");
            if (go) lightMeter = go.GetComponent<DayNightLightMeter>();
        }

        // --- Navigation auto-wiring ---
        if (!mainMenuPanel)
            mainMenuPanel = FindDeep(transform.root, "MainMenu_Panel")?.gameObject;

        if (!backToMenuButton)
            backToMenuButton = FindDeep(panelRoot.transform, "BackToMainMenu")?.GetComponent<Button>();

        if (backToMenuButton)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(GoBackToMainMenu);
        }

        if (closeXButton)
        {
            closeXButton.onClick.RemoveAllListeners();
            closeXButton.onClick.AddListener(CloseWeatherPanel);
        }

        // Subscribe to global rain events
        RainStartedGlobal += OnRainStarted;
        RainStoppedGlobal += OnRainStopped;

        SetOnlyOneIcon(null);

        // Try to auto-find Shed if not assigned
        TryResolveShedReference();

        // Prepare the outline on the deploy button (if any)
        SetupDeployButtonOutline();
    }

    void OnDestroy()
    {
        RainStartedGlobal -= OnRainStarted;
        RainStoppedGlobal  -= OnRainStopped;

        if (backToMenuButton) backToMenuButton.onClick.RemoveListener(GoBackToMainMenu);
        if (closeXButton)     closeXButton.onClick.RemoveListener(CloseWeatherPanel);
    }

    void OnEnable()
    {
        RefreshOnce(true);
        SyncRainTentButton();       // also updates outline inside
        UpdateDeployHintOutline();  // ensure correct on enable
    }

    void Update()
    {
        if (liveUpdate) RefreshOnce();
    }

    // ===== NAV methods (όμοια με EnergyPanel αλλά χωρίς unfreeze) =====
    public void GoBackToMainMenu()
    {
        if (panelRoot) panelRoot.SetActive(false);
        if (!mainMenuPanel)
            mainMenuPanel = FindDeep(transform.root, "MainMenu_Panel")?.gameObject;

        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (controlCenterPanel) controlCenterPanel.SetActive(true);

        // Ενημέρωση ControlCenterUI (αν υπάρχει), όπως στο EnergyPanel
        var cc = controlCenterPanel ? controlCenterPanel.GetComponentInParent<ControlCenterUI>(true) : null;
        if (cc) cc.OnClickBack();
    }

    public void CloseWeatherPanel()
    {
        // Same behavior as EnergyPanel Close, χωρίς ξεπάγωμα παίκτη
        if (panelRoot && panelRoot.activeSelf)
            panelRoot.SetActive(false);

        if (!mainMenuPanel)
            mainMenuPanel = FindDeep(transform.root, "MainMenu_Panel")?.gameObject;

        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (controlCenterPanel) controlCenterPanel.SetActive(true);

        var cc = controlCenterPanel ? controlCenterPanel.GetComponentInParent<ControlCenterUI>(true) : null;
        if (cc) cc.OnClickBack();
    }

    // ===== Global rain callbacks =====
    private void OnRainStarted()
    {
        _forceRainIcon = true;
        SetOnlyOneIcon(rainIcon);
        UpdateDeployHintOutline();
    }

    private void OnRainStopped()
    {
        _forceRainIcon = false;
        UpdateIconsFromLight();
        UpdateDeployHintOutline();
    }

    private void RefreshOnce(bool forceInstant = false)
    {
        float lightFactor = lightMeter ? Mathf.Clamp01(lightMeter.LightFactor01) : 1f;
        float targetCelsius = Mathf.Lerp(minCelsius, maxCelsius, lightFactor);

        _currentCelsius = (forceInstant || smoothLerpSpeed <= 0f)
            ? targetCelsius
            : Mathf.Lerp(_currentCelsius, targetCelsius, 1f - Mathf.Exp(-smoothLerpSpeed * Time.deltaTime));

        float t = Mathf.InverseLerp(minCelsius, maxCelsius, _currentCelsius);
        Color textCol = tempColorGradient.Evaluate(t);

        if (celsiusText)
        {
            celsiusText.text = $"{_currentCelsius:0.0} °C";
            celsiusText.color = textCol;
            celsiusText.faceColor = textCol;
        }

        if (_forceRainIcon)
            SetOnlyOneIcon(rainIcon);
        else
            UpdateIconsFromLight(lightFactor);

        // In case icon visibility changed due to light, keep outline in sync
        UpdateDeployHintOutline();
    }

    private void UpdateIconsFromLight(float? lightFactorOpt = null)
    {
        float lightFactor = lightFactorOpt ?? (lightMeter ? Mathf.Clamp01(lightMeter.LightFactor01) : 1f);
        float lightThreshold = 1f - nightTintThreshold;

        float low  = lightThreshold - hysteresis;
        float high = lightThreshold + hysteresis;

        if (_isNightStable)
        {
            if (lightFactor > high) _isNightStable = false;
        }
        else
        {
            if (lightFactor < low) _isNightStable = true;
        }

        SetOnlyOneIcon(_isNightStable ? nightIcon : sunnyIcon);
    }

    private void SetOnlyOneIcon(RawImage target)
    {
        if (sunnyIcon) sunnyIcon.gameObject.SetActive(target == sunnyIcon);
        if (nightIcon) nightIcon.gameObject.SetActive(target == nightIcon);
        if (rainIcon)  rainIcon.gameObject.SetActive(target == rainIcon);
    }

    private void EnsureDefaultGradient()
    {
        if (tempColorGradient != null && tempColorGradient.colorKeys.Length > 0)
            return;

        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.06f,0.16f,0.53f), 0f),
                new GradientColorKey(new Color(1f,0.65f,0f), 0.7f),
                new GradientColorKey(new Color(1f,0.1f,0.1f), 1f),
            },
            new[] {
                new GradientAlphaKey(1f,0f),
                new GradientAlphaKey(1f,1f),
            }
        );
        tempColorGradient = g;
    }

    // === Helpers to raise events globally (if you ever need them) ===
    public static void RaiseRainStarted() => RainStartedGlobal?.Invoke();
    public static void RaiseRainStopped() => RainStoppedGlobal?.Invoke();

    // ========= RAIN TENT (SHED) LOGIC =========
    // Hook this to the Button's OnClick()
    public void ToggleRainTent()
    {
        TryResolveShedReference();

        if (!shed)
        {
            Debug.LogWarning("[WeatherPanelUI] Shed not found. Assign it, or ensure a child named 'Shed' exists under Finished_Tent.");
            return;
        }

        shed.SetActive(!shed.activeSelf);
        SyncRainTentButton();      // also updates outline inside
    }

    private void SyncRainTentButton()
    {
        bool active = shed != null && shed.activeSelf;

        if (deployTentLabel)
        {
            deployTentLabel.text = active ? textDrop : textDeploy;
            deployTentLabel.color = labelColor;
        }

        if (deployTentButton)
        {
            var img = deployTentButton.GetComponent<Image>();
            if (img) img.color = active ? colorDrop : colorDeploy;
        }

        // Outline hint follows current state too
        UpdateDeployHintOutline();
    }

    private void TryResolveShedReference()
    {
        if (shed && shed.transform) return;

        Transform root = finishedTentRoot ? finishedTentRoot : transform.root;
        if (!root) return;

        var found = FindDeepChildByName(root, "Shed");
        if (found) shed = found.gameObject;
    }

    private void SetupDeployButtonOutline()
    {
        if (!deployTentButton) return;

        _deployOutline = deployTentButton.GetComponent<Outline>();
        if (!_deployOutline && autoAddOutline)
        {
            _deployOutline = deployTentButton.gameObject.AddComponent<Outline>();
        }

        if (_deployOutline)
        {
            _deployOutline.useGraphicAlpha = false; // solid outline
            _deployOutline.effectColor = outlineColor;
            _deployOutline.effectDistance = new Vector2(outlineThickness, -outlineThickness);
            _deployOutline.enabled = false; // start hidden
        }
    }

    /// <summary>
    /// Shows the thick red outline ONLY when:
    /// - rain icon is visible (or forced via OnRainStarted)
    /// - and the shed is NOT active (we are in 'Deploy Rain Tent' state)
    /// </summary>
    private void UpdateDeployHintOutline()
    {
        if (!_deployOutline && autoAddOutline) SetupDeployButtonOutline();
        if (!_deployOutline) return;

        bool isRainingIconVisible = rainIcon && rainIcon.gameObject.activeSelf;
        bool isShedActive = shed != null && shed.activeSelf;

        bool shouldShow = isRainingIconVisible && !isShedActive;

        _deployOutline.effectColor = outlineColor;
        _deployOutline.effectDistance = new Vector2(outlineThickness, -outlineThickness);
        _deployOutline.enabled = shouldShow;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (!root) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static Transform FindDeepChildByName(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;

            var inChild = FindDeepChildByName(c, name);
            if (inChild) return inChild;
        }
        return null;
    }
}
