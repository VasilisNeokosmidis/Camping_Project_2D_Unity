// DayNightLightMeter.cs
using UnityEngine;
using UnityEngine.UI;

public class DayNightLightMeter : MonoBehaviour
{
    public static DayNightLightMeter Instance { get; private set; }
    public Image nightTintImage; // assign your NightTint Image

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 1 = bright day (alpha 0), 0 = darkest night (alpha 1)
    public float LightFactor01
    {
        get
        {
            if (!nightTintImage) return 1f;
            float a = Mathf.Clamp01(nightTintImage.color.a);
            return 1f - a;
        }
    }
}
