using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TentWizardGate : MonoBehaviour
{
    [Header("Who can trigger")]
    [SerializeField] string playerTag = "Player";

    [Header("Ticket Placement Wizard")]
    [SerializeField] GameObject wizardRoot;
    [SerializeField] WizardCloseOpensPegPanel wizardCloser;
    [SerializeField] WizardPagerMinimal wizardPager;

    [Header("One-shot (if true, open only once ever)")]
    [SerializeField] bool openOnce = false;

    [Header("Reopen Control")]
    [Tooltip("Minimum time after closing before this gate can open again (even if the player still overlaps).")]
    [SerializeField] float reopenCooldown = 0.15f;

    // ==== Player freeze support ====
    Rigidbody2D playerBody;
    MonoBehaviour moveScript; // e.g. "MoveScript"
    RigidbodyConstraints2D _originalConstraints;
    bool _haveOriginalConstraints;

    // === One-open-per-stay state ===
    bool _fired;                     // for openOnce
    bool _hasOpenedDuringThisStay;   // opened once while overlapping
    bool _requireExitBeforeReopen;   // block reopen until exit
    Collider2D _currentPlayer;

    // Cooldown timestamp
    float _unlockTime = 0f;

    // NEW: logic to ensure first re-enter opens immediately
    bool _allowFirstEnterAfterClose; // armed on close
    bool _seenExitSinceClose;        // becomes true when player exits after close

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
        AutoWireWizard();
        AutoWirePlayer();
    }

    void AutoWireWizard()
    {
        if (wizardRoot && wizardCloser && wizardCloser.gameObject == wizardRoot)
        {
            var cv = wizardRoot.GetComponentInParent<Canvas>(true);
            if (cv && cv.name.Contains("Canvas_TicketPlacementInstructions"))
            {
                wizardPager = wizardRoot.GetComponent<WizardPagerMinimal>();
                return;
            }
        }

        var allClosers = Resources.FindObjectsOfTypeAll<WizardCloseOpensPegPanel>();
        foreach (var c in allClosers)
        {
            var go = c.gameObject;
            var cv = go.GetComponentInParent<Canvas>(true);
            if (!cv) continue;
            if (cv.name.Contains("Canvas_TicketPlacementInstructions"))
            {
                wizardRoot = go;
                wizardCloser = c;
                wizardPager = go.GetComponent<WizardPagerMinimal>();
                Debug.Log("[TentWizardGate] Auto-wired WizardRoot to: " + go.name + " under " + cv.name);
                return;
            }
        }

        wizardRoot = null;
        wizardCloser = null;
        wizardPager = null;
    }

    void AutoWirePlayer()
    {
        var player = GameObject.FindGameObjectWithTag(playerTag);
        if (!player)
        {
            Debug.LogWarning($"[TentWizardGate] No GameObject with tag '{playerTag}' found.");
            return;
        }

        playerBody = player.GetComponent<Rigidbody2D>();
        if (!playerBody)
            Debug.LogWarning("[TentWizardGate] Player has no Rigidbody2D.");

        moveScript = FindByTypeName(player, "MoveScript");
        if (!moveScript)
            Debug.LogWarning("[TentWizardGate] No 'MoveScript' found on Player.");
    }

    static MonoBehaviour FindByTypeName(GameObject go, string typeName)
    {
        var all = go.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in all)
        {
            if (mb == null) continue;
            if (mb.GetType().Name == typeName) return mb;
        }
        return null;
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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Cooldown guard
        if (Time.unscaledTime < _unlockTime) return;

        // If we required an exit, allow the FIRST enter after we've actually seen an exit
        if (_requireExitBeforeReopen)
        {
            if (_allowFirstEnterAfterClose && _seenExitSinceClose)
            {
                // Disarm and allow opening now
                _requireExitBeforeReopen = false;
                _allowFirstEnterAfterClose = false;
                _seenExitSinceClose = false;
                InteractionLock.BlockInteractions = false;
            }
            else
            {
                return; // still waiting for a proper exit+enter sequence
            }
        }

        if (_hasOpenedDuringThisStay) return;       // already opened during this overlap
        if (InteractionLock.BlockInteractions) return;
        if (_fired && openOnce) return;

        if (!wizardRoot || !wizardCloser)
        {
            Debug.LogWarning("[TentWizardGate] Wizard references not assigned.");
            return;
        }

        _currentPlayer = other;

        // Freeze player
        FreezePlayer();

        // Pass tent
        wizardCloser.SetPendingTent(gameObject);

        // Ensure canvas ON
        var parentCanvas = wizardRoot.GetComponentInParent<Canvas>(true);
        if (parentCanvas && !parentCanvas.gameObject.activeSelf)
            parentCanvas.gameObject.SetActive(true);

        ActivateHierarchy(wizardRoot);

        if (!wizardPager) wizardPager = wizardRoot.GetComponent<WizardPagerMinimal>();
        if (wizardPager) wizardPager.Show();
        else wizardRoot.SetActive(true);

        _hasOpenedDuringThisStay = true;
        if (openOnce) _fired = true;

        InteractionLock.BlockInteractions = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (_currentPlayer != null && other != _currentPlayer) return;

        _hasOpenedDuringThisStay = false;
        _currentPlayer = null;

        // If we were waiting for an exit after a close, mark that we've seen it
        if (_allowFirstEnterAfterClose)
            _seenExitSinceClose = true;

        InteractionLock.BlockInteractions = false;
    }

    /// <summary>
    /// Called by the wizard when it closes.
    /// </summary>
    /// <param name="requireExit">If true, player must exit trigger before it can reopen.</param>
    /// <param name="unfreezePlayer">If true, unfreezes the player now.</param>
    public void OnWizardClosed(bool requireExit = true, bool unfreezePlayer = true)
    {
        if (unfreezePlayer)
            UnfreezePlayer();
        else
            Debug.Log("[TentWizardGate] Wizard closed with player still frozen.");

        // Small cooldown so we don't reopen immediately due to lingering overlaps
        _unlockTime = Time.unscaledTime + reopenCooldown;

        _requireExitBeforeReopen = requireExit;
        InteractionLock.BlockInteractions = requireExit;

        // NEW: if we closed while the player is STILL inside the trigger,
        // arm the *first* re-enter after they step out to open immediately.
        _allowFirstEnterAfterClose = requireExit;
        _seenExitSinceClose = (_currentPlayer == null); // if not inside, first enter can open directly
    }

    void FreezePlayer()
    {
        if (!playerBody || !moveScript) AutoWirePlayer();

        if (moveScript) moveScript.enabled = false;

        if (playerBody)
        {
            if (!_haveOriginalConstraints)
            {
                _originalConstraints = playerBody.constraints;
                _haveOriginalConstraints = true;
            }

            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;

            playerBody.constraints =
                RigidbodyConstraints2D.FreezePositionX |
                RigidbodyConstraints2D.FreezePositionY |
                RigidbodyConstraints2D.FreezeRotation;

            playerBody.simulated = false;
        }
    }

    void UnfreezePlayer()
    {
        if (playerBody)
        {
            playerBody.simulated = true;
            if (_haveOriginalConstraints) playerBody.constraints = _originalConstraints;
        }
        if (moveScript) moveScript.enabled = true;
    }
}
