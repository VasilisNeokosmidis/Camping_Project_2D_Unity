using UnityEngine;
using System;
using System.Collections.Generic;

public class DeliveryTruckSpawner : MonoBehaviour
{
    [Header("Prefab & Scene Refs")]
    [SerializeField] private GameObject deliveryTruckPrefab;   // must have DeliveryTruckController + DeliveryTruckAgent
    [SerializeField] private Transform depot;                   // spawn/return position
    [SerializeField] private PreviewPathfinder2D pathfinder;   // shared grid/pathfinder

    int counter = 0;

    void Awake()
    {
        if (!pathfinder) pathfinder = FindObjectOfType<PreviewPathfinder2D>();
        if (!depot) depot = transform; // fallback to self as spawn
    }

    /// <summary>
    /// Spawns a truck, precomputes the full path to the tent, returns controller,
    /// and also outputs the planned ETA in seconds (road distance / truck speed).
    /// </summary>
    public DeliveryTruckController SpawnAndDeliver(
        Transform tentAnchor,
        Action onReachedTent,
        Action onComplete,
        out float plannedSeconds,
        float fallbackSpeedIfNeeded = 3.5f)
    {
        plannedSeconds = 0f;

        if (!deliveryTruckPrefab || !depot || !pathfinder || !tentAnchor)
        {
            Debug.LogWarning("[DeliveryTruckSpawner] Missing prefab/depot/pathfinder/anchor.");
            return null;
        }

        // Instantiate
        var go = Instantiate(deliveryTruckPrefab, depot.position, Quaternion.identity);
        go.name = $"Delivery_Truck ({++counter})";

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
#if UNITY_2021_3_OR_NEWER
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
#endif
        }

        var agent = go.GetComponent<DeliveryTruckAgent>();
        var ctrl  = go.GetComponent<DeliveryTruckController>();
        if (!agent || !ctrl)
        {
            Debug.LogError("[DeliveryTruckSpawner] Prefab missing DeliveryTruckAgent/DeliveryTruckController.");
            Destroy(go);
            return null;
        }

        // Build the path ONCE here so we can compute an accurate ETA.
        pathfinder.BuildGrid();
        var start = (Vector2)go.transform.position;
        var goal  = (Vector2)tentAnchor.position;

        var path = pathfinder.FindPath(start, goal);
        if (path == null || path.Count == 0)
        {
            // If the grid/path failed, fall back to a 2-point straight line so the game still works
            path = new List<Vector3> { start, goal };
        }
        else
        {
            // ensure exact goal at the end
            if ((path[path.Count - 1] - (Vector3)goal).sqrMagnitude > 0.0001f)
                path.Add(goal);
        }

        // Compute road length
        float length = 0f;
        for (int i = 1; i < path.Count; i++)
            length += Vector3.Distance(path[i - 1], path[i]);

        float speed = (ctrl != null && ctrl.Speed > 0f) ? ctrl.Speed : Mathf.Max(0.01f, fallbackSpeedIfNeeded);
        plannedSeconds = length / speed;

        // Hand the prebuilt path to the agent
        agent.InitWithPrebuiltPath(pathfinder, tentAnchor, path, onReachedTent, onComplete);
        return ctrl;
    }
}
