using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MapPanelController : MonoBehaviour,
    IPointerMoveHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    
    [Header("Playable area")]
    [SerializeField] Collider2D playableArea; // isTrigger collider for allowed area
    
    [Header("UI")]
    [SerializeField] GameObject panel;
    [SerializeField] Button confirmButton;
    [SerializeField] Button cancelButton;
    [SerializeField] Button closeButton;
    [SerializeField] RawImage miniMapImage;

    [Header("Mini-map")]
    [SerializeField] Camera miniMapCamera; // Orthographic top-down

    [Header("Cancel Behavior")]
    [SerializeField] GameObject canvas_MapPlacement; // Canvas_MapPlacementInstructions
    [SerializeField] GameObject mainMenuPanel;

    [Header("Placement")]
    [SerializeField] GameObject tentPrefab;
    [SerializeField] Transform worldParent;
    [SerializeField] float worldZ = 0f;
    [SerializeField, Range(0f, 1f)] float ghostAlpha = 0.5f;

    [Header("No-overlap settings")]
    [SerializeField] float minTentSpacing = 0.8f;
    [SerializeField] Texture2D cursorForbidden;
    [SerializeField] Vector2 cursorHotspot = default;

    [Header("(Optional) Existing tents in scene")]
    [SerializeField] bool scanExistingTentsOnOpen = false;
    [SerializeField] string tentTag = "Tent";

    [Header("No-build zones")]
    [SerializeField] LayerMask noBuildMask;
    [SerializeField] Vector2 footprintSize2D = new Vector2(1.6f, 1.6f);

    [Header("Raycast / Skip fixes")]
    [Tooltip("Any canvases/panels that might still be active/faded after Skip and block raycasts. We will set blocksRaycasts=false on Open().")]
    [SerializeField] CanvasGroup[] disableRaycastBlockersOnOpen;
    [Tooltip("If true, logs the topmost UI that is consuming the mouse raycast.")]
    [SerializeField] bool debugRaycast = false;

    public bool IsOpen => panel && panel.activeSelf;

    // ---- Session ghost record ----
    class Ghost { public GameObject go; public string uiTag; }

    GameObject cursorGhost;
    readonly List<Ghost> ghosts = new();
    readonly List<Transform> placedTents = new();
    bool pointerInside;
    bool lastValid;

    [Header("Tent Interaction")]
    [SerializeField] GameObject tentTouchPanel;

    // ===== Ground feedback (yellow) & counters =====
    [Header("Ground feedback")]
    [SerializeField] LayerMask groundMask;
    [SerializeField] Color warnHoverColor = Color.yellow;
    [SerializeField, Range(0f, 1f)] float fadedIconAlpha = 0.35f;

    [System.Serializable]
    public class GroundIconEntry
    {
        public string groundTag;
        public CanvasGroup iconGroup;
        public Image iconImage;
        public TMP_Text tmpCounter;
        public Text uiCounter;

        [HideInInspector] public int persistentCount;
        [HideInInspector] public int sessionCount;
    }

    [SerializeField] List<GroundIconEntry> groundIcons = new();
    readonly Dictionary<string, GroundIconEntry> groundMap = new();
    string lastHoverGroundTag;
    Color currentHoverTint = Color.white;

    // ===== Special “shadow” (green) =====
    [Header("Special shadow hover")]
    [SerializeField] string shadowTag = "Shadow";
    [SerializeField] LayerMask shadowLayer;
    [SerializeField] Color shadowHoverColor = Color.green;

    [Header("UX")]
    [SerializeField, Tooltip("If you right-click within this distance (world units) of a ghost, it will be removed.")]
    float rightClickRemoveRadius = 0.6f;

    // Optional wizard hook
    [SerializeField] InstructionWizardSimple wizard;

    void Awake()
    {
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton)  cancelButton.onClick.AddListener(OnCancel);
        if (closeButton)   closeButton.onClick.AddListener(OnCloseButton);
        if (panel) panel.SetActive(false);

        foreach (var e in groundIcons)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.groundTag)) continue;
            groundMap[e.groundTag] = e;
            e.persistentCount = 0;
            e.sessionCount = 0;
            SetIconFaded(e, true);
            SetIconCountText(e, 0);
        }

        if (tentPrefab && !tentPrefab.GetComponentInChildren<Collider2D>(true))
            Debug.LogWarning("[MapPanelController] tentPrefab has no Collider2D. " +
                             "Add a PolygonCollider2D (isTrigger=true) for finalized tents.");
    }

    // Close via the top-right X
    void OnCloseButton() => CloseAndReturnToMainMenu(rollbackSession: true);

    static void ActivateHierarchyUpToCanvas(GameObject node)
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

    // ---------------- Public open/close ----------------
    public void Open()
    {
        if (!panel) return;

        EnsureUIRootsOn();
        EnsurePointerReceiverSetup();   // << make sure raycasts hit us and not hidden blockers
        ClearSession();

        if (scanExistingTentsOnOpen)
        {
            placedTents.Clear();
            if (!string.IsNullOrEmpty(tentTag))
            {
                var found = GameObject.FindGameObjectsWithTag(tentTag);
                foreach (var go in found) placedTents.Add(go.transform);
            }
            else if (worldParent)
            {
                foreach (Transform t in worldParent)
                    if (t.name.Contains("Tent")) placedTents.Add(t);
            }
        }

        EnsureCursorGhost();
        SetCursorAllowed(true);

        // synthesize first hover
        var pos = Input.mousePosition;
        pointerInside = IsOverMap(pos);
        if (cursorGhost) cursorGhost.SetActive(pointerInside);

        if (pointerInside)
        {
            var worldPos = ScreenToWorldOnMiniMap(pos);
            cursorGhost.transform.position = worldPos;

            bool inside = IsInsidePlayableArea(worldPos);
            bool valid  = inside && IsPositionFree(worldPos);
            bool isShadow = IsShadowAt(worldPos);
            string uiTag = isShadow ? shadowTag : GetGroundTagAt(worldPos);
            lastHoverGroundTag = uiTag;

            if (!valid)
            {
                TintGhost(cursorGhost, 0.5f, Color.red);
                currentHoverTint = Color.red;
            }
            else if (isShadow)
            {
                TintGhost(cursorGhost, ghostAlpha, shadowHoverColor);
                currentHoverTint = shadowHoverColor;
            }
            else if (!string.IsNullOrEmpty(uiTag) && groundMap.ContainsKey(uiTag))
            {
                TintGhost(cursorGhost, ghostAlpha, warnHoverColor);
                currentHoverTint = warnHoverColor;
            }
            else
            {
                TintGhost(cursorGhost, ghostAlpha, Color.white);
                currentHoverTint = Color.white;
            }

            SetCursorAllowed(valid);
            lastValid = valid;
        }

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        if (debugRaycast) LogTopRaycast("Open()");
    }

    public void Close()
    {
        pointerInside = false;
        if (cursorGhost) cursorGhost.SetActive(false);
        ResetCursor();
        if (panel) panel.SetActive(false);
    }

    // ---------------- Pointer events ----------------
    public void OnPointerEnter(PointerEventData e)
    {
        if (e.pointerEnter == miniMapImage.gameObject) pointerInside = true;
        if (cursorGhost) cursorGhost.SetActive(true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        pointerInside = false;
        if (cursorGhost) cursorGhost.SetActive(false);
        SetCursorAllowed(true);
    }

    public void OnPointerMove(PointerEventData e)
    {
        if (!IsOpen || !pointerInside) return;
        if (!IsOverMap(e.position)) return;

        EnsureCursorGhost();
        var pos = ScreenToWorldOnMiniMap(e.position);
        cursorGhost.transform.position = pos;

        bool inside = IsInsidePlayableArea(pos);
        bool valid  = inside && IsPositionFree(pos);

        bool isShadow = IsShadowAt(pos);
        string uiTag = isShadow ? shadowTag : GetGroundTagAt(pos);
        lastHoverGroundTag = uiTag;

        if (!valid)
        {
            TintGhost(cursorGhost, 0.5f, Color.red);
            currentHoverTint = Color.red;
        }
        else if (isShadow)
        {
            TintGhost(cursorGhost, ghostAlpha, shadowHoverColor);
            currentHoverTint = shadowHoverColor;
        }
        else if (!string.IsNullOrEmpty(uiTag) && groundMap.ContainsKey(uiTag))
        {
            TintGhost(cursorGhost, ghostAlpha, warnHoverColor);
            currentHoverTint = warnHoverColor;
        }
        else
        {
            TintGhost(cursorGhost, ghostAlpha, Color.white);
            currentHoverTint = Color.white;
        }

        if (valid != lastValid)
        {
            SetCursorAllowed(valid);
            lastValid = valid;
        }

        if (debugRaycast) LogTopRaycast("OnPointerMove");
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!IsOpen || !IsOverMap(e.position)) return;

        var pos = ScreenToWorldOnMiniMap(e.position);

        if (e.button == PointerEventData.InputButton.Right)
        {
            TryRemoveGhostAt(pos);
            return;
        }

        if (e.button != PointerEventData.InputButton.Left) return;
        if (!IsInsidePlayableArea(pos)) return;
        if (!IsPositionFree(pos)) return;

        string tagForUI = IsShadowAt(pos) ? shadowTag : GetGroundTagAt(pos);

        var go = Instantiate(tentPrefab, pos, Quaternion.identity, worldParent);
        TintGhost(go, ghostAlpha, currentHoverTint);
        DisableAllColliders(go); // ghost

        ghosts.Add(new Ghost { go = go, uiTag = tagForUI });

        if (!string.IsNullOrEmpty(tagForUI))
        {
            if (!UI_AddToSession(tagForUI))
                Debug.LogWarning($"[MapPanelController] No GroundIconEntry mapped for tag '{tagForUI}'.");
        }
    }

    // ---------------- Buttons ----------------
    void OnConfirm()
    {
        foreach (var gh in ghosts)
        {
            var g = gh.go;
            if (!g) continue;

            SolidifyFinalColliders(g);

            g.tag = tentTag;
            if (!placedTents.Contains(g.transform))
                placedTents.Add(g.transform);
        }

        UI_CommitSession();
        ghosts.Clear();
        DestroyCursorGhost();
        ResetCursor();

        Close();

        if (canvas_MapPlacement) canvas_MapPlacement.SetActive(false);
        if (mainMenuPanel)
        {
            ActivateHierarchyUpToCanvas(mainMenuPanel);
            mainMenuPanel.SetActive(true);
        }
    }

    // Make ONLY EdgeCollider2D solid (enabled + non-trigger)
    void SolidifyFinalColliders(GameObject go)
    {
        var cols = go.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols)
        {
            c.enabled = true;
            if (c is PolygonCollider2D) c.isTrigger = true;
            else                        c.isTrigger = false;
        }
    }

    public void OnCancel() => CloseAndReturnToMainMenu(rollbackSession: true);

    // Shared close logic used by both Cancel and Close (X)
    void CloseAndReturnToMainMenu(bool rollbackSession)
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
            if (ghosts[i].go) Destroy(ghosts[i].go);
        ghosts.Clear();
        DestroyCursorGhost();
        ResetCursor();
        if (rollbackSession) UI_RollbackSession();

        Close();

        if (canvas_MapPlacement) canvas_MapPlacement.SetActive(false);

        if (mainMenuPanel)
        {
            ActivateHierarchyUpToCanvas(mainMenuPanel);
            mainMenuPanel.SetActive(true);
        }
    }

    // ---------------- Helpers ----------------
    void ClearSession()
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
            if (ghosts[i].go) Destroy(ghosts[i].go);
        ghosts.Clear();
        DestroyCursorGhost();
        lastValid = true;
        UI_RollbackSession();
    }

    void EnsureCursorGhost()
    {
        if (cursorGhost || !tentPrefab) return;
        cursorGhost = Instantiate(tentPrefab, Vector3.zero, Quaternion.identity, worldParent);
        TintGhost(cursorGhost, ghostAlpha, Color.white);
        DisableAllColliders(cursorGhost);
        cursorGhost.SetActive(false);
    }

    void DestroyCursorGhost()
    {
        if (cursorGhost) Destroy(cursorGhost);
        cursorGhost = null;
    }

    bool IsOverMap(Vector2 screenPos)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(miniMapImage.rectTransform, screenPos, null);
    }

    Vector3 ScreenToWorldOnMiniMap(Vector2 screenPos)
    {
        var rt = miniMapImage.rectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out var local);
        var r = rt.rect;
        float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
        var world = miniMapCamera.ViewportToWorldPoint(new Vector3(u, v, 0f));
        world.z = worldZ;
        return world;
    }

    // ---------- Playable-area check ----------
    bool IsInsidePlayableArea(Vector3 pos)
    {
        if (!playableArea) return true; // fail-open if not assigned
        return playableArea.OverlapPoint(new Vector2(pos.x, pos.y));
    }

    // ---------- Spatial rules ----------
    bool OverlapsNoBuild(Vector3 pos)
    {
        var prefabCollider = tentPrefab.GetComponentInChildren<Collider2D>(true);
        Vector2 size = footprintSize2D;
        if (prefabCollider) size = prefabCollider.bounds.size;

        Vector2 p2 = new Vector2(pos.x, pos.y);

        if (Physics2D.OverlapBox(p2, size, 0f, noBuildMask) != null)
            return true;

        var testBounds = new Bounds(new Vector3(pos.x, pos.y, 0f),
                                    new Vector3(size.x, size.y, 1f));

        foreach (var t in placedTents)
        {
            if (!t) continue;
            var cols = t.GetComponentsInChildren<Collider2D>(true);
            foreach (var c in cols)
            {
                if (!c.enabled) continue;
                if (c.OverlapPoint(p2)) return true;
                if (c.bounds.Intersects(testBounds)) return true;
            }
        }

        return false;
    }

    bool IsPositionFree(Vector3 pos)
    {
        if (OverlapsNoBuild(pos)) return false;

        float minSqr = minTentSpacing * minTentSpacing;

        for (int i = 0; i < placedTents.Count; i++)
        {
            var t = placedTents[i];
            if (!t) continue;
            if ((t.position - pos).sqrMagnitude < minSqr) return false;
        }

        for (int i = 0; i < ghosts.Count; i++)
        {
            var g = ghosts[i].go;
            if (!g) continue;
            if ((g.transform.position - pos).sqrMagnitude < minSqr) return false;
        }

        return true;
    }

    // ---------- Ground lookups & UI ----------
    string GetGroundTagAt(Vector3 pos)
    {
        var hit = Physics2D.OverlapPoint(new Vector2(pos.x, pos.y), groundMask);
        return hit ? hit.tag : null;
    }

    bool IsShadowAt(Vector3 pos)
    {
        var hit = Physics2D.OverlapPoint(new Vector2(pos.x, pos.y), shadowLayer);
        return hit && hit.CompareTag(shadowTag);
    }

    bool UI_AddToSession(string tag)
    {
        if (!groundMap.TryGetValue(tag, out var e)) return false;
        e.sessionCount++;
        SetIconCountText(e, e.persistentCount + e.sessionCount);
        SetIconFaded(e, false);
        return true;
    }

    void UI_RemoveOneFromSession(string tag)
    {
        if (!groundMap.TryGetValue(tag, out var e)) return;
        e.sessionCount = Mathf.Max(0, e.sessionCount - 1);
        int total = e.persistentCount + e.sessionCount;
        SetIconCountText(e, total);
        SetIconFaded(e, total == 0);
    }

    void UI_RollbackSession()
    {
        foreach (var e in groundIcons)
        {
            e.sessionCount = 0;
            SetIconCountText(e, e.persistentCount);
            SetIconFaded(e, (e.persistentCount == 0));
        }
    }

    void UI_CommitSession()
    {
        foreach (var e in groundIcons)
        {
            e.persistentCount += e.sessionCount;
            e.sessionCount = 0;
            SetIconCountText(e, e.persistentCount);
            SetIconFaded(e, e.persistentCount == 0);
        }
    }

    void SetIconFaded(GroundIconEntry e, bool faded)
    {
        if (!e?.iconGroup) return;
        e.iconGroup.alpha = faded ? fadedIconAlpha : 1f;
    }

    void SetIconCountText(GroundIconEntry e, int value)
    {
        if (e.tmpCounter) e.tmpCounter.text = value.ToString();
        if (e.uiCounter)  e.uiCounter.text = value.ToString();
    }

    // Use OS/default cursor when allowed, forbidden texture when not allowed
