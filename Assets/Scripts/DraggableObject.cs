using UnityEngine;

public class DraggableObject : MonoBehaviour
{
    private Vector3 _offset; // Przechowuje r�nic� mi�dzy pozycj� obiektu a pozycj� kursora
    public static bool IsDragging = false;
    private Camera _mainCamera;
    private Vector3 _startPosition; // Pozycja przed przesuni�ciem


    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void OnMouseDown()
    {
        IsDragging = true;

        //Zapisuje pocz�tkow� pozycj�
        _startPosition = transform.position;

        // Oblicza offset mi�dzy pozycj� obiektu a kursorem
        _offset = transform.position - GetMouseWorldPosition();
    }

    private void OnMouseDrag()
    {
        if (IsDragging)
        {
            // Aktualizuje pozycj� obiektu na podstawie kursora
            Vector3 newPosition = GetMouseWorldPosition() + _offset;
            newPosition.z = 0; // Ustaw Z na 0
            transform.position = newPosition;
        }
    }

    private void OnMouseUp()
    {
        //Sprawdza, czy obiekt zosta� przesuni�ty
        if(transform.position != _startPosition)
        {
            // Pr�buje przypi�� obiekt do najbli�szego pola siatki
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

        // Pobiera pozycj� myszy w �wiecie gry
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
            // Przesuwa obiekt do pozycji �rodka pola
            Vector3 tilePosition = collider.transform.position;
            tilePosition.z = 0; // Ustaw Z na 0
            transform.position = tilePosition;

            // Aktualizuje zaj�to�� pola
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

        //Je�eli przesuwamy obiekt b�d�cy jednostk� to odznaczamy j�
        if (this.gameObject.GetComponent<Unit>() != null && Unit.SelectedUnit == this.gameObject)
        {
            this.gameObject.GetComponent<Unit>().SelectUnit();
        }
    }
}
