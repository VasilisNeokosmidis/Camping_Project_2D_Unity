using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BridgeForbiddenSpawner : MonoBehaviour
{
    [Header("Prefab to spawn while raining")]
    [SerializeField] GameObject forbiddenPrefab;

    [Header("Parent object that holds all bridge children")]
    [SerializeField] GameObject bridgesRoot;   // assign "Bridges" GO

    [Header("Spawn settings")]
    [SerializeField] Vector3 localOffset = Vector3.zero;
    [SerializeField] bool setSortingForSpawn = true;
    [SerializeField] string sortingLayerName = "Foreground";
    [SerializeField] int orderInLayer = 50;

    [Header("Rain events (hook in Inspector)")]
    public UnityEvent OnRainStarted;
    public UnityEvent OnRainStopped;

    readonly Dictionary<Transform, GameObject> activeMarkers = new();

    void OnEnable()
    {
        if (OnRainStarted != null) OnRainStarted.AddListener(SpawnAll);
        if (OnRainStopped != null) OnRainStopped.AddListener(DespawnAll);
    }
    void OnDisable()
    {
        if (OnRainStarted != null) OnRainStarted.RemoveListener(SpawnAll);
        if (OnRainStopped != null) OnRainStopped.RemoveListener(DespawnAll);
        DespawnAll();
    }

    public void SpawnAll()
    {
        if (!forbiddenPrefab || !bridgesRoot) return;

        foreach (Transform br in bridgesRoot.transform)
        {
            if (!br) continue;
            if (activeMarkers.TryGetValue(br, out var exists) && exists) continue;

            // Spawn under the bridge
            var go = Instantiate(forbiddenPrefab, br, true); // keep world space, then position locally
            var tf = go.transform;
            tf.localPosition = localOffset;
            tf.localRotation = Quaternion.identity;

            // ✅ Force WORLD scale to (1,1,1) at spawn
            KeepWorldScale.Apply(tf, Vector3.one);

            // ✅ Keep it stable if parent scales later
            var locker = go.GetComponent<KeepWorldScale>();
            if (!locker) locker = go.AddComponent<KeepWorldScale>();
            locker.targetWorldScale = Vector3.one;

            if (setSortingForSpawn)
            {
                foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sortingLayerName = sortingLayerName;
                    sr.sortingOrder = orderInLayer;
                }
            }

            activeMarkers[br] = go;
        }
    }

    public void DespawnAll()
    {
        foreach (var kv in activeMarkers)
            if (kv.Value) Destroy(kv.Value);
        activeMarkers.Clear();
    }
}
