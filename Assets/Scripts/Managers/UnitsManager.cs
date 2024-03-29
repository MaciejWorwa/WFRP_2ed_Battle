using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UIElements;
using System.Reflection;

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

    [SerializeField] private GameObject _unitPanel;
    [SerializeField] private TMP_Text _nameDisplay;
    [SerializeField] private TMP_Text _raceDisplay;
    [SerializeField] private TMP_Text _healthDisplay;
    [SerializeField] private GameObject _unitPrefab;
    [SerializeField] private CustomDropdown _unitsDropdown;
    [SerializeField] private TMP_InputField _unitNameInputField;
    [SerializeField] private UnityEngine.UI.Toggle _randomPositionToggle;
    [SerializeField] private UnityEngine.UI.Toggle _unitTagToggle;
    [SerializeField] private UnityEngine.UI.Button _createUnitButton;
    [SerializeField] private UnityEngine.UI.Button _removeUnitButton;
    [SerializeField] private UnityEngine.UI.Button _updateUnitButton;
    [SerializeField] private UnityEngine.UI.Button _removeUnitConfirmButton;
    [SerializeField] private GameObject _removeUnitConfirmPanel;
    [SerializeField] private int _unitsAmount;
    public static bool IsTileSelecting;
    public static bool IsUnitRemoving;
    public static bool IsUnitEditing = false;

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
        if(_unitsDropdown.SelectedButton == null)
        {
            Debug.Log("Wybierz jednostkę z listy.");
            return;
        }

        //Resetuje input field z nazwą jednostki
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

        //Wczytuje statystyki dla danego typu jednostki
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

    #region Unit editing
    public void EditUnitModeOn()
    {
        IsUnitEditing = true;
        
        _createUnitButton.gameObject.SetActive(false);
        _removeUnitButton.gameObject.SetActive(false);
        _randomPositionToggle.gameObject.SetActive(false);
        _updateUnitButton.gameObject.SetActive(true);
    }

    public void EditUnitModeOff()
    {
        IsUnitEditing = false;
        
        _createUnitButton.gameObject.SetActive(true);
        _removeUnitButton.gameObject.SetActive(true);
        _randomPositionToggle.gameObject.SetActive(true);
        _updateUnitButton.gameObject.SetActive(false);
    }

    public void UpdateUnitNameOrRace()
    {
        if(Unit.SelectedUnit == null) return;

        if(_unitsDropdown.SelectedButton == null)
        {
            Debug.Log("Wybierz rasę z listy.");
            return;
        }
    
        GameObject unit = Unit.SelectedUnit;

        //Ustala nową rasę na podstawie rasy wybranej z listy
        unit.GetComponent<Stats>().Id = _unitsDropdown.GetSelectedIndex();

        //Ustawia tag postaci, który definiuje, czy jest to sojusznik, czy przeciwnik, a także jej domyślny kolor.
        if (_unitTagToggle.isOn)
        {
            unit.tag = "PlayerUnit";
            unit.GetComponent<Unit>().DefaultColor = new Color(0f, 0.54f, 0.17f, 1.0f);
        }
        else
        {
            unit.tag = "EnemyUnit";
            unit.GetComponent<Unit>().DefaultColor = new Color(0.72f, 0.15f, 0.17f, 1.0f);
        }
        unit.GetComponent<Unit>().ChangeUnitColor(unit);

        //Aktualizuje statystyki
        DataManager.Instance.LoadAndUpdateStats(unit);

        //Aktualizuje imię postaci
        if(_unitNameInputField.text.Length > 0)
        {
            unit.GetComponent<Stats>().Name = _unitNameInputField.text;
            unit.GetComponent<Unit>().DisplayUnitName();
            //Resetuje input field z nazwą jednostki
            _unitNameInputField.text = null;
        }

        //Ustala inicjatywę i aktualizuje kolejkę inicjatywy
        unit.GetComponent<Stats>().Initiative = unit.GetComponent<Stats>().Zr + Random.Range(1, 11);
        RoundsManager.Instance.RemoveUnitFromInitiativeQueue(unit.GetComponent<Unit>());
        RoundsManager.Instance.AddUnitToInitiativeQueue(unit.GetComponent<Unit>());
        RoundsManager.Instance.UpdateInitiativeQueue();

        //Aktualizuje wyświetlany panel ze statystykami
        UpdateUnitPanel(unit);
    }

    public void EditAttribute(GameObject textInput)
    {
        GameObject unit = Unit.SelectedUnit;

        // Pobiera pole ze statystyk postaci o nazwie takiej samej jak nazwa textInputa (z wyłączeniem słowa "input")
        string attributeName = textInput.name.Replace("_input", "");
        FieldInfo field = unit.GetComponent<Stats>().GetType().GetField(attributeName);

        if(field == null) return;

        // Zmienia wartść cechy
        if (field.FieldType == typeof(int))
        {
            // Pobiera wartość inputa, starając się przekonwertować ją na int
            int value = int.TryParse(textInput.GetComponent<TMP_InputField>().text, out int inputValue) ? inputValue : 0;

            field.SetValue(unit.GetComponent<Stats>(), value);
            Debug.Log($"Atrybut {field.Name} zmieniony na {value}");
        }
        else if (field.FieldType == typeof(bool)) 
        {
            bool boolValue = textInput.GetComponent<UnityEngine.UI.Toggle>().isOn; 
            field.SetValue(unit.GetComponent<Stats>(), boolValue);
            Debug.Log($"Atrybut {field.Name} zmieniony na {boolValue}");
        }
        else
        {
            Debug.Log($"Nie udało się zmienić wartości cechy.");
        }         

        if(attributeName == "MaxHealth")
        {
            unit.GetComponent<Stats>().TempHealth = unit.GetComponent<Stats>().MaxHealth;
            unit.GetComponent<Unit>().DisplayUnitHealthPoints();
        }
        if(attributeName == "K" || attributeName == "Odp")
        {
            unit.GetComponent<Unit>().CalculateStrengthAndToughness();
        }

        UpdateUnitPanel(unit);
    }
    #endregion

    #region Update unit panel (at the top of the screen)
    public void UpdateUnitPanel(GameObject unit)
    {
        if(unit == null)
        {
            _unitPanel.SetActive(false);
            return;
        }
        else
        {
            _unitPanel.SetActive(true);
        }

        Stats stats = unit.GetComponent<Stats>();
        _nameDisplay.text = stats.Name;
        _raceDisplay.text = stats.Race;
        _healthDisplay.text = stats.TempHealth + "/" + stats.MaxHealth;

        LoadAttributes(unit);
    }

    private void LoadAttributes(GameObject unit)
    {
        // Wyszukuje wszystkie pola tekstowe i przyciski do ustalania statystyk postaci wewnatrz gry
        GameObject[] attributeInputFields = GameObject.FindGameObjectsWithTag("Attribute");

        foreach (var inputField in attributeInputFields)
        {
            // Pobiera pole ze statystyk postaci o nazwie takiej samej jak nazwa textInputa (z wyłączeniem słowa "input")
            string attributeName = inputField.name.Replace("_input", "");
            FieldInfo field = unit.GetComponent<Stats>().GetType().GetField(attributeName);

            if(field == null) continue;

            // Jeśli znajdzie takie pole, to zmienia wartość wyświetlanego tekstu na wartość tej cechy
            if (field.FieldType == typeof(int)) // to działa dla cech opisywanych wartościami int
            {
                int value = (int)field.GetValue(unit.GetComponent<Stats>());

                if (inputField.GetComponent<TMPro.TMP_InputField>() != null)
                {
                    inputField.GetComponent<TMPro.TMP_InputField>().text = value.ToString();
                }
                else if (inputField.GetComponent<UnityEngine.UI.Slider>() != null)
                {
                    inputField.GetComponent<UnityEngine.UI.Slider>().value = value;
                }
            }
            else if (field.FieldType == typeof(bool)) // to działa dla cech opisywanych wartościami bool
            {
                bool value = (bool)field.GetValue(unit.GetComponent<Stats>());
                inputField.GetComponent<UnityEngine.UI.Toggle>().isOn = value;
            }
        }
    }
    #endregion
}
