using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

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
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            // Jeśli instancja już istnieje, a próbujemy utworzyć kolejną, niszczymy nadmiarową
            Destroy(gameObject);
        }
    }

    [SerializeField] private Transform _allElementsGrid;
    public List<GameObject> AllElements;
    [SerializeField] private UnityEngine.UI.Button _removeElementButton;
    public static bool IsElementRemoving = false;
    [SerializeField] private UnityEngine.UI.Toggle _highObstacleToggle;
    [SerializeField] private UnityEngine.UI.Toggle _lowObstacleToggle;

    private void Start()
    {
        ResetAllSelectedElements();
    }

    public void PlaceElementOnRandomTile()
    {
        int x = Random.Range(0, GridManager.Width);
        int y = Random.Range(0, GridManager.Height);

        Vector3 position = new Vector3(x + GridManager.Instance.gameObject.transform.position.x, y + GridManager.Instance.gameObject.transform.position.y, 1);

        PlaceElementOnSelectedTile(position);
    }

    public void PlaceElementOnSelectedTile(Vector3 position)
    {
        // Sprawdza, czy wskaźnik znajduje się nad GUI, lub nie wybrano żadnego obiektu
        if (/*EventSystem.current.IsPointerOverGameObject() ||*/ MapElementUI.SelectedElement == null) return;

        Collider2D collider = Physics2D.OverlapCircle(position, 0.1f);

        if (collider != null && collider.gameObject.CompareTag("Tile"))
        {
            GameObject newElement = Instantiate(MapElementUI.SelectedElement, position, Quaternion.identity);

            //Dodanie elementu do listy wszystkich obecnych na mapie elementów
            AllElements.Add(newElement);

            newElement.tag = "MapElement";
            newElement.GetComponent<MapElement>().IsHighObstacle = _highObstacleToggle.isOn;
            newElement.GetComponent<MapElement>().IsLowObstacle = _lowObstacleToggle.isOn;
        }
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
        Collider2D[] colliders = Physics2D.OverlapCircleAll(position, 0.1f);

        // Usuwa przeszkody z klikniętego miejsca
        for (int i = 0; i < colliders.Length; i++)
        {
            //if (colliders[i].CompareTag("Tile")) continue;

            Destroy(colliders[i].gameObject);
        }
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

        if(data.Elements.Count == 0) return;

        GridManager.Width = data.Elements[0].GridWidth;
        GridManager.Height = data.Elements[0].GridHeight;   
        GridManager.Instance.GenerateGrid();  

        foreach (var mapElement in data.Elements)
        {
            Vector3 position = new Vector3(mapElement.position[0], mapElement.position[1], mapElement.position[2]);

            GameObject prefab = Resources.Load<GameObject>(mapElement.Name);

            GameObject newObject = Instantiate(prefab, position, Quaternion.identity);
            AllElements.Add(newObject);

            MapElement newElement = newObject.GetComponent<MapElement>();

            newElement.tag = mapElement.Tag;
            newElement.IsHighObstacle = mapElement.IsHighObstacle;
            newElement.IsLowObstacle = mapElement.IsLowObstacle;   
        }

        GridManager.Instance.CheckTileOccupancy();
    }
}