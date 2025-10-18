// CameraBackgroundByLight.cs
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraBackgroundByLight : MonoBehaviour
{
    [Header("References (optional)")]
    public DayNightLightMeter lightMeter;   // auto-resolves if left empty
    public Camera targetCamera;             // auto-resolves if left empty

    [Header("Colors")]
    [Tooltip("Sky color when LightFactor01 = 1 (full day).")]
    public Color daySky = new Color(0.45f, 0.75f, 1f); // soft sky blue
    [Tooltip("Background when LightFactor01 = 0 (full night).")]
    public Color night = Color.black;

    [Header("Smoothing")]
    [Tooltip("How quickly to ease the background (higher = snappier).")]
    [Range(0f, 10f)] public float lerpSpeed = 2f;

    void Awake()
    {
        if (!targetCamera) targetCamera = GetComponent<Camera>();
        if (!lightMeter)   lightMeter   = DayNightLightMeter.Instance;
    }

    void OnEnable()
    {
        if (targetCamera) targetCamera.clearFlags = CameraClearFlags.SolidColor;
    }

    void Update()
    {
        if (!targetCamera) return;

        // If the meter isn't found, assume full day so things don’t go black.
        float t = (lightMeter != null) ? Mathf.Clamp01(lightMeter.LightFactor01) : 1f;

        // Target color for this frame.
        Color target = Color.Lerp(night, daySky, t);

        // Exponential smoothing so it feels gradual and stable.
        float k = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        targetCamera.backgroundColor = Color.Lerp(targetCamera.backgroundColor, target, k);
    }
}
