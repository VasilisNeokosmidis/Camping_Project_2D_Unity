using System;
using UnityEngine;

public static class TentStateStore
{
    [Serializable]
    class StateDTO
    {
        public bool party = false;
        public bool glow  = false;
        public bool saveMode = false;
    }

    static string Key(string tentId) => $"TENT_STATE_{tentId}";

    public static event Action<string, bool> OnSaveModeChanged;

    public static void SaveParty(string tentId, bool on)
    {
        var s = LoadDTO(tentId);
        s.party = on;
        SaveDTO(tentId, s);
    }

    public static bool TryLoadParty(string tentId, out bool on)
    {
        on = LoadDTO(tentId).party;
        return true;
    }

    public static void SaveGlow(string tentId, bool on)
    {
        var s = LoadDTO(tentId);
        s.glow = on;
        SaveDTO(tentId, s);
    }

    public static bool TryLoadGlow(string tentId, out bool on)
    {
        on = LoadDTO(tentId).glow;
        return true;
    }

    public static void SaveSaveMode(string tentId, bool on, bool raiseEvent = true)
    {
        var s = LoadDTO(tentId);
        s.saveMode = on;
        SaveDTO(tentId, s);
        if (raiseEvent) OnSaveModeChanged?.Invoke(tentId, on);
    }

    public static bool TryLoadSaveMode(string tentId, out bool on)
    {
        on = LoadDTO(tentId).saveMode;
        return true;
    }

    static StateDTO LoadDTO(string tentId)
    {
        var json = PlayerPrefs.GetString(Key(tentId), string.Empty);
        if (string.IsNullOrEmpty(json)) return new StateDTO();
        try { return JsonUtility.FromJson<StateDTO>(json) ?? new StateDTO(); }
        catch { return new StateDTO(); }
    }

    static void SaveDTO(string tentId, StateDTO dto)
    {
        var json = JsonUtility.ToJson(dto);
        PlayerPrefs.SetString(Key(tentId), json);
        PlayerPrefs.Save();
    }
}
