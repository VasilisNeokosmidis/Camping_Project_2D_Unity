using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class ShelterEntranceController : MonoBehaviour
{
    static readonly List<ShelterEntranceController> _all = new();

    public static bool IsPlayerInsideAnyShelter { get; private set; }

    // ---------------- Registry helpers ----------------
    static void EnsureRegistry()
    {
        if (_all.Count > 0) return;
#if UNITY_2023_1_OR_NEWER
        _all.AddRange(Object.FindObjectsByType<ShelterEntranceController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
#else
        _all.AddRange(Object.FindObjectsOfType<ShelterEntranceController>(true));
#endif
    }

    // Καλεί StartNavigation() σε όλα
    public static void StartNavigationForAll()
    {
        EnsureRegistry();

        Debug.Log($"[Shelter] StartNavigationForAll → {_all.Count} controllers");
        foreach (var s in _all)
        {
            if (!s) continue;
            s.StartNavigation();
        }

        SignInteract.UnfreezePlayer();
    }

    // Καλεί OnRainStopped() σε όλα
    public static void RainStoppedForAll()
    {
        EnsureRegistry();

        Debug.Log($"[Shelter] RainStoppedForAll → {_all.Count} controllers");
        foreach (var s in _all)
        {
            if (!s) continue;
            s.OnRainStopped();
        }
    }

    // ΝΕΟ: Σκληραίνει (non-trigger) τα edges για ΟΛΑ τα shelters άμεσα
    public static void SolidifyAllEdgesImmediate()
    {
        EnsureRegistry();

        Debug.Log($"[Shelter] SolidifyAllEdgesImmediate → {_all.Count} controllers");
        foreach (var s in _all)
        {
            if (!s) continue;
            s.CancelSolidify();
            s.SetEdges(isTrigger: false);
            s.phase = Phase.Idle;
        }
    }

    // ================= Inspector =================
    [Header("Assign colliders (ALL on THIS GameObject)")]
    [SerializeField] BoxCollider2D entryBox;
    [SerializeField] List<EdgeCollider2D> edgeWalls = new();

    [Header("Player")]
    [SerializeField] string playerTag = "Player";

    [Header("Delays")]
    [SerializeField] float solidifyDelay_DuringNav = 2f;
    [SerializeField] float solidifyDelay_AfterRain = 0.12f;

    [Header("UI event (optional)")]
    public UnityEvent onPlayerTouchedEntryBox;

    [Header("UI hooks (optional)")]
    [SerializeField] RainTipUI rainTipUI;

    enum Phase { Idle, WaitingBoxTouch, WaitingEdgeTouch_Nav, WaitingEdgeTouch_AfterRain }
    Phase phase = Phase.Idle;
    bool edgeTouchConsumed;
    Coroutine solidifyRoutine;

    void Reset()
    {
        if (!entryBox) entryBox = GetComponent<BoxCollider2D>();
        if (edgeWalls.Count == 0) edgeWalls.AddRange(GetComponents<EdgeCollider2D>());
    }

    void OnEnable()
    {
        if (!_all.Contains(this)) _all.Add(this);
    }

    void OnDisable()
    {
        _all.Remove(this);
    }

    void Awake()
    {
        SetEdges(isTrigger: false);
        SetEntryBox(enabled: false, isTrigger: true);
        phase = Phase.Idle;

        if (!entryBox) Debug.LogWarning($"[Shelter] '{name}' missing entryBox.");
        if (edgeWalls.Count == 0) Debug.LogWarning($"[Shelter] '{name}' has no edgeWalls.");
    }

    // ===================== Public API =====================
    public void StartNavigation()
    {
        Debug.Log($"[Shelter] StartNavigation on {name}");
        SetEntryBox(enabled: true, isTrigger: true);
        SetEdges(isTrigger: false);

        CancelSolidify();
        edgeTouchConsumed = false;
        phase = Phase.WaitingBoxTouch;
    }

    public void OnRainStopped()
    {
        Debug.Log($"[Shelter] OnRainStopped on {name}");
        SetEntryBox(enabled: false, isTrigger: true);

        if (IsPlayerInsideAnyShelter)
        {
            // Ο παίκτης είναι μέσα: ανοίγουμε προσωρινά (trigger) για να βγει
            SetEdges(isTrigger: true);
            edgeTouchConsumed = false;
            phase = Phase.WaitingEdgeTouch_AfterRain;

            if (rainTipUI) rainTipUI.ShowRainPassedMessage();
        }
        else
        {
            // Δεν είναι μέσα κανείς → κλείνουμε άμεσα
            SetEdges(isTrigger: false);
            phase = Phase.Idle;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        switch (phase)
        {
            case Phase.WaitingBoxTouch:
                HandleBoxTouched();
                break;

            case Phase.WaitingEdgeTouch_Nav:
                // Εδώ παραμένει per-shelter λογική (αν τη χρειάζεσαι)
                HandleEdgeTouched(true, solidifyDelay_DuringNav, false);
                break;

            case Phase.WaitingEdgeTouch_AfterRain:
                // ΕΔΩ είναι το exit: κλείνουμε ΟΛΑ τα shelters μετά από το κοινό delay
                HandleEdgeTouched(true, solidifyDelay_AfterRain, true);
                break;
        }
    }

    void HandleBoxTouched()
    {
        onPlayerTouchedEntryBox?.Invoke();
        IsPlayerInsideAnyShelter = true;

        if (rainTipUI) rainTipUI.ShowShelterEnteredMessage();

        SetEntryBox(enabled: false, isTrigger: true);
        SetEdges(isTrigger: true);

        edgeTouchConsumed = false;
        CancelSolidify();
        phase = Phase.WaitingEdgeTouch_Nav;
    }

    void HandleEdgeTouched(bool firstOnly, float delay, bool leavingNow)
    {
        if (firstOnly && edgeTouchConsumed) return;
        edgeTouchConsumed = true;

        if (leavingNow)
        {
            // Ο παίκτης βγαίνει τώρα από shelter μετά τη βροχή:
            IsPlayerInsideAnyShelter = false;
            if (rainTipUI) rainTipUI.HideRainMessage();

            // Αντί για per-shelter solidify, κάνε GLOBAL solidify σε ΟΛΑ μετά από delay
            CancelSolidify();
            StartCoroutine(Co_SolidifyAllEdgesAfter(delay));
            return;
        }

        // Προηγούμενη συμπεριφορά για άλλες φάσεις (per-shelter)
        CancelSolidify();
        solidifyRoutine = StartCoroutine(Co_SolidifyEdgesAfter(delay));
    }

    // Per-shelter solidify (χρησιμοποιείται μόνο σε NAV φάσεις)
    System.Collections.IEnumerator Co_SolidifyEdgesAfter(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
        SetEdges(isTrigger: false);
        solidifyRoutine = null;
        phase = Phase.Idle;
    }

    // GLOBAL solidify για ΟΛΑ τα shelters (με delay)
    System.Collections.IEnumerator Co_SolidifyAllEdgesAfter(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);

        EnsureRegistry();
        Debug.Log($"[Shelter] Co_SolidifyAllEdgesAfter → {_all.Count} controllers");

        foreach (var s in _all)
        {
            if (!s) continue;
            s.CancelSolidify();
            s.SetEdges(isTrigger: false);
            s.phase = Phase.Idle;
        }
    }

    void CancelSolidify()
    {
        if (solidifyRoutine != null) StopCoroutine(solidifyRoutine);
        solidifyRoutine = null;
    }

    void SetEntryBox(bool enabled, bool isTrigger)
    {
        if (!entryBox) return;
        entryBox.enabled = enabled;
        entryBox.isTrigger = isTrigger;
    }

    void SetEdges(bool isTrigger)
    {
        foreach (var e in edgeWalls)
        {
            if (!e) continue;
            e.enabled = true;
            e.isTrigger = isTrigger;
        }
    }

    public void LockInsideDuringRain()
    {
        Debug.Log($"[Shelter] LockInsideDuringRain on {name}");
        IsPlayerInsideAnyShelter = true;

        // Entry box stays disabled
        SetEntryBox(enabled: false, isTrigger: true);

        // Edges closed (solid walls) so the player must stay in
        SetEdges(isTrigger: false);

        CancelSolidify();
        phase = Phase.WaitingEdgeTouch_Nav; // behave like "entered shelter"
    }

    public static void LockInsideAll()
    {
        EnsureRegistry();

        Debug.Log($"[Shelter] LockInsideAll → {_all.Count} controllers");
        foreach (var s in _all)
        {
            if (!s) continue;
            s.LockInsideDuringRain();
        }
    }
}
