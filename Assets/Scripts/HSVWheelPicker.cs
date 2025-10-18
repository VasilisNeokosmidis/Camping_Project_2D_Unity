using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage)), RequireComponent(typeof(RectTransform))]
public class HSVWheelPicker : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("Wheel")]
    [SerializeField, Min(64)] int wheelSize = 256;
    [SerializeField, Range(0f, 1f)] float innerHole = 0f;

    [Header("Click Marker (ring)")]
    [SerializeField] float markerSize = 12f;          // diameter in px
    [SerializeField] float markerStroke = 2f;         // ring thickness in px
    [SerializeField] Color markerColor = Color.red;   // ring color

    public event Action<Color> onColorChanged;

    RawImage _img;
    RectTransform _rt;
    Texture2D _tex;

    // keep V internally; wheel edits only H & S
    float _v = 1f;

    Vector2 _pickUV = new(0.5f, 0.5f);

    // UI marker (runtime-generated ring sprite)
    RectTransform _markerRt;
    Image _markerImg;
    Texture2D _markerTex;
    Sprite _markerSprite;

    void Awake()
    {
        _img = GetComponent<RawImage>();
        _rt  = GetComponent<RectTransform>();

        BuildWheelTexture();
        EnsureMarker();
        UpdateMarker(_pickUV);
    }

    void OnDestroy()
    {
        if (_tex) Destroy(_tex);
        if (_markerSprite) Destroy(_markerSprite);
        if (_markerTex) Destroy(_markerTex);
    }

    void EnsureMarker()
    {
        if (_markerRt) return;

        var go = new GameObject("WheelMarker", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling(); // on top of the wheel

        _markerRt = go.GetComponent<RectTransform>();
        _markerRt.anchorMin = _markerRt.anchorMax = new Vector2(0.5f, 0.5f);
        _markerRt.pivot     = new Vector2(0.5f, 0.5f);
        _markerRt.sizeDelta = new Vector2(markerSize, markerSize);

        _markerImg = go.GetComponent<Image>();
        _markerImg.raycastTarget = false;

        RefreshMarkerVisual();
    }

    void RefreshMarkerVisual()
    {
        // rebuild a small ring texture/sprite
        if (_markerSprite) { Destroy(_markerSprite); _markerSprite = null; }
        if (_markerTex)    { Destroy(_markerTex);    _markerTex = null; }

        int size = Mathf.Max(4, Mathf.RoundToInt(markerSize));
        float stroke = Mathf.Clamp(markerStroke, 1f, size * 0.5f);

        _markerTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float rOuter = Mathf.Min(cx, cy);
        float rInner = Mathf.Max(0f, rOuter - stroke);

        // simple ring (transparent fill)
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            Color c = (r <= rOuter && r >= rInner) ? markerColor : new Color(0,0,0,0);
            _markerTex.SetPixel(x, y, c);
        }
        _markerTex.Apply(false, false);

        _markerSprite = Sprite.Create(_markerTex, new Rect(0, 0, size, size),
                                      new Vector2(0.5f, 0.5f), 100f);
        _markerImg.sprite = _markerSprite;
        _markerImg.color  = Color.white; // use texture’s red; keep center transparent
    }

    void BuildWheelTexture()
    {
        int w = Mathf.NextPowerOfTwo(wheelSize);
        _tex = new Texture2D(w, w, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        float cx = (w - 1) * 0.5f, cy = (w - 1) * 0.5f;
        float maxR = Mathf.Min(cx, cy), minR = maxR * innerHole;

        for (int y = 0; y < w; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = x - cx, dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);

            if (r > maxR || r < minR) { _tex.SetPixel(x, y, new Color(0,0,0,0)); continue; }

            float angle = Mathf.Atan2(dy, dx);
            float h = (angle / (2f * Mathf.PI) + 1f) % 1f;
            float s = Mathf.InverseLerp(minR, maxR, r);

            _tex.SetPixel(x, y, Color.HSVToRGB(h, s, 1f));
        }
        _tex.Apply(false, false);

        _img.texture = _tex;
        _img.color   = Color.white;
    }

    public void OnPointerDown(PointerEventData e) => Pick(e);
    public void OnDrag   (PointerEventData e)      => Pick(e);

    void Pick(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera, out var local);

        var rect = _rt.rect;
        Vector2 uv = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, local.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, local.y)
        );

        Vector2 center = new(0.5f, 0.5f);
        Vector2 v = uv - center;
        float r = v.magnitude;
        float maxRad = 0.5f;
        float minRad = maxRad * innerHole;

        if (r > maxRad) { v = v.normalized * maxRad; uv = center + v; }
        if (r < minRad) { v = v.normalized * minRad; uv = center + v; }

        _pickUV = uv;
        UpdateMarker(uv);
        EmitFromUV(uv);
    }

    void UpdateMarker(Vector2 uv)
    {
        if (!_markerRt) return;
        var rect = _rt.rect;
        float x = Mathf.Lerp(rect.xMin, rect.xMax, uv.x);
        float y = Mathf.Lerp(rect.yMin, rect.yMax, uv.y);
        _markerRt.anchoredPosition = new Vector2(x, y);
    }

    void EmitFromUV(Vector2 uv)
    {
        Vector2 center = new(0.5f, 0.5f);
        Vector2 d = uv - center;

        float angle = Mathf.Atan2(d.y, d.x);
        float h = (angle / (2f * Mathf.PI) + 1f) % 1f;

        float r = d.magnitude / 0.5f;
        float s = Mathf.Clamp01(Mathf.InverseLerp(innerHole, 1f, r));

        onColorChanged?.Invoke(Color.HSVToRGB(h, s, _v));
    }

    public void SetColor(Color c, bool invokeEvent = false)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        _v = v;

        float maxRad = 0.5f, minRad = maxRad * innerHole;
        float r = Mathf.Lerp(minRad, maxRad, s);
        float angle = h * Mathf.PI * 2f;

        Vector2 center = new(0.5f, 0.5f);
        Vector2 offset = new(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        _pickUV = center + offset;

        UpdateMarker(_pickUV);
        if (invokeEvent) onColorChanged?.Invoke(c);
    }

#if UNITY_EDITOR
    // So you can tweak marker size/thickness in the Inspector at runtime
    void OnValidate()
    {
        if (_markerImg) RefreshMarkerVisual();
        if (_markerRt)  _markerRt.sizeDelta = new Vector2(markerSize, markerSize);
    }
#endif
}
