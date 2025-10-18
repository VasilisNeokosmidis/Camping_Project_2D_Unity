using UnityEngine;
using UnityEngine.EventSystems;  // Needed for pointer hover detection

public class QuitButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void QuitGame()
    {
        // Works in a built .exe/.app
        Application.Quit();

        // For testing in the Unity Editor
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // When mouse hovers over the button
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.ApplyHover();
    }

    // When mouse leaves the button
    public void OnPointerExit(PointerEventData eventData)
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.ApplyDefault();
    }
}
