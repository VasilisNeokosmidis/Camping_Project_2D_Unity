using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(DeliveryTruckController))]
public class DeliveryTruckAgent : MonoBehaviour
{
    private PreviewPathfinder2D pathfinder;
    private Transform tentAnchor;
    private DeliveryTruckController controller;
    private DeliveryTruckPathPreview preview; // optional

    private Action onReachedTentCb;
    private Action onCompleteCb;

    // Legacy entry (kept if other code still calls it)
    public void Init(PreviewPathfinder2D pf, Transform anchor, Action onReachedTent, Action onComplete)
    {
        pathfinder = pf;
        tentAnchor = anchor;
        onReachedTentCb = onReachedTent;
        onCompleteCb = onComplete;

        if (!controller) controller = GetComponent<DeliveryTruckController>();
        if (!preview)    preview    = GetComponent<DeliveryTruckPathPreview>();

        // Build here (legacy)
        pathfinder.BuildGrid();
        var start = (Vector2)transform.position;
        var goal  = (Vector2)tentAnchor.position;

        var path = pathfinder.FindPath(start, goal);
        if (path == null || path.Count == 0) { Destroy(gameObject); return; }
        if ((path[path.Count - 1] - (Vector3)goal).sqrMagnitude > 0.0001f)
            path.Add(goal);

        BeginWithPath(path);
    }

    // New entry: path already computed by the Spawner (also used for ETA).
    public void InitWithPrebuiltPath(PreviewPathfinder2D pf, Transform anchor, List<Vector3> prebuiltPath,
                                     Action onReachedTent, Action onComplete)
    {
        pathfinder = pf;
        tentAnchor = anchor;
        onReachedTentCb = onReachedTent;
        onCompleteCb = onComplete;

        if (!controller) controller = GetComponent<DeliveryTruckController>();
        if (!preview)    preview    = GetComponent<DeliveryTruckPathPreview>();

        if (prebuiltPath == null || prebuiltPath.Count == 0) { Destroy(gameObject); return; }
        BeginWithPath(prebuiltPath);
    }

    void BeginWithPath(List<Vector3> path)
    {
        if (preview)
        {
            preview.SetTentAnchor(tentAnchor);
            preview.SetPathLine(new List<Vector3>(path));
            if (preview.Mode == DeliveryTruckPathPreview.PreviewMode.Off) preview.Hide();
            else preview.Show();
        }

        controller.DeliverAndReturn(
            path,
            onReachedTent: () =>
            {
                onReachedTentCb?.Invoke();
            },
            onComplete: () =>
            {
                if (preview && preview.Mode == DeliveryTruckPathPreview.PreviewMode.DuringDelivery)
                    preview.Hide();
                onCompleteCb?.Invoke();
                Destroy(gameObject);
            });
    }
}
