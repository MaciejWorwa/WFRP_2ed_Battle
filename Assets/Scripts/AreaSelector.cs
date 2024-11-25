using System.Collections.Generic;
using UnityEngine;

public class AreaSelector : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static AreaSelector instance;

    // Publiczny dostęp do instancji
    public static AreaSelector Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            // Jeśli instancja już istnieje, a próbujemy utworzyć kolejną, niszczymy nadmiarową
            Destroy(gameObject);
        }
    }

    private Vector3 _startPoint; // Początek prostokąta
    private Vector3 _endPoint; // Koniec prostokąta
    private Rect _selectionRect; // Obszar zaznaczenia
    private GameObject _visualization; // Wizualizacja prostokąta
    private LineRenderer _lineRenderer; // Obrys prostokąta

    [SerializeField] private LayerMask _selectableLayer; // Warstwa obiektów do zaznaczania
    public List<Unit> SelectedUnits;

    void Update()
    {
        if (Input.GetMouseButton(0) && SelectedUnits != null && SelectedUnits.Count > 0)
        {
            //Odznacza wizualnie zaznaczone jednostki
            for (int i = SelectedUnits.Count - 1; i >= 0; i--) 
            {
                SelectedUnits[i].GetComponent<Renderer>().material.color = SelectedUnits[i].DefaultColor;
            }
            SelectedUnits.Clear();
        }

        if(!GameManager.IsMapHidingMode && !UnitsManager.IsUnitRemoving && !UnitsManager.IsMultipleUnitsSelecting) return;

        if (Input.GetMouseButtonDown(0) && !GameManager.Instance.IsPointerOverPanel()) // Start rysowania prostokąta
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
        else return;

        // Utwórz Rect dla obszaru zaznaczenia
        _selectionRect = CreateSelectionRect(_startPoint, _endPoint);

        // Wykryj wszystkie Colliders2D wewnątrz prostokąta
        Collider2D[] colliders = Physics2D.OverlapAreaAll(_selectionRect.min, _selectionRect.max, _selectableLayer);
        
        bool collidersContainsUnit = false;
        SelectedUnits = new List<Unit>();

        // Obsłuż wykryte obiekty
        foreach (Collider2D collider in colliders)
        {
            //Zasłania lub odsłania obszary mapy
            if(GameManager.IsMapHidingMode)
            {
                MapEditor.Instance.CoverOrUncoverTile(collider);
                continue;
            }

            //Usuwa wszystkie jednostki w zaznaczonym obszarze
            if(collider.GetComponent<Unit>())
            {
                collidersContainsUnit = true;

                if(UnitsManager.IsUnitRemoving)
                {
                    UnitsManager.Instance.DestroyUnit(collider.gameObject);
                }
                else
                {
                    SelectedUnits.Add(collider.GetComponent<Unit>());
                }
            }
        }

        if(SelectedUnits.Count > 1)
        {
            //Gdy zaznaczamy więcej jednostek, to odznaczamy (w standardowy sposób) tą, która już była wybrana
            if(Unit.SelectedUnit != null)
            {
                Unit.SelectedUnit.GetComponent<Unit>().SelectUnit();
            }

            //Wyróżnia zaznaczone jednostki innym kolorem
            for (int i = SelectedUnits.Count - 1; i >= 0; i--) 
            {
                SelectedUnits[i].GetComponent<Renderer>().material.color = SelectedUnits[i].HighlightColor;
            }
        }

        if(collidersContainsUnit)
        {
            UnitsManager.IsUnitRemoving = false;
            UnitsManager.Instance.SelectMultipleUnitsMode(false);
        }
    }

    Vector3[] CalculateRectangleCorners(Vector3 start, Vector3 end)
    {
        // Oblicz wierzchołki prostokąta na podstawie punktów start i end
        return new Vector3[]
        {
            new Vector3(start.x, start.y, -6),
            new Vector3(start.x, end.y, -6),
            new Vector3(end.x, end.y, -6),
            new Vector3(end.x, start.y, -6)
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
