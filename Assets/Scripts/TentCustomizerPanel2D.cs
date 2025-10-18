using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering.Universal;

// [ExecuteAlways]
public class TentCustomizerPanel2D : MonoBehaviour
{
    private sealed class PreviewCloneMarker : MonoBehaviour { }

    [Header("Root")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Button closeButton;
    [SerializeField] Button cancelButton;
    [SerializeField] Button applyButton;

    [Header("Panels (inside ControlCenter_Panel)")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject colorPanel;
    [SerializeField] GameObject controlCenterPanel;

    [Header("Parts List UI")]
    [SerializeField] RectTransform partsListContent;
    [SerializeField] Button partButtonPrefab;
    [SerializeField] float partRowHeight = 40f;
    [SerializeField] float partRowSpacing = 6f;

    [Header("Color Picker (HSV Wheel)")]
    [SerializeField] HSVWheelPicker colorWheel;
    [SerializeField] TMP_Text rLabel, gLabel, bLabel;

    [Header("Selection Info")]
    [SerializeField] TMP_Text selectedPartLabel;

    [Header("Preview")]
    [SerializeField] RawImage previewImage;
    [SerializeField] Camera previewCamera;
    [SerializeField] Transform previewRoot;
    [SerializeField] string previewLayerName = "TentPreview";
    [SerializeField] float previewPadding = 0.5f;

    [Header("Preview Output")]
    [SerializeField] bool useFixedRenderTexture = false;
    [SerializeField] RenderTexture fixedRenderTexture;

    [Header("Anchor")]
    [SerializeField] string anchorObjectName = "PlacementAnchor";
    [SerializeField] Vector3 anchorOffset = new Vector3(0f, 0f, -10f);

    [Header("Zoom")]
    [SerializeField, Range(0.05f, 2f)] float zoomFactor = 1.18f;

    [Header("Party Mode")]
    [SerializeField] Button partyModeButton;
    [SerializeField] TMP_Text partyModeLabel;
    [SerializeField, Min(0.01f)] float partyTickSeconds = 0.15f;

    [Header("Glow (Light2D)")]
    [SerializeField] Button glowButton;
    [SerializeField] TMP_Text glowLabel;
    [SerializeField] string glowLightName = "GlowLight";

    [Header("Highlight (preview only)")]
    [SerializeField] Color highlightColor = Color.red;
    [SerializeField, Min(0.001f)] float highlightWidth = 0.03f;
    [SerializeField, Range(6, 128)] int circleSegments = 48;
    [SerializeField] Material highlightMaterial;

    [Header("Navigation")]
    [SerializeField] Button backToMenuButton;
    [SerializeField] Button closeXButton;

    [Header("Button Colors")]
    [SerializeField] Color applyEnabledColor = new(0.25f, 0.8f, 0.25f, 1f);
    [SerializeField] Color applyDisabledColor = new(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] Color cancelEnabledColor = Color.white;
    [SerializeField] Color cancelDisabledColor = new(0.75f, 0.75f, 0.75f, 1f);

    [Header("Unsaved Changes Prompt")]
    [SerializeField] GameObject unsavedConfirmPanel;
    [SerializeField] Button unsavedYesButton;
    [SerializeField] Button unsavedNoButton;
    [SerializeField] TMP_Text unsavedMessageLabel;

    [Header("Persistence")]
    [SerializeField] bool persistBetweenRuns = false;

    [Header("Preview Camera Lock")]
    [SerializeField] bool lockToFirstComputedSize = true;
    [SerializeField] bool lockToFixedSize = false;
    [SerializeField] float fixedOrthoSize = 3.035975f;

    [Header("Diagnostics")]
    [SerializeField] bool debugLogs = false;

    // -------- runtime --------
    TentInstance2D _tentInstance;
    TentPart2D[] _parts;
    TentBatteryController _battery;             // NEW
    TentPart2D _selected;
    string _tentId;

    readonly Dictionary<string, Color> _originalColors = new();
    readonly Dictionary<string, Color> _workingColors = new();
    readonly Dictionary<string, Color> _baselineColors = new();
    readonly Dictionary<string, Color> _partyStartColors = new();
    Color _workColor = Color.white;

    GameObject _previewCloneRoot = null;
    Transform _anchor;
    int _previewLayer = -1;

    RenderTexture _rt;
    bool _ownsRT = false;

    bool _partyModeActive = false;
    bool _glowActive = false;

    bool _baselineParty = false;
    bool _baselineGlow = false;

    bool _saveModeActive = false;
    bool _batteryDepletedLock = false;          // NEW

    bool _suppressDirty = false;
    bool _loadingState = false;

    readonly List<Light2D> _worldGlowLights = new();
    readonly List<GameObject> _previewGlowLightGOs = new();
    readonly List<GameObject> _activeOutlineGOs = new();

    float _firstOrthoSize = -1f;

    System.Action _pendingContinuation = null;

    const string LOG = "[TentCustomizer] ";

    static readonly HashSet<GameObject> s_createdPreviewClones = new();

    // ---------- Unity lifecycle ----------
    void Awake()
    {
        WireButtons();
        if (panelRoot) panelRoot.SetActive(false);

        SafeAutoFindPreviewCamera();
        if (!previewImage) previewImage = GetComponentInChildren<RawImage>(true);
        if (previewImage) previewImage.color = Color.white;

        if (colorWheel) colorWheel.onColorChanged += OnWheelColorChanged;

        _previewLayer = LayerMask.NameToLayer(previewLayerName);
        if (_previewLayer < 0)
            Debug.LogError($"{LOG}Missing layer '{previewLayerName}'. Create it in Project Settings > Tags & Layers.");

        if (previewCamera) EnforcePreviewCameraSettings();

        if (!highlightMaterial)
        {
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (sh) highlightMaterial = new Material(sh);
        }

        SetButtonsInteractable(false);
    }

    void OnEnable() => StartCoroutine(EndOfFrameSanity());

    System.Collections.IEnumerator EndOfFrameSanity()
    {
        yield return null;
        EnforcePreviewCameraSettings();
        EnsureRenderTexture();
    }

    void OnDestroy()
    {
        if (colorWheel) colorWheel.onColorChanged -= OnWheelColorChanged;
        TentBatteryController.BatteryDepletedChanged -= HandleBatteryDepletedChanged; // NEW
        DestroyPreviewCloneNow();
        ReleaseRT();
    }

    void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled) return;
        EnsureRenderTexture();
    }

