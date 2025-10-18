using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal; // Light2D

public class EnergyPanelUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private GameObject panelRoot;                 // default: this.gameObject
    [SerializeField] private TentBatteryController battery;        // auto-found if null

    [Header("Glow & Party Buttons")]
    [SerializeField] private Button glowButton;
    [SerializeField] private Button partyButton;

    [Header("Navigation")]
    [SerializeField] private GameObject mainMenuPanel;             // auto-found if left null
    [SerializeField] private Button backToMenuButton;              // optional; auto-found if named "BackToMainMenu"
    [SerializeField] private Button closeXButton;                  // optional X button
    [SerializeField] private GameObject controlCenterPanel;        // optional Control Center panel

    [Header("Consumption (Values)")]
    [SerializeField] private TMP_Text idleValueText;               // Idle_Consumption_Value
    [SerializeField] private TMP_Text glowValueText;               // Glow_Consumption_Value
    [SerializeField] private TMP_Text partyValueText;              // Party_Consumption_Value
    [SerializeField] private TMP_Text acValueText;                 // AC_Consumption_Value
    [SerializeField] private TMP_Text totalValueText;              // Total_Consumption_Value

    [Header("Consumption (Labels) - optional")]
    [SerializeField] private TMP_Text idleLabelText;               // Idle_Consumption_Label
    [SerializeField] private TMP_Text glowLabelText;               // Glow_Consumption_Label
    [SerializeField] private TMP_Text partyLabelText;              // Party_Consumption_Label
    [SerializeField] private TMP_Text acLabelText;                 // AC_Consumption_Label
    [SerializeField] private TMP_Text totalLabelText;              // Total_Consumption_Label

    [Header("Production (Values)")]
    [SerializeField] private TMP_Text prodDayValueText;            // Production_Current_Consumption_Day_Value
    [SerializeField] private TMP_Text prodNightValueText;          // Production_Current_Consumption_Night_Value
    [SerializeField] private TMP_Text prodCurrentValueText;        // Production_Current_Consumption_Current_Value

    [Header("Production (Labels) - optional")]
    [SerializeField] private TMP_Text prodDayLabelText;            // Production_Current_Consumption_Day_Label
    [SerializeField] private TMP_Text prodNightLabelText;          // Production_Current_Consumption_Night_Label
    [SerializeField] private TMP_Text prodCurrentLabelText;        // Production_Current_Consumption_Current_Label (NEW)

    [Header("Extras (optional)")]
    [SerializeField] private TMP_Text netText;                     // optional net label
    [SerializeField] private TMP_Text lightFactorText;             // optional "Light: 0.78"

    [Header("Editable Inputs")]
    [SerializeField] private TMP_InputField partyInput;            // optional: edit party drain (%/min)
    [SerializeField] private TMP_InputField acInput;               // NEW: edit AC drain (%/min)

    [Header("Current & Battery")]
    [SerializeField] private TMP_Text currentConsumptionValueText; // Current_Consumption_Value  (net shown here)
    [SerializeField] private TMP_Text currentBatteryValueText;     // Current_Battery_Value       (xx%)

    [Header("Save Mode")]
    [SerializeField] private Button saveModeButton;                // "Save Mode Activated!" TMP Button
    [SerializeField] private TMP_Text saveModeButtonText;          // label inside the button
    [SerializeField, Range(0, 100)] private int saveModeThresholdPercent = 50;

    [Header("Formatting")]
    [Range(0, 3)] public int decimals = 2;

    [Header("Grey-out Settings")]
    [Tooltip("How much to dim inactive fields (0 = no dim, 1 = very dim)")]
    [Range(0f, 1f)] public float dimStrength = 0.75f;
    [Tooltip("Multiply alpha when dimming (e.g., 0.6 keeps 60% opacity).")]
    [Range(0f, 1f)] public float dimAlpha = 0.6f;

    // ---------- Label Lock (NEW) ----------
    [Header("Label Lock")]
    [Tooltip("Keep production labels exactly as set in the Inspector and auto-restore if changed at runtime.")]
    [SerializeField] private bool lockProductionLabels = true;

    private string _expectedProdDayLabel;
    private string _expectedProdNightLabel;
    private string _expectedProdCurrentLabel;

    // cache each text's original color so we can restore it after dimming
    private readonly Dictionary<TMP_Text, Color> _baseColor = new();

    // cached tent id/state
    private TentInstance2D _tentInstance;
    private string _tentId;
    private bool _saveModeOn = false;

    // battery 0% lock
    bool _batteryDepletedLock = false;

    void Awake()
    {
        if (!panelRoot) panelRoot = gameObject;
        if (!battery) battery = GetComponentInParent<TentBatteryController>(true);

        if (closeXButton)
        {
            closeXButton.onClick.RemoveAllListeners();
            closeXButton.onClick.AddListener(CloseEnergyPanel);
        }

        // --- Auto-find value fields by name if not assigned ---
        idleValueText = idleValueText ?? FindText("Idle_Consumption_Value");
        glowValueText = glowValueText ?? FindText("Glow_Consumption_Value");
        partyValueText = partyValueText ?? FindText("Party_Consumption_Value");
        acValueText = acValueText ?? FindText("AC_Consumption_Value");
        totalValueText = totalValueText ?? FindText("Total_Consumption_Value");
        prodDayValueText = prodDayValueText ?? FindText("Production_Current_Consumption_Day_Value");
        prodNightValueText = prodNightValueText ?? FindText("Production_Current_Consumption_Night_Value");
        prodCurrentValueText = prodCurrentValueText ?? FindText("Production_Current_Consumption_Current_Value");

        // --- Auto-find label fields (optional) ---
        idleLabelText = idleLabelText ?? FindText("Idle_Consumption_Label");
        glowLabelText = glowLabelText ?? FindText("Glow_Consumption_Label");
        partyLabelText = partyLabelText ?? FindText("Party_Consumption_Label");
        acLabelText = acLabelText ?? FindText("AC_Consumption_Label");
        totalLabelText = totalLabelText ?? FindText("Total_Consumption_Label");
        prodDayLabelText = prodDayLabelText ?? FindText("Production_Current_Consumption_Day_Label");
        prodNightLabelText = prodNightLabelText ?? FindText("Production_Current_Consumption_Night_Label");
        prodCurrentLabelText = prodCurrentLabelText ?? FindText("Production_Current_Consumption_Current_Label");

        // ---------- Cache expected compile-time texts (so we can restore them) ----------
        _expectedProdDayLabel     = prodDayLabelText     ? prodDayLabelText.text     : null;
        _expectedProdNightLabel   = prodNightLabelText   ? prodNightLabelText.text   : null;
        _expectedProdCurrentLabel = prodCurrentLabelText ? prodCurrentLabelText.text : null;

        // --- Navigation: auto-find main menu + back button ---
        if (!mainMenuPanel)
            mainMenuPanel = FindDeep(transform.root, "MainMenu_Panel")?.gameObject;

        if (!backToMenuButton)
            backToMenuButton = FindDeep(panelRoot.transform, "BackToMainMenu")?.GetComponent<Button>();

        if (backToMenuButton)
        {
            backToMenuButton.onClick.RemoveListener(GoBackToMainMenu);
            backToMenuButton.onClick.AddListener(GoBackToMainMenu);
        }

        // Party input (editable)
        if (partyInput == null)
        {
            var t = FindDeep(transform, "Party_Consumption_Value");
            if (t) partyInput = t.GetComponent<TMP_InputField>();
        }
        if (partyInput != null)
            partyInput.onEndEdit.AddListener(OnPartyEdited);

        // AC input (editable)
        if (acInput == null)
        {
            var t = FindDeep(transform, "AC_Consumption_Value");
            if (t) acInput = t.GetComponent<TMP_InputField>();
        }
        if (acInput != null)
            acInput.onEndEdit.AddListener(OnACEdited);

        // Save Mode Button auto-find
        if (!saveModeButton)
        {
            var t = FindDeep(transform, "Save Mode Activated!");
            if (t) saveModeButton = t.GetComponent<Button>();
        }
        if (!saveModeButtonText && saveModeButton)
            saveModeButtonText = saveModeButton.GetComponentInChildren<TMP_Text>(true);

        if (saveModeButton)
        {
            saveModeButton.onClick.RemoveAllListeners();
            saveModeButton.onClick.AddListener(OnSaveModeButtonClicked);
        }

        // capture base colors so greying can restore them
        CaptureBaseColors();

        // cache tent id
        if (battery)
        {
            _tentInstance = battery.GetComponentInParent<TentInstance2D>(true);
            _tentId = _tentInstance ? _tentInstance.TentInstanceId : null;
        }

        // IMPORTANT: Do not overwrite Inspector labels if lock is on
        if (!lockProductionLabels)
            ApplyProductionHeadings();
    }

    void OnEnable()
    {
        // --- Sync editable fields with current battery values ---
        if (partyInput && battery)
            partyInput.text = battery.partyDrainPerMinute.ToString("F" + decimals);

        if (acInput && battery)
            acInput.text = battery.acDrainPerMinute.ToString("F" + decimals);

        // --- Sync Save Mode from persistence store ---
        if (!string.IsNullOrEmpty(_tentId))
            _saveModeOn = TentStateStore.TryLoadSaveMode(_tentId, out var on) && on;

        // battery initial lock
        _batteryDepletedLock = battery && battery.GetPercentRounded() == 0;

        // subscribe to battery depleted event
        TentBatteryController.BatteryDepletedChanged += OnBatteryDepletedChanged;

        // --- Update button visuals and availability ---
        UpdateSaveModeButtonUI();
        UpdateSaveModeAvailability();

        // apply locks (save mode and/or battery 0%)
        ApplySaveModeLocks();

        // labels
        if (!lockProductionLabels)
            ApplyProductionHeadings();
        if (lockProductionLabels)
            TMPro.TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTMPTextChanged);
    }

    void OnDisable()
    {
        if (lockProductionLabels)
            TMPro.TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTMPTextChanged);

        TentBatteryController.BatteryDepletedChanged -= OnBatteryDepletedChanged;
    }

    void OnBatteryDepletedChanged(string tentId, bool depleted)
    {
        if (!string.IsNullOrEmpty(_tentId) && _tentId == tentId)
        {
            _batteryDepletedLock = depleted;
            ApplySaveModeLocks();
        }
    }

    void Update()
    {
        if (!panelRoot || !panelRoot.activeInHierarchy) return;

        UpdateValues();

        if (lockProductionLabels)
            EnforceLabelLock();

        UpdateSaveModeAvailability();
    }

    // -------- NAV --------
    public void GoBackToMainMenu()
    {
        if (panelRoot) panelRoot.SetActive(false);
        if (!mainMenuPanel)
            mainMenuPanel = FindDeep(transform.root, "MainMenu_Panel")?.gameObject;
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
    }

    void CloseEnergyPanel()
    {
        System.Action doClose = () =>
        {
            if (panelRoot && panelRoot.activeSelf)
                panelRoot.SetActive(false);

            if (mainMenuPanel) mainMenuPanel.SetActive(true);
            if (controlCenterPanel) controlCenterPanel.SetActive(true);

            var player = GameObject.FindWithTag("Player");
            if (player)
            {
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb) rb.constraints = RigidbodyConstraints2D.FreezeRotation;

                var move = player.GetComponent<MoveScript>();
                if (move) move.enabled = true;
            }

            var cc = controlCenterPanel ? controlCenterPanel.GetComponentInParent<ControlCenterUI>(true) : null;
            if (cc) cc.OnClickBack();
        };
        doClose();
    }

    // -------- DATA REFRESH --------
    void UpdateValues()
    {
        if (!battery)
        {
            Write(idleValueText, "—");
            Write(glowValueText, "—");
            Write(partyValueText, "—");
            Write(acValueText, "—");
            Write(totalValueText, "—");
            Write(prodDayValueText, "—");
            Write(prodNightValueText, "—");
            if (prodCurrentValueText) Write(prodCurrentValueText, "—");
            if (netText) Write(netText, "—");
            if (lightFactorText) Write(lightFactorText, "—");
            if (currentConsumptionValueText) Write(currentConsumptionValueText, "—");
            if (currentBatteryValueText) Write(currentBatteryValueText, "—");

            GreyPair(true, idleLabelText, idleValueText);
            GreyPair(true, glowLabelText, glowValueText);
            GreyPair(true, partyLabelText, partyValueText);
            GreyPair(true, acLabelText, acValueText);
            GreyPair(true, totalLabelText, totalValueText);
            GreyPair(true, prodDayLabelText, prodDayValueText);
            GreyPair(true, prodNightLabelText, prodNightValueText);
            return;
        }

        string fmt = "F" + Mathf.Clamp(decimals, 0, 3);

        float idle = battery.IdleDrainPerMinuteNow;
        float glow = battery.GlowDrainPerMinuteNow;
        float party = battery.PartyDrainPerMinuteNow;
        float ac = battery.ACDrainPerMinuteNow;
        float total = battery.TotalConsumptionPerMinuteNow;
        float curProd = battery.ChargePerMinuteNow;
        float net = battery.NetPerMinuteNow;
        float light = battery.LightFactor01Now;

        Write(idleValueText, $"{idle.ToString(fmt)} %/min");
        Write(glowValueText, $"{glow.ToString(fmt)} %/min");
        Write(partyValueText, $"{party.ToString(fmt)} %/min");
        Write(acValueText, $"{ac.ToString(fmt)} %/min");
        Write(totalValueText, $"{total.ToString(fmt)} %/min");

        float ratedDay = battery.dayChargePerMinute;
        Write(prodDayValueText, $"{ratedDay.ToString(fmt)} %/min");
        Write(prodNightValueText, $"{total.ToString(fmt)} %/min");
        if (prodCurrentValueText) Write(prodCurrentValueText, $"{curProd.ToString(fmt)} %/min");

        if (netText)
        {
            string sign = net >= 0f ? "+" : "";
            netText.text = $"{sign}{net.ToString(fmt)} %/min";
            netText.color = net >= 0f ? Color.green : Color.red;
        }
        if (currentConsumptionValueText)
        {
            string sign = net >= 0f ? "+" : "";
            currentConsumptionValueText.text = $"{sign}{net.ToString(fmt)} %/min";
            currentConsumptionValueText.color = net >= 0f ? Color.green : Color.red;
        }
        if (currentBatteryValueText) currentBatteryValueText.text = $"{battery.GetPercentRounded()}%";
        if (lightFactorText) lightFactorText.text = $"Light: {light.ToString("F2")}";

        GreyPair(!battery.GlowOnNow, glowLabelText, glowValueText);
        GreyPair(!battery.PartyOnNow, partyLabelText, partyValueText);
        GreyPair(!battery.ACOnNow, acLabelText, acValueText);
        if (partyInput && partyInput.textComponent) SetColorDimmed(partyInput.textComponent, !battery.PartyOnNow);
        if (acInput && acInput.textComponent) SetColorDimmed(acInput.textComponent, !battery.ACOnNow);

        GreyPair(false, prodDayLabelText, prodDayValueText);
        GreyPair(false, prodNightLabelText, prodNightValueText);
        GreyPair(false, idleLabelText, idleValueText);
        GreyPair(false, totalLabelText, totalValueText);
    }

    // --- Save Mode availability (show/hide when below threshold or already ON) ---
    void UpdateSaveModeAvailability()
    {
        if (!saveModeButton || battery == null) return;

        int pct = battery.GetPercentRounded();
        bool shouldShow = _saveModeOn || pct < saveModeThresholdPercent;

        if (saveModeButton.gameObject.activeSelf != shouldShow)
            saveModeButton.gameObject.SetActive(shouldShow);
    }

    // --- Save Mode button click -> toggle ---
    private void OnSaveModeButtonClicked()
    {
        if (string.IsNullOrEmpty(_tentId)) return;

        _saveModeOn = !_saveModeOn;
        TentStateStore.SaveSaveMode(_tentId, _saveModeOn);

        if (_tentInstance == null && battery)
            _tentInstance = battery.GetComponentInParent<TentInstance2D>(true);

        if (_tentInstance)
        {
            // Kill party driver
            var driver = _tentInstance.GetComponent<TentPartyDriver>();
            if (driver) UnityEngine.Object.Destroy(driver);

            // Force glow OFF on matching lights
            var lights = _tentInstance.GetComponentsInChildren<Light2D>(true);
            foreach (var l in lights)
            {
                if (!l) continue;
                if (!string.IsNullOrEmpty(battery.glowLightName) && l.name != battery.glowLightName) continue;
                if (_saveModeOn)
                {
                    l.enabled = false;
                    if (l.gameObject.activeSelf) l.gameObject.SetActive(false);
                }
            }

            // Restore applied (non-party) colors ONCE
            if (_saveModeOn)
                RestoreAppliedColorsToTent();

            // NEW: AC OFF visually & logically via alpha only (no disabling)
            ForceACOffForTent();
        }

        UpdateSaveModeButtonUI();
        UpdateSaveModeAvailability();
        ApplySaveModeLocks();
    }

    // --- Visuals for the Save Mode button ---
    void UpdateSaveModeButtonUI()
    {
        if (!saveModeButton) return;

        if (saveModeButtonText)
            saveModeButtonText.text = _saveModeOn ? "Save Mode: ON" : "Save Mode: OFF";

        var img = saveModeButton.GetComponent<Image>();
        if (img)
            img.color = _saveModeOn ? new Color(1f, 0.6f, 0.6f, 1f) : Color.white;

        saveModeButton.interactable = true;
    }

    // --- Inputs ---
    private void OnPartyEdited(string s)
    {
        if (!battery) return;
        if (float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            battery.partyDrainPerMinute = Mathf.Clamp(v, 0f, 200f);
            partyInput.text = battery.partyDrainPerMinute.ToString("F" + decimals,
                System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            partyInput.text = battery.partyDrainPerMinute.ToString("F" + decimals,
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void OnACEdited(string s)
    {
        if (!battery) return;
        if (float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            battery.acDrainPerMinute = Mathf.Clamp(v, 0f, 200f);
            acInput.text = battery.acDrainPerMinute.ToString("F" + decimals,
                System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            acInput.text = battery.acDrainPerMinute.ToString("F" + decimals,
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // ---------------- helpers ----------------
    private TMP_Text FindText(string name)
    {
        var root = panelRoot ? panelRoot.transform : transform;
        var t = FindDeep(root, name);
        return t ? t.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (!root) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static void Write(TMP_Text t, string s) { if (t) t.text = s; }

    // --- Greying support ---
    void CaptureBaseColors()
    {
        CacheColor(idleLabelText); CacheColor(idleValueText);
        CacheColor(glowLabelText); CacheColor(glowValueText);
        CacheColor(partyLabelText); CacheColor(partyValueText);
        CacheColor(acLabelText); CacheColor(acValueText);
        CacheColor(totalLabelText); CacheColor(totalValueText);
        CacheColor(prodDayLabelText); CacheColor(prodDayValueText);
        CacheColor(prodNightLabelText); CacheColor(prodNightValueText);
        CacheColor(netText); CacheColor(lightFactorText);
        CacheColor(currentConsumptionValueText);
        CacheColor(currentBatteryValueText);
        if (partyInput && partyInput.textComponent) CacheColor(partyInput.textComponent);
        if (acInput && acInput.textComponent) CacheColor(acInput.textComponent);
    }

    void CacheColor(TMP_Text t)
    {
        if (!t) return;
        if (!_baseColor.ContainsKey(t)) _baseColor[t] = t.color;
    }

    void GreyPair(bool grey, TMP_Text label, TMP_Text value)
    {
        SetColorDimmed(label, grey);
        SetColorDimmed(value, grey);
    }

    void SetColorDimmed(TMP_Text t, bool dim)
    {
        if (!t) return;
        CacheColor(t);
        var baseCol = _baseColor[t];
        if (!dim) { t.color = baseCol; return; }

        var target = Color.gray;
        target.a = baseCol.a * dimAlpha;
        t.color = Color.Lerp(baseCol, target, Mathf.Clamp01(dimStrength));
    }

    // Restore tent colors from paint store
    void RestoreAppliedColorsToTent()
    {
        if (_tentInstance == null || string.IsNullOrEmpty(_tentId)) return;

        var map = TentPaintStore.GetForTent(_tentId, createIfMissing: false);
        if (map == null) return;

        var partsById = new Dictionary<string, TentPart2D>();
        foreach (var p in _tentInstance.GetComponentsInChildren<TentPart2D>(true))
            if (p && !string.IsNullOrEmpty(p.partId)) partsById[p.partId] = p;

        foreach (var kv in map)
            if (partsById.TryGetValue(kv.Key, out var part)) part.ApplyColor(kv.Value);
    }

    // AC OFF = dim alpha only; never disable the renderer
    void ForceACOffForTent()
    {
        if (_tentInstance == null || battery == null) return;

        var acTf = _tentInstance.transform.Find(battery.acRelativePath);
        var sr = acTf ? acTf.GetComponent<SpriteRenderer>() : null;
        if (!sr) return;

        var c = sr.color;
        c.a = 102f / 255f; // dimmed OFF
        sr.color = c;

        if (!sr.enabled) sr.enabled = true;
    }

    // ---------- Headings helper ----------
    void ApplyProductionHeadings()
    {
        if (lockProductionLabels) return;

        if (prodDayLabelText)     prodDayLabelText.text     = "Max Day %";
        if (prodNightLabelText)   prodNightLabelText.text   = "Current Consumption";
        if (prodCurrentLabelText) prodCurrentLabelText.text = "Current Production";
    }

    // ---------- TMP change hook ----------
    void OnTMPTextChanged(UnityEngine.Object obj)
    {
        if (!lockProductionLabels) return;

        if (obj == prodCurrentLabelText && prodCurrentLabelText && _expectedProdCurrentLabel != null)
        {
            if (prodCurrentLabelText.text != _expectedProdCurrentLabel)
            {
                Debug.LogWarning($"[EnergyPanelUI] Someone changed 'Current Production' label at runtime. Restoring. Stack:\n{Environment.StackTrace}");
                prodCurrentLabelText.text = _expectedProdCurrentLabel;
            }
        }
        else if (obj == prodNightLabelText && prodNightLabelText && _expectedProdNightLabel != null)
        {
            if (prodNightLabelText.text != _expectedProdNightLabel)
            {
                Debug.LogWarning($"[EnergyPanelUI] Someone changed 'Night/Consumption' label at runtime. Restoring. Stack:\n{Environment.StackTrace}");
                prodNightLabelText.text = _expectedProdNightLabel;
            }
        }
        else if (obj == prodDayLabelText && prodDayLabelText && _expectedProdDayLabel != null)
        {
            if (prodDayLabelText.text != _expectedProdDayLabel)
            {
                Debug.LogWarning($"[EnergyPanelUI] Someone changed 'Day' label at runtime. Restoring. Stack:\n{Environment.StackTrace}");
                prodDayLabelText.text = _expectedProdDayLabel;
            }
        }
    }

    void EnforceLabelLock()
    {
        if (prodCurrentLabelText && _expectedProdCurrentLabel != null && prodCurrentLabelText.text != _expectedProdCurrentLabel)
            prodCurrentLabelText.text = _expectedProdCurrentLabel;

        if (prodNightLabelText && _expectedProdNightLabel != null && prodNightLabelText.text != _expectedProdNightLabel)
            prodNightLabelText.text = _expectedProdNightLabel;

        if (prodDayLabelText && _expectedProdDayLabel != null && prodDayLabelText.text != _expectedProdDayLabel)
            prodDayLabelText.text = _expectedProdDayLabel;
    }

    // --- SaveMode/Battery -> Locks for Glow/Party buttons ---
    private void ApplySaveModeLocks()
    {
        bool locked = _saveModeOn || _batteryDepletedLock;

        if (glowButton)
        {
            glowButton.interactable = !locked;
            var img = glowButton.GetComponent<Image>();
            if (img) img.color = locked ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
        }

        if (partyButton)
        {
            partyButton.interactable = !locked;
            var img = partyButton.GetComponent<Image>();
            if (img) img.color = locked ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
        }
    }
}
