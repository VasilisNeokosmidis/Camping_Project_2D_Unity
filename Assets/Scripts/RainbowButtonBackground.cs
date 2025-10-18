using UnityEngine;
using UnityEngine.UI;

public class RainbowButtonBackground : MonoBehaviour
{
    [SerializeField] private float speed = 1f; // how fast rainbow cycles
    private Image image;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    void Update()
    {
        if (!image) return;

        // Hue cycles from 0 → 1 continuously
        float hue = Mathf.Repeat(Time.time * speed, 1f);

        // Saturation and Value at full to get vivid rainbow
        Color rainbow = Color.HSVToRGB(hue, 1f, 1f);

        image.color = rainbow;
    }
}
