using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;

public class MapEditor : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static MapEditor instance;

    // Publiczny dostęp do instancji
    public static MapEditor Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            AllElements = instance.AllElements;

            // Niszczymy starą instancję, która już istnieje
            Destroy(instance.gameObject);
        }

        // Aktualizujemy referencję do nowej instancji
        instance = this;

        // Ustawiamy obiekt, aby nie był niszczony przy ładowaniu nowej sceny
        DontDestroyOnLoad(gameObject);
    }

    [SerializeField] private Transform _allElementsGrid;
    public List<GameObject> AllElements;
    [SerializeField] private UnityEngine.UI.Button _removeElementButton;
    public static bool IsElementRemoving = false;
    [SerializeField] private UnityEngine.UI.Toggle _highObstacleToggle;
    [SerializeField] private UnityEngine.UI.Toggle _lowObstacleToggle;
    [SerializeField] private UnityEngine.UI.Toggle _isColliderToggle;
    [SerializeField] private UnityEngine.UI.Toggle _randomRotationToggle;
    [SerializeField] private UnityEngine.UI.Slider _rotationSlider;
    [SerializeField] private TMP_InputField _rotationInputField;
    private Vector3 _mousePosition;
    private GameObject _cursorObject;
 
    private void Start()
    {
        ResetAllSelectedElements();
    }

    void Update()
    {
        if(MapElementUI.SelectedElement != null)
        {
            ReplaceCursorWithMapElement();
        }
    }
    
    private void ReplaceCursorWithMapElement()
    {
        // Zaktualizuj pozycję kursora
        _mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        _mousePosition.z = 0; // Ustaw Z na 0, aby sprite był na tej samej płaszczyźnie

        // Uzyskaj rozmiar BoxCollider2D
        BoxCollider2D collider = MapElementUI.SelectedElement.GetComponent<BoxCollider2D>();

        // Oblicz offset na podstawie rozmiaru collidera
        Vector3 offset = Vector3.zero;
        if (collider != null)
        {
            offset = new Vector3(-collider.size.x / 2, collider.size.y / 2, 0);
        }

        // Sprawdź, czy kursor jest nowym obiektem
        if (_cursorObject == null || _cursorObject.name != MapElementUI.SelectedElement.name + "Cursor")
        {
            if(_cursorObject != null)
            {
                Destroy(_cursorObject);
            }

            Quaternion rotation = Quaternion.Euler(0, 0, _rotationSlider.value);
            _cursorObject = Instantiate(MapElementUI.SelectedElement, _mousePosition + offset, rotation);

            _cursorObject.name = MapElementUI.SelectedElement.name + "Cursor";
            _cursorObject.GetComponent<BoxCollider2D>().enabled = false;
        }

        // Ustaw pozycję sprite'a na pozycję kursora, z uwzględnieniem offsetu
        _cursorObject.transform.position = _mousePosition + offset;
    }

    public void PlaceElementOnRandomTile()
    {
        List<Vector3> availablePositions = new List<Vector3>();
        Transform gridTransform = GridManager.Instance.transform;

        // Wypełnianie listy dostępnymi pozycjami
        for (int x = 0; x < GridManager.Width; x++)
        {
            for (int y = 0; y < GridManager.Height; y++)
            {
                Vector3 worldPosition = gridTransform.TransformPoint(new Vector3(x, y, 0));
                Collider2D collider = Physics2D.OverlapPoint(worldPosition);

                if (collider != null && collider.gameObject.CompareTag("Tile"))
                {
                    availablePositions.Add(worldPosition);
                }
            }
        }

        if (availablePositions.Count == 0)
        {
            Debug.Log("Nie można umieścić więcej elementów na mapie. Brak wolnych pól.");
            return;
        }

        // Losowanie pozycji z dostępnych
        Vector3 selectedPosition = availablePositions[Random.Range(0, availablePositions.Count)];

        PlaceElementOnSelectedTile(selectedPosition);
    }

    public void PlaceElementOnSelectedTile(Vector3 position)
    {
        // Sprawdza, czy wybrano
        if (MapElementUI.SelectedElement == null) return;

        BoxCollider2D boxCollider = MapElementUI.SelectedElement.GetComponent<BoxCollider2D>();

        // Aktualizowanie zajętości pól
        GridManager.Instance.CheckTileOccupancy();
        
        Collider2D collider = Physics2D.OverlapPoint(position);

        if (collider != null && collider.gameObject.CompareTag("Tile"))
        {
            if (collider.GetComponent<Tile>().IsOccupied) return;
        
            if(_randomRotationToggle.isOn)
            {
                SetRandomElementRotation();
            }

            Quaternion rotation = Quaternion.Euler(0, 0, _rotationSlider.value);

            if (boxCollider.size.y > boxCollider.size.x) //Elementy zajmujące dwa pola
            {
                float rotationZ = _rotationSlider.value;
                if (rotationZ < 45 || (rotationZ >= 135 && rotationZ < 225) || rotationZ > 315)
                {
                    position = new Vector3(position.x, position.y + 0.5f, position.z);
                                        
                    Collider2D pointCollider = Physics2D.OverlapPoint(new Vector3(position.x, position.y + 0.5f, position.z));
                    if (pointCollider != null && !pointCollider.gameObject.CompareTag("Tile")) return;  
                }
                else
                {
                    position = new Vector3(position.x - 0.5f, position.y, position.z);

                    Collider2D pointCollider = Physics2D.OverlapPoint(new Vector3(position.x - 0.5f, position.y, position.z));
                    if (pointCollider != null && !pointCollider.gameObject.CompareTag("Tile")) return;  
                }
            }
            else if(MapElementUI.SelectedElement.transform.localScale.x > 1.5f) //Elementy zajmujące 4 pola
            {
                position = new Vector3(position.x - 0.5f, position.y + 0.5f, position.z);

                Collider2D circleCollider = Physics2D.OverlapCircle(position, 1f);
                if (circleCollider != null && !circleCollider.gameObject.CompareTag("Tile")) return;  
            }

            GameObject newElement = Instantiate(MapElementUI.SelectedElement, position, rotation);

            //Dodanie elementu do listy wszystkich obecnych na mapie elementów
            AllElements.Add(newElement);

            newElement.tag = "MapElement";
            newElement.GetComponent<MapElement>().IsHighObstacle = _highObstacleToggle.isOn;
            newElement.GetComponent<MapElement>().IsLowObstacle = _lowObstacleToggle.isOn;
            newElement.GetComponent<MapElement>().IsCollider = _isColliderToggle.isOn;

            collider.GetComponent<Tile>().IsOccupied = true;
        }
    }

    public void ChangeElementRotation(GameObject gameObject)
    {
        // Oznacza, że rotacja została wprowadzona przy użyciu slidera, a nie InputFielda
        if (gameObject.GetComponent<UnityEngine.UI.Slider>() != null)
        {
            _rotationInputField.text = _rotationSlider.value.ToString();
        }
        else // Oznacza, że rotacja została wprowadzona przy użyciu InputFielda
        {
            if (int.TryParse(_rotationInputField.text, out int value))
            {
                value = Mathf.Clamp(value, 0, 360);
                _rotationSlider.value = value;
                _rotationInputField.text = value.ToString();
            }
        }

        if(_cursorObject != null)
        {
            _cursorObject.transform.rotation = Quaternion.Euler(0, 0, _rotationSlider.value);
        }
    }

    public void SetRandomElementRotation()
    {
        int value = Random.Range(0,361);
        _rotationSlider.value = value;
        _rotationInputField.text = value.ToString();
    }

    public void ResetAllSelectedElements()
    {
        for (int i = _allElementsGrid.childCount - 1; i >= 0; i--)
        {
            MapElementUI childElement = _allElementsGrid.GetChild(i).GetComponent<MapElementUI>();

            childElement.ResetColor(childElement.GetComponent<UnityEngine.UI.Image>());

            MapElementUI.SelectedElement = null;
            MapElementUI.SelectedElementImage = null;
        }

        Destroy(_cursorObject);
    }

    //Przed rozpoczęciem bitwy ustala collidery elementów mapy
    public void SetAllElementsColliders(bool allElementShouldHaveColliders)
    {
        foreach (var element in AllElements)
        {
            if(allElementShouldHaveColliders == true)
            {
                element.GetComponent<MapElement>().SetColliderState(true);
            }
            else
            {
                element.GetComponent<MapElement>().SetColliderState(element.GetComponent<MapElement>().IsCollider);
            }
        }
    }

    public void RemoveElementsMode(bool isOn)
    {
        IsElementRemoving = isOn;

        //Zmienia kolor przycisku usuwania jednostek na aktywny lub nieaktywny w zależności od stanu
        _removeElementButton.GetComponent<UnityEngine.UI.Image>().color = isOn ? Color.green : Color.white;

        if(isOn)
        {
            ResetAllSelectedElements();
            Debug.Log("Wybierz element otoczenia, który chcesz usunąć. Przytrzymując lewy przycisk myszy i przesuwając po mapie, możesz usuwać wiele elementów naraz.");
        }
    }

    public void RemoveElement(Vector3 position)
    {
        Collider2D collider = Physics2D.OverlapPoint(position);

        // Usuwa przeszkodę z klikniętego miejsca
        Destroy(collider.gameObject);
    }

    public void RemoveElementsOutsideTheGrid()
    {
        // Usuwa wszystkie przeszkody poza siatką bitewną
        for (int i = AllElements.Count - 1; i >= 0; i--)
        {
            int rightBound = GridManager.Width / 2;
            int topBound = GridManager.Height / 2;

            if (GridManager.Height % 2 == 0) topBound--;

            if (GridManager.Width % 2 == 0) rightBound--;

            Vector3 pos = AllElements[i].transform.position;

            if (Mathf.Abs(pos.x) > GridManager.Width / 2 || Mathf.Abs(pos.y) > GridManager.Height / 2 || pos.y > topBound || pos.x > rightBound)
            {
                Destroy(AllElements[i]);
                AllElements.RemoveAt(i);
            }
        }
    }

    public void LoadMapData(MapElementsContainer data)
    {
        for (int i = AllElements.Count - 1; i >= 0; i--)
        {
            Destroy(AllElements[i]);
            AllElements.RemoveAt(i);
        }

        if (data.Elements.Count == 0) return;

        foreach (var mapElement in data.Elements)
        {
            Vector3 position = new Vector3(mapElement.position[0], mapElement.position[1], mapElement.position[2]);

            Quaternion rotation = Quaternion.Euler(0, 0, mapElement.rotationZ);

            GameObject prefab = Resources.Load<GameObject>(mapElement.Name);

            GameObject newObject = Instantiate(prefab, position, rotation);
            AllElements.Add(newObject);

            MapElement newElement = newObject.GetComponent<MapElement>();

            newElement.tag = mapElement.Tag;
            newElement.IsHighObstacle = mapElement.IsHighObstacle;
            newElement.IsLowObstacle = mapElement.IsLowObstacle;
            newElement.IsCollider = mapElement.IsCollider;

            //Jeśli nie jesteśmy w edytorze map to ustalamy, czy element ma collider, czy nie. W edytorze chcemy, aby każdy miał collider, aby móc je usuwać i obracać
            if(SceneManager.GetActiveScene().buildIndex != 0)
            {
                newElement.SetColliderState(newElement.IsCollider);
            }
        }

        GridManager.Instance.CheckTileOccupancy();
    }
}
