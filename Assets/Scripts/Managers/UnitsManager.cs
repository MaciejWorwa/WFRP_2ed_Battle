using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UIElements;

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
    [SerializeField] private CustomDropdown _unitsDropdown;
    [SerializeField] private TMP_InputField _unitNameInputField;
    [SerializeField] private UnityEngine.UI.Toggle _randomPositionToggle;
    [SerializeField] private UnityEngine.UI.Toggle _unitTagToggle;
    [SerializeField] private UnityEngine.UI.Button _createUnitButton;
    [SerializeField] private UnityEngine.UI.Button _removeUnitButton;
    [SerializeField] private UnityEngine.UI.Button _removeUnitConfirmButton;
    [SerializeField] private GameObject _removeUnitConfirmPanel;
    [SerializeField] private int _unitsAmount;
    public static bool IsTileSelecting;
    public static bool IsUnitRemoving;

    void Start()
    {
        //Wczytuje listę wszystkich jednostek
        DataManager.Instance.LoadAndUpdateStats();

        _removeUnitConfirmButton.onClick.AddListener(() =>
        {
            if(Unit.SelectedUnit!= null)
            {
                DestroyUnit(Unit.SelectedUnit);
                _removeUnitConfirmPanel.SetActive(false);
            }
            else
            {
                Debug.Log("Aby usunąć jednostkę, musisz najpierw ją wybrać.");
            }
        });
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Delete) && Unit.SelectedUnit != null)
        {
            if(_removeUnitConfirmPanel.activeSelf == false)
            {
                _removeUnitConfirmPanel.SetActive(true);
            }
            else
            {
                DestroyUnit(Unit.SelectedUnit);
                _removeUnitConfirmPanel.SetActive(false);
            }
        }
    }

    #region Creating units
    public void CreateUnitMode(GameObject button)
    {
        if (!_randomPositionToggle.isOn)
        {
            IsTileSelecting = true;

            //Zmienia kolor przycisku tworzenia jednostek na aktywny
            _createUnitButton.GetComponent<UnityEngine.UI.Image>().color = Color.green;

            Debug.Log("Wybierz pole na którym chcesz stworzyć jednostkę.");
            return;
        }
        else
        {
            CreateUnit(_unitsDropdown.GetSelectedIndex(), _unitNameInputField.text, Vector2.zero);
        }
    }

    public void CreateUnitOnSelectedTile(Vector2 position)
    {
        CreateUnit(_unitsDropdown.GetSelectedIndex(), _unitNameInputField.text, position);

        //Resetuje kolor przycisku tworzenia jednostek
        _createUnitButton.GetComponent<UnityEngine.UI.Image>().color = Color.white;
    }

    public void CreateUnit(int unitId, string unitName, Vector2 position)
    {
        //Resetuje inpu field z nazwą jednostki
        _unitNameInputField.text = null;

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
            if(_randomPositionToggle.isOn)
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
                if(_randomPositionToggle.isOn)
                {
                    Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
                }
                else
                {
                    Debug.Log("Wybrane pole jest zajęte. Nie można utworzyć nowej jednostki.");
                }
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

        //Wczytuje statystyki dla danego typu jednostki, którą najpierw oznacza jako wybraną
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
        _removeUnitButton.GetComponent<UnityEngine.UI.Image>().color = Color.green;

        Debug.Log("Wybierz jednostkę, którą chcesz usunąć.");
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
        _removeUnitButton.GetComponent<UnityEngine.UI.Image>().color = Color.white;

        //Resetuje Tile, żeby nie było uznawane jako zajęte
        GridManager.Instance.ResetTileOccupancy(unit.transform.position);
    }
    #endregion
}
