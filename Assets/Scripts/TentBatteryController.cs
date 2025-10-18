using UnityEngine;
using TMPro;
using UnityEngine.Rendering.Universal; // Light2D

public class TentBatteryController : MonoBehaviour
{
    public static event System.Action<string, bool> BatteryDepletedChanged; // (tentId, depleted)
    [Header("Charging Rates (percent per MINUTE)")]
    [Range(0, 200)] public float dayChargePerMinute = 0.10f;
    [Range(0, 200)] public float nightChargePerMinute = 0.00f;
    [Range(0, 200)] public float idleDrainPerMinute = 0.20f;

    [Header("Glow drain (extra percent per MINUTE)")]
    [Range(0, 200)] public float glowDrainPerMinute = 0.40f;
    [Tooltip("Name of the Light2D that defines 'glow'. Leave empty to use ANY Light2D under the tent.")]
    public string glowLightName = "GlowLight";

    [Header("Party drain (extra percent per MINUTE)")]
    [Tooltip("Extra drain while Party mode driver is active on this tent.")]
    [Range(0, 200)] public float partyDrainPerMinute = 0.20f;

    [Header("AC drain (extra percent per MINUTE)")]
    [Range(0, 200)] public float acDrainPerMinute = 0.30f;

    [Header("AC detection")]
    [Tooltip("Relative path from tent root to the AC SpriteRenderer.")]
    public string acRelativePath = "Battery/AC";
    [Tooltip("If AC sprite alpha >= this, we consider AC ON.")]
    [Range(0f, 1f)] public float acOnAlphaThreshold = 0.95f;

    [Header("Day/Night detection")]
    [Tooltip("If LightFactor01 >= this value we consider it DAY; otherwise NIGHT.")]
    [Range(0f, 1f)] public float dayThreshold = 0.5f;

