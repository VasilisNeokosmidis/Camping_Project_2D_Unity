using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ControlCenterIntroWizard : MonoBehaviour
{
    [Serializable]
    public class WizardPage
    {
        public GameObject pageRoot;   // Page_1, Page_2, ...
        public Button prevButton;
        public Button nextButton;
        public Button closeButton;    // per-page CLOSE
        public Button skipButton;     // NEW: per-page SKIP
    }

    [Header("Wizard Root (panel that holds all pages)")]
    [SerializeField] private GameObject wizardRoot; // ControlCenterInstructions_WizardRoot

    [Header("Pages (assign in order; Page_1 must be index 0)")]
    [SerializeField] private List<WizardPage> pages = new List<WizardPage>();

    [Header("UI Text")]
    [SerializeField] private string finishText = "Finish";
    [SerializeField] private string nextText = "Next";

    [Header("Scene References (optional)")]
    [Tooltip("Canvas that holds the instructions (e.g., Canvas_ControlCenterInstructions)")]
    [SerializeField] private GameObject instructionsCanvasRoot; // optional auto-found

    [Header("Targets to ENABLE on Skip")]
    [SerializeField] private GameObject tentControlCenter_Panel; // assign TentControlCenter_Panel
    [SerializeField] private GameObject mainMenu_Panel;          // assign MainMenu_Panel

    [Header("Player")]
    [SerializeField] private string playerTag = "Player"; // used for unfreeze (close only)

    private int index = 0;
    private Action onFinished;

    void Awake()
    {
        if (!wizardRoot) wizardRoot = gameObject;
        wizardRoot.SetActive(false);      // start hidden
        SetAllPagesActive(false);         // pages off

        if (!instructionsCanvasRoot)
        {
            instructionsCanvasRoot =
                GameObject.Find("Canvas_ControlCenterInstructions") ??
                GameObject.Find("Canvas_ControlCenter_Instructions") ??
                GameObject.Find("Canvas_CC_Instructions");
        }
    }

    public void Open(Action finishedCallback)
    {
        onFinished = finishedCallback;
        index = 0;                        // ALWAYS start at Page_1
        wizardRoot.SetActive(true);
        ShowPage(index);
    }

    void ShowPage(int i)
    {
        for (int p = 0; p < pages.Count; p++)
            if (pages[p].pageRoot) pages[p].pageRoot.SetActive(p == i);

        var wp = pages[i];

        // Prev
        if (wp.prevButton)
        {
            wp.prevButton.onClick.RemoveAllListeners();
            wp.prevButton.onClick.AddListener(OnPrev);
            wp.prevButton.gameObject.SetActive(true);

            bool enabledPrev = (i > 0);
            wp.prevButton.interactable = enabledPrev;

            var cg = wp.prevButton.GetComponent<CanvasGroup>();
            if (cg)
            {
                cg.alpha = enabledPrev ? 1f : 0.5f;
                cg.interactable = enabledPrev;
                cg.blocksRaycasts = enabledPrev;
            }
        }

        // Next / Finish
        if (wp.nextButton)
        {
            wp.nextButton.onClick.RemoveAllListeners();
            wp.nextButton.onClick.AddListener(OnNext);
            SetButtonLabel(wp.nextButton, i == pages.Count - 1 ? finishText : nextText);
            wp.nextButton.gameObject.SetActive(true);
        }

        // Close (HARD EXIT) — 🔴 red background
        if (wp.closeButton)
        {
            wp.closeButton.onClick.RemoveAllListeners();
            wp.closeButton.onClick.AddListener(OnCloseRequested);
            wp.closeButton.gameObject.SetActive(true);

            var img = wp.closeButton.GetComponent<Image>();
            if (img) img.color = Color.red;
        }

        // NEW: Skip (soft exit of instructions, enable desired panels)
        if (wp.skipButton)
        {
            wp.skipButton.onClick.RemoveAllListeners();
            wp.skipButton.onClick.AddListener(OnSkipRequested);
            wp.skipButton.gameObject.SetActive(true);
        }
    }

    void OnPrev()
    {
        if (index <= 0) return;
        index--;
        ShowPage(index);
    }

    void OnNext()
    {
        bool last = (index >= pages.Count - 1);
        if (!last)
        {
            index++;
            ShowPage(index);
        }
        else
        {
            pages[index].pageRoot?.SetActive(false);
            wizardRoot.SetActive(false);
            onFinished?.Invoke();
        }
    }

    /// <summary>
    /// CLOSE = hard exit: hide everything, signal computer exit, unfreeze player.
    /// </summary>
    void OnCloseRequested()
    {
        if (index >= 0 && index < pages.Count) pages[index].pageRoot?.SetActive(false);
        if (wizardRoot) wizardRoot.SetActive(false);
        if (instructionsCanvasRoot) instructionsCanvasRoot.SetActive(false);

        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas) canvas.gameObject.SetActive(false);

        var comp = FindFirstComputerInteract2D();
        if (comp) comp.ForceRequireExit();

        UnfreezePlayer();
    }

    /// <summary>
    /// SKIP = soft exit of the instructions:
    /// - Disable instructions canvas & wizard root & current page
    /// - Enable TentControlCenter_Panel and MainMenu_Panel
    /// (No unfreeze / no computer exit)
    /// </summary>
    void OnSkipRequested()
{
    // Hide current page & wizard/instructions
    if (index >= 0 && index < pages.Count) pages[index].pageRoot?.SetActive(false);
    if (wizardRoot) wizardRoot.SetActive(false);
    if (instructionsCanvasRoot) instructionsCanvasRoot.SetActive(false);

    // Either let the finished callback perform all panel logic...
    onFinished?.Invoke();

    // ...or, if you want to keep these lines, it's safe to enable UI too:
    if (tentControlCenter_Panel) tentControlCenter_Panel.SetActive(true);
    if (mainMenu_Panel)         mainMenu_Panel.SetActive(true);
}

    void SetAllPagesActive(bool active)
    {
        foreach (var p in pages)
            if (p.pageRoot) p.pageRoot.SetActive(active);
    }

    void UnfreezePlayer()
    {
        var player = GameObject.FindWithTag(playerTag);
        if (!player) return;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        var move = FindByTypeName(player, "MoveScript");
        if (move) move.enabled = true;
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

    static ComputerInteract2D FindFirstComputerInteract2D()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<ComputerInteract2D>();
#else
        return UnityEngine.Object.FindObjectOfType<ComputerInteract2D>();
#endif
    }

    static void SetButtonLabel(Button btn, string text)
    {
        if (!btn) return;
        var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmp) { tmp.text = text; return; }
        var t = btn.GetComponentInChildren<Text>(true);
        if (t) t.text = text;
    }
}
