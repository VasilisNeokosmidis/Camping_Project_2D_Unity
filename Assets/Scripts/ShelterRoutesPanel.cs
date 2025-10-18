using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShelterRoutesPanel : MonoBehaviour
{
    [Header("UI")]
    public GameObject canvasRoot;    // Canvas_Shelter
    public GameObject panelRoot;     // Shelter_Panel
    public RawImage previewImage;

    [Header("Preview Camera")]
    public Camera previewCamera;

    [Header("Pathfinding")]
    public PreviewPathfinder2D pathfinder;
    public Transform startFromSign;

    [Header("Preview Output")]
    public RenderTexture previewRT;

    [Header("Lines (PreviewOnly)")]
    public string previewOnlyLayerName = "PreviewOnly";
    public float lineWidth = 0.15f;
    public Material lineMaterial;

    [Header("Live updates")]
    public bool refreshWhileOpen = false;
    public float refreshEvery = 0.5f;

    [Header("Integration")]
    public bool startShelterFlowOnOpen = true;

    [Header("Camera Fixed Position")]
    public Vector3 previewCamPosition = new Vector3(-15.1f, 4.7f, -10f);

    [Header("Panels (do NOT disable on close/stop)")]
    public GameObject canvas_MapControlCenter;
    public GameObject tentControlCenter_Panel;

    [Header("Other Panels")]
    public GameObject mainMenuPanel;

    [Header("Lines World Parent (NOT under Canvas)")]
    public Transform linesWorldParent;

    [Header("Debug")]
    public bool debugVerbose = false;

    public bool IsOpen { get; private set; }

    readonly List<LineRenderer> linePool = new();
    Coroutine refreshCo;

    const string LOG = "[SRP] ";

    void Awake()
    {
        if (!panelRoot) panelRoot = gameObject;
        IsOpen = false;
    }

    void EnsureWorldParent()
    {
        if (linesWorldParent) return;
        var go = GameObject.Find("PreviewOnlyWorld");
        if (!go) go = new GameObject("PreviewOnlyWorld");
        int layer = LayerMask.NameToLayer(previewOnlyLayerName);
        if (layer < 0) Debug.LogWarning($"{LOG}Layer '{previewOnlyLayerName}' missing.");
        go.layer = layer >= 0 ? layer : 0;
        linesWorldParent = go.transform;
        if (debugVerbose) Debug.Log($"{LOG}Using world parent: {go.name} (layer={LayerMask.LayerToName(go.layer)})");
    }

    public void OpenPanel()
    {
        if (canvasRoot) canvasRoot.SetActive(true);
        if (panelRoot)  panelRoot.SetActive(true);
        IsOpen = true;

        EnsureWorldParent();

        if (previewCamera)
        {
            previewCamera.transform.position = previewCamPosition;
            if (debugVerbose)
                Debug.Log($"{LOG}PreviewCamera pos set to {previewCamPosition}. CullingMask={LayerMaskToString(previewCamera.cullingMask)} targetTexture={(previewCamera.targetTexture?previewCamera.targetTexture.name:"<null>")}");
        }

        EnsureStartFromSign();

        if (pathfinder)
        {
            if (debugVerbose) Debug.Log($"{LOG}Building grid ...");
            pathfinder.BuildGrid();
        }
        else
        {
            Debug.LogWarning($"{LOG}No pathfinder assigned.");
        }

        WirePreviewOutput();
        ComputeAndDrawAll();

        if (startShelterFlowOnOpen)
        {
            ShelterEntranceController.StartNavigationForAll();
            if (debugVerbose) Debug.Log($"{LOG}StartNavigationForAll() triggered.");
        }

        if (refreshWhileOpen)
        {
            refreshCo = StartCoroutine(LoopRefresh());
            if (debugVerbose) Debug.Log($"{LOG}LoopRefresh started @ {refreshEvery}s.");
        }
    }

    void WirePreviewOutput()
    {
        if (!previewCamera || !previewImage)
        {
            Debug.LogWarning($"{LOG}WirePreviewOutput skipped (camera/image null).");
            return;
        }
        if (!previewRT)
        {
            previewRT = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32);
            previewRT.name = "ShelterPreview_RT";
            previewRT.Create();
            if (debugVerbose) Debug.Log($"{LOG}Created RenderTexture {previewRT.name}.");
        }
        previewCamera.targetTexture = previewRT;
        previewImage.texture = previewRT;
        if (debugVerbose) Debug.Log($"{LOG}Preview wired: targetTexture={previewCamera.targetTexture?.name}, rawImage.texture={previewImage.texture?.name}");
    }

    public void ClosePanel()
    {
        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = null;

        foreach (var ln in linePool) if (ln) ln.positionCount = 0;

        if (panelRoot)  panelRoot.SetActive(false);
        if (canvasRoot) canvasRoot.SetActive(false);

        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        IsOpen = false;
        if (debugVerbose) Debug.Log($"{LOG}ClosePanel() → cleared lines & hid panel.");
    }

    public void StopNavigation()
    {
        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = null;

        foreach (var ln in linePool) if (ln) ln.positionCount = 0;

        if (panelRoot)  panelRoot.SetActive(false);
        if (canvasRoot) canvasRoot.SetActive(false);

        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        IsOpen = false;
        if (debugVerbose) Debug.Log($"{LOG}StopNavigation() → cleared lines & hid panel.");
    }

    IEnumerator LoopRefresh()
    {
        while (panelRoot && panelRoot.activeInHierarchy)
        {
            if (pathfinder)
            {
                pathfinder.BuildGrid();
                if (debugVerbose) Debug.Log($"{LOG}Grid rebuilt in refresh.");
            }
            ComputeAndDrawAll();
            yield return new WaitForSeconds(refreshEvery);
        }
    }

    void ComputeAndDrawAll()
    {
        if (!pathfinder)
        {
            Debug.LogWarning($"{LOG}No pathfinder, cannot compute.");
            return;
        }
        if (!startFromSign)
        {
            Debug.LogWarning($"{LOG}No startFromSign.");
            return;
        }

        var shelters = ShelterRegistry.All;
        EnsurePoolSize(shelters.Count);

        var bestIdx = -1;
        float bestCost = float.PositiveInfinity;
        var paths = new List<List<Vector3>>(shelters.Count);

        Vector3 startPos = startFromSign.position;
        if (debugVerbose) Debug.Log($"{LOG}Compute from StartPos {startPos} to {shelters.Count} shelters.");

        for (int i = 0; i < shelters.Count; i++)
        {
            var shelter = shelters[i];
            List<Vector3> path = null;

            if (shelter && shelter.placementAnchor)
            {
                Vector3 goal = shelter.placementAnchor.position;
                path = pathfinder.FindPath(startPos, goal);
                if (debugVerbose)
                {
                    Debug.Log($"{LOG}Shelter[{i}] goal={goal} pathLen={(path==null?0:path.Count)}");
                }
            }
            else
            {
                if (debugVerbose) Debug.LogWarning($"{LOG}Shelter[{i}] missing placementAnchor.");
            }

            if (path == null)
                if (debugVerbose) Debug.LogWarning($"{LOG}Shelter[{i}] path is NULL.");

            paths.Add(path);

            float cost = PathCost(path);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestIdx = i;
            }
        }

        for (int i = 0; i < shelters.Count; i++)
        {
            var lr = linePool[i];
            var path = paths[i];

            DrawPath(lr, path);

            if (i == bestIdx && path != null && path.Count > 1)
                ColorLine(lr, Color.green);
            else
                ColorLine(lr, Color.red);
        }

        if (debugVerbose)
            Debug.Log($"{LOG}Best path index={bestIdx} bestCost={bestCost}");
    }

    void EnsurePoolSize(int count)
{
    while (linePool.Count < count)
    {
        var go = new GameObject("ShelterRoute_Line");
        if (linesWorldParent) go.transform.SetParent(linesWorldParent, false);
        int layer = LayerMask.NameToLayer(previewOnlyLayerName);
        go.layer = layer >= 0 ? layer : 0;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        lr.startWidth = lineWidth;
        lr.endWidth   = lineWidth;
        lr.material = lineMaterial
            ? new Material(lineMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

        // ⬇️ εδώ αλλάζουμε
        lr.sortingLayerName = "Background";
        lr.sortingOrder     = 100;

        linePool.Add(lr);

        if (debugVerbose)
            Debug.Log($"{LOG}LineRenderer created on layer {LayerMask.LayerToName(go.layer)} sortLayer={lr.sortingLayerName} sortOrder={lr.sortingOrder}");
    }

    for (int i = count; i < linePool.Count; i++)
        linePool[i].positionCount = 0;
}

    void DrawPath(LineRenderer lr, List<Vector3> pts)
    {
        if (!lr) return;
        if (pts == null || pts.Count == 0)
        {
            lr.positionCount = 0;
            if (debugVerbose) Debug.Log($"{LOG}DrawPath: empty/null path → hide line.");
            return;
        }

        lr.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            p.z = -0.1f; // να βγαίνει πάνω από tiles
            lr.SetPosition(i, p);
        }

        if (debugVerbose)
        {
            Debug.Log($"{LOG}DrawPath: set {pts.Count} points. p0={lr.GetPosition(0)} pLast={lr.GetPosition(lr.positionCount-1)}");
        }
    }

    float PathCost(List<Vector3> pts)
    {
        if (pts == null || pts.Count < 2) return float.PositiveInfinity;
        float sum = 0f;
        for (int i = 1; i < pts.Count; i++)
            sum += Vector3.Distance(pts[i - 1], pts[i]);
        return sum;
    }

    void ColorLine(LineRenderer lr, Color c)
    {
        if (!lr) return;
        lr.startColor = c;
        lr.endColor   = c;
    }

    void EnsureStartFromSign()
    {
        if (!startFromSign)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) startFromSign = player.transform;
        }
        if (debugVerbose)
        {
            Debug.Log($"{LOG}StartFromSign={(startFromSign?startFromSign.name:"<null>")} pos={(startFromSign?startFromSign.position:Vector3.zero)}");
        }
    }

    string LayerMaskToString(LayerMask m)
    {
        var names = new List<string>();
        for (int i = 0; i < 32; i++)
            if ((m.value & (1 << i)) != 0) names.Add(LayerMask.LayerToName(i));
        return string.Join(",", names);
    }
}
