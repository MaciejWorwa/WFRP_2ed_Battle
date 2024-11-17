using UnityEngine;

public class DraggableObject : MonoBehaviour
{
    private Vector3 _offset; // Przechowuje różnicę między pozycją obiektu a pozycją kursora
    public static bool IsDragging = false;
    private Camera _mainCamera;
    private Vector3 _startPosition; // Pozycja przed przesunięciem


    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void OnMouseDown()
    {
        if(GameManager.IsMapHidingMode) return;
        
        IsDragging = true;

        //Zapisuje początkową pozycję
        _startPosition = transform.position;

        // Oblicza offset między pozycją obiektu a kursorem
        _offset = transform.position - GetMouseWorldPosition();
    }

    private void OnMouseDrag()
    {
        if (IsDragging)
        {
            // Aktualizuje pozycję obiektu na podstawie kursora
            Vector3 newPosition = GetMouseWorldPosition() + _offset;
            newPosition.z = 0; // Ustaw Z na 0
            transform.position = newPosition;
        }
    }

    private void OnMouseUp()
    {
        if(GameManager.IsMapHidingMode) return;

        //Sprawdza, czy obiekt został przesunięty
        if(transform.position != _startPosition)
        {
            // Próbuje przypiąć obiekt do najbliższego pola siatki
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

        // Pobiera pozycję myszy w świecie gry
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
            // Przesuwa obiekt do pozycji środka pola
            Vector3 tilePosition = collider.transform.position;
            tilePosition.z = 0; // Ustaw Z na 0
            transform.position = tilePosition;

            // Aktualizuje zajętość pola
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

        //Jeżeli przesuwamy obiekt będący jednostką to odznaczamy ją
        if (this.gameObject.GetComponent<Unit>() != null && Unit.SelectedUnit == this.gameObject)
        {
            this.gameObject.GetComponent<Unit>().SelectUnit();
        }
    }
}
