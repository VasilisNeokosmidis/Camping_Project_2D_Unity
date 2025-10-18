using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Cursors")]
    public Texture2D defaultCursor;    // your game’s normal arrow
    public Vector2  defaultHotspot;    // e.g. (0,0) or (2,1)
    public Texture2D hoverCursor;      // your hand pointer
    public Vector2  hoverHotspot;      // e.g. fingertip (8,0) for 16–32px

    public Texture2D forbiddenCursor;
public Vector2  forbiddenHotspot;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyDefault();                      // set at boot
    }

    // CursorManager.cs
public void ApplyDefault()
{
    if (defaultCursor) Cursor.SetCursor(defaultCursor, defaultHotspot, CursorMode.Auto);
    else               Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // OS default
}

public void ApplyForbidden()
{
    if (forbiddenCursor) Cursor.SetCursor(forbiddenCursor, forbiddenHotspot, CursorMode.Auto);
}

    public void ApplyHover()
    {
        if (hoverCursor != null)
            Cursor.SetCursor(hoverCursor, hoverHotspot, CursorMode.Auto);
    }
}
