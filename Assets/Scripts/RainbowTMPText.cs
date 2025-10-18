using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class RainbowTMPText : MonoBehaviour
{
    [SerializeField] private float speed = 1f; // how fast rainbow cycles
    private TMP_Text tmpText;

    void Awake()
    {
        tmpText = GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (!tmpText) return;

        // Cycle hue smoothly over time
        float hue = Mathf.Repeat(Time.time * speed, 1f);

        // Full saturation + brightness for vivid rainbow
        Color rainbow = Color.HSVToRGB(hue, 1f, 1f);

        tmpText.color = rainbow;
    }
}
