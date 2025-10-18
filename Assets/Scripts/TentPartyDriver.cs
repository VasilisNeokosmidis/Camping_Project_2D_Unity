using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Randomly changes colors for the given TentPart2D list.
/// If applyGlow is true, each random color is brightened by glowBoost.
/// Automatically restores the original colors when disabled/destroyed.
/// </summary>
public class TentPartyDriver : MonoBehaviour
{
    public TentPart2D[] parts;
    public float tickSeconds = 0.15f;

    // Extra brightness on top of randomization
    public bool applyGlow = false;
    [Range(1f, 2f)] public float glowBoost = 1.25f;

    private Coroutine _co;
    private readonly Dictionary<TentPart2D, Color> _snapshot = new();

    /// <summary>
    /// Start (or restart) party mode. If this component is disabled,
    /// enabling it will auto-start and snapshot.
    /// </summary>
    public void Run()
    {
        if (!isActiveAndEnabled)
        {
            // Let OnEnable() do the snapshot + start once we're enabled.
            enabled = true;
            return;
        }

        // Already enabled -> restart with a fresh snapshot.
        StopLoop();
        CaptureSnapshot();
        _co = StartCoroutine(Loop());
    }

    private void OnEnable()
    {
        // If enabled without calling Run(), still behave correctly.
        if (_co == null)
        {
            CaptureSnapshot();
            _co = StartCoroutine(Loop());
        }
    }

    private void OnDisable()
    {
        StopLoop();
        RestoreSnapshot();
    }

    private void OnDestroy()
    {
        // Safety if destroyed directly (eg. Save Mode)
        RestoreSnapshot();
    }

    private void StopLoop()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
    }

    private void CaptureSnapshot()
    {
        _snapshot.Clear();
        if (parts == null) return;

        foreach (var p in parts)
        {
            if (!p || string.IsNullOrEmpty(p.partId)) continue;
            // If the same part appears twice, the dictionary keeps one entry.
            _snapshot[p] = p.GetCurrentColor();
        }
    }

    private void RestoreSnapshot()
    {
        if (_snapshot.Count == 0) return;

        foreach (var kv in _snapshot)
        {
            var p = kv.Key;
            if (!p) continue;
            p.ApplyColor(kv.Value);
        }

        _snapshot.Clear();
    }

    private IEnumerator Loop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.01f, tickSeconds));

        while (true)
        {
            if (parts != null)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    if (!p || string.IsNullOrEmpty(p.partId)) continue;

                    // Bright, saturated colors
                    Color c = Random.ColorHSV(
                        0f, 1f,   // hue
                        0.7f, 1f, // saturation
                        0.7f, 1f, // value
                        1f, 1f    // alpha
                    );

                    if (applyGlow)
                    {
                        Color.RGBToHSV(c, out var h, out var s, out var v);
                        v = Mathf.Clamp01(v * glowBoost);
                        s = Mathf.Clamp01(s * Mathf.Lerp(1f, 1.08f, (glowBoost - 1f)));
                        c = Color.HSVToRGB(h, s, v);
                    }

                    p.ApplyColor(c);
                }
            }

            yield return wait;
        }
    }
}
