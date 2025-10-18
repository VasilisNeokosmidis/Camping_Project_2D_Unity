using UnityEngine; 
using TMPro;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class PaymentFlow : MonoBehaviour
{
    [Header("Cart & Panels")]
    [SerializeField] private OrderCart cart;
    [SerializeField] private GameObject paymentOptionPanel;
    [SerializeField] private GameObject sufficientBalancePanel;

    [Header("Money UI")]
    [SerializeField] private TMP_Text accountsPayableTMP;
    [SerializeField] private TMP_Text accountBalanceTMP;

    [Header("Delivery")]
    [SerializeField] private Transform tentPlacementAnchor;      // auto-found (PlacementAnchor)
    [SerializeField] private DeliveryTruckSpawner truckSpawner;  // auto-found
    [SerializeField] private PreviewPathfinder2D pathfinder;     // optional

    [Header("Buttons")]
    [SerializeField] private Button orderNowButton;              // "Order Now" button

    [Header("Order Status UI")]
    [SerializeField] private GameObject orderStatusRoot;         // Order_Status (disabled by default)
    [SerializeField] private TMP_Text orderTimeTMP;              // "Order_Time"
    [SerializeField] private TMP_Text orderDescTMP;              // "Order_Description"

    [Header("Texts")]
    [SerializeField] private string desc_WaitingForRainStop = "order will start after rain stop";
    [SerializeField] private string desc_Countdown          = "Estimated Arrival in";

    [Header("ETA Settings")]
    [Tooltip("Used only as a fallback if path speed is unknown; normally we use truck's Speed.")]
    [SerializeField] private float truckSpeedForETA = 3.5f;      // units/second (fallback)
    [Tooltip("How often to refresh the ETA label.")]
    [SerializeField] private float etaUpdateInterval = 0.2f;

    private float pendingAmount = 0f;
    private bool  hasPending    = false;

    // ---- per-delivery linking ----
    private int   nextDeliveryId = 0;       // increments per order
    private int   activeDeliveryId = -1;    // id of the delivery shown in UI (-1 = none)
    private bool  isDelivering = false;     // TRUE while active delivery is going to tent
    private Transform activeTruck;          // truck transform for the *active* delivery

    // ---- countdown state (deadline-based, survives UI close) ----
    private float etaDeadlineTs = -1f;      // absolute Time.time when truck should reach tent
    private float freezeStartedAt = -1f;    // when rain started (for deadline shift)
    private bool  waitingForRainToStartDelivery = false; // NEW: order accepted while raining
    private Coroutine etaCo;

    const float EPS = 0.005f;

    void Awake()
    {
        if (paymentOptionPanel)     paymentOptionPanel.SetActive(false);
        if (sufficientBalancePanel) sufficientBalancePanel.SetActive(false);

        // Auto-find anchors if not wired
        if (!tentPlacementAnchor)
        {
            var root = transform.root;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "PlacementAnchor") { tentPlacementAnchor = t; break; }
        }
        if (!truckSpawner) truckSpawner = FindObjectOfType<DeliveryTruckSpawner>();
        if (!pathfinder)   pathfinder   = FindObjectOfType<PreviewPathfinder2D>();

        AutoWireOrderStatusUI();

        if (EconomyWallet.Instance)
            EconomyWallet.Instance.OnMoneyChanged += RefreshMoneyLabels;
        RefreshMoneyLabels();

        if (cart) cart.OnCartChanged += HandleCartChanged;

        if (orderStatusRoot) orderStatusRoot.SetActive(false);
        if (orderTimeTMP)    orderTimeTMP.text = string.Empty;
        if (orderDescTMP)    orderDescTMP.text = string.Empty;

        RefreshOrderButton();
    }

    void OnEnable()
    {
        AutoWireOrderStatusUI();

        DeliveryWeatherPause.OnRainStarted += HandleRainStart;
        DeliveryWeatherPause.OnRainStopped += HandleRainStop;

        // If we re-open while it's already raining and we were waiting, ensure the waiting UI is shown.
        if (isDelivering && activeDeliveryId != -1)
        {
            if (orderStatusRoot) orderStatusRoot.SetActive(true);

            if (waitingForRainToStartDelivery)
            {
                SetDescription(desc_WaitingForRainStop);
                SetTimePlaceholder();
            }
            else
            {
                // if we are mid-delivery, resume live ETA
                ResumeETAForActiveDelivery();
            }
        }
        RefreshOrderButton();
    }

    void OnDisable()
    {
        DeliveryWeatherPause.OnRainStarted -= HandleRainStart;
        DeliveryWeatherPause.OnRainStopped -= HandleRainStop;

        // Stop only the UI coroutine (deadline keeps running in background).
        if (etaCo != null) { StopCoroutine(etaCo); etaCo = null; }
    }

    void OnDestroy()
    {
        if (EconomyWallet.Instance)
            EconomyWallet.Instance.OnMoneyChanged -= RefreshMoneyLabels;
        if (cart) cart.OnCartChanged -= HandleCartChanged;
    }

    void AutoWireOrderStatusUI()
    {
        if (!orderStatusRoot)
        {
            foreach (var tr in transform.root.GetComponentsInChildren<Transform>(true))
                if (tr.name == "Order_Status") { orderStatusRoot = tr.gameObject; break; }
        }

        if (!orderTimeTMP && orderStatusRoot)
        {
            foreach (var tmp in orderStatusRoot.GetComponentsInChildren<TMP_Text>(true))
                if (tmp.name == "Order_Time") { orderTimeTMP = tmp; break; }
        }
        if (!orderDescTMP && orderStatusRoot)
        {
            foreach (var tmp in orderStatusRoot.GetComponentsInChildren<TMP_Text>(true))
                if (tmp.name == "Order_Description") { orderDescTMP = tmp; break; }
        }
    }

    // --- Rain events ---
    void HandleRainStart()
    {
        if (!isDelivering || activeDeliveryId == -1) return;

        // If truck already spawned and is on the way, freeze countdown visually
        if (!waitingForRainToStartDelivery)
        {
            if (freezeStartedAt < 0f) freezeStartedAt = Time.time;
        }
        // If we're in "waiting" mode, nothing to do here (UI already shows waiting).
    }

    void HandleRainStop()
    {
        if (!isDelivering || activeDeliveryId == -1) return;

        if (waitingForRainToStartDelivery)
        {
            // It was raining when order was placed; start the delivery now.
            StartDeliveryNow(activeDeliveryId);
            return;
        }

        // Delivery was already running; unfreeze by pushing deadline by paused time
        if (freezeStartedAt >= 0f)
        {
            float paused = Time.time - freezeStartedAt;
            etaDeadlineTs += paused;
            freezeStartedAt = -1f;
        }
    }

    // Called by "Order Now"
    public void OpenPaymentOptions()
    {
        if (isDelivering) { RefreshOrderButton(); return; }
        if (!cart || cart.ItemCount == 0 || cart.CurrentTotal <= EPS)
        {
            RefreshOrderButton();
            return;
        }
        if (paymentOptionPanel) paymentOptionPanel.SetActive(true);
        if (sufficientBalancePanel) sufficientBalancePanel.SetActive(false);
        ClearPending();
    }

    // --- Payments ---
    public void PayWithAccountsPayable()
    {
        float amount = cart ? cart.CurrentTotal : 0f;
        if (amount <= EPS) { CloseAllPanels(); RefreshOrderButton(); return; }

        EconomyWallet.Instance.AddToAccountsPayable(amount);
        AfterSuccessfulOrder();
    }

    public void PayWithBalance()
    {
        float amount = cart ? cart.CurrentTotal : 0f;
        if (amount <= EPS) { CloseAllPanels(); RefreshOrderButton(); return; }

        if (EconomyWallet.Instance.TrySpend(amount))
        {
            AfterSuccessfulOrder();
        }
        else
        {
            pendingAmount = amount;
            hasPending = true;
            if (paymentOptionPanel)     paymentOptionPanel.SetActive(false);
            if (sufficientBalancePanel) sufficientBalancePanel.SetActive(true);
        }
    }

    public void SufficientYes_ChargeToAP()
    {
        if (!hasPending) { CloseAllPanels(); return; }
        EconomyWallet.Instance.AddToAccountsPayable(pendingAmount);
        ClearPending();
        AfterSuccessfulOrder();
    }

    public void SufficientNo_CancelOrder()
    {
        ClearPending();
        CloseAllPanels();
        RefreshOrderButton();
    }

    public void BackToPaymentOptions()
    {
        if (sufficientBalancePanel) sufficientBalancePanel.SetActive(false);
        if (paymentOptionPanel)     paymentOptionPanel.SetActive(true);
    }

    public void CloseAllPanels()
    {
        if (paymentOptionPanel)     paymentOptionPanel.SetActive(false);
        if (sufficientBalancePanel) sufficientBalancePanel.SetActive(false);
    }

    // ---- helpers ----
    void AfterSuccessfulOrder()
    {
        if (cart) cart.ClearCart();
        RefreshMoneyLabels();

        if (orderStatusRoot) orderStatusRoot.SetActive(true);

        int myId = ++nextDeliveryId;
        activeDeliveryId = myId;
        isDelivering = true;
        RefreshOrderButton();

        if (DeliveryWeatherPause.IsRaining)
        {
            // NEW: Wait mode — do NOT spawn truck yet.
            waitingForRainToStartDelivery = true;
            etaDeadlineTs = -1f;        // no countdown yet
            freezeStartedAt = -1f;
            activeTruck = null;

            SetDescription(desc_WaitingForRainStop);
            SetTimePlaceholder();       // "--:--"

            // When rain stops, HandleRainStop() will call StartDeliveryNow(myId)
        }
        else
        {
            // Normal: spawn immediately and start ETA/countdown
            waitingForRainToStartDelivery = false;
            StartDeliveryNow(myId);
        }

        CloseAllPanels();
    }

  void StartDeliveryNow(int myId)
{
    if (!truckSpawner || !tentPlacementAnchor) { FinishAsFailed(myId); return; }
    if (activeDeliveryId != myId) return; // safety

    float plannedSeconds;
    var controller = truckSpawner.SpawnAndDeliver(
        tentPlacementAnchor,
        onReachedTent: () =>
        {
            if (activeDeliveryId != myId) return;

            isDelivering = false;
            waitingForRainToStartDelivery = false;
            if (etaCo != null) { StopCoroutine(etaCo); etaCo = null; }
            etaDeadlineTs = Time.time;
            freezeStartedAt = -1f;
            SetETAFormatted(0f);
            RefreshOrderButton();
        },
        onComplete: () =>
        {
            if (activeDeliveryId == myId)
                activeTruck = null;
        },
        plannedSeconds: out plannedSeconds,
        fallbackSpeedIfNeeded: truckSpeedForETA
    );

    if (controller == null)
    {
        FinishAsFailed(myId);
        return;
    }

    activeTruck = controller.transform;

    // Switch UI to countdown mode
    SetDescription(desc_Countdown);

    etaDeadlineTs = Time.time + Mathf.Max(0f, plannedSeconds);
    freezeStartedAt = -1f;

    // ✅ IMPORTANT: flip the flag BEFORE starting the coroutine
    waitingForRainToStartDelivery = false;

    StartOrRestartETA(myId);
}


    void FinishAsFailed(int myId)
    {
        if (activeDeliveryId != myId) return;

        // Graceful reset
        isDelivering = false;
        waitingForRainToStartDelivery = false;
        activeTruck = null;
        etaDeadlineTs = -1f;
        freezeStartedAt = -1f;
        if (etaCo != null) { StopCoroutine(etaCo); etaCo = null; }

        // You may want to set a failure message here instead of clearing
        SetDescription(string.Empty);
        if (orderTimeTMP) orderTimeTMP.text = string.Empty;

        RefreshOrderButton();
    }

    void ResumeETAForActiveDelivery()
    {
        if (activeDeliveryId == -1) return;

        if (waitingForRainToStartDelivery)
        {
            SetDescription(desc_WaitingForRainStop);
            SetTimePlaceholder();
            return;
        }

        StartOrRestartETA(activeDeliveryId);
    }

    void StartOrRestartETA(int myId)
    {
        WriteCurrentETAOnce(); // draw immediately
        if (etaCo != null) StopCoroutine(etaCo);
        etaCo = StartCoroutine(DeadlineETA_Co(myId));
    }

    void WriteCurrentETAOnce() => SetETAFormatted(GetRemaining());

    // Compute remaining time; freeze visually during rain after truck has started.
    float GetRemaining()
    {
        if (etaDeadlineTs < 0f) return 0f;

        float remaining = etaDeadlineTs - Time.time;

        if (DeliveryWeatherPause.IsRaining && freezeStartedAt >= 0f)
            remaining += (Time.time - freezeStartedAt); // keep visual constant while raining

        return Mathf.Max(0f, remaining);
    }

    // Deadline-driven ETA — continues while UI is closed; visually freezes during rain.
    IEnumerator DeadlineETA_Co(int myId)
    {
        while (activeDeliveryId == myId && !waitingForRainToStartDelivery)
        {
            SetETAFormatted(GetRemaining());
            yield return new WaitForSeconds(etaUpdateInterval);
        }

        if (activeDeliveryId == myId)
            etaCo = null;
    }

    void SetETAFormatted(float seconds)
    {
        if (!orderTimeTMP) return;
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int mm = total / 60;
        int ss = total % 60;
        orderTimeTMP.text = $"{mm:0}:{ss:00}";
    }

    void SetDescription(string txt)
    {
        if (orderDescTMP) orderDescTMP.text = txt ?? string.Empty;
    }

    void SetTimePlaceholder()
    {
        if (orderTimeTMP) orderTimeTMP.text = "--:--";
    }

    void RefreshMoneyLabels()
    {
        if (!EconomyWallet.Instance) return;
        if (accountsPayableTMP)
            accountsPayableTMP.text = $"Accounts Payable: {EconomyWallet.Instance.AccountsPayable:0.##}$";
        if (accountBalanceTMP)
            accountBalanceTMP.text  = $"Account Balance: {EconomyWallet.Instance.AccountBalance:0.##}$";
    }

    void ClearPending() { pendingAmount = 0f; hasPending = false; }

    // --- button state management ---
    void HandleCartChanged(float total, int itemCount) => RefreshOrderButton();

    void RefreshOrderButton()
    {
        if (!orderNowButton) return;
        bool canOrder = !isDelivering && cart && cart.ItemCount > 0 && cart.CurrentTotal > EPS;
        orderNowButton.interactable = canOrder;
    }
}
