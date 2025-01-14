using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform _rectTransform;
    private Canvas _rootCanvas;

    private Vector2 _dragStartPosition;
    private Vector2 _panelStartPosition;
    private bool _cameFromRight = true;

    [SerializeField] private Canvas _mainCameraCanvas;   // Canvas dla pierwszego ekranu
    [SerializeField] private Canvas _playersCameraCanvas;  // Canvas dla drugiego ekranu


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

        Vector2 screenPos = eventData.position;
        float primaryWidth = Display.displays[0].systemWidth;
        bool IsCursorOnSecondScreen(Vector2 pos) => pos.x > primaryWidth || pos.x < 0;

        // Określ docelowy Canvas na podstawie pozycji kursora
        Canvas targetCanvas = IsCursorOnSecondScreen(screenPos)
                            ? MultiScreenDisplay.Instance.PlayersCameraCanvas
                            : MultiScreenDisplay.Instance.MainCameraCanvas;

        if (_rootCanvas != targetCanvas)
        {
            // Zachowaj bieżącą pozycję ekranu panelu przed zmianą rodzica
            Vector2 panelScreenPos = RectTransformUtility.WorldToScreenPoint(_rootCanvas.worldCamera, _rectTransform.position);

            // Korekta pozycji X w zależności od kierunku przejścia
            if (_rootCanvas == MultiScreenDisplay.Instance.MainCameraCanvas && targetCanvas == MultiScreenDisplay.Instance.PlayersCameraCanvas)
            {
                // Przechodzimy z głównego na drugi ekran
                if(screenPos.x > primaryWidth)
                {
                    panelScreenPos.x -= primaryWidth;
                    _cameFromRight = true;
                }
                else if(screenPos.x < 0)
                {
                    panelScreenPos.x += primaryWidth;
                    _cameFromRight = false;
                }
            }
            else if (_rootCanvas == MultiScreenDisplay.Instance.PlayersCameraCanvas && targetCanvas == MultiScreenDisplay.Instance.MainCameraCanvas)
            {
                // Przechodzimy z drugiego na główny ekran
                if(_cameFromRight)
                {
                    // Powrót z prawej strony na główny ekran
                    panelScreenPos.x += primaryWidth;
                }
                else
                {
                    // Powrót z lewej strony na główny ekran
                    panelScreenPos.x -= primaryWidth;
                }
            }

            transform.SetParent(targetCanvas.transform, worldPositionStays: false);
            _rootCanvas = targetCanvas;
            _rectTransform.localScale = Vector3.one;

            // Przekształć skorygowaną pozycję ekranową na lokalną pozycję w nowym Canvasie
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.GetComponent<RectTransform>(),
                panelScreenPos,
                _rootCanvas.worldCamera,
                out Vector2 localPoint
            );
            _rectTransform.anchoredPosition = localPoint;

            // Zaktualizuj punkty początkowe dla kontynuacji przeciągania
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.GetComponent<RectTransform>(),
                eventData.position,
                _rootCanvas.worldCamera,
                out _dragStartPosition
            );
            _panelStartPosition = _rectTransform.anchoredPosition;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.GetComponent<RectTransform>(),
            eventData.position,
            _rootCanvas.worldCamera,
            out Vector2 currentDragPosition
        );

        Vector2 dragDelta = currentDragPosition - _dragStartPosition;
        _rectTransform.anchoredPosition = _panelStartPosition + dragDelta;
    }
}

