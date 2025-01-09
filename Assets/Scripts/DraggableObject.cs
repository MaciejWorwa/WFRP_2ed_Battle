using UnityEngine;

public class DraggableObject : MonoBehaviour
{
    private Vector3 _offset; // Przechowuje różnicę między pozycją obiektu a pozycją kursora
    public static bool IsDragging = false;
    private Camera _mainCamera;
    private Vector3 _startPosition; // Pozycja przed przesunięciem
    public static DraggableObject CurrentlyDragging = null;


    private void Start()
    {
        _mainCamera = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            DraggableObject obj = GetDraggableObjectUnderMouse();
            if (obj != null)
            {
                // Rozpoczynamy przeciąganie wybranego obiektu
                CurrentlyDragging = obj;
                CurrentlyDragging.BeginDrag();
            }
        }

        // Aktualizacja pozycji dla przeciąganego obiektu
        if (DraggableObject.CurrentlyDragging != null && Input.GetMouseButton(0))
        {
            DraggableObject.CurrentlyDragging.UpdateDrag();
        }

        // Zakończenie przeciągania
        if (Input.GetMouseButtonUp(0) && DraggableObject.CurrentlyDragging != null)
        {
            DraggableObject.CurrentlyDragging.EndDrag();
            DraggableObject.CurrentlyDragging = null;
        }
    }

    private DraggableObject GetDraggableObjectUnderMouse()
    {
        Vector3 mousePos = Input.mousePosition;
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray);

        // Przeglądaj wszystkie trafienia i wybierz najbardziej odpowiedni obiekt
        foreach (var hit in hits)
        {
            DraggableObject draggable = hit.collider.GetComponent<DraggableObject>();
            if (draggable != null)
            {
                return draggable;
            }
        }
        return null;
    }

    public void BeginDrag()
    {
        if(GameManager.IsMapHidingMode || MapEditor.IsElementRemoving || UnitsManager.IsMultipleUnitsSelecting || MovementManager.Instance.IsMoving)
            return;

        IsDragging = true;
        _startPosition = transform.position;
        _offset = transform.position - GetMouseWorldPosition();
    }

    public void UpdateDrag()
    {
        if (!IsDragging) return;

        Vector3 newPosition = GetMouseWorldPosition() + _offset;
        newPosition.z = 0;
        transform.position = newPosition;
    }

    public void EndDrag()
    {
        if(GameManager.IsMapHidingMode || MapEditor.IsElementRemoving || UnitsManager.IsMultipleUnitsSelecting || MovementManager.Instance.IsMoving)
            return;

        if(transform.position != _startPosition)
        {
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
        //Sprawdzamy, czy przesuwany obiekt jest jednostką
        Unit unit = GetComponent<Unit>();

        // Jeżeli przesuwamy obiekt będący jednostką to odznaczamy ją
        if (unit != null)
        {
            //Odznaczamy jednostkę, którą przesuwamy
            if(Unit.SelectedUnit == this.gameObject)
            {
                unit.SelectUnit();
            }

            // Sprawdza, czy jednostka jest już w kolejce inicjatywy. Jeśli nie to dodaje ją.
            if (!InitiativeQueueManager.Instance.InitiativeQueue.ContainsKey(unit))
            {
                InitiativeQueueManager.Instance.AddUnitToInitiativeQueue(unit);
                InitiativeQueueManager.Instance.UpdateInitiativeQueue();
                InitiativeQueueManager.Instance.SelectUnitByQueue();
            }
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

                //Jeżeli przesuwamy jednostkę na zakryte pole, usuwamy ją z kolejki inicjatywy
                if (unit != null)
                {
                    // Dodatkowe sprawdzenie obecności obiektu z tagiem "TileCover" w tym miejscu
                    Collider2D[] cellColliders = Physics2D.OverlapPointAll(transform.position);
                    foreach (var cellCollider in cellColliders)
                    {
                        if (cellCollider != null && cellCollider.CompareTag("TileCover"))
                        {
                            // Jeśli znaleziono obiekt TileCover, usuń jednostkę z kolejki inicjatywy
                            InitiativeQueueManager.Instance.RemoveUnitFromInitiativeQueue(unit);
                            InitiativeQueueManager.Instance.UpdateInitiativeQueue();
                            InitiativeQueueManager.Instance.SelectUnitByQueue();
                            break;
                        }
                    }
                }

                return true;
            }
        }

        // Jeśli pole jest zajęte to wracamy na wcześniejszą pozycję
        transform.position = _startPosition;
        return false;
    }
}
