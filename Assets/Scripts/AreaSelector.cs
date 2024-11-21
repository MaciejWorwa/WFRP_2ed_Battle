using System.Collections.Generic;
using UnityEngine;

public class AreaSelector : MonoBehaviour
{
    private Vector3 _startPoint; // Początek prostokąta
    private Vector3 _endPoint; // Koniec prostokąta
    private Rect _selectionRect; // Obszar zaznaczenia
    private GameObject _visualization; // Wizualizacja prostokąta
    private LineRenderer _lineRenderer; // Obrys prostokąta

    [SerializeField] private LayerMask _selectableLayer; // Warstwa obiektów do zaznaczania

    void Update()
    {
        if(!GameManager.IsMapHidingMode && !UnitsManager.IsUnitRemoving) return;

        if (Input.GetMouseButtonDown(0)) // Start rysowania prostokąta
        {
            StartSelection();
        }

        if (Input.GetMouseButton(0)) // Rysowanie prostokąta w trakcie przeciągania myszy
        {
            UpdateSelection();
        }

        if (Input.GetMouseButtonUp(0)) // Zakończenie zaznaczania
        {
            EndSelection();

            //Odświeża pola w zasięgu ruchu, aby uwzględnić nowo odsłonięte pola
            if(Unit.SelectedUnit != null)
            {
                GridManager.Instance.HighlightTilesInMovementRange(Unit.SelectedUnit.GetComponent<Stats>());
            }
        }
    }

    void StartSelection()
    {
        // Usuń istniejący SelectionBox, jeśli jeszcze nie został usunięty
        if (_visualization != null)
        {
            Destroy(_visualization);
        }

        // Ustaw początkowy punkt (współrzędne ekranu na świat)
        _startPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        _startPoint.z = 0;

        // Tworzenie wizualizacji prostokąta
        _visualization = new GameObject("SelectionBox");
        _lineRenderer = _visualization.AddComponent<LineRenderer>();
        _lineRenderer.positionCount = 5; // Prostokąt ma 4 wierzchołki + powrót do początku
        _lineRenderer.startWidth = 0.05f;
        _lineRenderer.endWidth = 0.05f;
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default")); // Domyślny shader
        _lineRenderer.startColor = Color.green;
        _lineRenderer.endColor = Color.green;
        _lineRenderer.loop = true;
    }

    void UpdateSelection()
    {
        // Ustaw końcowy punkt
        _endPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        _endPoint.z = 0;

        // Oblicz wierzchołki prostokąta
        Vector3[] corners = CalculateRectangleCorners(_startPoint, _endPoint);

        // Ustaw wierzchołki w LineRenderer
        if(_lineRenderer != null)
        {
            _lineRenderer.SetPositions(new Vector3[] { corners[0], corners[1], corners[2], corners[3], corners[0] });
        }
    }

    void EndSelection()
    {
        // Usuń wizualizację prostokąta
        if (_visualization != null)
        {
            Destroy(_visualization);
            _visualization = null; // Zapobieganie przypadkowemu ponownemu użyciu
        }

        // Utwórz Rect dla obszaru zaznaczenia
        _selectionRect = CreateSelectionRect(_startPoint, _endPoint);

        // Wykryj wszystkie Colliders2D wewnątrz prostokąta
        Collider2D[] colliders = Physics2D.OverlapAreaAll(_selectionRect.min, _selectionRect.max, _selectableLayer);
        
        bool collidersContainsUnit = false;

        // Obsłuż wykryte obiekty
        foreach (Collider2D collider in colliders)
        {
            //Zasłania lub odsłania obszary mapy
            if(GameManager.IsMapHidingMode)
            {
                MapEditor.Instance.CoverOrUncoverTile(collider);
            }
            else if(UnitsManager.IsUnitRemoving && collider.GetComponent<Unit>()) //Usuwa wszystkie jednostki w zaznaczonym obszarze
            {
                collidersContainsUnit = true;
                UnitsManager.Instance.DestroyUnit(collider.gameObject);
            }   
        }

        if(collidersContainsUnit)
        {
            UnitsManager.IsUnitRemoving = false;
        }
    }

    Vector3[] CalculateRectangleCorners(Vector3 start, Vector3 end)
    {
        // Oblicz wierzchołki prostokąta na podstawie punktów start i end
        return new Vector3[]
        {
            new Vector3(start.x, start.y, 0),
            new Vector3(start.x, end.y, 0),
            new Vector3(end.x, end.y, 0),
            new Vector3(end.x, start.y, 0)
        };
    }

    Rect CreateSelectionRect(Vector3 start, Vector3 end)
    {
        // Tworzenie prostokąta na podstawie punktów start i end
        float xMin = Mathf.Min(start.x, end.x);
        float yMin = Mathf.Min(start.y, end.y);
        float xMax = Mathf.Max(start.x, end.x);
        float yMax = Mathf.Max(start.y, end.y);

        return new Rect(new Vector2(xMin, yMin), new Vector2(xMax - xMin, yMax - yMin));
    }
}