    [Header("Charge Shape")]
    public AnimationCurve lightToChargeCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.20f, 0f),
        new Keyframe(0.50f, 0.60f),
        new Keyframe(0.80f, 0.90f),
        new Keyframe(1f, 1f)
    );

    [Header("Presets (optional)")]
    public RatePreset preset = RatePreset.SlowDrain;
    public bool applyPresetNow = false;
    public enum RatePreset { Custom, Balanced, Friendly, Survival, SlowDrain }

    [Header("Start State")]
    public bool startAtZero = false;
    [Range(0, 100)] public float initialPercent = 100f;

    [Header("UI (drag from child Canvas)")]
    [SerializeField] private TextMeshProUGUI batteryText;

    [Header("UI Formatting")]
    [Range(0, 3)] public int uiDecimals = 0;

    [Header("Sprites (children of Battery)")]
    [SerializeField] private GameObject noBattery, veryLow, low, medium, high, veryHigh;

    [Header("Debug (read-only)")]
    [SerializeField] private float percent = 0f;
    [SerializeField] private int percentRounded = 0;

    // Live math mirrors (for Inspector)
    [SerializeField] private float dbgLight01 = 0f;
    [SerializeField] private float dbgDayWeight = 0f;
    [SerializeField] private float dbgChargePerMin = 0f;
    [SerializeField] private float dbgExtraGlow = 0f;
    [SerializeField] private float dbgExtraParty = 0f;
    [SerializeField] private float dbgExtraAC = 0f;           // NEW
    [SerializeField] private float dbgNetPerMin = 0f;

    [Header("Debug Logging")]
    public bool debugLogs = false;
    [Range(0.1f, 5f)] public float debugLogInterval = 1f;

    const string LOG = "[TentBattery] ";
    int lastShownPercent = -1;
    float _nextDebugTime = 0f;

    // caches
    Light2D[] _glowLights;
    Transform _tentRoot;
    string _tentId;
    bool _batteryDepleted = false;   // true όταν είμαστε στο 0% (rounded)
    bool _lastGlowState = false;

    // AC cache
    SpriteRenderer _acSprite;
    bool _lastACState = false;


    // ----------- Quick properties for UI -----------
    public float LightFactor01Now =>
        DayNightLightMeter.Instance ? DayNightLightMeter.Instance.LightFactor01 : 1f;

    public bool IsDayNow => LightFactor01Now >= dayThreshold;

    public float ChargePerMinuteNow
    {
        get
        {
            float light01 = LightFactor01Now;
            float dayWeight = lightToChargeCurve != null ? Mathf.Clamp01(lightToChargeCurve.Evaluate(light01)) : Mathf.Clamp01(light01);
            return Mathf.Lerp(nightChargePerMinute, dayChargePerMinute, dayWeight);
        }
    }

    public float IdleDrainPerMinuteNow => idleDrainPerMinute;
    public bool GlowOnNow => IsGlowOn();
    public bool PartyOnNow
    {
        get
        {
            if (_tentRoot == null) return false;
            var inst = _tentRoot.GetComponent<TentInstance2D>();
            if (!inst) return false;
            var d = inst.GetComponent<TentPartyDriver>();
            return d && d.enabled;
        }
    }

    public bool ACOnNow => IsACOn();                           // NEW
    public float GlowDrainPerMinuteNow => GlowOnNow ? glowDrainPerMinute : 0f;
    public float PartyDrainPerMinuteNow => PartyOnNow ? partyDrainPerMinute : 0f;
    public float ACDrainPerMinuteNow => ACOnNow ? acDrainPerMinute : 0f; // NEW

    public float TotalConsumptionPerMinuteNow =>
        IdleDrainPerMinuteNow + GlowDrainPerMinuteNow + PartyDrainPerMinuteNow + ACDrainPerMinuteNow; // NEW (+ AC)

    public float NetPerMinuteNow => ChargePerMinuteNow - TotalConsumptionPerMinuteNow;

    void Awake()
    {
        if (!noBattery) noBattery = transform.Find("No_Battery")?.gameObject;
        if (!veryLow) veryLow = transform.Find("Very_Low_Battery")?.gameObject;
        if (!low) low = transform.Find("Low_Battery")?.gameObject;
        if (!medium) medium = transform.Find("Medium_Battery")?.gameObject;
        if (!high) high = transform.Find("High_Battery")?.gameObject;
        if (!veryHigh) veryHigh = transform.Find("Very_High_Battery")?.gameObject;
        if (!batteryText) batteryText = GetComponentInChildren<TextMeshProUGUI>(true);

        var inst = GetComponentInParent<TentInstance2D>();
        _tentRoot = inst ? inst.transform : transform.root;
        _tentId = inst ? inst.TentInstanceId : null;
        CacheGlowLights();

        // cache AC sprite
        var acTf = _tentRoot ? _tentRoot.Find(acRelativePath) : null;
        _acSprite = acTf ? acTf.GetComponent<SpriteRenderer>() : null;

        percent = startAtZero ? 0f : Mathf.Clamp(initialPercent, 0f, 100f);
        percentRounded = Mathf.RoundToInt(percent);
        ForceApplyVisuals();

        _lastGlowState = IsGlowOn();
        _lastACState = ACOnNow;
    }

    void Update()
{
    // --- live rates ---
    float light01      = LightFactor01Now;
    float dayWeight    = lightToChargeCurve != null ? Mathf.Clamp01(lightToChargeCurve.Evaluate(light01)) : Mathf.Clamp01(light01);
    float chargePerMin = Mathf.Lerp(nightChargePerMinute, dayChargePerMinute, dayWeight);
    float extraGlow    = GlowOnNow  ? glowDrainPerMinute  : 0f;
    float extraParty   = PartyOnNow ? partyDrainPerMinute : 0f;
    float extraAC      = ACOnNow    ? acDrainPerMinute    : 0f;

    float netPerMinute   = chargePerMin - idleDrainPerMinute - extraGlow - extraParty - extraAC;
    float netPerSecond   = netPerMinute / 60f;
    float deltaThisFrame = netPerSecond * Time.deltaTime;

    // --- integrate battery ---
    percent = Mathf.Clamp(percent + deltaThisFrame, 0f, 100f);
    int shown = Mathf.RoundToInt(percent);
    percentRounded = shown;

    // --- battery depletion edge detection (==0%) -> force Party/Glow OFF + notify UI ---
    bool nowDepleted = (shown == 0);
    if (nowDepleted != _batteryDepleted)
    {
        _batteryDepleted = nowDepleted;

        if (_batteryDepleted)
        {
            // hard-off Party & Glow in the world when we hit 0%
            TurnOffPartyAndGlowWorld();
        }

        // notify panels (Energy/Color) for lock/unlock of buttons
        if (!string.IsNullOrEmpty(_tentId))
            TentBatteryController.BatteryDepletedChanged?.Invoke(_tentId, _batteryDepleted);
    }

    // --- debug mirrors (inspector) ---
    dbgLight01      = light01;
    dbgDayWeight    = dayWeight;
    dbgChargePerMin = chargePerMin;
    dbgExtraGlow    = extraGlow;
    dbgExtraParty   = extraParty;
    dbgExtraAC      = extraAC;
    dbgNetPerMin    = netPerMinute;

#if UNITY_EDITOR
    if (Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif

    // --- UI sprite & text refresh ---
    if (uiDecimals > 0 || shown != lastShownPercent)
    {
        lastShownPercent = shown;
        ApplyVisuals(shown);
    }

    // --- state change logs ---
    if (GlowOnNow != _lastGlowState)
    {
        _lastGlowState = GlowOnNow;
        if (debugLogs)
            Debug.Log($"{LOG}Glow state changed -> {(_lastGlowState ? "ON" : "OFF")} | glowDrain={(_lastGlowState ? glowDrainPerMinute : 0f):0.###}%/min");
    }

    bool acNow = ACOnNow;
    if (acNow != _lastACState)
    {
        _lastACState = acNow;
        if (debugLogs)
            Debug.Log($"{LOG}AC state changed -> {(acNow ? "ON" : "OFF")} | acDrain={(acNow ? acDrainPerMinute : 0f):0.###}%/min");
    }

    if (debugLogs && Time.time >= _nextDebugTime)
    {
        _nextDebugTime = Time.time + Mathf.Max(0.1f, debugLogInterval);
        Debug.Log($"{LOG}light01={light01:0.###} | dayWeight={dayWeight:0.###} | charge={chargePerMin:0.###} | idle={idleDrainPerMinute:0.###} | glow={(GlowOnNow ? glowDrainPerMinute : 0f):0.###} | party={(PartyOnNow ? partyDrainPerMinute : 0f):0.###} | AC={(ACOnNow ? acDrainPerMinute : 0f):0.###} | net={netPerMinute:0.###}%/min");
    }
}


    // --- Public helpers ---
    public int GetPercentRounded() => Mathf.RoundToInt(percent);
    public float GetPercent() => percent;
    public void SetPercent(float v) { percent = Mathf.Clamp(v, 0f, 100f); percentRounded = Mathf.RoundToInt(percent); ForceApplyVisuals(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (applyPresetNow)
        {
            ApplyPresetValues(preset);
            applyPresetNow = false;
        }

        percent = Mathf.Clamp(percent, 0f, 100f);
        percentRounded = Mathf.RoundToInt(percent);
        if (!Application.isPlaying) ForceApplyVisuals();
    }
#endif

    void ApplyPresetValues(RatePreset p)
    {
        switch (p)
        {
            case RatePreset.Balanced:
                dayChargePerMinute = 0.60f;
                nightChargePerMinute = 0.00f;
                idleDrainPerMinute = 0.04f;
                glowDrainPerMinute = 0.20f;
                partyDrainPerMinute = 0.10f;
                acDrainPerMinute = 0.20f;
                break;

            case RatePreset.Friendly:
                dayChargePerMinute = 0.80f;
                nightChargePerMinute = 0.05f;
                idleDrainPerMinute = 0.02f;
                glowDrainPerMinute = 0.12f;
                partyDrainPerMinute = 0.06f;
                acDrainPerMinute = 0.15f;
                break;

            case RatePreset.Survival:
                dayChargePerMinute = 0.40f;
                nightChargePerMinute = 0.00f;
                idleDrainPerMinute = 0.06f;
                glowDrainPerMinute = 0.30f;
                partyDrainPerMinute = 0.15f;
                acDrainPerMinute = 0.35f;
                break;

            case RatePreset.SlowDrain:
                dayChargePerMinute = 0.10f;
                nightChargePerMinute = 0.00f;
                idleDrainPerMinute = 0.20f;
                glowDrainPerMinute = 0.40f;
                partyDrainPerMinute = 0.20f;
                acDrainPerMinute = 0.30f;
                break;

            case RatePreset.Custom:
            default:
                break;
        }
    }

    void ForceApplyVisuals() => ApplyVisuals(Mathf.RoundToInt(percent));

    void ApplyVisuals(int p)
    {
        if (batteryText)
        {
            if (uiDecimals <= 0) batteryText.text = p + "%";
            else batteryText.text = GetPercent().ToString("F" + Mathf.Clamp(uiDecimals, 0, 3)) + "%";

            if (p == 0) batteryText.color = Color.black;
            else if (p <= 20) batteryText.color = Color.red;
            else if (p <= 40) batteryText.color = new Color(1f, 0.5f, 0f);
            else if (p <= 60) batteryText.color = Color.yellow;
            else if (p <= 80) batteryText.color = Color.cyan;
            else batteryText.color = Color.green;
        }

        SetActiveSafe(noBattery, p == 0);
        SetActiveSafe(veryLow, p >= 1 && p <= 20);
        SetActiveSafe(low, p >= 21 && p <= 40);
        SetActiveSafe(medium, p >= 41 && p <= 60);
        SetActiveSafe(high, p >= 61 && p <= 80);
        SetActiveSafe(veryHigh, p >= 81 && p <= 100);
    }

    static void SetActiveSafe(GameObject go, bool active)
    {
        if (go && go.activeSelf != active) go.SetActive(active);
    }

    // --- Glow detection ---
    void CacheGlowLights()
    {
        if (_tentRoot == null) { _glowLights = System.Array.Empty<Light2D>(); return; }

        var all = _tentRoot.GetComponentsInChildren<Light2D>(true);
        if (string.IsNullOrEmpty(glowLightName))
            _glowLights = all;
        else
        {
            var list = new System.Collections.Generic.List<Light2D>();
            foreach (var l in all) if (l && l.name == glowLightName) list.Add(l);
            _glowLights = list.ToArray();
        }
    }

    bool IsGlowOn()
    {
        if (_glowLights == null || _glowLights.Length == 0) return false;

        bool foundAny = false;
        foreach (var l in _glowLights)
        {
            if (!l) continue;
            foundAny = true;
            if (l.enabled && l.gameObject.activeInHierarchy) return true;
        }

        if (!foundAny)
        {
            CacheGlowLights();
            foreach (var l in _glowLights)
                if (l && l.enabled && l.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    // --- AC detection ---
    bool IsACOn()
    {
        if (!_acSprite) return false;

        // We keep the renderer enabled elsewhere; treat ON when alpha >= threshold
        return _acSprite.enabled
               && _acSprite.gameObject.activeInHierarchy
               && _acSprite.color.a >= acOnAlphaThreshold;
    }

    public void NotifyGlowState(bool glowOn)
    {
        if (debugLogs)
            Debug.Log($"{LOG}NotifyGlowState({glowOn}) called. (Auto-detect remains active.)");
    }

void TurnOffPartyAndGlowWorld()
{
    // Party off (καταστρέφει τον driver αν υπάρχει)
    if (_tentRoot)
    {
        var inst   = _tentRoot.GetComponent<TentInstance2D>();
        var driver = inst ? inst.GetComponent<TentPartyDriver>() : null;
        if (driver) Destroy(driver);
    }

    // Glow off (όλα τα matching Light2D)
    if (_glowLights == null || _glowLights.Length == 0) CacheGlowLights();
    foreach (var l in _glowLights)
    {
        if (!l) continue;
        l.enabled = false;
        if (l.gameObject.activeSelf) l.gameObject.SetActive(false);
    }
}
}