void SetCursorAllowed(bool allowed)
{
    if (CursorManager.Instance)
    {
        if (allowed) CursorManager.Instance.ApplyDefault();
        else         CursorManager.Instance.ApplyForbidden();
        return;
    }

    // fallback if no manager
    if (allowed) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    else if (cursorForbidden) Cursor.SetCursor(cursorForbidden, cursorHotspot, CursorMode.Auto);
}

void ResetCursor()
{
    if (CursorManager.Instance) CursorManager.Instance.ApplyDefault();
    else                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
}

    void TintGhost(GameObject go, float alpha, Color tint)
    {
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var c = tint; c.a = alpha;
            sr.color = c;
        }
    }

    void TryRemoveGhostAt(Vector3 cursorWorldPos)
    {
        if (ghosts.Count == 0) return;

        float bestSqr = rightClickRemoveRadius * rightClickRemoveRadius;
        int bestIndex = -1;

        for (int i = 0; i < ghosts.Count; i++)
        {
            var g = ghosts[i].go;
            if (!g) continue;
            float d = (g.transform.position - cursorWorldPos).sqrMagnitude;
            if (d <= bestSqr) { bestSqr = d; bestIndex = i; }
        }

        if (bestIndex == -1) return;

        var ghost = ghosts[bestIndex];
        if (ghost.go) Destroy(ghost.go);
        if (!string.IsNullOrEmpty(ghost.uiTag)) UI_RemoveOneFromSession(ghost.uiTag);
        ghosts.RemoveAt(bestIndex);
    }

    void DisableAllColliders(GameObject go)
    {
        var cols = go.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols) c.enabled = false;
    }

    bool EnableAllColliders(GameObject go, bool setTrigger = true)
    {
        bool found = false;
        var cols = go.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols)
        {
            c.enabled = true;
            if (setTrigger) c.isTrigger = true;
            found = true;
        }
        return found;
    }

    public void OpenInstructionsAgain()
    {
        if (panel) panel.SetActive(false);
        if (wizard) wizard.Show(true);  // return to map on X/ESC
    }

    void EnsureUIRootsOn()
    {
        var cv = GetComponentInParent<Canvas>(true);
        if (cv && !cv.gameObject.activeSelf) cv.gameObject.SetActive(true);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (panel && !panel.activeSelf) panel.SetActive(true);
    }

    // ======== NEW: fixes for pointer events after Skip ========
    void EnsurePointerReceiverSetup()
    {
        // 1) Our map image must be a raycast target and under a canvas with a GraphicRaycaster
        if (miniMapImage)
        {
            miniMapImage.raycastTarget = true;
            var cv = miniMapImage.canvas;
            if (cv)
            {
                var gr = cv.GetComponent<GraphicRaycaster>();
                if (gr && !gr.enabled) gr.enabled = true;

                var cg = cv.GetComponent<CanvasGroup>();
                if (cg)
                {
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
            }

            // 2) This component must be on the same GO or a parent of the raycast target
            //    (to receive IPointer* callbacks)
            if (!transform.IsChildOf(miniMapImage.transform) && !miniMapImage.transform.IsChildOf(transform))
            {
                Debug.LogWarning("[MapPanelController] This component is not an ancestor of miniMapImage. " +
                                 "Pointer events may not reach it. Put MapPanelController on miniMapImage or a parent.");
            }
        }

        // 3) Disable raycast blocking on known overlays that might remain active after Skip
        if (disableRaycastBlockersOnOpen != null)
        {
            foreach (var cg in disableRaycastBlockersOnOpen)
            {
                if (!cg) continue;
                cg.blocksRaycasts = false; // <- the key bit
            }
        }
    }

    void LogTopRaycast(string from)
    {
        if (!debugRaycast || EventSystem.current == null || miniMapImage == null) return;

        var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        if (results.Count > 0)
        {
            var top = results[0];
            Debug.Log($"[MapPanelController] {from} top raycast: {top.gameObject.name} (layer={top.gameObject.layer})");
        }
        else
        {
            Debug.Log($"[MapPanelController] {from} no UI hit.");
        }
    }
}
