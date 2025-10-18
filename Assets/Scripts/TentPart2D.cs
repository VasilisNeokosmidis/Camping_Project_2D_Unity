// TentPart2D.cs
using UnityEngine;

[DisallowMultipleComponent]
public class TentPart2D : MonoBehaviour
{
    [Tooltip("Unique id for this part within the tent (e.g. left_top, right_side, door).")]
    public string partId = "part";

    [Tooltip("Renderers to tint. If empty, will auto-grab SpriteRenderer on this object and its children.")]
    public SpriteRenderer[] renderers;

    Color _originalColor = Color.white;

    void Reset()
    {
        var sr = GetComponentsInChildren<SpriteRenderer>(true);
        if (sr != null && sr.Length > 0) renderers = sr;
        if (renderers != null && renderers.Length > 0)
            _originalColor = renderers[0].color;
    }

    public void ApplyColor(Color c)
    {
        if (renderers == null || renderers.Length == 0) return;
        foreach (var r in renderers) r.color = c;
    }

    public Color GetCurrentColor()
    {
        if (renderers == null || renderers.Length == 0) return _originalColor;
        return renderers[0].color;
    }
}
