using System;
using System.Collections.Generic;
using UnityEngine;

public class CompletionCloseUnfreeze : MonoBehaviour
{
    // Broadcast in case other systems want to react
    public static event Action OnRequestUnfreeze;

    [Header("UI To Close")]
    [SerializeField] GameObject completionPanel;
    [SerializeField] GameObject pegPanelRoot;
    [SerializeField] List<GameObject> alsoDisable = new();

    [Header("Player Control")]
    [SerializeField] MonoBehaviour playerMovementScript;
    [SerializeField] Rigidbody2D playerRb;
    [SerializeField] Transform player;

    [Header("Misc")]
    [SerializeField] bool forceTimeScaleToOne = true;

    // ========= Public methods to wire from UI buttons =========

    public void OnCompletionClosePressed()
    {
        ForceCloseUI();
        UnfreezePlayer();
        OnRequestUnfreeze?.Invoke();
    }

    public void OnLosePressed()
    {
        ForceCloseUI();
        UnfreezePlayer();
        OnRequestUnfreeze?.Invoke();
    }

    // ========= Internal helpers =========

    void ForceCloseUI()
    {
        if (completionPanel) completionPanel.SetActive(false);
        if (pegPanelRoot && pegPanelRoot.activeSelf) pegPanelRoot.SetActive(false);

        if (alsoDisable != null)
        {
            foreach (var go in alsoDisable)
                if (go) go.SetActive(false);
        }

        if (forceTimeScaleToOne && Time.timeScale != 1f)
            Time.timeScale = 1f;
    }

    void UnfreezePlayer()
    {
        // Enable movement script
        if (playerMovementScript) playerMovementScript.enabled = true;

        // Ensure we have a Rigidbody2D
        var rb = playerRb;
        if (!rb && player) rb = player.GetComponent<Rigidbody2D>();

        if (rb)
        {
            // Unfreeze constraints (keep only rotation frozen in 2D top-down)
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // 🔹 Re-enable physics simulation
            rb.simulated = true;
        }
    }
}
