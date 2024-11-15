using UnityEngine;

public class DraggableObject : MonoBehaviour
{
    private Vector3 _offset; // Przechowuje ró¿nicê miêdzy pozycj¹ obiektu a pozycj¹ kursora
    public static bool IsDragging = false;
    private Camera _mainCamera;
    private Vector3 _startPosition; // Pozycja przed przesuniêciem


    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void OnMouseDown()
    {
        IsDragging = true;

        //Zapisuje pocz¹tkow¹ pozycjê
        _startPosition = transform.position;

        // Oblicza offset miêdzy pozycj¹ obiektu a kursorem
        _offset = transform.position - GetMouseWorldPosition();
    }

    private void OnMouseDrag()
    {
        if (IsDragging)
        {
            // Aktualizuje pozycjê obiektu na podstawie kursora
            Vector3 newPosition = GetMouseWorldPosition() + _offset;
            newPosition.z = 0; // Ustaw Z na 0
            transform.position = newPosition;
        }
    }

    private void OnMouseUp()
    {
        //Sprawdza, czy obiekt zosta³ przesuniêty
        if(transform.position != _startPosition)
        {
            // Próbuje przypi¹æ obiekt do najbli¿szego pola siatki
            SnapToGrid();

            //Zwalnia poprzednie pole
            Collider2D collider = Physics2D.OverlapPoint(_startPosition);
            if (collider != null && collider.CompareTag("Tile"))
            {
                collider.GetComponent<Tile>().IsOccupied = false;
            }  
        }

        IsDragging = false;
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (_mainCamera == null)
        {
            _mainCamera= Camera.main;
        }

        // Pobiera pozycjê myszy w œwiecie gry
        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = _mainCamera.WorldToScreenPoint(transform.position).z;
        return _mainCamera.ScreenToWorldPoint(mouseScreenPosition);
    }

    private void SnapToGrid()
    {
        Collider2D[] colliders = Physics2D.OverlapPointAll(transform.position);
        foreach(var collider in colliders)
        if (collider != null && collider.CompareTag("Tile") && collider.GetComponent<Tile>().IsOccupied == false)
        {
            // Przesuwa obiekt do pozycji œrodka pola
            Vector3 tilePosition = collider.transform.position;
            tilePosition.z = 0; // Ustaw Z na 0
            transform.position = tilePosition;

            // Aktualizuje zajêtoœæ pola
            Tile tile = collider.GetComponent<Tile>();
            if (tile != null && !tile.IsOccupied)
            {
                tile.IsOccupied = true;
            }
        }
        else
        {
            transform.position = _startPosition;
        }

        //Je¿eli przesuwamy obiekt bêd¹cy jednostk¹ to odznaczamy j¹
        if (this.gameObject.GetComponent<Unit>() != null && Unit.SelectedUnit == this.gameObject)
        {
            this.gameObject.GetComponent<Unit>().SelectUnit();
        }
    }
}
