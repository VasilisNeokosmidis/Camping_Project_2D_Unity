using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pulses brightness (HSV Value) of the provided TentPart2D list.
/// Add this component to the Tent root when Glow is ON.
/// </summary>
public class TentGlowDriver : MonoBehaviour
{
    [Tooltip("Tent parts to glow (assigned by TentCustomizerPanel2D).")]
    public TentPart2D[] parts;

    [Tooltip("How fast the pulse animates.")]
    public float speed = 2f;

    [Tooltip("How strong the pulse is (0..2, where 0.35 is subtle, 1.0 is strong).")]
    public float amplitude = 0.35f;

    // internal state
    readonly Dictionary<TentPart2D, Color> _base = new();
    float _t0;
    bool _running;

    /// <summary>Call after assigning parts/speed/amplitude.</summary>
    public void Run()
    {
        RefreshBasesFromCurrent();
        _running = true;
        enabled = true;
        // Debug.Log("[TentGlowDriver] Run with " + (_base.Count) + " base colors.");
    }

    /// <summary>Stops animation and restores original colors captured in RefreshBasesFromCurrent().</summary>
    public void StopAndRestore()
    {
        _running = false;
        foreach (var kv in _base)
            if (kv.Key) kv.Key.ApplyColor(kv.Value);
        _base.Clear();
        enabled = false;
        // Debug.Log("[TentGlowDriver] Stopped and restored.");
    }

    /// <summary>Re-captures each part's current color as the baseline for pulsing.</summary>
    public void RefreshBasesFromCurrent()
    {
        _base.Clear();
        if (parts == null) return;
        foreach (var p in parts)
            if (p && !string.IsNullOrEmpty(p.partId))
                _base[p] = p.GetCurrentColor();
        _t0 = Time.time;
    }

    void Update()
    {
        if (!_running || parts == null) return;

        float phase   = Mathf.Sin((Time.time - _t0) * speed) * amplitude; // -amp..+amp
        float factor  = 1f + phase; // 1 ± amplitude

        foreach (var p in parts)
        {
            if (!p || !_base.TryGetValue(p, out var c0)) continue;

            Color.RGBToHSV(c0, out var h, out var s, out var v);
            v = Mathf.Clamp01(v * factor);
            var c = Color.HSVToRGB(h, s, v);
            p.ApplyColor(c);
        }
    }
}
