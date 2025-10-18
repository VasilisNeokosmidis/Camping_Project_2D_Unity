using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class DeliveryTruckPathPreview : MonoBehaviour
{
    public enum PreviewMode
    {
        Off,            // never show
        DuringDelivery, // show while driving, hide when done
        AlwaysOn        // keep visible even after done
    }

    [Header("Preview Settings")]
    [SerializeField] private PreviewMode mode = PreviewMode.DuringDelivery;
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float zOffset = -0.1f;

    [Header("Pathfinding")]
    [SerializeField] private PreviewPathfinder2D pathfinder; // auto-found in Awake
    [SerializeField] private Transform tentPlacementAnchor;  // can be set at runtime

    LineRenderer lr;

    public PreviewMode Mode => mode;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 0;
        lr.startWidth = lr.endWidth = lineWidth;
        lr.useWorldSpace = true;

        if (!pathfinder) pathfinder = FindObjectOfType<PreviewPathfinder2D>();
        if (!tentPlacementAnchor)
        {
            var root = transform.root;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "PlacementAnchor") { tentPlacementAnchor = t; break; }
        }
    }

    public void SetTentAnchor(Transform anchor) => tentPlacementAnchor = anchor;

    /// <summary>Compute a path from current truck position to tent anchor.</summary>
    public List<Vector3> ComputePathFromTruckToAnchor()
    {
        if (!pathfinder || !tentPlacementAnchor) return null;
        pathfinder.BuildGrid();

        var start = (Vector2)transform.position;
        var goal  = (Vector2)tentPlacementAnchor.position;

        var path = pathfinder.FindPath(start, goal);
        if (path == null || path.Count == 0) return null;

        // ensure final point is exactly the anchor
        if ((path[path.Count - 1] - (Vector3)goal).sqrMagnitude > 0.0001f)
            path.Add(goal);

        return path;
    }

    /// <summary>Apply a path to the line (does not change visibility policy).</summary>
    public void SetPathLine(List<Vector3> path)
    {
        if (path == null || path.Count == 0) { lr.positionCount = 0; return; }
        // lift Z a bit for rendering order
        for (int i = 0; i < path.Count; i++)
            path[i] = new Vector3(path[i].x, path[i].y, zOffset);

        lr.positionCount = path.Count;
        lr.SetPositions(path.ToArray());
    }

    public void Show() { if (mode != PreviewMode.Off) lr.enabled = true; }
    public void Hide() { lr.enabled = false; }
}
