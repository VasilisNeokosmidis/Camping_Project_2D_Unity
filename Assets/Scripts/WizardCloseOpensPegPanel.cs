using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class WizardCloseOpensPegPanel : MonoBehaviour
{
    [Header("Open this after Finish/Skip")]
    [SerializeField] GameObject pegPanel;             // π.χ. .../Canvas_TicketPlacement/PegPanel
    [SerializeField] PegPanelController pegPanelCtrl;

    [Header("Instruction & Target Canvases (optional)")]
    [SerializeField] GameObject canvas_TicketPlacementInstructions; // Canvas_TicketPlacementInstructions
    [SerializeField] GameObject canvas_TicketPlacement;             // Canvas_TicketPlacement

    [Header("Auto-find (names contain)")]
    [SerializeField] string[] preferCanvasNameContains =
        { "Canvas_TicketPlacement", "Canvas_TicketPlacementInstructions", "Canvas_MapPlacementInstructions" };
    [SerializeField] string pegPanelObjectName = "PegPanel";
    [SerializeField] string instructionsCanvasName = "Canvas_TicketPlacementInstructions";
    [SerializeField] string targetCanvasName       = "Canvas_TicketPlacement";

    [SerializeField] bool verboseLogs = true;

    GameObject  _pendingTent;   // set by TentWizardGate
    CanvasGroup _cg;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;
        _cg.interactable   = false;

        if (!canvas_TicketPlacementInstructions)
            canvas_TicketPlacementInstructions = FindObjectByExactName(instructionsCanvasName);

        if (!canvas_TicketPlacement)
            canvas_TicketPlacement = FindObjectByExactName(targetCanvasName);

        if (!pegPanel)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            var candidates = all.Where(t => t.gameObject.name == pegPanelObjectName)
                                .Select(t => t.gameObject).ToList();

            if (candidates.Count > 0)
            {
                var chosen = candidates
                    .OrderByDescending(go =>
                    {
                        var cv = go.GetComponentInParent<Canvas>(true);
                        if (!cv) return 0;
                        return preferCanvasNameContains.Any(s => cv.name.Contains(s)) ? 1 : 0;
                    })
                    .First();
                pegPanel = chosen;
                if (verboseLogs) Debug.Log("[WizardCloser] Auto-found PegPanel: " + pegPanel.GetHierarchyPath(), this);
            }
        }
        if (pegPanel && !pegPanelCtrl) pegPanelCtrl = pegPanel.GetComponent<PegPanelController>();
    }

    void OnEnable()  { _cg.blocksRaycasts = _cg.interactable = true; }
    void OnDisable() { _cg.blocksRaycasts = _cg.interactable = false; }

    public void SetPendingTent(GameObject tent) => _pendingTent = tent;

    public void CloseWizardOnly()
    {
        var pager = GetComponent<WizardPagerMinimal>();
        var current = pager ? pager.CurrentPageGO : null;
        if (current && current.activeSelf) current.SetActive(false);

        if (gameObject.activeSelf) gameObject.SetActive(false);

        var cv = GetComponentInParent<Canvas>(true);
        if (cv && cv.gameObject.activeSelf) cv.gameObject.SetActive(false);
        if (canvas_TicketPlacementInstructions && canvas_TicketPlacementInstructions.activeSelf)
            canvas_TicketPlacementInstructions.SetActive(false);

        NotifyGateClosed(requireExit: true); // ← ensure exit needed

        if (verboseLogs) Debug.Log("[WizardCloser] Wizard closed ONLY (no PegPanel). Require exit before reopen.", this);
    }

    public void OpenPegPanelFromWizardFinish()
    {
        EnableTargetCanvasAndPegPanel();

        if (pegPanelCtrl && _pendingTent)
        {
            if (verboseLogs) Debug.Log("[WizardCloser] Passing tent to PegPanelController", this);
            pegPanelCtrl.SetTargetTent(_pendingTent);
        }
        _pendingTent = null;

        if (verboseLogs) Debug.Log("[WizardCloser] PegPanel opened from Finish.", this);
    }

    public void SkipInstructionsOpenPegPanel(GameObject currentPage)
    {
        if (currentPage && currentPage.activeSelf) currentPage.SetActive(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);

        if (canvas_TicketPlacementInstructions && canvas_TicketPlacementInstructions.activeSelf)
            canvas_TicketPlacementInstructions.SetActive(false);
        else
        {
            var cv = GetComponentInParent<Canvas>(true);
            if (cv && cv.gameObject.activeSelf) cv.gameObject.SetActive(false);
        }

        EnableTargetCanvasAndPegPanel();

            // ✅ ΔΩΣΕ ΣΤΟ PEG PANEL ΠΟΙΟ TENT ΝΑ ΑΝΤΙΚΑΤΑΣΤΗΣΕΙ
        if (pegPanelCtrl && _pendingTent)
        pegPanelCtrl.SetTargetTent(_pendingTent);

        // κράτα το block για re-open ΧΩΡΙΣ unfreeze όπως είχαμε:
        NotifyGateClosed(requireExit: true, unfreezePlayer: false);

        if (verboseLogs) Debug.Log("[WizardCloser] SKIP: Closed Instructions + Opened PegPanel (player still frozen).", this);
    }

    void EnableTargetCanvasAndPegPanel()
    {
        if (canvas_TicketPlacement && !canvas_TicketPlacement.activeSelf)
        {
            if (verboseLogs) Debug.Log("[WizardCloser] Enabling Canvas_TicketPlacement: " + canvas_TicketPlacement.name, this);
            canvas_TicketPlacement.SetActive(true);
        }
        if (pegPanel)
        {
            var cv = pegPanel.GetComponentInParent<Canvas>(true);
            if (cv && !cv.gameObject.activeSelf)
            {
                if (verboseLogs) Debug.Log("[WizardCloser] Enabling parent Canvas: " + cv.name, this);
                cv.gameObject.SetActive(true);
            }
            if (!pegPanel.activeSelf)
            {
                if (verboseLogs) Debug.Log("[WizardCloser] Enabling PegPanel GO", this);
                pegPanel.SetActive(true);
            }
        }
        else
        {
            Debug.LogWarning("[WizardCloser] PegPanel not assigned/auto-found.", this);
        }
    }

void NotifyGateClosed(bool requireExit, bool unfreezePlayer = true)
    {
        if (_pendingTent)
        {
            var gate = _pendingTent.GetComponent<TentWizardGate>();
            if (gate) gate.OnWizardClosed(requireExit, unfreezePlayer);
        }
        else
        {
    #if UNITY_2023_1_OR_NEWER
            var gate = Object.FindFirstObjectByType<TentWizardGate>(FindObjectsInactive.Include);
    #else
            var gate = Object.FindObjectOfType<TentWizardGate>();
    #endif
            if (gate) gate.OnWizardClosed(requireExit, unfreezePlayer);
        }
    }
    GameObject FindObjectByExactName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        var t = all.FirstOrDefault(x => x.name == exactName);
        return t ? t.gameObject : null;
    }
}

static class GOPathUtil
{
    public static string GetHierarchyPath(this GameObject go)
    {
        if (!go) return "(null)";
        var t = go.transform;
        var path = go.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
