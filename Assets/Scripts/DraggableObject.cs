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
        if(GameManager.IsMapHidingMode || MapEditor.IsElementRemoving || UnitsManager.IsMultipleUnitsSelecting ||  MovementManager.Instance.IsMoving) return;
        
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
        if(GameManager.IsMapHidingMode || MapEditor.IsElementRemoving || UnitsManager.IsMultipleUnitsSelecting ||  MovementManager.Instance.IsMoving) return;

        //Sprawdza, czy obiekt został przesunięty
        if(transform.position != _startPosition)
        {
            // Próbuje przypiąć obiekt do najbliższego pola siatki
            SnapToGrid(); 
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

    private bool SnapToGrid()
    { 
        // Jeżeli przesuwamy obiekt będący jednostką to odznaczamy ją
        if (this.gameObject.GetComponent<Unit>() != null && Unit.SelectedUnit == this.gameObject)
        {
            this.gameObject.GetComponent<Unit>().SelectUnit();
        }

        Vector2 offset = Vector2.zero;

        if (this.gameObject.GetComponent<MapElement>() != null)
        {
            BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();

            if (boxCollider.size.y > boxCollider.size.x) // Elementy zajmujące dwa pola
            {
                float rotationZ = transform.eulerAngles.z; // Pobiera wartość kąta w stopniach

                if (rotationZ < 45 || (rotationZ >= 135 && rotationZ < 225) || rotationZ > 315)
                {
                    offset = new Vector2(0, 0.5f);
                    Collider2D pointCollider = Physics2D.OverlapPoint(transform.position + (Vector3)offset);
                    if (pointCollider != null && pointCollider.gameObject != this.gameObject && (!pointCollider.CompareTag("Tile") || pointCollider.GetComponent<Tile>().IsOccupied))
                    {
                        transform.position = _startPosition;
                        return false; 
                    }  
                }
                else
                {
                    offset = new Vector2(-0.5f, 0);
                    Collider2D pointCollider = Physics2D.OverlapPoint(transform.position + (Vector3)offset);
                    if (pointCollider != null && pointCollider.gameObject != this.gameObject && (!pointCollider.CompareTag("Tile") || pointCollider.GetComponent<Tile>().IsOccupied))
                    {
                        transform.position = _startPosition;
                        return false; 
                    }  
                }
            }
            else if (transform.localScale.x > 1.5f) // Elementy zajmujące 4 pola
            {
                offset = new Vector2(-0.5f, 0.5f);
                Collider2D circleCollider = Physics2D.OverlapCircle(transform.position + (Vector3)offset, 0.8f);
                if (circleCollider != null && circleCollider.gameObject != this.gameObject && (!circleCollider.CompareTag("Tile") || circleCollider.GetComponent<Tile>().IsOccupied))
                {
                    transform.position = _startPosition;
                    return false; 
                }  
            }
        }

        Collider2D[] colliders = Physics2D.OverlapPointAll(transform.position);
        foreach (var collider in colliders)
        {
            if (collider != null && collider.CompareTag("Tile") && collider.GetComponent<Tile>().IsOccupied == false)
            {
                // Przesuwa obiekt do pozycji środka pola z ewentualnym offsetem
                transform.position = (Vector2)collider.transform.position + offset;

                Physics2D.SyncTransforms();
                
                // Aktualizowanie zajętości pól
                GridManager.Instance.CheckTileOccupancy();
                if(Unit.SelectedUnit != null)
                {
                    GridManager.Instance.HighlightTilesInMovementRange(Unit.SelectedUnit.GetComponent<Stats>());  
                }

                return true;
            }
        }

        // Jeśli pole jest zajęte to wracamy na wcześniejszą pozycję
        transform.position = _startPosition;
        return false;
    }
}
