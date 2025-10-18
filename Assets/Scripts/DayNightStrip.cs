using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if USING_URP_2D
using UnityEngine.Rendering.Universal; // only if you want Light2D control
#endif

public class DayNightStrip : MonoBehaviour
{


    [Header("Strips (containers)")]
    public RectTransform leftStrip;    // Sun path
    public RectTransform rightStrip;   // Moon path

    [Header("Icons (children of strips)")]
    public RectTransform sunIcon;      // child of leftStrip
    public RectTransform moonIcon;     // child of rightStrip

    [Header("Fullscreen tint (black Image)")]
    public Image nightTint;

    [Header("Timing (seconds)")]
    public float transitionDuration = 30f; // day->night or night->day
    public float holdTopSeconds = 2f;  // pause when icon reaches top

    [Header("Brightness (alpha)")]
    [Range(0, 1)] public float dayAlpha = 0.05f; // bright
    [Range(0, 1)] public float nightAlpha = 0.75f; // dark

    [Header("Motion")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float verticalMarginPx = 0f;


#if USING_URP_2D
    public Light2D globalLight;             // drag your Global Light 2D here
    [Range(0f, 1f)] public float dayLightIntensity = 1f;
    [Range(0f, 1f)] public float nightLightIntensity = 0.25f;
#endif

    void OnEnable()
    {
        // Ensure night is darker than day even if inspector values were swapped.
        if (nightAlpha < dayAlpha)
        {
            float tmp = nightAlpha;
            nightAlpha = dayAlpha;
            dayAlpha = tmp;
        }

        ForceBottomAnchor(sunIcon);
        ForceBottomAnchor(moonIcon);

        // Start: Sun at top (day), Moon hidden below
        PlaceTopInside(leftStrip, sunIcon);
        PlaceBottomOff(rightStrip, moonIcon);
        sunIcon.gameObject.SetActive(true);
        moonIcon.gameObject.SetActive(false);

        // Daylight at start
        SetTint(dayAlpha);
#if USING_URP_2D
        if (globalLight) globalLight.intensity = dayLightIntensity;
#endif

        StartCoroutine(RunLoop());
    }

    IEnumerator RunLoop()
    {
        while (true)
        {
            // ---------- HOLD DAY ----------
            sunIcon.gameObject.SetActive(true);
            moonIcon.gameObject.SetActive(false);
            PlaceTopInside(leftStrip, sunIcon);
            SetTint(dayAlpha);
#if USING_URP_2D
            if (globalLight) globalLight.intensity = dayLightIntensity;
#endif
            yield return new WaitForSeconds(holdTopSeconds);

            // ---------- Day -> Night ----------
            moonIcon.gameObject.SetActive(true);
            yield return CrossMove(
                leftStrip, sunIcon, true,   // sun top->bottom
                rightStrip, moonIcon, true,   // moon bottom->top
                dayAlpha, nightAlpha
            );
            sunIcon.gameObject.SetActive(false);
            PlaceTopInside(rightStrip, moonIcon);
            SetTint(nightAlpha);
#if USING_URP_2D
            if (globalLight) globalLight.intensity = nightLightIntensity;
#endif

            // ---------- HOLD NIGHT ----------
            yield return new WaitForSeconds(holdTopSeconds);

            // ---------- Night -> Day ----------
            sunIcon.gameObject.SetActive(true);
            yield return CrossMove(
                leftStrip, sunIcon, false,  // sun bottom->top
                rightStrip, moonIcon, false,  // moon top->bottom
                nightAlpha, dayAlpha
            );
            moonIcon.gameObject.SetActive(false);
            PlaceTopInside(leftStrip, sunIcon);
            SetTint(dayAlpha);
#if USING_URP_2D
            if (globalLight) globalLight.intensity = dayLightIntensity;
#endif
        }
    }

    IEnumerator CrossMove(RectTransform sunStrip, RectTransform sun, bool sunFromTopToBottom,
                          RectTransform moonStrip, RectTransform moon, bool moonFromBottomToTop,
                          float tintFrom, float tintTo)
    {
        float sunStart = sunFromTopToBottom ? TopInsideY(sunStrip, sun) : BottomOffY(sun);
        float sunEnd = sunFromTopToBottom ? BottomOffY(sun) : TopInsideY(sunStrip, sun);
        float moonStart = moonFromBottomToTop ? BottomOffY(moon) : TopInsideY(moonStrip, moon);
        float moonEnd = moonFromBottomToTop ? TopInsideY(moonStrip, moon) : BottomOffY(moon);

        sun.anchoredPosition = new Vector2(0f, sunStart);
        moon.anchoredPosition = new Vector2(0f, moonStart);

        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            float u = curve.Evaluate(Mathf.Clamp01(t / transitionDuration));

            sun.anchoredPosition = new Vector2(0f, Mathf.Lerp(sunStart, sunEnd, u));
            moon.anchoredPosition = new Vector2(0f, Mathf.Lerp(moonStart, moonEnd, u));

            float a = Mathf.Lerp(tintFrom, tintTo, u);
            SetTint(a);
#if USING_URP_2D
            if (globalLight) globalLight.intensity = Mathf.Lerp(
                (tintFrom == dayAlpha) ? dayLightIntensity : nightLightIntensity,
                (tintTo   == dayAlpha) ? dayLightIntensity : nightLightIntensity,
                u);
#endif
            yield return null;
        }

        sun.anchoredPosition = new Vector2(0f, sunEnd);
        moon.anchoredPosition = new Vector2(0f, moonEnd);

        if (sunFromTopToBottom) sun.gameObject.SetActive(false);
        if (!moonFromBottomToTop) moon.gameObject.SetActive(false);
    }

    // ------- Helpers -------
    void ForceBottomAnchor(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); // bottom-center
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    float TopInsideY(RectTransform strip, RectTransform icon)
    {
        float h = icon.rect.height;
        float pivot = icon.pivot.y;
        return strip.rect.height - verticalMarginPx - h * (1f - pivot);
    }

    float BottomOffY(RectTransform icon) => -icon.rect.height;

    void PlaceTopInside(RectTransform strip, RectTransform icon)
        => icon.anchoredPosition = new Vector2(0f, TopInsideY(strip, icon));

    void PlaceBottomOff(RectTransform strip, RectTransform icon)
        => icon.anchoredPosition = new Vector2(0f, BottomOffY(icon));

    void SetTint(float a)
    {
        if (!nightTint) return;
        // Always black; only alpha varies
        nightTint.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
        // Also ensure the Image covers the whole screen if it’s on a Screen Space canvas
        // (Anchors 0..1 and sizeDelta 0..0 are recommended in the RectTransform)
    }
    
    
}
