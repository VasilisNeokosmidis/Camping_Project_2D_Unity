// TentInstance2D.cs
using UnityEngine;

public class TentInstance2D : MonoBehaviour
{
    [Tooltip("SO describing this tent type/interior.")]
    public TentDefinition def;

    [SerializeField, HideInInspector]
    string _tentInstanceId;                   // unique per placed tent

    public string TentInstanceId => _tentInstanceId;

    // Editor-time auto-ID (won't touch prefab asset; only scene instances)
    void OnValidate()
    {
#if UNITY_EDITOR
        if (!isActiveAndEnabled) return;

        // Only assign for scene instances, not prefab assets
        if (string.IsNullOrEmpty(_tentInstanceId) && gameObject.scene.IsValid())
        {
            _tentInstanceId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
