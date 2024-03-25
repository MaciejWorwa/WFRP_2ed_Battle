using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//using UnityEngine.UIElements;
using TMPro;

public class UnitsManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static UnitsManager instance;

    // Publiczny dostęp do instancji
    public static UnitsManager Instance
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
    [SerializeField] private GameObject _unitPrefab;
    [SerializeField] private TMP_Dropdown _unitsDropdown;
    [SerializeField] private TMP_InputField _unitNameInputField;
    [SerializeField] private Toggle _unitTagToggle;
    [SerializeField] private Button _createUnitButton;
    [SerializeField] private Button _destroyUnitButton;
    [SerializeField] private int _unitsAmount;
    public static bool IsTileSelecting;
    public static bool IsUnitRemoving;
    public static bool RandomPositionMode = false;

    #region Creating units
    public void CreateUnitMode(GameObject button)
    {
        if(!RandomPositionMode)
        {
            IsTileSelecting = true;

            //Zmienia kolor przycisku tworzenia jednostek na aktywny
            _createUnitButton.GetComponent<Image>().color = Color.green;

            Debug.Log("Wybierz pole na którym chcesz stworzyć jednostkę.");
            return;
        }
        else
        {
            CreateUnit(_unitsDropdown.value + 1, _unitNameInputField.text, Vector2.zero); // TEMP
        }
    }

    public void CreateUnitOnSelectedTile(Vector2 position)
    {
        CreateUnit(_unitsDropdown.value + 1, _unitNameInputField.text, position);

        //Resetuje kolor przycisku tworzenia jednostek
        _createUnitButton.GetComponent<Image>().color = Color.white;
    }

    public void SetRandomPositionMode(GameObject button)
    {
        RandomPositionMode = !RandomPositionMode;

        //Resetuje kolor przycisku tworzenia jednostek
        _createUnitButton.GetComponent<Image>().color = Color.white;

        if (RandomPositionMode)
        {
            button.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.5f);
        }
        else
        {
            button.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
        }
    }

    public void CreateUnit(int unitId, string unitName, Vector2 position)
    {
        int width = GridManager.Instance.Width;
        int height = GridManager.Instance.Height;

        // Liczba dostępnych pól
        int availableTiles = width * height;

        // Sprawdzenie, czy szerokość i wysokość siatki są liczbami parzystymi
        bool xEven = (width % 2 == 0) ? true : false;
        bool yEven = (height % 2 == 0) ? true : false;

        // Ilość prób stworzenia postaci na losowym polu
        int attempts = 0;

        // Pole na którym chcemy stworzyć jednostkę
        GameObject selectedTile = GameObject.Find($"Tile {position.x - GridManager.Instance.transform.position.x} {position.y - GridManager.Instance.transform.position.y}");

        do
        {
            if(RandomPositionMode)
            {
                // Generowanie losowej pozycji na mapie
                int x = xEven ? Random.Range(-width / 2, width / 2) : Random.Range(-width / 2, width / 2 + 1);
                int y = yEven ? Random.Range(-height / 2, height / 2) : Random.Range(-height / 2, height / 2 + 1);

                position = new Vector2(x, y);

                // Aktualizujemy pole na którym chcemy stworzyć jednostkę
                selectedTile = GameObject.Find($"Tile {position.x - GridManager.Instance.transform.position.x} {position.y - GridManager.Instance.transform.position.y}");
            }

            // Zmniejszenie liczby dostępnych pól
            availableTiles--;

            // Inkrementacja liczby prób
            attempts++;

            // Sprawdzenie, czy liczba prób nie przekracza maksymalnej liczby dostępnych pól
            if (attempts > availableTiles)
            {
                Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
                return;
            }
        }
        while (selectedTile.GetComponent<Tile>().IsOccupied == true);

        IsTileSelecting = false;
      
        //Tworzy nową postać na odpowiedniej pozycji
        GameObject newUnit = Instantiate(_unitPrefab, position, Quaternion.identity);

        //Umieszcza postać jako dziecko EmptyObject'u do którego są podpięte wszystkie jednostki
        newUnit.transform.SetParent(GameObject.Find("----------Units-------------------").transform);

        //Ustawia Id postaci, które będzie definiować jego rasę i statystyki
        newUnit.GetComponent<Stats>().Id = unitId;

        //Ustawia tag postaci, który definiuje, czy jest to sojusznik, czy przeciwnik, a także jej domyślny kolor.
        if (_unitTagToggle.isOn)
        {

            newUnit.tag = "PlayerUnit";
            newUnit.GetComponent<Unit>().DefaultColor = new Color(0f, 0.54f, 0.17f, 1.0f);
        }
        else
        {
            newUnit.tag = "EnemyUnit";
            newUnit.GetComponent<Unit>().DefaultColor = new Color(0.72f, 0.15f, 0.17f, 1.0f);
        }
        newUnit.GetComponent<Unit>().ChangeUnitColor(newUnit);

        //Zmienia status wybranego pola na zajęte
        selectedTile.GetComponent<Tile>().IsOccupied = true;

        // Aktualizuje liczbę wszystkich postaci
        _unitsAmount++;

        //Wczytuje statystyki dla danego typu jednostki na podstawie jego Id
        DataManager.Instance.LoadAndUpdateStats(newUnit);

        //Ustala nazwę GameObjectu jednostki
        if(unitName.Length < 1)
        {
            newUnit.name = newUnit.GetComponent<Stats>().Race + $" {_unitsAmount}";
        }
        else
        {
            newUnit.name = unitName;
        }

        //Ustala początkową inicjatywę i dodaje jednostkę do kolejki inicjatywy
        newUnit.GetComponent<Stats>().Initiative = newUnit.GetComponent<Stats>().Zr + Random.Range(1, 11);
        RoundsManager.Instance.AddUnitToInitiativeQueue(newUnit.GetComponent<Unit>());
    }
    #endregion

    #region Removing units
    public void DestroyUnitMode()
    {
        IsUnitRemoving = true;

        //Zmienia kolor przycisku usuwania jednostek na aktywny
        _destroyUnitButton.GetComponent<Image>().color = Color.green;
    }
    public void DestroyUnit(GameObject unit = null)
    {
        if(unit == null)
        {
            unit = Unit.SelectedUnit;
        }
        else if (unit == Unit.SelectedUnit)
        {
            //Resetuje podświetlenie pól w zasięgu ruchu jeżeli usuwana postać jest obecnie aktywną
            GridManager.Instance.ResetColorOfTilesInMovementRange();
        }

        //Usunięcie jednostki z kolejki inicjatywy
        RoundsManager.Instance.RemoveUnitFromInitiativeQueue(unit.GetComponent<Unit>());
        //Aktualizuje kolejkę inicjatywy
        RoundsManager.Instance.UpdateInitiativeQueue();

        Destroy(unit);
        IsUnitRemoving = false;

        //Resetuje kolor przycisku tworzenia jednostek
        _destroyUnitButton.GetComponent<Image>().color = Color.white;

        //Resetuje Tile, żeby nie było uznawane jako zajęte
        GridManager.Instance.ResetTileOccupancy(unit.transform.position);
    }
    #endregion
}
