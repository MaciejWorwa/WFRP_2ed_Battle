using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform _rectTransform;
    private Canvas _rootCanvas;

    private Vector2 _dragStartPosition;
    private Vector2 _panelStartPosition;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _rootCanvas = GetRootCanvas();

        if (_rootCanvas == null)
        {
            Debug.LogError("Panel nie znajduje się w nadrzędnym obiekcie z przypisanym Canvasem!");
        }
    }

    private Canvas GetRootCanvas()
    {
        // Znajdź główny Canvas w hierarchii
        Transform current = transform;
        while (current != null)
        {
            Canvas canvas = current.GetComponent<Canvas>();
            if (canvas != null && canvas.isRootCanvas)
                return canvas;

            current = current.parent;
        }
        return null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_rootCanvas == null) return;

        // Pobranie początkowej pozycji kursora w lokalnym układzie Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.GetComponent<RectTransform>(),
            eventData.position,
            _rootCanvas.worldCamera,
            out _dragStartPosition
        );

        // Zapisanie bieżącej pozycji panelu
        _panelStartPosition = _rectTransform.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rootCanvas == null) return;

        // Pobranie bieżącej pozycji kursora w lokalnym układzie Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.GetComponent<RectTransform>(),
            eventData.position,
            _rootCanvas.worldCamera,
            out Vector2 currentDragPosition
        );

        // Obliczenie różnicy pozycji kursora
        Vector2 dragDelta = currentDragPosition - _dragStartPosition;

        // Zaktualizuj pozycję panelu
        _rectTransform.anchoredPosition = _panelStartPosition + dragDelta;
    }
}
