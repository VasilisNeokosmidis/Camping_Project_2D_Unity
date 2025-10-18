using System;
using System.Collections.Generic;
using UnityEngine;

public static class TentPaintStore
{
    // PlayerPrefs keys:  TENT_PAINT_<tentId>
    [Serializable]
    class ColorEntry { public string partId; public string rgba; }  // #RRGGBBAA
    [Serializable]
    class TentPaintDTO { public List<ColorEntry> entries = new(); }

    static string Key(string tentId) => $"TENT_PAINT_{tentId}";

    public static Dictionary<string, Color> GetForTent(string tentId, bool createIfMissing)
    {
        var json = PlayerPrefs.GetString(Key(tentId), string.Empty);
        if (string.IsNullOrEmpty(json))
            return createIfMissing ? new Dictionary<string, Color>() : null;

        try
        {
            var dto = JsonUtility.FromJson<TentPaintDTO>(json);
            var map = new Dictionary<string, Color>(StringComparer.Ordinal);
            if (dto?.entries != null)
            {
                foreach (var e in dto.entries)
                    if (!string.IsNullOrEmpty(e.partId) &&
                        ColorUtility.TryParseHtmlString(e.rgba, out var c))
                        map[e.partId] = c;
            }
            return map;
        }
        catch { return createIfMissing ? new Dictionary<string, Color>() : null; }
    }

    public static void SetColor(string tentId, string partId, Color color)
    {
        if (string.IsNullOrEmpty(tentId) || string.IsNullOrEmpty(partId)) return;

        var map = GetForTent(tentId, createIfMissing: true);
        map[partId] = color;
        SaveMap(tentId, map);
    }

    public static void Save(string tentId)
    {
        // No-op when using SetColor->SaveMap per call, but keep API for future batching.
        PlayerPrefs.Save();
    }

    public static bool TryGetColor(string tentId, string partId, out Color c)
    {
        c = default;
        var map = GetForTent(tentId, createIfMissing: false);
        return map != null && map.TryGetValue(partId, out c);
    }

    public static void ClearTent(string tentId)
    {
        PlayerPrefs.DeleteKey(Key(tentId));
    }

    static void SaveMap(string tentId, Dictionary<string, Color> map)
    {
        var dto = new TentPaintDTO();
        foreach (var kv in map)
        {
            dto.entries.Add(new ColorEntry
            {
                partId = kv.Key,
                rgba = "#" + ColorUtility.ToHtmlStringRGBA(kv.Value)
            });
        }
        var json = JsonUtility.ToJson(dto);
        PlayerPrefs.SetString(Key(tentId), json);
    }
}
