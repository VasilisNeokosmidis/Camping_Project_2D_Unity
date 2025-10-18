using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class ShelterMarkPointer : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    MarkController controller;
    bool _hover;

    void Awake() => controller = GetComponentInParent<MarkController>();

    public void OnPointerEnter(PointerEventData e)
    {
        _hover = true;
        CursorManager.Instance?.ApplyHover();     // ← hand
    }

    public void OnPointerExit(PointerEventData e)
    {
        _hover = false;
        CursorManager.Instance?.ApplyDefault();   // ← back to your game’s default (not Windows)
    }

    public void OnPointerClick(PointerEventData e)
    {
        controller?.OpenPanel();
    }

    void OnDisable()
    {
        if (_hover) CursorManager.Instance?.ApplyDefault();
        _hover = false;
    }
}
