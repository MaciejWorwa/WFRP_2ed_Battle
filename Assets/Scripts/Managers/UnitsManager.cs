using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UIElements;
using System.Reflection;
using System;
using System.IO;
using static UnityEngine.GraphicsBuffer;

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
    [SerializeField] private TMP_Text _raceDisplay;
    [SerializeField] private TMP_Text _healthDisplay;
    [SerializeField] private UnityEngine.UI.Slider _healthBar;
    [SerializeField] private UnityEngine.UI.Image _tokenDisplay;
    [SerializeField] private GameObject _unitPrefab;
    [SerializeField] private CustomDropdown _unitsDropdown;
    public Transform UnitsScrollViewContent;
    [SerializeField] private TMP_InputField _unitNameInputField;
    [SerializeField] private UnityEngine.UI.Slider _modifierAttributeSlider;
    [SerializeField] private UnityEngine.UI.Toggle _rollForHalfValueToggle; // Rzut na połowę cechy (gdy jednostka nie posiada umiejętności)
    [SerializeField] private UnityEngine.UI.Toggle _unitTagToggle;
    [SerializeField] private UnityEngine.UI.Toggle _unitSizeToggle;
    [SerializeField] private UnityEngine.UI.Button _createUnitButton; // Przycisk do tworzenia jednostek na losowych pozycjach
    [SerializeField] private UnityEngine.UI.Button _removeUnitButton;
    [SerializeField] private UnityEngine.UI.Button _selectUnitsButton; // Przycisk do zaznaczania wielu jednostek
    [SerializeField] private UnityEngine.UI.Button _removeSavedUnitFromListButton; // Przycisk do usuwania zapisanych jednostek z listy
    [SerializeField] private UnityEngine.UI.Toggle _savedUnitsManagingToggle;
    [SerializeField] private UnityEngine.UI.Button _updateUnitButton;
    [SerializeField] private UnityEngine.UI.Button _removeUnitConfirmButton;
    [SerializeField] private GameObject _removeUnitConfirmPanel;
    public static bool IsTileSelecting;
    public static bool IsMultipleUnitsSelecting = false;
    public static bool IsUnitRemoving = false;
    public static bool IsUnitEditing = false;
    public bool IsSavedUnitsManaging = false;
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
        if(Input.GetKeyDown(KeyCode.Delete) && (Unit.SelectedUnit != null || (AreaSelector.Instance.SelectedUnits != null && AreaSelector.Instance.SelectedUnits.Count > 1)))
        {
            if(_removeUnitConfirmPanel.activeSelf == false)
            {
                _removeUnitConfirmPanel.SetActive(true);
            }
            else
            {
                //Jeśli jest zaznaczone więcej jednostek, to usuwa wszystkie
                if(AreaSelector.Instance.SelectedUnits != null && AreaSelector.Instance.SelectedUnits.Count > 1)
                {
                    for (int i = AreaSelector.Instance.SelectedUnits.Count - 1; i >= 0; i--) 
                    {
                        DestroyUnit(AreaSelector.Instance.SelectedUnits[i].gameObject);
                    }
                    AreaSelector.Instance.SelectedUnits.Clear();
                }
                else
                {
                    DestroyUnit(Unit.SelectedUnit);
                }
                _removeUnitConfirmPanel.SetActive(false);
            }
        }
    }

    #region Creating units
    public void CreateUnitMode()
    {
        IsTileSelecting = true;

        Debug.Log("Wybierz pole na którym chcesz stworzyć jednostkę.");
        return;
    }

    public void CreateUnitOnSelectedTile(Vector2 position)
    {
        CreateUnit(_unitsDropdown.GetSelectedIndex(), _unitNameInputField.text, position);
    
        //Resetuje kolor przycisku z wybraną jednostką na liście jednostek
        CreateUnitButton.SelectedUnitButtonImage.color = new Color(0.55f, 0.66f, 0.66f, 0.05f);
    }

    public void CreateUnitOnRandomTile()
    {
        List<Vector2> availablePositions = AvailablePositions();
        Vector2 position = Vector2.zero;

        if (!SaveAndLoadManager.Instance.IsLoading)
        {
            if (availablePositions.Count == 0)
            {
                Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
                return;
            }

            // Wybranie losowej pozycji z dostępnych
            int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
            position = availablePositions[randomIndex];
        }

        CreateUnit(_unitsDropdown.GetSelectedIndex(), _unitNameInputField.text, position);
    }

    private List<Vector2> AvailablePositions()
    {
        List<Vector2> availablePositions = new List<Vector2>();

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

        return availablePositions;
    }

    public GameObject CreateUnit(int unitId, string unitName, Vector2 position)
    {
        if (_unitsDropdown.SelectedButton == null && SaveAndLoadManager.Instance.IsLoading != true)
        {
            Debug.Log("Wybierz jednostkę z listy.");
            return null;
        }

        //Resetuje input field z nazwą jednostki
        _unitNameInputField.text = null;

        // Pole na którym chcemy stworzyć jednostkę
        GameObject selectedTile = GameObject.Find($"Tile {position.x - GridManager.Instance.transform.position.x} {position.y - GridManager.Instance.transform.position.y}");

        //Gdy próbujemy wczytać jednostkę na polu, które nie istnieje (bo np. siatka jest obecnie mniejsza niż siatka, na której były zapisywane jednostki) lub jest zajęte to wybiera im losową pozycję
        if ((selectedTile == null || selectedTile.GetComponent<Tile>().IsOccupied) && SaveAndLoadManager.Instance.IsLoading == true)
        {
            List<Vector2> availablePositions = AvailablePositions();

            if (availablePositions.Count == 0)
            {
                Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
                return null;
            }

            // Wybranie losowej pozycji z dostępnych
            int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
            position = availablePositions[randomIndex];

            selectedTile = GameObject.Find($"Tile {position.x - GridManager.Instance.transform.position.x} {position.y - GridManager.Instance.transform.position.y}");
        }
        else if(selectedTile == null)
        {
            Debug.Log("Nie można stworzyć jednostki.");
            return null;
        }

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

        // Ustala unikalne Id jednostki
        int newUnitId = 1;
        bool idExists;
        // Pętla sprawdzająca, czy inne jednostki mają takie samo Id
        do
        {
            idExists = false;

            foreach (var unit in AllUnits)
            {
                if (unit.GetComponent<Unit>().UnitId == newUnitId)
                {
                    idExists = true;
                    newUnitId++; // Zwiększa id i sprawdza ponownie
                    break;
                }
            }
        }
        while (idExists);
        newUnit.GetComponent<Unit>().UnitId = newUnitId;

        //Ustala nazwę jednostki (potrzebne, do wczytywania jednostek z listy zapisanych jednostek)
        if(_unitsDropdown.SelectedButton != null && IsSavedUnitsManaging)
        {
            newUnit.GetComponent<Stats>().Name = _unitsDropdown.SelectedButton.GetComponentInChildren<TextMeshProUGUI>().text;
        }

        //Wczytuje statystyki dla danego typu jednostki
        DataManager.Instance.LoadAndUpdateStats(newUnit);

        //Ustala nazwę GameObjectu jednostki
        if (unitName.Length < 1)
        {
            newUnit.name = newUnit.GetComponent<Stats>().Race + $" {newUnitId}";
        }
        else
        {
            newUnit.name = unitName;
        }

        // Wczytuje dane zapisanej jednostki
        if (IsSavedUnitsManaging)
        {
            SaveAndLoadManager.Instance.IsLoading = true;
        
            string savedUnitsFolder = Path.Combine(Application.dataPath, "Resources", "savedUnits");
            string baseFileName = newUnit.GetComponent<Stats>().Name;

            //string unitFilePath = Path.Combine(savedUnitsFolder, baseFileName + "_unit.json");
            string weaponFilePath = Path.Combine(savedUnitsFolder, baseFileName + "_weapon.json");
            string inventoryFilePath = Path.Combine(savedUnitsFolder, baseFileName + "_inventory.json");
            string tokenJsonPath = Path.Combine(savedUnitsFolder, baseFileName + "_token.json");

            // SaveAndLoadManager.Instance.LoadComponentDataWithReflection<UnitData, Unit>(newUnit, unitFilePath);
            SaveAndLoadManager.Instance.LoadComponentDataWithReflection<WeaponData, Weapon>(newUnit, weaponFilePath);

            // Wczytaj ekwipunek
            if (File.Exists(inventoryFilePath))
            {
                InventoryData inventoryData = JsonUtility.FromJson<InventoryData>(File.ReadAllText(inventoryFilePath));
                foreach (var weapon in inventoryData.AllWeapons)
                {
                    InventoryManager.Instance.AddWeaponToInventory(weapon, newUnit);
                }

                //Wczytanie aktualnie dobytych broni
                foreach(var weapon in newUnit.GetComponent<Inventory>().AllWeapons)
                {
                    if(weapon.Id == inventoryData.EquippedWeaponsId[0])
                    {
                        newUnit.GetComponent<Inventory>().EquippedWeapons[0] = weapon;
                    }
                    if(weapon.Id == inventoryData.EquippedWeaponsId[1])
                    {
                        newUnit.GetComponent<Inventory>().EquippedWeapons[1] = weapon;
                    }
                }
                InventoryManager.Instance.CheckForEquippedWeapons();
            }

            // Wczytaj token
            if (File.Exists(tokenJsonPath))
            {
                string tokenJson = File.ReadAllText(tokenJsonPath);
                TokenData tokenData = JsonUtility.FromJson<TokenData>(tokenJson);
                StartCoroutine(TokensManager.Instance.LoadTokenImage(tokenData.filePath, newUnit));
            }

            SaveAndLoadManager.Instance.IsLoading = false;
        }

        if (SaveAndLoadManager.Instance.IsLoading != true)
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

            //Jednostki duże
            if (_unitSizeToggle.isOn)
            {
                newUnit.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
                newUnit.GetComponent<Stats>().IsBig = true;
            }
            else
            {
                newUnit.transform.localScale = new Vector3(1f, 1f, 1f);
                newUnit.GetComponent<Stats>().IsBig = false;
            }

            //Losuje początkowe statystyki dla człowieka, elfa, krasnoluda i niziołka
            if (newUnit.GetComponent<Stats>().Id <= 4 && !IsSavedUnitsManaging)
            {
                newUnit.GetComponent<Stats>().RollForBaseStats();
            }

            //Ustala początkową inicjatywę i dodaje jednostkę do kolejki inicjatywy
            newUnit.GetComponent<Stats>().Initiative = newUnit.GetComponent<Stats>().Zr + UnityEngine.Random.Range(1, 11);
            InitiativeQueueManager.Instance.AddUnitToInitiativeQueue(newUnit.GetComponent<Unit>());

            //Dodaje do ekwipunku początkową broń adekwatną dla danej jednostki i wyposaża w nią
            if (newUnit.GetComponent<Stats>().PrimaryWeaponId > 0 && !IsSavedUnitsManaging)
            {
                Unit.LastSelectedUnit = Unit.SelectedUnit != null ? Unit.SelectedUnit : null;
                Unit.SelectedUnit = newUnit;
                SaveAndLoadManager.Instance.IsLoading = true; // Tylko po to, żeby informacja o dobyciu broni i dodaniu do ekwipunku z metody GrabWeapon i LoadWeapon nie były wyświetlane w oknie wiadomości

                InventoryManager.Instance.GrabPrimaryWeapon();
            }
        }

        return newUnit;
    }

    public void SetSavedUnitsManaging()
    {
        IsSavedUnitsManaging = _savedUnitsManagingToggle.isOn;

        if(IsSavedUnitsManaging)
        {
            IsUnitEditing = false;

            _unitNameInputField.gameObject.SetActive(false);
            _createUnitButton.gameObject.SetActive(false);
            _removeUnitButton.gameObject.SetActive(false);
            _selectUnitsButton.gameObject.SetActive(false);
            _updateUnitButton.gameObject.SetActive(false);
            _removeSavedUnitFromListButton.gameObject.SetActive(true);
        }
        else
        {
            _unitNameInputField.gameObject.SetActive(true);
            _removeSavedUnitFromListButton.gameObject.SetActive(false);
            EditUnitModeOff();
        }
    }
    #endregion

    #region Removing units
    public void DestroyUnitMode()
    {
        if(GameManager.IsMapHidingMode)
        {
            Debug.Log("Aby usuwać jednostki, wyjdź z trybu ukrywania obszarów.");
            return;
        }

        IsUnitRemoving = !IsUnitRemoving;

        //Zmienia kolor przycisku usuwania jednostek na aktywny
        _removeUnitButton.GetComponent<UnityEngine.UI.Image>().color = IsUnitRemoving ? Color.green : Color.white;

        if(IsUnitRemoving)
        {
            //Jeżeli jest włączony tryb zaznaczania wielu jednostek to go resetuje
            if(IsMultipleUnitsSelecting)
            {
                SelectMultipleUnitsMode();
            }
            Debug.Log("Wybierz jednostkę, którą chcesz usunąć. Możesz również zaznaczyć obszar, wtedy zostaną usunięte wszystkie znajdujące się w nim jednostki.");
        }
    }
    public void DestroyUnit(GameObject unit = null)
    {
        if(unit == null)
        {
            unit = Unit.SelectedUnit;
        }
        else if (unit == Unit.SelectedUnit)
        {
            unit.GetComponent<Unit>().SelectUnit();
        }

        //Usunięcie jednostki z kolejki inicjatywy
        InitiativeQueueManager.Instance.RemoveUnitFromInitiativeQueue(unit.GetComponent<Unit>());
        //Aktualizuje kolejkę inicjatywy
        InitiativeQueueManager.Instance.UpdateInitiativeQueue();

        //Uwolnienie jednostki uwięzionej przez jednostkę, która umiera
        if(unit.GetComponent<Unit>().TrappedUnitId != 0)
        {
            foreach (var u in AllUnits)
            {
                if (u.UnitId == unit.GetComponent<Unit>().TrappedUnitId && u.Trapped == true)
                {
                    u.Trapped = false;
                }
            }
        }

        //Usuwa jednostkę z listy wszystkich jednostek
        AllUnits.Remove(unit.GetComponent<Unit>());

        Destroy(unit);

        //Resetuje Tile, żeby nie było uznawane jako zajęte
        GridManager.Instance.ResetTileOccupancy(unit.transform.position);

        // IsUnitRemoving = false;

        //Resetuje kolor przycisku usuwania jednostek
        _removeUnitButton.GetComponent<UnityEngine.UI.Image>().color = Color.white;
    }
    #endregion

    #region Unit selecting
    public void SelectMultipleUnitsMode(bool value = true)
    {
        // Jeśli `value` jest false, wyłącza tryb zaznaczania, w przeciwnym razie przełącza tryb
        IsMultipleUnitsSelecting = value ? !IsMultipleUnitsSelecting : false;

        // Ustawia kolor przycisku w zależności od stanu
        _selectUnitsButton.GetComponent<UnityEngine.UI.Image>().color = IsMultipleUnitsSelecting ? Color.green : Color.white;

        // Wyświetla komunikat, jeśli tryb zaznaczania jest aktywny
        if (IsMultipleUnitsSelecting)
        {
            //Jeżeli jest włączony tryb usuwania jednostek to go resetuje
            if(IsUnitRemoving)
            {
                DestroyUnitMode();
            }
            Debug.Log("Zaznacz jednostki na wybranym obszarze przy użyciu myszy. Klikając Ctrl+C możesz je skopiować, a następnie wkleić przy pomocy Ctrl+V.");
        }
    }

    public void ResetSelectedUnitState()
    {
        if(Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();

        unit.StunDuration = 0; 
        unit.HelplessDuration = 0;
        unit.Trapped = false;
        unit.TrappedUnitId = 0;
        unit.IsScared = false;
        unit.IsFearTestPassed = true;

        RoundsManager.Instance.UnitsWithActionsLeft[unit] = 2;

        UpdateUnitPanel(Unit.SelectedUnit);
    }
    #endregion

    #region Unit editing
    public void EditUnitModeOn(Animator panelAnimator)
    {
        IsUnitEditing = true;
        
        _createUnitButton.gameObject.SetActive(false);
        _removeUnitButton.gameObject.SetActive(false);
        _selectUnitsButton.gameObject.SetActive(false);
        _updateUnitButton.gameObject.SetActive(true);
        _removeSavedUnitFromListButton.gameObject.SetActive(false);

        //Jeśli panel edycji jednostek jest schowany to wysuwamy go
        if(AnimationManager.Instance.PanelStates[panelAnimator] == false)
        {
            AnimationManager.Instance.TogglePanel(panelAnimator);
        }
    }

    public void EditUnitModeOff()
    {
        IsUnitEditing = false;
        
        _createUnitButton.gameObject.SetActive(true);
        _removeUnitButton.gameObject.SetActive(true);
        _selectUnitsButton.gameObject.SetActive(true);
        _updateUnitButton.gameObject.SetActive(false);

        if(_savedUnitsManagingToggle.isOn)
        {
            _savedUnitsManagingToggle.isOn = false;
            _removeSavedUnitFromListButton.gameObject.SetActive(false);
        }
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
        Stats stats = unit.GetComponent<Stats>();

        //Aktualizuje imię postaci
        if (_unitNameInputField.text.Length > 0)
        {
            stats.Name = _unitNameInputField.text;

            unit.GetComponent<Unit>().DisplayUnitName();

            unit.name = _unitNameInputField.text;
            //Resetuje input field z nazwą jednostki
            _unitNameInputField.text = null;
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

        //Jednostki duże
        if (_unitSizeToggle.isOn)
        {
            stats.IsBig = true;
            unit.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
        }
        else
        {
            stats.IsBig = false;
            unit.transform.localScale = new Vector3(1f, 1f, 1f);
        }
        //Sprawdza, czy rasa jest zmieniana
        if (stats.Id != _unitsDropdown.GetSelectedIndex())
        {
            bool changeName = false;

            if (stats.Name.Contains(stats.Race))
            {
                changeName = true;
            }

            // Sprawdza, czy ostatni jeden lub dwa znaki to liczba
            string currentName = stats.Name;
            string numberSuffix = "";
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(currentName, @"(\d{1,2})$");
            if (match.Success)
            {
                numberSuffix = match.Value; // Przechowuje numer znaleziony na końcu nazwy
            }

            // Ustala nową rasę na podstawie rasy wybranej z listy
            stats.Id = _unitsDropdown.GetSelectedIndex();

            string newRaceName = _unitsDropdown.SelectedButton.GetComponentInChildren<TextMeshProUGUI>().text;

            if (changeName)
            {
                // Jeśli zmieniamy nazwę, dodajemy zachowaną liczbę (jeśli istnieje)
                if (!string.IsNullOrEmpty(numberSuffix))
                {
                    stats.Name = $"{newRaceName} {numberSuffix}";
                }
                else
                {
                    stats.Name = newRaceName;
                }
            }

            //Aktualizuje statystyki
            DataManager.Instance.LoadAndUpdateStats(unit);

            //Losuje początkowe statystyki dla człowieka, elfa, krasnoluda i niziołka
            if (stats.Id <= 4 && !IsSavedUnitsManaging)
            {
                stats.RollForBaseStats();
                unit.GetComponent<Unit>().DisplayUnitHealthPoints();
            }

            //Aktualizuje siłę i wytrzymałość
            unit.GetComponent<Unit>().CalculateStrengthAndToughness();

            //Aktualizuje aktualną żywotność
            stats.TempHealth = stats.MaxHealth;

            //Ustala inicjatywę i aktualizuje kolejkę inicjatywy
            stats.Initiative = stats.Zr + UnityEngine.Random.Range(1, 11);
            InitiativeQueueManager.Instance.RemoveUnitFromInitiativeQueue(unit.GetComponent<Unit>());
            InitiativeQueueManager.Instance.AddUnitToInitiativeQueue(unit.GetComponent<Unit>());
            InitiativeQueueManager.Instance.UpdateInitiativeQueue();

            //Dodaje do ekwipunku początkową broń adekwatną dla danej jednostki i wyposaża w nią
            if (stats.PrimaryWeaponId > 0 && changeName)
            {
                Unit.LastSelectedUnit = Unit.SelectedUnit != null ? Unit.SelectedUnit : null;
                Unit.SelectedUnit = unit;
                SaveAndLoadManager.Instance.IsLoading = true; // Tylko po to, żeby informacja o dobyciu broni i dodaniu do ekwipunku z metody GrabWeapon i LoadWeapon nie były wyświetlane w oknie wiadomości

                InventoryManager.Instance.GrabPrimaryWeapon();
            }

            unit.GetComponent<Unit>().DisplayUnitName();
            unit.GetComponent<Unit>().DisplayUnitHealthPoints();
        }

        //Aktualizuje wyświetlany panel ze statystykami
        UpdateUnitPanel(unit);
    }

    public void UpdateInitiative()
    {
        if (Unit.SelectedUnit == null) return;

        GameObject unit = Unit.SelectedUnit;
        Stats stats = unit.GetComponent<Stats>();

        //Ustala nową inicjatywę
        stats.Initiative = stats.Zr + UnityEngine.Random.Range(1, 11);

        //Uwzględnienie kary do Zręczności za pancerz
        if(stats.Armor_head >= 3 || stats.Armor_torso >= 3 || stats.Armor_arms >= 3 || stats.Armor_legs >= 3)
        {
            stats.Initiative -= 10;
        }

        //Aktualizuje kolejkę inicjatywy
        InitiativeQueueManager.Instance.InitiativeQueue[unit.GetComponent<Unit>()] = stats.Initiative;
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
        else if (field.FieldType == typeof(string))
        {
            string value = textInput.GetComponent<TMP_InputField>().text;
            field.SetValue(unit.GetComponent<Stats>(), value);
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
        else if(attributeName == "K" || attributeName == "Odp")
        {
            unit.GetComponent<Unit>().CalculateStrengthAndToughness();
        }
        else if(attributeName == "Name")
        {
            unit.GetComponent<Unit>().DisplayUnitName();
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

            //W trybie ukrywania statystyk, panel wrogich jednostek pozostaje wyłączony
            if(GameManager.IsStatsHidingMode && unit.CompareTag("EnemyUnit"))
            {
                _unitPanel.transform.Find("VerticalLayoutGroup/Stats_Panel/Stats_display").gameObject.SetActive(false);
            }
            else
            {
                _unitPanel.transform.Find("VerticalLayoutGroup/Stats_Panel/Stats_display").gameObject.SetActive(true);
            }
            
            //Ukrywa lub pokazuje nazwę jednostki w panelu
            if(GameManager.IsNamesHidingMode && !MultiScreenDisplay.Instance.PlayersCamera.gameObject.activeSelf && Display.displays.Length == 1)
            {
                _unitPanel.transform.Find("Name_input").gameObject.SetActive(false);
            }
            else
            {
                _unitPanel.transform.Find("Name_input").gameObject.SetActive(true);
            }

            Unit unitComponent = unit.GetComponent<Unit>();

            if(unitComponent.StunDuration == 0 && unitComponent.HelplessDuration == 0 && unitComponent.Trapped == false && unitComponent.IsScared == false && unitComponent.TrappedUnitId == 0)
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
                else if (unitComponent.TrappedUnitId != 0)
                {
                    state = "unieruchamiania innej jednostki swoją bronią.";
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
            unit.GetComponent<Unit>().CanCastSpell = true;

            if(unit.GetComponent<Spell>() == null)
            {
                unit.AddComponent<Spell>();
            }
        }
        else
        {
            _spellbookButton.SetActive(false);
        }
        //_nameDisplay.text = stats.Name;
        _raceDisplay.text = stats.Race;

        _healthDisplay.text = stats.TempHealth + "/" + stats.MaxHealth;
        _healthBar.maxValue = stats.MaxHealth;
        _healthBar.value = stats.TempHealth;
        UpdateHealthBarColor(stats.TempHealth, stats.MaxHealth, _healthBar.transform.Find("Fill Area/Fill").GetComponent<UnityEngine.UI.Image>());

        _tokenDisplay.sprite = unit.transform.Find("Token").GetComponent<SpriteRenderer>().sprite;

        LoadAttributes(unit);
    }

    private void UpdateHealthBarColor(float tempHealth, float maxHealth, UnityEngine.UI.Image image)
    {
        float percentage = tempHealth / maxHealth * 100;

        if (percentage <= 30)
        {
            image.color = new Color(0.81f, 0f, 0.137f); // Kolor czerwony, jeśli wartość <= 30%
        }
        else if (percentage > 30 && percentage <= 70)
        {
            image.color = new Color(1f, 0.6f, 0f); // Kolor pomarańczowy, jeśli wartość jest między 31% a 70%
        }
        else
        {
            image.color = new Color(0.3f, 0.65f, 0.125f); // Kolor zielony, jeśli wartość > 70%
        }
    }

    public void LoadAttributesByButtonClick()
    {
        if(Unit.SelectedUnit == null) return;

        GameObject unit = Unit.SelectedUnit;
        LoadAttributes(unit);
    }

    public void LoadAttributes(GameObject unit)
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
            else if (field.FieldType == typeof(string)) // to działa dla cech opisywanych wartościami string
            {
                string value = (string)field.GetValue(unit.GetComponent<Stats>());

                if (inputField.GetComponent<TMPro.TMP_InputField>() != null)
                {
                    inputField.GetComponent<TMPro.TMP_InputField>().text = value;
                }
            }

            if(attributeName == "Initiative")
            {
                //Aktualizuje kolejkę inicjatywy
                InitiativeQueueManager.Instance.InitiativeQueue[unit.GetComponent<Unit>()] = unit.GetComponent<Stats>().Initiative;
                InitiativeQueueManager.Instance.UpdateInitiativeQueue();
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
        if(_rollForHalfValueToggle.isOn)
        {
            value = value / 2;
        }
        int successLevel = Math.Abs(value + modifier - rollResult) / 10;

        //Uwzględnienie kary do Zręczności za pancerz
        if(attributeName == "Zr" && (stats.Armor_head >= 3 || stats.Armor_torso >= 3 || stats.Armor_arms >= 3 || stats.Armor_legs >= 3))
        {
            modifier -= 10;
        }

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

        bool enemyUnitExists = false;
        bool playerUnitExists = false;

        foreach (var pair in InitiativeQueueManager.Instance.InitiativeQueue)
        {
            Stats unitStats = pair.Key.GetComponent<Stats>();

            if (pair.Key.CompareTag("EnemyUnit")) enemyUnitExists = true;
            if (pair.Key.CompareTag("PlayerUnit")) playerUnitExists = true;

            if(unitStats.Terryfying)
            {
                if(pair.Key.CompareTag("EnemyUnit")) terryfyingEnemyExist = true;
                else if (pair.Key.CompareTag("PlayerUnit")) terryfyingPlayerExist = true;
            }
            else if(unitStats.Frightening)
            {
                if(pair.Key.CompareTag("EnemyUnit")) frighteningEnemyExist = true;
                else if (pair.Key.CompareTag("PlayerUnit"))frighteningPlayerExist = true;
            }
        }

        // Sprawdza, czy istnieją jednostki tylko jednego typu
        if (!enemyUnitExists || !playerUnitExists)
        {
            // Jeśli istnieje tylko jeden typ jednostek, wszystkie jednostki przestają się bać
            foreach (var pair in InitiativeQueueManager.Instance.InitiativeQueue)
            {
                pair.Key.IsScared = false;
            }

            return;
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

        //Jednostki z SW równym 0, np. Zombie są istotami bez własnej woli, których nie dotyczą testy tej cechy
        if (unitStats.SW == 0) return;

        //Uwzględnia zdolność Odwaga
        int rollModifier = unitStats.StoutHearted ? 10 : 0;

        int rollResult = UnityEngine.Random.Range(1, 101);

        string stringResult = "";

        if (rollResult <= (unitStats.SW + rollModifier))
        {
            RoundsManager.Instance.UnitsWithActionsLeft[unit] = 2;
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

        //Jednostki z SW równym 0, np. Zombie są istotami bez własnej woli, których nie dotyczą testy tej cechy
        if (unitStats.SW == 0) return;

        int rollResult = UnityEngine.Random.Range(1, 101);

        if (rollResult <= unitStats.SW)
        {
            RoundsManager.Instance.UnitsWithActionsLeft[unit] = 2;
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

    public void SetSelectedUnitFearState(bool value)
    {
        if(Unit.SelectedUnit == null) return;

        Unit.SelectedUnit.GetComponent<Unit>().IsScared = value;

        if(value == true)
        {
            RoundsManager.Instance.UnitsWithActionsLeft[Unit.SelectedUnit.GetComponent<Unit>()] = 0;
        }

        UpdateUnitPanel(Unit.SelectedUnit);
    }
    #endregion

}
