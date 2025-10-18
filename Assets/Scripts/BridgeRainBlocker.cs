// BridgeRainBlocker.cs
using System.Collections.Generic;
using UnityEngine;

public class BridgeRainBlocker : MonoBehaviour
{
    [SerializeField] GameObject bridgesRoot;     // the "Bridges" parent
    [SerializeField] bool setIsTrigger = false;  // false = solid, true = trigger

    readonly List<Collider2D> cached = new();

    void Awake()
    {
        Cache();
    }

    void Cache()
    {
        cached.Clear();
        if (!bridgesRoot) return;

        foreach (Transform br in bridgesRoot.transform)
        {
            // grab any Box/Edge/etc. colliders on the bridge or its children
            cached.AddRange(br.GetComponentsInChildren<Collider2D>(true));
        }
    }

    // Hook these in WeatherManager UnityEvents
    public void EnableBridgeBlocker()
    {
        foreach (var c in cached)
        {
            if (!c) continue;
            c.enabled = true;
            c.isTrigger = setIsTrigger;
        }
    }

    public void DisableBridgeBlocker()
    {
        foreach (var c in cached)
        {
            if (!c) continue;
            c.enabled = false;
        }
    }
}
