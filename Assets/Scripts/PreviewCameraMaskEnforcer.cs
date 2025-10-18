using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class PreviewCameraMaskEnforcer : MonoBehaviour
{
    [Tooltip("Camera to enforce the mask on. Defaults to this GameObject's Camera.")]
    public Camera targetCamera;

    [Tooltip("Layers to EXCLUDE from the preview camera (default: Player only).")]
    public LayerMask excludedLayers;

    void Reset()
    {
        targetCamera = GetComponent<Camera>();
        // Default: exclude "Player" if it exists
        int player = LayerMask.NameToLayer("Player");
        if (player >= 0)
            excludedLayers = (1 << player);
    }

    void OnEnable()   => Apply();
    void OnValidate() => Apply();
    void LateUpdate() => Apply();     // overrides any runtime changes each frame

    void Apply()
    {
        if (!targetCamera) targetCamera = GetComponent<Camera>();
        if (!targetCamera) return;

        // Everything minus excluded
        int mask = ~excludedLayers.value;
        targetCamera.cullingMask = mask;
    }
}
