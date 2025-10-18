using UnityEngine;
using UnityEngine.UI;

public class SignInteract : MonoBehaviour
{
    public static SignInteract Instance { get; private set; }

    [Header("Root Canvas")]
    [SerializeField] private GameObject canvas_MapControlCenter;

    [Header("Panels")]
    [SerializeField] private GameObject tentControlCenter_Panel;
    [SerializeField] private GameObject mainMenu_Panel;
    [SerializeField] private GameObject canvas_MapPlacement;
    [SerializeField] private GameObject canvas_MapPlacementInstructions;
    [SerializeField] private GameObject wizardRoot;

    [Header("Buttons (Main Menu)")]
    [SerializeField] private Button btn_SetATent;
    [SerializeField] private Button btn_Exit; // Wire this to CloseAll()

    [Header("Buttons (Map Placement)")]
    [SerializeField] private Button btn_ClosePlacement;

    [Header("Logic")]
    [SerializeField] private InstructionWizardSimple wizard;
    [SerializeField] private MapPanelController mapPanelController;

    [Header("Player Freeze")]
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private MonoBehaviour[] movementComponents;
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private bool freezeByTimeScale = false;

    private float _animOriginalSpeed = 1f;
    private bool _isFrozen;

    // === One-open-per-stay state ===
    private bool hasOpenedDuringThisStay = false;   // set after first open while inside
    private bool requireExitBeforeReopen = false;   // set on CloseAll; cleared on trigger exit
    private Collider2D currentPlayer = null;

    void OnEnable() => Instance = this;

    void OnDisable() => ForceUnlockAndUnfreeze(); // fail-safe

    private void Awake()
    {
        SetActiveSafe(canvas_MapControlCenter, false);
        SetActiveSafe(tentControlCenter_Panel, false);
        SetActiveSafe(mainMenu_Panel, false);
        SetActiveSafe(canvas_MapPlacement, false);
        SetActiveSafe(canvas_MapPlacementInstructions, false);
        SetActiveSafe(wizardRoot, false);

        if (btn_SetATent)
        {
            btn_SetATent.onClick.RemoveAllListeners();
            btn_SetATent.onClick.AddListener(OpenTentInstructions);
        }
        if (btn_Exit)
        {
            btn_Exit.onClick.RemoveAllListeners();
            btn_Exit.onClick.AddListener(CloseAll);
        }
        if (btn_ClosePlacement)
        {
            btn_ClosePlacement.onClick.RemoveAllListeners();
            btn_ClosePlacement.onClick.AddListener(CloseAll);
        }

        // Ensure wizard knows which map panel to open on Finish
        if (wizard && mapPanelController)
        {
            var f = typeof(InstructionWizardSimple)
                .GetField("mapPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f != null && f.GetValue(wizard) == null) f.SetValue(wizard, mapPanelController);
        }

        if (playerAnimator) _animOriginalSpeed = playerAnimator.speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // If we already opened in this stay, or we require exit, ignore.
        if (hasOpenedDuringThisStay || requireExitBeforeReopen) return;

        // Clear stale lock if somehow left behind and no UI is open
        if (InteractionLock.BlockInteractions && !IsAnyUiOpen())
            InteractionLock.BlockInteractions = false;

        if (InteractionLock.BlockInteractions) return;

        currentPlayer = other;
        OpenMainMenu();
        hasOpenedDuringThisStay = true; // allow only once until the player exits
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (currentPlayer != null && other != currentPlayer) return;

        // Leaving the trigger resets latches, enabling a new open next time.
        hasOpenedDuringThisStay = false;
        requireExitBeforeReopen = false;
        currentPlayer = null;
    }

    private bool IsAnyUiOpen()
    {
        return (canvas_MapControlCenter && canvas_MapControlCenter.activeInHierarchy) ||
               (tentControlCenter_Panel && tentControlCenter_Panel.activeInHierarchy) ||
               (mainMenu_Panel && mainMenu_Panel.activeInHierarchy) ||
               (canvas_MapPlacement && canvas_MapPlacement.activeInHierarchy) ||
               (canvas_MapPlacementInstructions && canvas_MapPlacementInstructions.activeInHierarchy) ||
               (wizardRoot && wizardRoot.activeInHierarchy);
    }

    private void OpenMainMenu()
    {
        // Extra safety: if already open, do nothing
        if (IsAnyUiOpen()) return;

        SetActiveSafe(canvas_MapControlCenter, true);
        SetActiveSafe(tentControlCenter_Panel, true);
        SetActiveSafe(mainMenu_Panel, true);

        SetActiveSafe(canvas_MapPlacement, false);
        SetActiveSafe(canvas_MapPlacementInstructions, false);
        SetActiveSafe(wizardRoot, false);

        NormalizePanel(tentControlCenter_Panel);
        NormalizePanel(mainMenu_Panel);

        SetFrozenState(true);
        InteractionLock.BlockInteractions = true;
    }

    private void OpenTentInstructions()
    {
        if (!wizard) { Debug.LogWarning("[SignInteract] Missing InstructionWizardSimple."); return; }

        SetActiveSafe(mainMenu_Panel, false);
        SetActiveSafe(canvas_MapPlacement, false);

        SetActiveSafe(canvas_MapPlacementInstructions, true);
        SetActiveSafe(wizardRoot, true);

        wizard.Show(false);

        if (!_isFrozen) SetFrozenState(true);
    }

    public void CloseAll()
    {
        // Close every related UI
        SetActiveSafe(wizardRoot, false);
        SetActiveSafe(canvas_MapPlacementInstructions, false);
        SetActiveSafe(canvas_MapPlacement, false);
        SetActiveSafe(mainMenu_Panel, false);
        SetActiveSafe(tentControlCenter_Panel, false);
        SetActiveSafe(canvas_MapControlCenter, false);

        // Unfreeze + unlock
        SetFrozenState(false);
        InteractionLock.BlockInteractions = false;

        // Require the player to exit the trigger before a new open
        requireExitBeforeReopen = true;
    }

    public void ForceUnlockAndUnfreeze()
    {
        SetFrozenState(false);
        InteractionLock.BlockInteractions = false;
    }

    // ====== Freeze helpers ======
    private void SetFrozenState(bool freeze)
    {
        if (_isFrozen == freeze) return;
        _isFrozen = freeze;

        if (freezeByTimeScale)
        {
            Time.timeScale = freeze ? 0f : 1f;
            return;
        }

        if (movementComponents != null)
        {
            foreach (var comp in movementComponents)
            {
                if (!comp) continue;
                comp.enabled = !freeze;
            }
        }

        if (playerBody)
        {
#if UNITY_6000_0_OR_NEWER
            if (freeze) { playerBody.linearVelocity = Vector2.zero; playerBody.angularVelocity = 0f; }
#else
            if (freeze) { playerBody.velocity = Vector2.zero; playerBody.angularVelocity = 0f; }
#endif
            playerBody.simulated = !freeze;
        }

        if (playerAnimator)
            playerAnimator.speed = freeze ? 0f : _animOriginalSpeed;
    }

    // ====== UI helpers ======
    private static void SetActiveSafe(GameObject go, bool on)
    {
        if (go && go.activeSelf != on) go.SetActive(on);
    }

    private static void NormalizePanel(GameObject go)
    {
        if (!go) return;
        var rt = go.GetComponent<RectTransform>();
        if (!rt) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition3D = Vector3.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    public static void UnfreezePlayer()
{
    if (Instance) 
        Instance.ForceUnlockAndUnfreeze();
}
}