    // ---------- UI wiring ----------
    void WireButtons()
    {
        if (backToMenuButton)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(GoBackToMainMenu);
        }
        if (closeXButton)
        {
            closeXButton.onClick.RemoveAllListeners();
            closeXButton.onClick.AddListener(CloseColorPanel);
        }
        if (applyButton)
        {
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(ApplyAll);
        }
        if (cancelButton)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CancelChanges);
        }

        if (!cancelButton && closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CancelChanges);
        }

        if (partyModeButton)
        {
            partyModeButton.onClick.RemoveAllListeners();
            partyModeButton.onClick.AddListener(() => TogglePartyMode(userAction: true));
        }
        if (glowButton)
        {
            glowButton.onClick.RemoveAllListeners();
            glowButton.onClick.AddListener(() => SetGlowEnabled(!_glowActive, userAction: true));
        }

        if (unsavedConfirmPanel) unsavedConfirmPanel.SetActive(false);

        if (unsavedYesButton)
        {
            unsavedYesButton.onClick.RemoveAllListeners();
            unsavedYesButton.onClick.AddListener(() =>
            {
                ApplyAll();
                HideUnsavedConfirm();
                _pendingContinuation?.Invoke();
                _pendingContinuation = null;
            });
        }

        if (unsavedNoButton)
        {
            unsavedNoButton.onClick.RemoveAllListeners();
            unsavedNoButton.onClick.AddListener(() =>
            {
                CancelChanges();
                HideUnsavedConfirm();
                _pendingContinuation?.Invoke();
                _pendingContinuation = null;
            });
        }
    }

    // ---------- Open / Close ----------
    public void OpenForTent(GameObject tentGO)
    {
        if (!tentGO) return;

        ClearHighlights();
        UnbindRT();

        if (tentGO.GetComponentInParent<PreviewCloneMarker>() != null)
        {
            Debug.LogWarning($"{LOG}Refused to open with a preview clone as source.");
            return;
        }

        _tentInstance = tentGO.GetComponentInParent<TentInstance2D>();
        if (!_tentInstance) return;

        _tentId = _tentInstance.TentInstanceId;
        _battery = _tentInstance.GetComponentInChildren<TentBatteryController>(true); // NEW

        _suppressDirty = true;
        _loadingState = true;

        var allParts = tentGO.GetComponentsInChildren<TentPart2D>(true);
        var filtered = new List<TentPart2D>(allParts.Length);
        foreach (var p in allParts)
        {
            if (!p) continue;
            if (previewRoot && p.transform.IsChildOf(previewRoot)) continue;
            if (_previewLayer >= 0 && p.gameObject.layer == _previewLayer) continue;
            filtered.Add(p);
        }
        _parts = filtered.ToArray();

        _anchor = FindAnchor(tentGO.transform, anchorObjectName);

        BuildPartsList();

        if (!_previewCloneRoot) BuildPreviewCloneFromTent(tentGO);
        else
        {
            if (!_previewCloneRoot.activeSelf) _previewCloneRoot.SetActive(true);
            EnsureRenderTexture();
            EnforcePreviewCameraSettings();
            FramePreviewCameraToBounds(_previewCloneRoot);
            ApplyPreviewSizeLock();
        }

        BuildPreviewGlowLights();

        _originalColors.Clear();
        foreach (var p in _parts)
            if (!string.IsNullOrEmpty(p.partId))
                _originalColors[p.partId] = p.GetCurrentColor();

        _workingColors.Clear();
        foreach (var kv in _originalColors) _workingColors[kv.Key] = kv.Value;

        if (_parts.Length > 0) SelectPart(_parts[0]);

        CacheWorldGlowLights(_tentInstance.transform);
        _glowActive = AreWorldGlowLightsOn();
        _partyModeActive = IsPartyRunning();

        SetGlowEnabled(_glowActive, userAction: false);
        if (_partyModeActive) EnablePartyRuntime(noDirty: true);
        else DisablePartyRuntime(restoreSnapshot: false, noDirty: true);

        // Load Save Mode (per tent)
        _saveModeActive = !string.IsNullOrEmpty(_tentId)
                       && TentStateStore.TryLoadSaveMode(_tentId, out var on)
                       && on;

        // Battery lock initial + subscribe
        _batteryDepletedLock = _battery && _battery.GetPercentRounded() == 0;
        TentBatteryController.BatteryDepletedChanged -= HandleBatteryDepletedChanged; // safety
        TentBatteryController.BatteryDepletedChanged += HandleBatteryDepletedChanged;

        ApplySaveModeUIAndEnforcement(_saveModeActive, quiet: true);
        ApplyBatteryLocks(); // NEW

        if (panelRoot) panelRoot.SetActive(true);
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (colorPanel) colorPanel.SetActive(true);

        FramePreviewCameraToBounds(_previewCloneRoot ? _previewCloneRoot : tentGO);
        ApplyPreviewSizeLock();
        StartCoroutine(RefitPreviewNextFrame());

        CaptureBaselineFromWorking();
        _baselineParty = _partyModeActive && !_saveModeActive && !_batteryDepletedLock;
        _baselineGlow = _glowActive && !_saveModeActive && !_batteryDepletedLock;

        _loadingState = false;
        _suppressDirty = false;

        RecomputeDirty();
        SetButtonsInteractable(false);
    }

    void ApplyPreviewSizeLock()
    {
        if (!previewCamera) return;

        if (lockToFirstComputedSize)
        {
            if (_firstOrthoSize < 0f) _firstOrthoSize = previewCamera.orthographicSize; // capture once
            previewCamera.orthographicSize = _firstOrthoSize;
            return;
        }

        if (lockToFixedSize)
        {
            previewCamera.orthographicSize = fixedOrthoSize;
        }
    }

    System.Collections.IEnumerator RefitPreviewNextFrame()
    {
        yield return null;
        EnsureRenderTexture();
        EnforcePreviewCameraSettings();
        if (_previewCloneRoot) FramePreviewCameraToBounds(_previewCloneRoot);
        else if (_tentInstance) FramePreviewCameraToBounds(_tentInstance.gameObject);
        ApplyPreviewSizeLock();
    }

    void EnforcePreviewCameraSettings()
    {
        if (!previewCamera) return;

        // orthographic RT-only camera (does not touch size)
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0, 0, 0, 0);
        previewCamera.nearClipPlane = 0.03f;
        previewCamera.farClipPlane = 100f;

        if (_previewLayer >= 0)
            previewCamera.cullingMask = 1 << _previewLayer;

        var urp = previewCamera.GetUniversalAdditionalCameraData();
        if (urp != null)
        {
            urp.cameraStack.Clear();
            urp.renderPostProcessing = false;
            urp.renderShadows = false;
            urp.requiresDepthTexture = false;
            urp.requiresColorTexture = false;
        }
    }

    void CloseColorPanel()
    {
        Action doClose = () =>
        {
            DestroyPreviewCloneNow();

            if (colorPanel && colorPanel.activeSelf) colorPanel.SetActive(false);
            if (mainMenuPanel) mainMenuPanel.SetActive(true);
            if (controlCenterPanel) controlCenterPanel.SetActive(true);
            if (panelRoot) panelRoot.SetActive(true);

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

        if (HasUnsavedChanges())
        {
            ShowUnsavedConfirm(doClose, "Unsaved Changes. Want to save?");
            return;
        }

        doClose();
    }

    void GoBackToMainMenu()
    {
        if (HasUnsavedChanges())
        {
            ShowUnsavedConfirm(() =>
            {
                if (mainMenuPanel) mainMenuPanel.SetActive(true);
                if (colorPanel) colorPanel.SetActive(false);
            }, "Unsaved Changes. Want to save?");
            return;
        }

        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (colorPanel) colorPanel.SetActive(false);
    }

    // ---------- Apply / Cancel ----------
    void ApplyAll()
    {
        if (_tentInstance == null) { SetButtonsInteractable(false); return; }

        foreach (var p in _parts)
        {
            if (string.IsNullOrEmpty(p.partId)) continue;
            Color c = _workingColors.TryGetValue(p.partId, out var wc) ? wc : p.GetCurrentColor();
            p.ApplyColor(c);

            if (persistBetweenRuns)
                TentPaintStore.SetColor(_tentId, p.partId, c);
        }
        if (persistBetweenRuns) TentPaintStore.Save(_tentId);

        if (persistBetweenRuns)
        {
            TentStateStore.SaveParty(_tentId, _partyModeActive);
            TentStateStore.SaveGlow(_tentId, _glowActive);
        }

        CaptureBaselineFromWorking();
        _baselineParty = _partyModeActive;
        _baselineGlow = _glowActive;

        RecomputeDirty();
    }

    void CancelChanges()
    {
        if (_tentInstance == null) { SetButtonsInteractable(false); return; }

        _suppressDirty = true;

        RestoreBaselineToScene();

        if (_baselineParty) EnablePartyRuntime(noDirty: true);
        else DisablePartyRuntime(restoreSnapshot: false, noDirty: true);

        SetGlowEnabled(_baselineGlow, userAction: false);

        _suppressDirty = false;

        RecomputeDirty();
        SetButtonsInteractable(false);
    }

    // ---------- Dirty / Baseline ----------
    void CaptureBaselineFromWorking()
    {
        _baselineColors.Clear();
        foreach (var kv in _workingColors) _baselineColors[kv.Key] = kv.Value;
    }

    void RestoreBaselineToScene()
    {
        foreach (var p in _parts)
        {
            if (!p || string.IsNullOrEmpty(p.partId)) continue;
            if (_baselineColors.TryGetValue(p.partId, out var c))
            {
                _workingColors[p.partId] = c;
                p.ApplyColor(c);
            }
        }

        if (_selected) SyncWheelToColor(_selected.GetCurrentColor());
        UpdateRGBLabels(_selected ? _selected.GetCurrentColor() : Color.white);
    }

    void RecomputeDirty()
    {
        if (_suppressDirty || _loadingState) return;

        bool colorsDiff = ColorsDiffer(_workingColors, _baselineColors);
        bool partyDiff = _partyModeActive != _baselineParty;
        bool glowDiff = _glowActive != _baselineGlow;

        bool any = colorsDiff || partyDiff || glowDiff;
        SetButtonsInteractable(any);
    }

    static bool ColorsDiffer(Dictionary<string, Color> a, Dictionary<string, Color> b)
    {
        if (a.Count != b.Count) return true;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var c2)) return true;
            var c1 = kv.Value;
            if (Mathf.Abs(c1.r - c2.r) > 0.001f ||
                Mathf.Abs(c1.g - c2.g) > 0.001f ||
                Mathf.Abs(c1.b - c2.b) > 0.001f ||
                Mathf.Abs(c1.a - c2.a) > 0.001f) return true;
        }
        return false;
    }

    bool HasUnsavedChanges()
    {
        bool colorsDiff = ColorsDiffer(_workingColors, _baselineColors);
        bool partyDiff = _partyModeActive != _baselineParty;
        bool glowDiff = _glowActive != _baselineGlow;
        return colorsDiff || partyDiff || glowDiff;
    }

    void ShowUnsavedConfirm(Action continueAction, string msg = null)
    {
        _pendingContinuation = continueAction;
        if (unsavedMessageLabel && !string.IsNullOrEmpty(msg))
            unsavedMessageLabel.text = msg;
        if (unsavedConfirmPanel) unsavedConfirmPanel.SetActive(true);
    }

    void HideUnsavedConfirm()
    {
        if (unsavedConfirmPanel) unsavedConfirmPanel.SetActive(false);
    }

    // ---------- Party ----------
    void TogglePartyMode(bool userAction)
    {
        if (_saveModeActive || _batteryDepletedLock) return;

        if (_partyModeActive)
            DisablePartyRuntime(restoreSnapshot: true, noDirty: false);
        else
            EnablePartyRuntime(noDirty: false);

        RecomputeDirty();
    }

    void EnablePartyRuntime(bool noDirty)
    {
        if (_parts == null || _partyModeActive) { SetPartyButtonVisuals(); return; }
        _partyModeActive = true;

        _partyStartColors.Clear();
        foreach (var p in _parts)
            if (p && !string.IsNullOrEmpty(p.partId))
                _partyStartColors[p.partId] = p.GetCurrentColor();

        if (_tentInstance)
        {
            var driver = _tentInstance.GetComponent<TentPartyDriver>();
            if (!driver) driver = _tentInstance.gameObject.AddComponent<TentPartyDriver>();
            driver.parts = _parts;
            driver.tickSeconds = partyTickSeconds;
            driver.enabled = true;
            driver.Run();
        }

        SetPartyButtonVisuals();
        if (!noDirty) RecomputeDirty();
    }

    void DisablePartyRuntime(bool restoreSnapshot, bool noDirty)
    {
        if (_tentInstance)
        {
            var driver = _tentInstance.GetComponent<TentPartyDriver>();
            if (driver) Destroy(driver);
        }

        _partyModeActive = false;

        if (restoreSnapshot && _partyStartColors.Count > 0)
        {
            foreach (var p in _parts)
            {
                if (!p || string.IsNullOrEmpty(p.partId)) continue;
                if (_partyStartColors.TryGetValue(p.partId, out var c))
                {
                    p.ApplyColor(c);
                    _workingColors[p.partId] = c;
                }
            }
            if (_selected) SyncWheelToColor(_selected.GetCurrentColor());
            UpdateRGBLabels(_selected ? _selected.GetCurrentColor() : Color.white);
        }

        SetPartyButtonVisuals();
        if (!noDirty) RecomputeDirty();
    }

    void SetPartyButtonVisuals()
    {
        if (!partyModeButton) return;
        var label = partyModeLabel ? partyModeLabel : partyModeButton.GetComponentInChildren<TMP_Text>(true);
        if (label) label.text = _partyModeActive ? "Party: ON" : "Party: OFF";
        var img = partyModeButton.GetComponent<Image>();
        if (img) img.color = _partyModeActive ? new Color(0.85f, 1f, 0.85f, 1f) : Color.white;

        if ((_saveModeActive || _batteryDepletedLock) && img) img.color = new Color(1f, 0.6f, 0.6f, 1f);
        if (partyModeButton) partyModeButton.interactable = !_saveModeActive && !_batteryDepletedLock;
    }

    // ---------- Glow ----------
    void SetGlowEnabled(bool on, bool userAction)
    {
        if (_saveModeActive || _batteryDepletedLock) on = false;
        _glowActive = on;

        foreach (var go in _previewGlowLightGOs)
        {
            if (!go) continue;
            if (on && !go.activeSelf) go.SetActive(true);
            if (!on && go.activeSelf) go.SetActive(false);
            var l = go.GetComponent<Light2D>();
            if (l) l.enabled = on;
        }

        foreach (var l in _worldGlowLights)
        {
            if (!l) continue;
            if (on && !l.gameObject.activeSelf) l.gameObject.SetActive(true);
            if (!on && l.gameObject.activeSelf) l.gameObject.SetActive(false);
            l.enabled = on;
        }

        RefreshGlowUI();
        if (!_suppressDirty) RecomputeDirty();
    }

    void RefreshGlowUI()
    {
        if (!glowButton) return;
        var label = glowLabel ? glowLabel : glowButton.GetComponentInChildren<TMP_Text>(true);
        if (label) label.text = _glowActive ? "Glow: ON" : "Glow: OFF";
        var img = glowButton.GetComponent<Image>();
        if (img) img.color = _glowActive ? new Color(1f, 0.95f, 0.8f, 1f) : Color.white;

        if ((_saveModeActive || _batteryDepletedLock) && img) img.color = new Color(1f, 0.6f, 0.6f, 1f);
        if (glowButton) glowButton.interactable = !_saveModeActive && !_batteryDepletedLock;
    }

    // Scan only the *real* tent; exclude preview/layer
    void CacheWorldGlowLights(Transform tentRoot)
    {
        _worldGlowLights.Clear();
        if (!tentRoot) return;

        var scene = tentRoot.gameObject.scene;
        int previewLayer = _previewLayer;
        var dedup = new HashSet<Light2D>();

        var all = tentRoot.GetComponentsInChildren<Light2D>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var l = all[i];
            if (!l) continue;

            if (!l.gameObject.scene.IsValid() || l.gameObject.scene != scene) continue;
            if (previewLayer >= 0 && l.gameObject.layer == previewLayer) continue;
            if (previewRoot && l.transform.IsChildOf(previewRoot)) continue;
            if (!string.IsNullOrEmpty(glowLightName) && l.name != glowLightName) continue;

            if (dedup.Add(l)) _worldGlowLights.Add(l);
        }

        if (debugLogs) Debug.Log($"{LOG}Found world lights: {_worldGlowLights.Count} (filter: {(string.IsNullOrEmpty(glowLightName) ? "<all>" : glowLightName)})");
    }

    bool IsPartyRunning()
    {
        var driver = _tentInstance ? _tentInstance.GetComponent<TentPartyDriver>() : null;
        return driver && driver.enabled;
    }

    bool AreWorldGlowLightsOn()
    {
        foreach (var l in _worldGlowLights)
            if (l && l.enabled && l.gameObject.activeInHierarchy)
                return true;
        return false;
    }

    // ---------- Parts / UI ----------
    void SelectPart(TentPart2D part)
    {
        _selected = part;
        var label = string.IsNullOrEmpty(part.partId) ? part.name : part.partId;
        if (selectedPartLabel) selectedPartLabel.text = $"Selected:\n{label}";

        Color c;
        if (!_workingColors.TryGetValue(part.partId, out c))
            c = part.GetCurrentColor();

        _workColor = c;
        SyncWheelToColor(c);
        _selected.ApplyColor(c);
        UpdateRGBLabels(c);

        BuildOutlineForSelected(_selected);
    }

    void OnWheelColorChanged(Color c)
    {
        _workColor = c;
        if (_selected)
        {
            _workingColors[_selected.partId] = c;
            _selected.ApplyColor(c);
        }
        UpdateRGBLabels(_selected ? _selected.GetCurrentColor() : c);
        RecomputeDirty();
    }

    void SyncWheelToColor(Color c)
    {
        if (colorWheel) colorWheel.SetColor(c, invokeEvent: false);
    }

    void UpdateRGBLabels(Color c)
    {
        if (rLabel) rLabel.text = $"R: {Mathf.RoundToInt(c.r * 255f)}";
        if (gLabel) gLabel.text = $"G: {Mathf.RoundToInt(c.g * 255f)}";
        if (bLabel) bLabel.text = $"B: {Mathf.RoundToInt(c.b * 255f)}";
    }

    // ---------- Parts list ----------
    void BuildPartsList()
    {
        if (!partsListContent || !partButtonPrefab) return;

        EnsurePartsListLayout();

        for (int i = partsListContent.childCount - 1; i >= 0; i--)
            Destroy(partsListContent.GetChild(i).gameObject);

        var unique = new Dictionary<string, TentPart2D>();
        foreach (var p in _parts)
        {
            if (!p) continue;
            string id = string.IsNullOrEmpty(p.partId) ? p.name : p.partId;
            if (!unique.ContainsKey(id)) unique[id] = p;
        }

        foreach (var kv in unique)
        {
            string id = kv.Key;
            TentPart2D rep = kv.Value;

            var btn = Instantiate(partButtonPrefab, partsListContent);
            btn.transform.localScale = Vector3.one;

            var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = partRowHeight;
            le.flexibleWidth = 1;

            var label = btn.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = id;

            btn.onClick.AddListener(() => SelectPart(rep));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(partsListContent);
    }

    // ---------- Outline (preview) ----------
    void BuildOutlineForSelected(TentPart2D part)
    {
        ClearHighlights();
        if (part == null) return;

        var sr = part.GetComponentInChildren<SpriteRenderer>(true);
        int sortLayer = sr ? sr.sortingLayerID : 0;
        int sortOrder = sr ? sr.sortingOrder + 100 : 10000;

        var cols = part.GetComponentsInChildren<Collider2D>(true);
        int countSegments = 0;

        foreach (var c in cols)
        {
            if (!c.enabled) continue;

            if (c is PolygonCollider2D poly)
            {
                for (int i = 0; i < poly.pathCount; i++)
                {
                    Vector2[] path = poly.GetPath(i);
                    if (path != null && path.Length > 0)
                    {
                        CreateLineFromPreviewLocalPoints(
                            PreviewLocalPoints(poly.transform, path, true), true, sortLayer, sortOrder);
                        countSegments += path.Length;
                    }
                }
            }
            else if (c is EdgeCollider2D edge)
            {
                var pts = edge.points;
                CreateLineFromPreviewLocalPoints(
                    PreviewLocalPoints(edge.transform, pts, false), false, sortLayer, sortOrder);
                countSegments += pts.Length;
            }
            else if (c is BoxCollider2D box)
            {
                Vector2 half = box.size * 0.5f;
                Vector2 off = box.offset;
                Vector2[] p =
                {
                    new(off.x - half.x, off.y - half.y),
                    new(off.x + half.x, off.y - half.y),
                    new(off.x + half.x, off.y + half.y),
                    new(off.x - half.x, off.y + half.y)
                };
                CreateLineFromPreviewLocalPoints(
                    PreviewLocalPoints(box.transform, p, true), true, sortLayer, sortOrder);
                countSegments += 4;
            }
            else if (c is CircleCollider2D circle)
            {
                float r = circle.radius;
                Vector2 off = circle.offset;
                var localPts = new Vector3[circleSegments + 1];
                for (int i = 0; i <= circleSegments; i++)
                {
                    float t = (i / (float)circleSegments) * Mathf.PI * 2f;
                    Vector2 local = off + new Vector2(Mathf.Cos(t) * r, Mathf.Sin(t) * r);
                    var w = circle.transform.TransformPoint(local);
                    var l = previewRoot ? previewRoot.InverseTransformPoint(w) : w;
                    localPts[i] = new Vector3(l.x, l.y, 0f);
                }
                CreateLineFromPreviewLocalPoints(localPts, true, sortLayer, sortOrder);
                countSegments += circleSegments;
            }
        }

        if (debugLogs)
            Debug.Log($"{LOG}Outline built for '{part?.partId}' with {cols.Length} collider(s), {countSegments} points.");
    }

    Vector3[] PreviewLocalPoints(Transform colliderTransform, Vector2[] localPts, bool loop)
    {
        int len = localPts.Length + (loop ? 1 : 0);
        var arr = new Vector3[len];
        for (int i = 0; i < localPts.Length; i++)
        {
            var w = colliderTransform.TransformPoint(localPts[i]);
            var l = previewRoot ? previewRoot.InverseTransformPoint(w) : w;
            arr[i] = new Vector3(l.x, l.y, 0f);
        }
        if (loop && localPts.Length > 0) arr[len - 1] = arr[0];
        return arr;
    }

    void CreateLineFromPreviewLocalPoints(Vector3[] pts, bool loop, int sortingLayerID, int sortingOrder)
    {
        var go = new GameObject("[PreviewOutline]");
        _activeOutlineGOs.Add(go);
        if (_previewLayer >= 0) go.layer = _previewLayer;

        if (previewRoot) go.transform.SetParent(previewRoot, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);

        lr.widthMultiplier = highlightWidth;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;

        if (highlightMaterial) lr.material = highlightMaterial;
        lr.startColor = lr.endColor = highlightColor;

        lr.sortingLayerID = sortingLayerID;
        lr.sortingOrder = sortingOrder;
    }

    void ClearHighlights()
    {
        for (int i = 0; i < _activeOutlineGOs.Count; i++)
            if (_activeOutlineGOs[i]) Destroy(_activeOutlineGOs[i]);
        _activeOutlineGOs.Clear();
    }

    // ---------- Utility ----------
    void SafeAutoFindPreviewCamera()
    {
        if (previewCamera) return;

        var cams = GetComponentsInChildren<Camera>(true);
        foreach (var c in cams)
        {
            if (c.transform.IsChildOf(transform))
            {
                previewCamera = c;
                return;
            }
        }

        foreach (var c in cams)
        {
            if (c.gameObject.name.Contains("PreviewCamera"))
            {
                previewCamera = c;
                return;
            }
        }
    }

    Transform FindAnchor(Transform root, string childName)
    {
        if (!root) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (!go) return;
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }

    // ----- Save Mode handling -----
    void HandleSaveModeChanged(string tentId, bool on)
    {
        if (_tentInstance == null || _tentId != tentId) return;
        ApplySaveModeUIAndEnforcement(on, quiet: false);
        ApplyBatteryLocks();
    }

    void ApplySaveModeUIAndEnforcement(bool on, bool quiet)
    {
        _saveModeActive = on;
        _suppressDirty = true;

        if (on)
        {
            DisablePartyRuntime(restoreSnapshot: true, noDirty: true);
            SetGlowEnabled(false, userAction: false);

            _baselineParty = false;
            _baselineGlow = false;
        }

        SetPartyButtonVisuals();
        RefreshGlowUI();

        _suppressDirty = false;
        if (!quiet) RecomputeDirty();
    }

    // ----- Battery depleted handling (NEW) -----
    void HandleBatteryDepletedChanged(string tentId, bool depleted)
    {
        if (_tentInstance == null || _tentId != tentId) return;

        _batteryDepletedLock = depleted;

        if (_batteryDepletedLock)
        {
            // Force-off modes visually & logically
            DisablePartyRuntime(restoreSnapshot: true, noDirty: true);
            SetGlowEnabled(false, userAction: false);
            _baselineParty = false;
            _baselineGlow = false;
        }

        ApplyBatteryLocks();
        RecomputeDirty();
    }

    void ApplyBatteryLocks()
    {
        // Buttons visuals (reuse the per-button methods)
        SetPartyButtonVisuals();
        RefreshGlowUI();
    }

    // ----- clone lifecycle -----
    void DestroyPreviewCloneNow()
    {
        ClearHighlights();

        // unsubscribe battery event to avoid leaks when panel closes
        TentBatteryController.BatteryDepletedChanged -= HandleBatteryDepletedChanged;

        if (_previewCloneRoot)
        {
            s_createdPreviewClones.Remove(_previewCloneRoot);
            Destroy(_previewCloneRoot);
            _previewCloneRoot = null;
        }

        ReleaseRT();
    }

    public static void TogglePreviewClone(bool active, string nameStartsWith = null)
    {
        foreach (var go in s_createdPreviewClones)
            if (go) go.SetActive(active);
    }

    void SetButtonsInteractable(bool on)
    {
        if (applyButton) applyButton.interactable = on;
        if (cancelButton) cancelButton.interactable = on;

        void Paint(Button b, Color enabledCol, Color disabledCol)
        {
            if (!b) return;
            var img = b.GetComponent<Image>();
            if (img) img.color = on ? enabledCol : disabledCol;

            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.disabledColor = Color.white;
            b.colors = cb;
        }

        Paint(applyButton, applyEnabledColor, applyDisabledColor);
        Paint(cancelButton, cancelEnabledColor, cancelDisabledColor);
    }

    void ReleaseRT()
    {
        UnbindRT();
        if (_ownsRT && _rt)
        {
            _rt.Release();
            Destroy(_rt);
        }
        _rt = null;
        _ownsRT = false;
    }

    void EnsurePartsListLayout()
    {
        var vlg = partsListContent.GetComponent<VerticalLayoutGroup>();
        if (!vlg) vlg = partsListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = partRowSpacing;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = partsListContent.GetComponent<ContentSizeFitter>();
        if (!fitter) fitter = partsListContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void FramePreviewCameraToBounds(GameObject targetRoot)
    {
        if (!targetRoot || !previewCamera || !previewImage) return;

        var srs = targetRoot.GetComponentsInChildren<Renderer>(true);
        if (srs.Length == 0) return;

        Bounds b = srs[0].bounds;
        for (int i = 1; i < srs.Length; i++) b.Encapsulate(srs[i].bounds);

        var cam = previewCamera;
        float aspect =
            cam.targetTexture ? (float)cam.targetTexture.width / cam.targetTexture.height :
            Mathf.Max(0.0001f, previewImage.rectTransform.rect.width /
                      Mathf.Max(1f, previewImage.rectTransform.rect.height));

        float width = b.size.x + previewPadding;
        float height = b.size.y + previewPadding;

        float orthoSize = Mathf.Max(height * 0.5f, (width * 0.5f) / aspect);
        cam.orthographicSize = orthoSize * Mathf.Clamp(zoomFactor, 0.05f, 2f);
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 100f;

        float z = cam.transform.position.z != 0f ? cam.transform.position.z : -10f;
        Vector3 targetPos = (_anchor != null)
            ? _anchor.position + anchorOffset
            : new Vector3(b.center.x, b.center.y, z);

        cam.transform.position = new Vector3(targetPos.x, targetPos.y, z);
    }

    void UnbindRT()
    {
        if (previewCamera) previewCamera.targetTexture = null;
        if (previewImage) previewImage.texture = null;
    }

    void EnsureRenderTexture()
    {
        if (!previewImage || !previewCamera) return;

        RenderTexture desiredRT = null;

        if (useFixedRenderTexture)
        {
            if (!fixedRenderTexture)
            {
                Debug.LogError($"{LOG}useFixedRenderTexture=true but no RenderTexture assigned.");
                return;
            }
            desiredRT = fixedRenderTexture;
            _ownsRT = false;
        }
        else
        {
            var rect = previewImage.rectTransform.rect;
            float scale = previewImage.canvas ? previewImage.canvas.scaleFactor : 1f;
            int w = Mathf.Max(128, Mathf.RoundToInt(rect.width * scale));
            int h = Mathf.Max(128, Mathf.RoundToInt(rect.height * scale));

            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                ReleaseRT();

                _rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32)
                {
                    name = "TentPreview_RT",
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _rt.Create();
                _ownsRT = true;
            }
            desiredRT = _rt;
        }

        if (previewCamera.targetTexture != desiredRT) previewCamera.targetTexture = desiredRT;
        if (previewImage.texture != desiredRT) previewImage.texture = desiredRT;
        if (previewImage.material != null) previewImage.material = null;

        previewCamera.enabled = true;
    }

    void BuildPreviewCloneFromTent(GameObject tentRoot)
    {
        if (!previewCamera || !previewImage) return;

        if (previewRoot && previewCamera.transform.parent != previewRoot)
            previewCamera.transform.SetParent(previewRoot, false);

        previewCamera.transform.localRotation = Quaternion.identity;
        EnforcePreviewCameraSettings();

        _previewCloneRoot = Instantiate(tentRoot, previewRoot ? previewRoot : transform);
        _previewCloneRoot.name = "[PreviewClone] " + tentRoot.name;
        _previewCloneRoot.AddComponent<PreviewCloneMarker>();
        s_createdPreviewClones.Add(_previewCloneRoot);

        // disable any cameras under the clone
        foreach (var c in _previewCloneRoot.GetComponentsInChildren<Camera>(true))
        {
            c.enabled = false;
            var extra = c.GetUniversalAdditionalCameraData();
            if (extra) extra.enabled = false;
        }

        // disable any Canvas in the clone
        foreach (var cnv in _previewCloneRoot.GetComponentsInChildren<Canvas>(true))
            cnv.gameObject.SetActive(false);

        // disable scripts except data holders
        var mbs = _previewCloneRoot.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in mbs)
        {
            if (!mb) continue;
            if (mb is TentPart2D) continue;
            if (mb is TentInstance2D) continue;
            mb.enabled = false;
        }

        // lights in clone off
        var lights = _previewCloneRoot.GetComponentsInChildren<Light2D>(true);
        foreach (var l in lights) if (l) { l.enabled = false; l.gameObject.SetActive(false); }

        // colliders & rigidbodies off
        foreach (var col in _previewCloneRoot.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        foreach (var rb in _previewCloneRoot.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;

        if (_previewLayer >= 0) SetLayerRecursively(_previewCloneRoot, _previewLayer);

        EnsureRenderTexture();
        EnforcePreviewCameraSettings();
        FramePreviewCameraToBounds(_previewCloneRoot);
        ApplyPreviewSizeLock();
    }
    
    void BuildPreviewGlowLights()
    {
        foreach (var go in _previewGlowLightGOs) if (go) Destroy(go);
        _previewGlowLightGOs.Clear();

        if (!_previewCloneRoot) return;
        var lights = _previewCloneRoot.GetComponentsInChildren<Light2D>(true);
        foreach (var l in lights)
        {
            if (!l) continue;
            if (!string.IsNullOrEmpty(glowLightName) && l.name != glowLightName) continue;
            l.enabled = false;
            l.gameObject.SetActive(false);
            _previewGlowLightGOs.Add(l.gameObject);
        }
    }
}
