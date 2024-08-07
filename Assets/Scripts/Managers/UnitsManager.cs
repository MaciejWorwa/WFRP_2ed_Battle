using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UIElements;
using System.Reflection;
using System;

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
    [SerializeField] private GameObject _spellbookButton;
    [SerializeField] private GameObject _actionsPanel;
    [SerializeField] private GameObject _statesPanel; //Panel opisujący obecny stan postaci, np. unieruchomienie
    [SerializeField] private TMP_Text _nameDisplay;
    [SerializeField] private TMP_Text _raceDisplay;
    [SerializeField] private TMP_Text _initiativeDisplay;
    [SerializeField] private TMP_Text _healthDisplay;
    [SerializeField] private UnityEngine.UI.Image _tokenDisplay;
    [SerializeField] private GameObject _unitPrefab;
    [SerializeField] private CustomDropdown _unitsDropdown;
    [SerializeField] private TMP_InputField _unitNameInputField;
    [SerializeField] private UnityEngine.UI.Slider _modifierAttributeSlider;
    [SerializeField] private UnityEngine.UI.Toggle _randomPositionToggle;
    [SerializeField] private UnityEngine.UI.Toggle _unitTagToggle;
    [SerializeField] private UnityEngine.UI.Button _createUnitButton;
    [SerializeField] private UnityEngine.UI.Button _removeUnitButton;
    [SerializeField] private UnityEngine.UI.Button _updateUnitButton;
    [SerializeField] private UnityEngine.UI.Button _removeUnitConfirmButton;
    [SerializeField] private GameObject _removeUnitConfirmPanel;
    public static bool IsTileSelecting;
    public static bool IsUnitRemoving;
    public static bool IsUnitEditing = false;
    public List<Unit> AllUnits = new List<Unit>();


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

    public GameObject CreateUnit(int unitId, string unitName, Vector2 position)
    {
        if(_unitsDropdown.SelectedButton == null && SaveAndLoadManager.Instance.IsLoading != true)
        {
            Debug.Log("Wybierz jednostkę z listy.");
            return null;
        }

        //Resetuje input field z nazwą jednostki
        _unitNameInputField.text = null;

        List<Vector3> availablePositions = new List<Vector3>();

        // Przejście przez wszystkie Tile w tablicy Tiles
        for (int x = 0; x < GridManager.Width; x++)
        {
            for (int y = 0; y < GridManager.Height; y++)
            {
                // Sprawdzenie, czy Tile nie jest zajęty
                if (!GridManager.Instance.Tiles[x, y].IsOccupied)
                {
                    // Dodanie pozycji Tile do listy dostępnych pozycji
                    availablePositions.Add(GridManager.Instance.Tiles[x, y].transform.position);
                }
            }
        }

        if (_randomPositionToggle.isOn && !SaveAndLoadManager.Instance.IsLoading)
        {
            if (availablePositions.Count == 0)
            {
                Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
                return null;
            }

            // Wybranie losowej pozycji z dostępnych
            int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
            position = availablePositions[randomIndex];
        }

        if (availablePositions.Count == 0)
        {
            Debug.Log("Wybrane pole jest zajęte. Nie można utworzyć nowej jednostki.");
            return null;
        }

        // Pole na którym chcemy stworzyć jednostkę
        GameObject selectedTile = GameObject.Find($"Tile {position.x - GridManager.Instance.transform.position.x} {position.y - GridManager.Instance.transform.position.y}");

        IsTileSelecting = false;
      
        //Tworzy nową postać na odpowiedniej pozycji
        GameObject newUnit = Instantiate(_unitPrefab, position, Quaternion.identity);

        //Umieszcza postać jako dziecko EmptyObject'u do którego są podpięte wszystkie jednostki
        newUnit.transform.SetParent(GameObject.Find("----------Units-------------------").transform);

        //Ustawia Id postaci, które będzie definiować jego rasę i statystyki
        newUnit.GetComponent<Stats>().Id = unitId;

        //Zmienia status wybranego pola na zajęte
        selectedTile.GetComponent<Tile>().IsOccupied = true;

        // Aktualizuje liczbę wszystkich postaci
        AllUnits.Add(newUnit.GetComponent<Unit>());

        //Ustala unikalne Id jednostki
        newUnit.GetComponent<Unit>().UnitId = AllUnits.Count;

        //Wczytuje statystyki dla danego typu jednostki
        DataManager.Instance.LoadAndUpdateStats(newUnit);

        //Ustala nazwę GameObjectu jednostki
        if(unitName.Length < 1)
        {
            newUnit.name = newUnit.GetComponent<Stats>().Race + $" {AllUnits.Count}";
        }
        else
        {
            newUnit.name = unitName;
        }

        if(SaveAndLoadManager.Instance.IsLoading != true)
        {
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

            //Losuje początkowe statystyki dla człowieka, elfa, krasnoluda i niziołka
            if (newUnit.GetComponent<Stats>().Id <= 4)
            {
                newUnit.GetComponent<Stats>().RollForBaseStats();
            }

            //Ustala początkową inicjatywę i dodaje jednostkę do kolejki inicjatywy
            newUnit.GetComponent<Stats>().Initiative = newUnit.GetComponent<Stats>().Zr + UnityEngine.Random.Range(1, 11);
            InitiativeQueueManager.Instance.AddUnitToInitiativeQueue(newUnit.GetComponent<Unit>());

            //Dodaje do ekwipunku początkową broń adekwatną dla danej jednostki i wyposaża w nią
            if (newUnit.GetComponent<Stats>().PrimaryWeaponId > 0)
            {

                Unit.LastSelectedUnit = Unit.SelectedUnit != null ? Unit.SelectedUnit : null;
                Unit.SelectedUnit = newUnit;
                SaveAndLoadManager.Instance.IsLoading = true; // Tylko po to, żeby informacja o dobyciu broni i dodaniu do ekwipunku z metody GrabWeapon i LoadWeapon nie były wyświetlane w oknie wiadomości

                InventoryManager.Instance.WeaponsDropdown.SetSelectedIndex(newUnit.GetComponent<Stats>().PrimaryWeaponId);
                InventoryManager.Instance.LoadWeapons();
                InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(1);
                InventoryManager.Instance.GrabWeapon();

                SaveAndLoadManager.Instance.IsLoading = false;
                Unit.SelectedUnit = Unit.LastSelectedUnit != null ? Unit.LastSelectedUnit : null;
            }
        }

        return newUnit;
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
        InitiativeQueueManager.Instance.RemoveUnitFromInitiativeQueue(unit.GetComponent<Unit>());
        //Aktualizuje kolejkę inicjatywy
        InitiativeQueueManager.Instance.UpdateInitiativeQueue();

        //Usuwa jednostkę z listy wszystkich jednostek
        AllUnits.Remove(unit.GetComponent<Unit>());

        //Wyłącza panel górny i dolny, a także wszystkie aktywne panele
        UpdateUnitPanel(null);
        GameManager.Instance.HideActivePanels();   

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
            Debug.Log("Wybierz rasę z listy. Zmiana rasy wpłynie na statystyki.");
            return;
        }
    
        GameObject unit = Unit.SelectedUnit;

        //Aktualizuje imię postaci
        if (_unitNameInputField.text.Length > 0)
        {
            unit.GetComponent<Stats>().Name = _unitNameInputField.text;
            unit.GetComponent<Unit>().DisplayUnitName();
            unit.name = _unitNameInputField.text;
            //Resetuje input field z nazwą jednostki
            _unitNameInputField.text = null;
        }
        else
        {
            unit.GetComponent<Stats>().Name = unit.GetComponent<Stats>().Race;
            unit.GetComponent<Unit>().DisplayUnitName();
        }

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

        //Sprawdza, czy rasa jest zmieniana
        if (unit.GetComponent<Stats>().Id != _unitsDropdown.GetSelectedIndex())
        {
            //Ustala nową rasę na podstawie rasy wybranej z listy
            unit.GetComponent<Stats>().Id = _unitsDropdown.GetSelectedIndex();

            //Aktualizuje statystyki
            DataManager.Instance.LoadAndUpdateStats(unit);

            //Losuje początkowe statystyki dla człowieka, elfa, krasnoluda i niziołka
            if (unit.GetComponent<Stats>().Id <= 4)
            {
                unit.GetComponent<Stats>().RollForBaseStats();
                unit.GetComponent<Unit>().DisplayUnitHealthPoints();
            }

            //Aktualizuje siłę i wytrzymałość
            unit.GetComponent<Unit>().CalculateStrengthAndToughness();

            //Ustala inicjatywę i aktualizuje kolejkę inicjatywy
            unit.GetComponent<Stats>().Initiative = unit.GetComponent<Stats>().Zr + UnityEngine.Random.Range(1, 11);
            InitiativeQueueManager.Instance.RemoveUnitFromInitiativeQueue(unit.GetComponent<Unit>());
            InitiativeQueueManager.Instance.AddUnitToInitiativeQueue(unit.GetComponent<Unit>());
            InitiativeQueueManager.Instance.UpdateInitiativeQueue();
        }

        //Aktualizuje wyświetlany panel ze statystykami
        UpdateUnitPanel(unit);
    }

    public void UpdateInitiative()
    {
        if (Unit.SelectedUnit == null) return;

        GameObject unit = Unit.SelectedUnit;

        //Ustala nową inicjatywę
        unit.GetComponent<Stats>().Initiative = unit.GetComponent<Stats>().Zr + UnityEngine.Random.Range(1, 11);

        //Aktualizuje kolejkę inicjatywy
        InitiativeQueueManager.Instance.InitiativeQueue[unit.GetComponent<Unit>()] = unit.GetComponent<Stats>().Initiative;
        InitiativeQueueManager.Instance.UpdateInitiativeQueue();

        UpdateUnitPanel(unit);
    }

    public void EditAttribute(GameObject textInput)
    {
        if (Unit.SelectedUnit == null) return;

        GameObject unit = Unit.SelectedUnit;

        // Pobiera pole ze statystyk postaci o nazwie takiej samej jak nazwa textInputa (z wyłączeniem słowa "input")
        string attributeName = textInput.name.Replace("_input", "");

        FieldInfo field = unit.GetComponent<Stats>().GetType().GetField(attributeName);

        if(field == null) return;

        // Zmienia wartść cechy
        if (field.FieldType == typeof(int) && textInput.GetComponent<UnityEngine.UI.Slider>() == null) // to działa dla cech opisywanych wartościami int (pomija umiejętności, które nie są ustawiane przy użyciu slidera)
        {
            // Pobiera wartość inputa, starając się przekonwertować ją na int
            int value = int.TryParse(textInput.GetComponent<TMP_InputField>().text, out int inputValue) ? inputValue : 0;

            field.SetValue(unit.GetComponent<Stats>(), value);
        }
        else if (field.FieldType == typeof(int) && textInput.GetComponent<UnityEngine.UI.Slider>() != null) // to działa z umiejętnościami
        {
            int value = (int)textInput.GetComponent<UnityEngine.UI.Slider>().value;
            field.SetValue(unit.GetComponent<Stats>(), value);
        }
        else if (field.FieldType == typeof(bool)) 
        {
            bool boolValue = textInput.GetComponent<UnityEngine.UI.Toggle>().isOn; 
            field.SetValue(unit.GetComponent<Stats>(), boolValue);
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
        _actionsPanel.SetActive(false);
        _statesPanel.SetActive(false);

        if (unit == null || SaveAndLoadManager.Instance.IsLoading)
        {
            _unitPanel.SetActive(false);
            return;
        }
        else
        {
            _unitPanel.SetActive(true);

            Unit unitComponent = unit.GetComponent<Unit>();

            if(unitComponent.StunDuration == 0 && unitComponent.HelplessDuration == 0 && unitComponent.Trapped == false && unitComponent.IsScared == false)
            {
                _actionsPanel.SetActive(true);
            }
            else
            {
                _statesPanel.SetActive(true);

                string state = "";
                int duration = 0;

                if(unitComponent.StunDuration > 0)
                {
                    state = "ogłuszenia";
                    duration = unitComponent.StunDuration;
                }
                else if (unitComponent.HelplessDuration > 0)
                {
                    state = "bezbronności";
                    duration = unitComponent.HelplessDuration;
                }
                else if (unitComponent.Trapped)
                {
                    state = "unieruchomienia";
                    duration = 0;
                }
                else if (unitComponent.IsScared)
                {
                    state = "strachu";
                    duration = 0;
                }

                string currentStateString = $"Wybrana jednostka nie może wykonywać akcji, ponieważ jest w stanie {state}.";
                string currentStateDurationString = $" Stan ten potrwa jeszcze {duration} rund/y.";

                if(duration == 0)
                {
                    currentStateDurationString = "";
                }

                _statesPanel.GetComponentInChildren<TMP_Text>().text = currentStateString + currentStateDurationString;
            }
        }

        Stats stats = unit.GetComponent<Stats>();

        if (stats.Mag > 0)
        {
            _spellbookButton.SetActive(true);

            if(unit.GetComponent<Spell>() == null)
            {
                unit.AddComponent<Spell>();
            }
        }
        else
        {
            _spellbookButton.SetActive(false);
        }
        _nameDisplay.text = stats.Name;
        _raceDisplay.text = stats.Race;
        _initiativeDisplay.text = stats.Initiative.ToString();
        _healthDisplay.text = stats.TempHealth + "/" + stats.MaxHealth;
        _tokenDisplay.sprite = unit.transform.Find("Token").GetComponent<SpriteRenderer>().sprite;

        LoadAttributes(unit);
    }

    public void LoadAttributesByButtonClick()
    {
        if(Unit.SelectedUnit == null) return;

        GameObject unit = Unit.SelectedUnit;
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
            if (field.FieldType == typeof(int) && inputField.GetComponent<UnityEngine.UI.Slider>() == null) // to działa dla cech opisywanych wartościami int (pomija umiejętności, które nie są ustawiane przy użyciu slidera)
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
            else if (field.FieldType == typeof(int) && inputField.GetComponent<UnityEngine.UI.Slider>() != null) // to działa dla umiejętnościami
            {
                int value = (int)field.GetValue(unit.GetComponent<Stats>());
                inputField.GetComponent<UnityEngine.UI.Slider>().value = value;
            }
            else if (field.FieldType == typeof(bool)) // to działa dla cech opisywanych wartościami bool
            {
                bool value = (bool)field.GetValue(unit.GetComponent<Stats>());
                inputField.GetComponent<UnityEngine.UI.Toggle>().isOn = value;
            }
        }
    }

    public void ChangeTemporaryHealthPoints(int amount)
    {
        if(Unit.SelectedUnit == null) return;
        
        Unit.SelectedUnit.GetComponent<Stats>().TempHealth += amount;

        Unit.SelectedUnit.GetComponent<Unit>().DisplayUnitHealthPoints();
        UpdateUnitPanel(Unit.SelectedUnit);
    }
    #endregion


    #region Attributes tests
    public void TestAttribute(string attributeName)
    {
        if(Unit.SelectedUnit == null) return;

        Stats stats = Unit.SelectedUnit.GetComponent<Stats>();

        FieldInfo field = stats.GetType().GetField(attributeName);

        int value = (int)field.GetValue(stats);
        int modifier = (int)_modifierAttributeSlider.value * 10;
        int rollResult = UnityEngine.Random.Range(1, 101);
        int successLevel = Math.Abs(value + modifier - rollResult) / 10;

        string resultString;

        if((rollResult <= value + modifier || rollResult <= 5) && rollResult < 96)
        {
            resultString = "<color=green>Test zdany.</color> Poziomy sukcesu:";
        }
        else
        {
            resultString = "<color=red>Test niezdany</color> Poziomy porażki:";
        }

        Debug.Log($"{stats.Name} wykonał test {attributeName}. Wynik rzutu: {rollResult} Wartość cechy: {value} Modyfikator: {modifier}. {resultString} {successLevel}");
    }
    #endregion

    #region Fear and terror mechanics
    public void LookForScaryUnits()
    {
        bool frighteningEnemyExist = false;
        bool terryfyingEnemyExist = false;
        bool frighteningPlayerExist = false;
        bool terryfyingPlayerExist = false;

        foreach (var unit in AllUnits)
        {
            Stats unitStats = unit.GetComponent<Stats>();

            if(unitStats.Terryfying)
            {
                if(unit.CompareTag("EnemyUnit")) terryfyingEnemyExist = true;
                else if (unit.CompareTag("PlayerUnit")) terryfyingPlayerExist = true;
            }
            else if(unitStats.Frightening)
            {
                if(unit.CompareTag("EnemyUnit")) frighteningEnemyExist = true;
                else if (unit.CompareTag("PlayerUnit"))frighteningPlayerExist = true;
            }
        }

        if(terryfyingEnemyExist)
        {
            AllUnitsTerrorRoll("PlayerUnit");
        }
        else if(frighteningEnemyExist)
        {
            AllUnitsFearRoll("PlayerUnit");
        }

        if(terryfyingPlayerExist)
        {
            AllUnitsTerrorRoll("EnemyUnit");
        }
        else if(frighteningPlayerExist)
        {
            AllUnitsFearRoll("EnemyUnit");
        }
    }

    private void AllUnitsFearRoll(string unitTag)
    {
        foreach (var unit in AllUnits)
        {
            //Pomija jednostki, których nie dotyczy ten rzut (czyli sojusznicy strasznej jednostki, postacie ze zdolnością nieustraszony lub jednostki, które wcześniej zdały test)
            if(unit.CompareTag(unitTag) == false || unit.GetComponent<Stats>().Fearless == true || unit.IsFearTestPassed) continue;

            FearRoll(unit);
        }
    }

    private void FearRoll(Unit unit)
    {
        Stats unitStats = unit.GetComponent<Stats>();

        //Uwzględnia zdolność Odwaga
        int rollModifier = unitStats.StoutHearted ? 10 : 0;

        int rollResult = UnityEngine.Random.Range(1, 101);

        string stringResult = "";

        if (rollResult <= (unitStats.SW + rollModifier))
        {
            unit.IsScared = false;
            unit.IsFearTestPassed = true;

            stringResult = $"<color=green>{unitStats.Name} zdał test strachu. Wynik rzutu: {rollResult}. Wartość cechy: {unitStats.SW}.";
        }
        else
        {
            RoundsManager.Instance.UnitsWithActionsLeft[unit] = 0;
            unit.IsScared = true;

            stringResult = $"<color=red>{unitStats.Name} nie zdał testu strachu. Wynik rzutu: {rollResult}. Wartość cechy: {unitStats.SW}.";
        }

        if (rollModifier != 0)
        {
            stringResult += $" Modyfikator: {rollModifier}";
        }

        Debug.Log($"{stringResult}</color>");
    }

    private void AllUnitsTerrorRoll(string unitTag)
    {
        foreach (var unit in AllUnits)
        {
            //Pomija jednostki, których nie dotyczy ten rzut (czyli sojusznicy strasznej jednostki, postacie ze zdolnością nieustraszony lub jednostki, które wcześniej zdały test)
            if(unit.CompareTag(unitTag) == false || unit.IsFearTestPassed) continue;

            if(unit.GetComponent<Stats>().Fearless == true) 
            {
                FearRoll(unit);
                continue;
            }

            TerrorRoll(unit);
        }
    }

    private void TerrorRoll(Unit unit)
    {
        Stats unitStats = unit.GetComponent<Stats>();

        int rollResult = UnityEngine.Random.Range(1, 101);

        if (rollResult <= unitStats.SW)
        {
            unit.IsScared = false;
            unit.IsFearTestPassed = true;
            Debug.Log($"<color=green> {unitStats.Name} zdał test grozy. Wynik rzutu: {rollResult} </color>");
        }
        else
        {
            RoundsManager.Instance.UnitsWithActionsLeft[unit] = 0;
            unit.IsScared = true;
            unitStats.PO ++;

            Debug.Log($"<color=red> {unitStats.Name} nie zdał testu grozy. Wynik rzutu: {rollResult} </color>");
        }
    }
    #endregion
}
