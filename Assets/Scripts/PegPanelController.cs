using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;

public class PegPanelController : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("UI Refs")]
    [SerializeField] RectTransform stakeImage;
    [SerializeField] Button lockAngleButton;
    [SerializeField] Slider pressureSlider;
    [SerializeField] Button hitButton;
    [SerializeField] Button nextPegButton;
    [SerializeField] TMP_Text angleText;
    [SerializeField] TMP_Text pressureText;
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] Image[] pegDots;

    [Header("NEW: Cancel")]
    [SerializeField] Button cancelButton;                 // <-- assign your Close/Cancel button here
    [SerializeField] GameObject canvas_TicketPlacement;   // <-- assign Canvas_TicketPlacement here (root GO)

    [Header("Angle Rules")]
    [Range(-180,180)] public float startingAngleDeg = 45f;
    [Range(-180,180)] public float targetAngleDeg = 0f;
    public float angleToleranceDeg = 3f;

    [Header("Pressure Rules")]
    [Range(0f,1f)] public float pressureTarget = 0.30f; 
    [Range(0f,1f)] public float pressureTolerance = 0.00f;

    [Header("Flow")]
    public int totalPegs = 4;

    [Header("Button Visuals")]
    [SerializeField] Image   lockBtnBackground;
    [SerializeField] TMP_Text lockBtnLabel;
    [SerializeField] Color   lockBtnColor_NotReady = new Color(0.85f, 0.1f, 0.1f);
    [SerializeField] Color   lockBtnColor_Ready    = new Color(0.1f, 0.7f, 0.2f);
    [SerializeField] Color   lockBtnTextColor      = Color.white;
    [SerializeField, Tooltip("Alpha used when a button is intentionally faded/disabled.")]
    float disabledAlpha = 0.45f;

    int   currentPegIndex = 0;
    bool  angleLocked = false;
    bool  hitConfirmed = false;
    float currentAngle;

    [Header("Angle Snapping / Visual")]
    [SerializeField] float snapStepDeg = 25f;
    [SerializeField] float visualZeroOffsetDeg = 0f;

    [Header("Completion UI / Tent Swap")]
    [SerializeField] GameObject completionPanel;
    [SerializeField] Button completionCloseButton;
    [SerializeField] GameObject finishedTentPrefab;
    GameObject targetTentInstance;

    // --- Hit button visuals (cached)
    TMP_Text hitBtnLabel;
    Graphic  hitBtnBackground;

    [Header("Hit Animation")]
    [SerializeField] float hitDownPixels = 20f;
    [SerializeField] float hitDownTime  = 0.08f;
    [SerializeField] float hitUpTime    = 0.12f;

    Vector2 stakeBasePos;
    bool isHitAnimating = false;

    CanvasGroup _cg;

    [Header("Player Placement After Build")]
    [SerializeField] Transform player;
    [SerializeField] Vector2   spawnOffset = new Vector2(0f, -0.1f);

    [Header("Optional player control refs")]
    [SerializeField] MonoBehaviour playerMovementScript;
    [SerializeField] Rigidbody2D  playerRb;

    [Header("Randomize Start")]
    [SerializeField] bool  randomizeStartAngle   = true;
    [SerializeField] float startAngleMin         = -90f;
    [SerializeField] float startAngleMax         =  90f;
    [SerializeField] bool  snapStartAngleToStep  = true;

    [SerializeField] bool  randomizeStartPressure = true;
    [SerializeField, Range(0f,1f)] float startPressureMin  = 0f;
    [SerializeField, Range(0f, 1f)] float startPressureMax = 1f;

    [SerializeField, Range(0.001f, 0.1f)]
    float uiPressureStep = 0.01f;

    float Snap01(float v, float step) => Mathf.Round(v / step) * step;

    void SetNextButtonLabel(string text)
    {
        if (!nextPegButton) return;
        var tmp = nextPegButton.GetComponentInChildren<TMP_Text>(true);
        if (tmp) tmp.text = text;
        else
        {
            var legacy = nextPegButton.GetComponentInChildren<Text>(true);
            if (legacy) legacy.text = text;
        }
    }

    void Awake()
    {
        if (lockAngleButton) lockAngleButton.onClick.AddListener(OnLockAngle);
        if (hitButton)       hitButton.onClick.AddListener(OnHitStake);
        if (nextPegButton)   nextPegButton.onClick.AddListener(OnNextPeg);

        // NEW: wire Cancel
        if (cancelButton)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CancelPegInstallation);
        }

        if (hitButton)
        {
            hitBtnBackground = hitButton.targetGraphic;
            hitBtnLabel = hitButton.GetComponentInChildren<TMP_Text>(true);
            hitButton.transition = Selectable.Transition.None;
        }
        if (lockAngleButton) lockAngleButton.transition = Selectable.Transition.None;

        if (pressureSlider)
        {
            pressureSlider.wholeNumbers = false;
            pressureSlider.minValue = 0f;
            pressureSlider.maxValue = 1f;
            pressureSlider.onValueChanged.AddListener(v =>
            {
                float s = Snap01(v, uiPressureStep);
                if (Mathf.Abs(s - v) > 0.0001f) pressureSlider.SetValueWithoutNotify(s);
                if (pressureText) pressureText.text = $"Pressure: {Mathf.RoundToInt(s * 100f)}%";
                UpdateHitButtonVisual();
            });
        }

        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (completionPanel) completionPanel.SetActive(false);
        ResetForPeg();
        RefreshUI();
        UpdateLockButtonVisual();
        UpdateHitButtonVisual();
        ShowStakeUI(true);
        DisablePlayerControl();
    }

    public void SetTargetTent(GameObject tentGO) => targetTentInstance = tentGO;

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (angleLocked || !stakeImage) return;

        Vector2 center = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, stakeImage.position);
        Vector2 mouse  = eventData.position;
        Vector2 dir    = mouse - center;

        float rawAngle = Vector2.SignedAngle(Vector2.up, dir);
        currentAngle = Mathf.Round(rawAngle / snapStepDeg) * snapStepDeg;
        stakeImage.localRotation = Quaternion.Euler(0, 0, currentAngle + visualZeroOffsetDeg);

        if (angleText) angleText.text = $"Angle: {Mathf.RoundToInt(Norm180(currentAngle))}°";
        SetFeedback("");
        UpdateLockButtonVisual();
    }

    void OnLockAngle()
    {
        if (!IsAngleCorrect()) { SetFeedback("Wrong angle."); return; }
        if (angleLocked) return;

        angleLocked = true;
        SetFeedback("Angle OK. Choose pressure.");
        if (pressureSlider) pressureSlider.interactable = true;

        UpdateLockButtonVisual();
        hitConfirmed = false;
        UpdateHitButtonVisual();
    }

    void OnHitStake()
    {
        if (!angleLocked) { SetFeedback("Lock the angle first."); return; }
        if (stakeImage && !isHitAnimating) StartCoroutine(HitAnim());

        bool pressureOk = IsPressureCorrect();

        if (pressureOk)
        {
            if (nextPegButton) nextPegButton.interactable = true;

            if (pegDots != null && currentPegIndex < pegDots.Length)
            {
                pegDots[currentPegIndex].color = Color.green;
                var c = pegDots[currentPegIndex].color; c.a = 1f; pegDots[currentPegIndex].color = c;
            }

            bool isLastPeg = (currentPegIndex >= totalPegs - 1);
            if (isLastPeg)
            {
                SetNextButtonLabel("Set the tent");
                SetFeedback("ANGLE OK. PRESSURE OK. Finalize the tent set up");
            }
            else
            {
                SetNextButtonLabel("Next Stake");
                SetFeedback("ANGLE OK. PRESSURE OK. Proceed to the next stake.");
            }
        }
        else
        {
            SetFeedback("Wrong pressure.");
            if (nextPegButton) nextPegButton.interactable = false;
            SetNextButtonLabel("Next Stake");
        }

        UpdateHitButtonVisual();
    }

    System.Collections.IEnumerator HitAnim()
    {
        isHitAnimating = true;

        Vector2 start = stakeBasePos;
        Vector2 down  = stakeBasePos + Vector2.down * hitDownPixels;

        float t = 0f;
        while (t < hitDownTime)
        {
            t += Time.deltaTime;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / hitDownTime), 2f);
            stakeImage.anchoredPosition = Vector2.LerpUnclamped(start, down, e);
            yield return null;
        }

        t = 0f;
        while (t < hitUpTime)
        {
            t += Time.deltaTime;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / hitUpTime), 2f);
            stakeImage.anchoredPosition = Vector2.LerpUnclamped(down, start, e);
            yield return null;
        }

        stakeImage.anchoredPosition = start;
        isHitAnimating = false;
    }

    void OnNextPeg()
    {
        currentPegIndex++;
        if (currentPegIndex >= totalPegs)
        {
            ShowStakeUI(false);
            if (completionPanel) completionPanel.SetActive(true);

            if (completionCloseButton)
            {
                completionCloseButton.onClick.RemoveAllListeners();
                completionCloseButton.onClick.AddListener(CompleteTentAndClose);
            }

            if (nextPegButton) nextPegButton.interactable = false;
            SetFeedback("All pegs completed!");
            return;
        }

        ResetForPeg();
        RefreshUI();
        UpdateLockButtonVisual();
        UpdateHitButtonVisual();
        SetFeedback($"Next peg ({currentPegIndex + 1}/{totalPegs})");
    }

    // ========= NEW: hard cancel / close =========
    public void CancelPegInstallation()
    {
        // Reset internal state
        currentPegIndex = 0;
        angleLocked = false;
        hitConfirmed = false;
        SetFeedback("Canceled.");

        // Hide this panel
        gameObject.SetActive(false);

        // Disable Canvas_TicketPlacement (so user MUST re-touch the tent)
        if (canvas_TicketPlacement && canvas_TicketPlacement.activeSelf)
            canvas_TicketPlacement.SetActive(false);

        // Re-enable player control
        EnablePlayerControl();

        // Tell the gate we "closed" and REQUIRE exit before it can open again
        NotifyGateClosed(requireExit: true, unfreezePlayer: true);

        // Clear current target tent
        targetTentInstance = null;
    }
    // ============================================

    // ====== Visual/state helpers for the button loop ======
    void UpdateLockButtonVisual()
    {
        if (!lockAngleButton || !lockBtnBackground) return;

        if (angleLocked)
        {
            lockAngleButton.interactable = false;
            SetGraphicColor(lockBtnBackground, lockBtnColor_Ready, disabledAlpha);
            if (lockBtnLabel) lockBtnLabel.color = new Color(lockBtnTextColor.r, lockBtnTextColor.g, lockBtnTextColor.b, disabledAlpha);
            return;
        }

        bool ready = IsAngleCorrect();
        lockAngleButton.interactable = true;
        SetGraphicColor(lockBtnBackground, ready ? lockBtnColor_Ready : lockBtnColor_NotReady, 1f);
        if (lockBtnLabel) lockBtnLabel.color = lockBtnTextColor;
    }

    void UpdateHitButtonVisual()
    {
        if (!hitButton || !hitBtnBackground) return;

        if (!angleLocked)
        {
            hitButton.interactable = false;
            SetGraphicColor(hitBtnBackground, lockBtnColor_NotReady, disabledAlpha);
            if (hitBtnLabel) hitBtnLabel.color = new Color(1f, 1f, 1f, disabledAlpha);
            return;
        }

        if (hitConfirmed)
        {
            hitButton.interactable = false;
            SetGraphicColor(hitBtnBackground, lockBtnColor_Ready, disabledAlpha);
            if (hitBtnLabel) hitBtnLabel.color = new Color(1f, 1f, 1f, disabledAlpha);
            return;
        }

        bool pressureOk = IsPressureCorrect();
        hitButton.interactable = true;
        SetGraphicColor(hitBtnBackground, pressureOk ? lockBtnColor_Ready : lockBtnColor_NotReady, 1f);
        if (hitBtnLabel) hitBtnLabel.color = Color.white;
    }

    void SetGraphicColor(Graphic g, Color baseColor, float alpha)
    {
        var c = baseColor; c.a = Mathf.Clamp01(alpha);
        g.color = c;
    }
    // =======================================================

    void CompleteTentAndClose()
    {
        if (completionPanel) completionPanel.SetActive(false);

        GameObject finished = null;

        if (targetTentInstance && finishedTentPrefab)
        {
            var src = targetTentInstance.transform;

            Transform parent = src.parent;
            int sibling = src.GetSiblingIndex();
            Vector3 lPos = src.localPosition;
            Quaternion lRot = src.localRotation;
            Vector3 lScale = src.localScale;

            var srcSR = targetTentInstance.GetComponentInChildren<SpriteRenderer>(true);
            string srcLayer = srcSR ? srcSR.sortingLayerName : null;
            int srcOrder = srcSR ? srcSR.sortingOrder : 0;

            Transform srcAnchor = src.Find("PlacementAnchor");
            Vector3 srcAnchorPos = srcAnchor ? srcAnchor.position : Vector3.zero;

            Destroy(targetTentInstance);

            finished = Instantiate(finishedTentPrefab, parent);
            finished.transform.SetSiblingIndex(sibling);
            finished.transform.localPosition = lPos;
            finished.transform.localRotation = lRot;
            finished.transform.localScale = lScale;

            Transform dstAnchor = finished.transform.Find("PlacementAnchor");
            if (srcAnchor && dstAnchor)
            {
                Vector3 delta = srcAnchorPos - dstAnchor.position;
                finished.transform.position += delta;
            }

            var dstSR = finished.GetComponentInChildren<SpriteRenderer>(true);
            if (srcSR && dstSR)
            {
                dstSR.sortingLayerName = srcLayer;
                dstSR.sortingOrder = srcOrder;
            }
        }

        if (player && finished)
        {
            Transform entranceSpawn = finished.transform.Find("EntranceSpawn");
            Vector3 targetPos;
            if (entranceSpawn) targetPos = entranceSpawn.position + (Vector3)spawnOffset;
            else
            {
                var sr = finished.GetComponentInChildren<SpriteRenderer>(true);
                if (sr)
                {
                    var b = sr.bounds;
                    targetPos = new Vector3(b.center.x, b.min.y - 0.1f, player.position.z) + (Vector3)spawnOffset;
                }
                else targetPos = finished.transform.position + (Vector3)spawnOffset;
            }

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb) rb.position = targetPos; else player.position = targetPos;
        }

        currentPegIndex = 0;
        angleLocked = false;
        hitConfirmed = false;
        targetTentInstance = null;
        gameObject.SetActive(false);

        if (playerRb) playerRb.simulated = true;
        EnablePlayerControl();
    }

    void DisablePlayerControl()
    {
        if (playerMovementScript) playerMovementScript.enabled = false;

        if (playerRb)
        {
            playerRb.linearVelocity = Vector2.zero;
            playerRb.angularVelocity = 0f;
            playerRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    void EnablePlayerControl()
    {
        if (playerMovementScript) playerMovementScript.enabled = true;
        if (playerRb) playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void ResetForPeg()
    {
        angleLocked  = false;
        hitConfirmed = false;

        if (randomizeStartAngle)
        {
            currentAngle = Random.Range(startAngleMin, startAngleMax);
            if (snapStartAngleToStep && snapStepDeg > 0f)
                currentAngle = Mathf.Round(currentAngle / snapStepDeg) * snapStepDeg;
        }
        else currentAngle = startingAngleDeg;

        if (stakeImage)
        {
            stakeImage.localRotation = Quaternion.Euler(0, 0, currentAngle + visualZeroOffsetDeg);
            stakeBasePos = stakeImage.anchoredPosition;
        }

        if (pressureSlider)
        {
            pressureSlider.minValue = 0f;
            pressureSlider.maxValue = 1f;

            float initialPressure = randomizeStartPressure
                ? Random.Range(startPressureMin, startPressureMax)
                : 0.5f;

            pressureSlider.value = Mathf.Clamp01(initialPressure);
            pressureSlider.interactable = false;
        }

        if (hitButton)     hitButton.interactable = false;
        if (nextPegButton) nextPegButton.interactable = false;
        SetNextButtonLabel("Next Stake");

        if (pegDots != null)
        {
            for (int i = 0; i < pegDots.Length; i++)
            {
                bool done = i < currentPegIndex;
                pegDots[i].color = done ? Color.green : new Color(1, 1, 1, 0.35f);
            }
        }

        if (angleText)    angleText.text    = $"Angle: {Mathf.RoundToInt(Norm180(currentAngle))}°";
        if (pressureText) pressureText.text = $"Pressure: {Mathf.RoundToInt((pressureSlider ? pressureSlider.value : 0.5f) * 100)}%";
        SetFeedback("");

        UpdateLockButtonVisual();
        UpdateHitButtonVisual();
    }

    void RefreshUI()
    {
        if (lockAngleButton) lockAngleButton.interactable = true;
        if (hitButton)       hitButton.interactable = false;
        if (nextPegButton)   nextPegButton.interactable = false;
    }

    bool IsAngleCorrect()
    {
        float delta = Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngleDeg));
        return delta <= Mathf.Max(0f, angleToleranceDeg);
    }

    bool IsPressureCorrect()
    {
        if (!pressureSlider) return false;
        int pct       = Mathf.RoundToInt(Snap01(pressureSlider.value, uiPressureStep) * 100f);
        int targetPct = Mathf.RoundToInt(pressureTarget * 100f);
        return pct == targetPct;
    }

    float Norm180(float deg) => Mathf.DeltaAngle(0f, deg);

    void SetFeedback(string s)
    {
        if (feedbackText) feedbackText.text = s;
    }

    void Update()
    {
        if (pressureSlider && pressureText)
            pressureText.text = $"Pressure: {Mathf.RoundToInt(pressureSlider.value * 100f)}%";
    }

    void ShowStakeUI(bool show)
    {
        if (_cg == null) return;
        _cg.alpha = show ? 1f : 0f;
        _cg.interactable = show;
        _cg.blocksRaycasts = show;
    }

    // --- Notify the TentWizardGate that we closed and require exit/re-touch ---
    void NotifyGateClosed(bool requireExit, bool unfreezePlayer)
    {
        TentWizardGate gate = null;

        if (targetTentInstance)
            gate = targetTentInstance.GetComponent<TentWizardGate>();

        if (!gate)
        {
#if UNITY_2023_1_OR_NEWER
            gate = Object.FindFirstObjectByType<TentWizardGate>(FindObjectsInactive.Include);
#else
            gate = Object.FindObjectOfType<TentWizardGate>();
#endif
        }

        if (gate)
            gate.OnWizardClosed(requireExit, unfreezePlayer);
    }
}
