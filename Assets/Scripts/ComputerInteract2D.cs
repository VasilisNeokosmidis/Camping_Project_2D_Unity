using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ComputerInteract2D : MonoBehaviour
{
    [Header("Who can open the computer")]
    public string playerTag = "Player";

    [Header("Control Center")]
    public GameObject controlCenterCanvasRoot;   // Canvas_ControlCenter
    public ControlCenterUI controlCenterUI;      // ControlCenter_Panel component

    [Header("Instructions")]
    public GameObject instructionsCanvasRoot;    // Canvas_ControlCenterInstructions
    public GameObject instructionsWizardRoot;    // ControlCenterInstructions_WizardRoot
    public ControlCenterIntroWizard introWizard; // wizard script

    [Header("Context")]
    public GameObject tentRoot;                  // must contain TentInstance2D

    [Header("Enable on touch (OnTriggerEnter2D)")]
    [SerializeField] List<GameObject> enableOnTouch = new();   // drag targets here
    [SerializeField] string[] alsoEnableByName;                 // optional: exact names to enable
    [SerializeField] bool disableThoseOnExit = false;           // if true, turn them off on exit

    // Auto-wired at runtime
    Rigidbody2D playerBody;
    MonoBehaviour moveScript; // type name == "MoveScript"

    // Original constraints to restore on exit
    RigidbodyConstraints2D _originalConstraints;
    bool _haveOriginalConstraints;

    // Re-open guards
    bool _requireExitBeforeReopen = false; // must leave trigger to open again
    bool _openedThisOverlap       = false; // we already opened once while inside

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    void Awake()
    {
        AutoWirePlayer();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Block if:
        // - we must exit first
        // - we already opened once in this same overlap
        // - a global lock is active
        if (_requireExitBeforeReopen || _openedThisOverlap || InteractionLock.BlockInteractions) return;

        // Enable requested targets immediately on touch
        EnableTouchTargets();

        if (!playerBody || !moveScript) AutoWirePlayer();
        if (!tentRoot)        { Debug.LogWarning("[ComputerInteract2D] No tentRoot assigned."); return; }
        if (!controlCenterUI) { Debug.LogWarning("[ComputerInteract2D] No ControlCenterUI assigned."); return; }

        // Minimal freeze: disable movement + freeze rigidbody (KEEP simulated = true)
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

            // DO NOT toggle playerBody.simulated here (leads to phantom re-Enter)
        }

        InteractionLock.BlockInteractions = true;

        // Mark that we have opened during this overlap
        _openedThisOverlap = true;

        // Start instructions (or open CC directly)
        if (introWizard)
        {
            if (instructionsCanvasRoot) instructionsCanvasRoot.SetActive(true);
            if (instructionsWizardRoot) instructionsWizardRoot.SetActive(true);

            introWizard.Open(() =>
            {
                if (instructionsCanvasRoot) instructionsCanvasRoot.SetActive(false);
                if (instructionsWizardRoot) instructionsWizardRoot.SetActive(false);
                OpenControlCenter();
            });
        }
        else
        {
            OpenControlCenter();
        }
    }

    void OpenControlCenter()
    {
        if (controlCenterCanvasRoot) controlCenterCanvasRoot.SetActive(true);

        controlCenterUI.Exited -= OnControlCenterExited;
        controlCenterUI.Exited += OnControlCenterExited;

        controlCenterUI.Open(tentRoot);
    }

    void OnControlCenterExited()
    {
        // Unfreeze player
        if (playerBody)
        {
            // Keep simulated true throughout; just restore constraints
            if (_haveOriginalConstraints) playerBody.constraints = _originalConstraints;
        }
        if (moveScript) moveScript.enabled = true;

        // Require a real exit before we allow re-open
        _requireExitBeforeReopen = true;
        InteractionLock.BlockInteractions = true;

        // Do NOT clear _openedThisOverlap here; we are still overlapping
        controlCenterUI.Exited -= OnControlCenterExited;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Optional: turn those targets back off when we leave
        if (disableThoseOnExit) DisableTouchTargets();

        // Now we left the trigger: allow a new open next time we enter
        _requireExitBeforeReopen = false;
        _openedThisOverlap = false;
        InteractionLock.BlockInteractions = false;
    }

    void OnDestroy()
    {
        if (controlCenterUI != null)
            controlCenterUI.Exited -= OnControlCenterExited;
    }

    // Called by wizard close to ensure no instant reopen while still overlapping
    public void ForceRequireExit()
    {
        _requireExitBeforeReopen = true;
        InteractionLock.BlockInteractions = true;

        // Make sure movement is restored if wizard closed directly
        if (playerBody && _haveOriginalConstraints)
            playerBody.constraints = _originalConstraints;
        if (moveScript) moveScript.enabled = true;
    }

    // --- NEW helpers ---
    void EnableTouchTargets()
    {
        if (enableOnTouch != null)
        {
            for (int i = 0; i < enableOnTouch.Count; i++)
                if (enableOnTouch[i]) enableOnTouch[i].SetActive(true);
        }

        if (alsoEnableByName != null)
        {
            foreach (var n in alsoEnableByName)
            {
                if (string.IsNullOrEmpty(n)) continue;
                var go = GameObject.Find(n);
                if (go) go.SetActive(true);
            }
        }
    }

    void DisableTouchTargets()
    {
        if (enableOnTouch != null)
        {
            for (int i = 0; i < enableOnTouch.Count; i++)
                if (enableOnTouch[i]) enableOnTouch[i].SetActive(false);
        }

        if (alsoEnableByName != null)
        {
            foreach (var n in alsoEnableByName)
            {
                if (string.IsNullOrEmpty(n)) continue;
                var go = GameObject.Find(n);
                if (go) go.SetActive(false);
            }
        }
    }

    // -------- Helpers --------
    void AutoWirePlayer()
    {
        var player = GameObject.FindGameObjectWithTag(playerTag);
        if (!player)
        {
            Debug.LogWarning($"[ComputerInteract2D] No GameObject with tag '{playerTag}' found.");
            return;
        }

        playerBody = player.GetComponent<Rigidbody2D>();
        if (!playerBody)
            Debug.LogWarning("[ComputerInteract2D] Player has no Rigidbody2D.");

        moveScript = FindByTypeName(player, "MoveScript");
        if (!moveScript)
            Debug.LogWarning("[ComputerInteract2D] No 'MoveScript' found on Player.");
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
}
