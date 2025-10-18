using UnityEngine;

public class ShelterMarker : MonoBehaviour
{
    [Tooltip("Anchor-στόχος μέσα στο prefab του shelter (π.χ. είσοδος).")]
    public Transform placementAnchor;

    void OnEnable()  => ShelterRegistry.Register(this);
    void OnDisable() => ShelterRegistry.Unregister(this);
}

public static class ShelterRegistry
{
    static readonly System.Collections.Generic.List<ShelterMarker> _all = new();
    public static System.Collections.Generic.IReadOnlyList<ShelterMarker> All => _all;

    public static void Register(ShelterMarker s) { if (!_all.Contains(s)) _all.Add(s); }
    public static void Unregister(ShelterMarker s) { _all.Remove(s); }
}
