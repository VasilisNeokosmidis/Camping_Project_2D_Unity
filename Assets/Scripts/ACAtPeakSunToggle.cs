using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sets alpha of Finished_Tent/*/Battery/AC sprites based on day/peak conditions,
/// using the same LightFactor01 source as the Energy Panel.
/// </summary>
public class ACAtPeakSunToggle : MonoBehaviour
{
    public enum TurnOnCondition { Day, Peak }

    [Header("References")]
    [SerializeField] DayNightLightMeter meter;   // auto-grab if null

    [Header("Condition")]
    [SerializeField] TurnOnCondition condition = TurnOnCondition.Day;

    [Tooltip("When ON condition is Day, use this threshold unless syncing from a TentBatteryController.")]
    [Range(0f, 1f)] public float dayThreshold = 0.5f;

    [Tooltip("Try to read dayThreshold from any TentBatteryController in the scene (keeps UI & AC aligned).")]
    public bool syncDayThresholdFromTent = true;

    [Tooltip("When ON condition is Peak, AC turns on if LightFactor01 >= this.")]
    [Range(0.80f, 1.00f)] public float peakLightThreshold = 0.95f;

    [Header("Hierarchy Search")]
    [Tooltip("Scene object names that count as a 'finished' tent root (prefix match).")]
    [SerializeField] string finishedTentNamePrefix = "Finished_Tent";

    [Tooltip("Relative path from tent root to the AC sprite.")]
    [SerializeField] string acRelativePath = "Battery/AC";

    [Header("Alpha values")]
    [Tooltip("Alpha when condition is met (AC ON).")]
    [Range(0f, 1f)] public float onAlpha = 1f;
    [Tooltip("Alpha when condition is not met (AC OFF).")]
    [Range(0f, 1f)] public float offAlpha = 0.35f;

    [Header("Rescan")]
    [Tooltip("Rescan the scene for new Finished_Tent objects (seconds). Set 0 to scan once.")]
    [Range(0f, 30f)] public float rescanInterval = 2f;

    // fields
    readonly List<SpriteRenderer> _acSprites = new List<SpriteRenderer>();
    readonly Dictionary<SpriteRenderer, string> _srToTentId = new Dictionary<SpriteRenderer, string>();
    float _nextScanTime;
    bool _syncedTentThreshold = false;

    

    void Awake()
    {
        if (!meter) meter = DayNightLightMeter.Instance;
        MaybeSyncDayThreshold();
        ScanScene();
    }

void Update()
{
    if (rescanInterval > 0f && Time.time >= _nextScanTime)
    {
        _nextScanTime = Time.time + rescanInterval;
        ScanScene();
    }

    if (syncDayThresholdFromTent && !_syncedTentThreshold)
        MaybeSyncDayThreshold();

    float light01 = meter ? meter.LightFactor01 : 1f;

    bool turnOn = condition == TurnOnCondition.Day
        ? (light01 >= dayThreshold)          // align with TentBatteryController.IsDayNow
        : (light01 >= peakLightThreshold);   // stricter "sun at peak" option

    float targetABase = turnOn ? onAlpha : (102f / 255f);

    for (int i = _acSprites.Count - 1; i >= 0; i--)
    {
        var sr = _acSprites[i];
        if (!sr) { _acSprites.RemoveAt(i); continue; }

        // Save Mode override
        string tentId;
        bool inSaveMode = _srToTentId.TryGetValue(sr, out tentId)
                          && TentStateStore.TryLoadSaveMode(tentId, out var sm) && sm;

        // ON = full alpha, OFF = 102/255
        float targetA = (!inSaveMode && turnOn) ? onAlpha : (102f / 255f);

        var c = sr.color;
        if (!Mathf.Approximately(c.a, targetA))
        {
            c.a = targetA;
            sr.color = c;
        }
    }
}



[SerializeField] TentBatteryController thresholdSource; // drag one in Inspector

void MaybeSyncDayThreshold()
{
    if (!syncDayThresholdFromTent) return;

    var src = thresholdSource;
#if UNITY_2023_1_OR_NEWER
    if (!src) src = Object.FindFirstObjectByType<TentBatteryController>(FindObjectsInactive.Include);
#else
    if (!src) src = Object.FindObjectOfType<TentBatteryController>();
#endif

    if (src)
    {
        dayThreshold = src.dayThreshold;
        _syncedTentThreshold = true;
    }
}

    [ContextMenu("Scan Scene Now")]
public void ScanScene()
{
    _acSprites.Clear();
    _srToTentId.Clear();

    var allRoots = gameObject.scene.GetRootGameObjects();
    foreach (var root in allRoots)
    {
        if (!root.name.StartsWith(finishedTentNamePrefix)) continue;

        // tent id (via TentInstance2D if present)
        var tentInst = root.GetComponentInChildren<TentInstance2D>(true);
        string tentId = tentInst ? tentInst.TentInstanceId : root.name; // fallback to name

        var acTf = root.transform.Find(acRelativePath);
        if (!acTf) continue;

        var sr = acTf.GetComponent<SpriteRenderer>();
        if (!sr) continue;

        _acSprites.Add(sr);
        _srToTentId[sr] = tentId;
    }
}

}
