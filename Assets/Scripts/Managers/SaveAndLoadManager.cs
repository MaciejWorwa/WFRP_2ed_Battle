using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using TMPro;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static UnityEngine.UI.CanvasScaler;
using SimpleFileBrowser;
using System.Xml.Linq;

public class SaveAndLoadManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static SaveAndLoadManager instance;

    // Publiczny dostęp do instancji
    public static SaveAndLoadManager Instance
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

    [SerializeField] private TMP_InputField _saveNameInput;
    [SerializeField] private Transform _savesScrollViewContent;
    [SerializeField] private GameObject _buttonPrefab; // Przycisk odpowiadający każdemu zapisowi na liście
    [SerializeField] private GameObject _loadGamePanel; 

    public bool IsLoading;
    public bool IsOnlyUnitsLoading;

    #region Saving methods
    public void SaveAllUnits(GameObject saveGamePanel)
    {
        if (_saveNameInput.text.Length < 1 || _saveNameInput.text == "autosave")
        {
            Debug.Log($"<color=red>Zapis nieudany. Niepoprawna nazwa pliku.</color>");
            return;
        }

        List<Unit> allUnits = UnitsManager.Instance.AllUnits;

        if (allUnits.Count < 1)
        {
            Debug.Log($"<color=red>Zapis nieudany. Aby zapisać grę, musisz stworzyć chociaż jedną postać.</color>");
            return;
        }

        SaveUnits(allUnits);

        //Zapisanie wszystkich elementów mapy
        SaveMap();

        //Resetuje inpu fielda i zamyka panel
        _saveNameInput.text = "";
        saveGamePanel.SetActive(false);

        Debug.Log($"<color=green>Zapisano stan gry.</color>");
    }

    public void SaveUnits(List<Unit> allUnits, string savesFolderName = "")
    {
        //BinaryFormatter formatter = new BinaryFormatter(); // ZAKOMENTOWANY KOD TO SPOSÓB SZYFROWANIA DANYCH. W PRZYPADKU KORZYSTANIA Z NIEGO ZMIENIC FORMAT PLIKÓW Z .JSON NA .FUN

        // Utworzenie listy nazw wszystkich jednostek
        List<string> unitNames = new List<string>();
        foreach (var unit in allUnits)
        {
            unitNames.Add(unit.GetComponent<Stats>().Name);
        }

        // Stworzenie folderu dla zapisów
        if (savesFolderName.Length < 1)
        {
            savesFolderName = _saveNameInput.text;
        }
        Directory.CreateDirectory(Application.persistentDataPath + "/" + savesFolderName);

        // Pobranie listy zapisanych plików
        string[] files = Directory.GetFiles(Application.persistentDataPath + "/" + savesFolderName, "*.json");

        // Sprawdzenie, czy w liście znajdują się pliki, których nazwa nie pasuje do nazw postaci w 'unitNames' i usunięcie ich
        foreach (string file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);

            if (!unitNames.Contains(fileName))
            {
                File.Delete(file);
            }
        }

        //Zapisuje numer rundy i dostępne akcje wszystkich jednostek
        SaveRoundsManager(savesFolderName, allUnits);

        //Zapisuje wymiary siatki
        SaveGridManager(savesFolderName);

        //Zapis statystyk wszystkich postaci
        foreach (var unit in allUnits)
        {
            string unitName = unit.GetComponent<Stats>().Name;

            string unitPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_unit.json");
            string statsPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_stats.json");
            string weaponPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_weapon.json");
            string inventoryPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_inventory.json");
            string tokenJsonPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_token.json");

            UnitData unitData = new UnitData(unit);
            StatsData statsData = new StatsData(unit.GetComponent<Stats>());
            WeaponData weaponData = new WeaponData(unit.GetComponent<Weapon>());
            InventoryData inventoryData = new InventoryData(unit.GetComponent<Inventory>());
            TokenData tokenData = new TokenData { filePath = unit.TokenFilePath};

            // Serializacja danych do JSON
            string unitJsonData = JsonUtility.ToJson(unitData, true);
            string statsJsonData = JsonUtility.ToJson(statsData, true);
            string weaponJsonData = JsonUtility.ToJson(weaponData, true);
            string inventoryJsonData = JsonUtility.ToJson(inventoryData, true);
            string tokenJsonData = JsonUtility.ToJson(tokenData, true);

            // Zapisanie danych do pliku
            File.WriteAllText(unitPath, unitJsonData);
            File.WriteAllText(statsPath, statsJsonData);
            File.WriteAllText(weaponPath, weaponJsonData);
            File.WriteAllText(inventoryPath, inventoryJsonData);
            File.WriteAllText(tokenJsonPath, tokenJsonData);
        }
    }

    private void SaveRoundsManager(string savesFolderName, List<Unit> allUnits)
    {
        string roundsManagerPath = Path.Combine(Application.persistentDataPath, savesFolderName, "RoundsManager.json");

        RoundsManagerData roundsManagerData = new RoundsManagerData(allUnits);

        // Serializacja do JSON
        foreach (var pair in RoundsManager.Instance.UnitsWithActionsLeft)
        {
            if (pair.Key == null) continue;

            roundsManagerData.Entries.Add(new UnitNameAndActionsLeft() { UnitName = pair.Key.gameObject.name, ActionsLeft = pair.Value });
        }
        string roundsManagerJsonData = JsonUtility.ToJson(roundsManagerData, true);

        // Zapisanie danych do pliku
        File.WriteAllText(roundsManagerPath, roundsManagerJsonData);
    }

    private void SaveGridManager(string savesFolderName)
    {
        string gridManagerPath = Path.Combine(Application.persistentDataPath, savesFolderName, "GridManager.json");

        GridManagerData gridManagerData = new GridManagerData();

        string gridManagerJsonData = JsonUtility.ToJson(gridManagerData, true);

        // Zapisanie danych do pliku
        File.WriteAllText(gridManagerPath, gridManagerJsonData);
    }

    public void SaveFortunePoints(string savesFolderName, Stats stats, int PS)
    {
        string unitName = stats.Name;

        string statsPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_stats.json");

        StatsData statsData = new StatsData(stats);
        statsData.PS = PS;

        string statsJsonData = JsonUtility.ToJson(statsData, true);
        File.WriteAllText(statsPath, statsJsonData);
    }

    public void SaveMap()
    {
        string savesFolderName;

        // Stworzenie folderu dla zapisów
        savesFolderName = _saveNameInput.text;
        Directory.CreateDirectory(Application.persistentDataPath + "/" + savesFolderName);

        // Pobranie listy zapisanych plików
        string previousFile = Path.Combine(Application.persistentDataPath, savesFolderName, "MapElements.json");

        // Usuwa plik z poprzedniego zapisu
        File.Delete(previousFile);

        MapElementsContainer container = new MapElementsContainer();

        if (MapEditor.Instance == null) return;

        // Zbieranie danych z każdego elementu
        foreach (var element in MapEditor.Instance.AllElements)
        {
            MapElement mapElement = element.GetComponent<MapElement>();
            if (mapElement != null)
            {
                MapElementsData data = new MapElementsData(mapElement);
                container.Elements.Add(data);
            }
        }

        container.BackgroundImagePath = MapEditor.BackgroundImagePath;
        container.BackgroundPositionX = MapEditor.BackgroundPositionX;
        container.BackgroundPositionY = MapEditor.BackgroundPositionY;
        container.BackgroundScale = MapEditor.BackgroundScale;

        // Ścieżka do pliku JSON
        string mapElementsPath = Path.Combine(Application.persistentDataPath, savesFolderName, "MapElements.json");

        // Konwersja kontenera z listą danych do JSON
        string mapElementsJsonData = JsonUtility.ToJson(container, true);

        // Zapis do pliku
        File.WriteAllText(mapElementsPath, mapElementsJsonData);

        //Zapisanie siatki
        SaveGridManager(savesFolderName);

        Debug.Log($"<color=green>Zapisano mapę.</color>");
    }

    #endregion

    #region Loading methods

    //Ustala, czy wczytujemy całą grę, czy jedynie jednostki
    public void SetLoadingType(bool value)
    {
        IsOnlyUnitsLoading = value;
    }
    public void LoadAllUnits(string saveName = "")
    {
        CustomDropdown dropdown = _savesScrollViewContent.GetComponent<CustomDropdown>();
        if(dropdown == null || (saveName == "" && dropdown.SelectedButton == null))
        {
            Debug.Log($"<color=red>Aby wczytać grę musisz wybrać plik z listy.</color>");
            return;
        }

        if(saveName.Length < 1)
        {
            saveName = dropdown.SelectedButton.GetComponentInChildren<TextMeshProUGUI>().text;
        }

        string saveFolderPath = Path.Combine(Application.persistentDataPath, saveName);

        if(!Directory.Exists(saveFolderPath))
        {
            Debug.Log("Nie znaleziono pliku o podanej nazwie.");
            return;
        }

        IsLoading = true;

        //Odznaczenie zaznaczonej postaci
        if (Unit.SelectedUnit != null)
        {
            Unit.SelectedUnit.GetComponent<Unit>().SelectUnit();
        }

        // Kopiuje listę jednostek do nowej listy, aby móc bezpiecznie modyfikować oryginalną listę
        List<Unit> unitsToRemove = new List<Unit>(UnitsManager.Instance.AllUnits);

        // Usuwa wszystkie obecne na polu bitwy jednostki
        foreach (var unit in unitsToRemove)
        {  
            if(unit != null)
            {
                //Resetuje pola zajęte przez jednostki, które zostaną usunięte
                GridManager.Instance.ResetTileOccupancy(unit.transform.position);
                
                UnitsManager.Instance.DestroyUnit(unit.gameObject);
            }
        }

        if(saveName != "autosave" && IsOnlyUnitsLoading != true)
        {
            //Wczytanie mapy
            LoadMap();
        }

        StartCoroutine(LoadAllUnitsWithDelay(saveFolderPath));

        if(_loadGamePanel!= null)
        {
            _loadGamePanel.SetActive(false);
        }
    }

    public IEnumerator LoadAllUnitsWithDelay(string saveFolderPath)
    {
        var unitFiles = Directory.GetFiles(saveFolderPath, "*_unit.json");

        if(unitFiles == null)
        {
            IsLoading = false;
            yield break;
        }

        foreach (string unitFile in unitFiles)
        {
            //Pobieramy nazwę jednostki, usuwając końcówkę nazwy pliku
            string baseFileName = Path.GetFileNameWithoutExtension(unitFile).Replace("_unit", "");

            //Ścieżki do konkretnych plików z danymi
            string unitFilePath = Path.Combine(saveFolderPath, baseFileName + "_unit.json");
            string statsFilePath = Path.Combine(saveFolderPath, baseFileName + "_stats.json");
            string weaponFilePath = Path.Combine(saveFolderPath, baseFileName + "_weapon.json");
            string inventoryFilePath = Path.Combine(saveFolderPath, baseFileName + "_inventory.json");
            string tokenJsonPath = Path.Combine(saveFolderPath, baseFileName + "_token.json");

            // Wczytanie i deserializacja StatsData
            StatsData statsData = JsonUtility.FromJson<StatsData>(File.ReadAllText(statsFilePath));

            // Wczytanie i deserializacja UnitData
            UnitData unitData = JsonUtility.FromJson<UnitData>(File.ReadAllText(unitFilePath));

            //Ustalenie pozycji jednostki
            Vector3 position = new Vector3(unitData.position[0], unitData.position[1], unitData.position[2]);

            //Stworzenie jednostki o konkretnym Id, nazwie i na ustalonej pozycji
            GameObject unitGameObject = UnitsManager.Instance.CreateUnit(statsData.Id, baseFileName, position);

            if(unitGameObject == null) yield break;

            //Wczytanie taga i koloru jednostki
            if (unitData.Tag == "PlayerUnit")
            {
                unitGameObject.GetComponent<Unit>().DefaultColor = new Color(0f, 0.54f, 0.17f, 1.0f);
            }
            else if (unitData.Tag == "EnemyUnit")
            {
                unitGameObject.GetComponent<Unit>().DefaultColor = new Color(0.72f, 0.15f, 0.17f, 1.0f);
            }
            unitGameObject.tag = unitData.Tag;
            unitGameObject.GetComponent<Unit>().ChangeUnitColor(unitGameObject);

            yield return new WaitForSeconds(0.05f); // Oczekiwanie na zainicjowanie komponentów

            // Kontynuacja wczytywania i aktualizacji pozostałych danych jednostki
            LoadComponentDataWithReflection<StatsData, Stats>(unitGameObject, statsFilePath);
            LoadComponentDataWithReflection<UnitData, Unit>(unitGameObject, unitFilePath);
            LoadComponentDataWithReflection<WeaponData, Weapon>(unitGameObject, weaponFilePath);

            //Wczytanie ekwipunku jednostki
            InventoryData inventoryData = JsonUtility.FromJson<InventoryData>(File.ReadAllText(inventoryFilePath));
            Unit.SelectedUnit = unitGameObject;
            foreach(var weapon in inventoryData.AllWeapons)
            {
                Weapon unitWeapon = Unit.SelectedUnit.GetComponent<Weapon>();

                var fields = typeof(Weapon).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                var thisFields = weapon.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                foreach (var field in fields)
                {
                    var thisField = thisFields.FirstOrDefault(f => f.Name == field.Name);
                    if (thisField != null)
                    {
                        var value = thisField.GetValue(weapon); // Pobieranie wartości z obiektu źródłowego
                        if (value != null)
                        {
                            field.SetValue(unitWeapon, value); // Ustawianie wartości na obiekt docelowy
                        }
                    }
                }

                DataManager.Instance.LoadAndUpdateWeapons(weapon);
            }
            //Wczytanie aktualnie dobytych broni
            foreach(var weapon in Unit.SelectedUnit.GetComponent<Inventory>().AllWeapons)
            {
                if(weapon.Id == inventoryData.EquippedWeaponsId[0])
                {
                    Unit.SelectedUnit.GetComponent<Inventory>().EquippedWeapons[0] = weapon;
                }
                if(weapon.Id == inventoryData.EquippedWeaponsId[1])
                {
                    Unit.SelectedUnit.GetComponent<Inventory>().EquippedWeapons[1] = weapon;
                }
            }
            InventoryManager.Instance.CheckForEquippedWeapons();

            // Wczytanie tokena, jeśli istnieje
            if (File.Exists(tokenJsonPath))
            {
                string tokenJson = File.ReadAllText(tokenJsonPath);
                TokenData tokenData = JsonUtility.FromJson<TokenData>(tokenJson);
                StartCoroutine(TokensManager.Instance.LoadTokenImage(tokenData.filePath, Unit.SelectedUnit));
            }

            //Dodaje jednostkę do kolejki inicjatywy
            InitiativeQueueManager.Instance.AddUnitToInitiativeQueue(unitGameObject.GetComponent<Unit>());

            if (unitGameObject.GetComponent<Unit>().IsSelected) unitGameObject.GetComponent<Unit>().SelectUnit();
        }

        LoadRoundsManager(saveFolderPath);

        Unit.SelectedUnit = null;
        InitiativeQueueManager.Instance.UpdateInitiativeQueue();

        IsLoading = false;
        Debug.Log($"<color=green>Wczytano stan gry.</color>");
    }

    private void LoadComponentDataWithReflection<TData, TComponent>(GameObject gameObject, string filePath)
        where TData : class
        where TComponent : Component
    {
        if (!File.Exists(filePath)) return;

        // Deserializacja JSON do obiektu danych
        string jsonData = File.ReadAllText(filePath);
        TData dataObject = JsonUtility.FromJson<TData>(jsonData);

        // Pobranie komponentu z GameObject
        TComponent component = gameObject.GetComponent<TComponent>();
        if (component == null) return;

        // Uzyskanie dostępu do pól w komponencie i aktualizacja ich wartości
        FieldInfo[] componentFields = component.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo[] dataFields = typeof(TData).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (FieldInfo dataField in dataFields)
        {
            FieldInfo componentField = componentFields.FirstOrDefault(f => f.Name == dataField.Name);
            if (componentField != null)
            {
                object value = dataField.GetValue(dataObject);
                componentField.SetValue(component, value);
            }
        }

        gameObject.GetComponent<Unit>().DisplayUnitHealthPoints();
    }
 
    private void LoadRoundsManager(string savesFolderPath)
    {
        string filePath = Path.Combine(savesFolderPath, "RoundsManager.json");

        // Sprawdź, czy plik istnieje
        if (File.Exists(filePath))
        {
            // Deserializuj dane z pliku JSON do obiektu RoundsManagerData
            string jsonData = File.ReadAllText(filePath);
            RoundsManagerData data = JsonUtility.FromJson<RoundsManagerData>(jsonData);

            // Załaduj wczytane dane do istniejącego obiektu RoundsManager
            RoundsManager.Instance.LoadRoundsManagerData(data);
        }
        else
        {
            Debug.LogError("Pliku nie znaleziono.");
        }
    }

    private void LoadGridManager(string filePath)
    {
        // Sprawdź, czy plik istnieje
        if (File.Exists(filePath))
        {
            // Deserializuj dane z pliku JSON do obiektu GridManagerData
            string jsonData = File.ReadAllText(filePath);
            GridManagerData data = JsonUtility.FromJson<GridManagerData>(jsonData);

            // Załaduj wczytane dane do istniejącego obiektu GridManager
            GridManager.Instance.LoadGridManagerData(data);

            GridManager.Instance.GenerateGrid();
            GridManager.Instance.CheckTileOccupancy();
        }
        else
        {
            Debug.LogError("Pliku nie znaleziono.");
        }
    }

    public void LoadMap()
    {
        CustomDropdown dropdown = _savesScrollViewContent.GetComponent<CustomDropdown>();
        string saveName = dropdown.SelectedButton.GetComponentInChildren<TextMeshProUGUI>().text;

        string mapElementsFilePath = Path.Combine(Application.persistentDataPath, saveName, "MapElements.json");
        string gridFilePath = Path.Combine(Application.persistentDataPath, saveName, "GridManager.json");

        LoadGridManager(gridFilePath);

        // Sprawdź, czy plik istnieje
        if (File.Exists(mapElementsFilePath) && MapEditor.Instance != null)
        {
            string jsonData = File.ReadAllText(mapElementsFilePath);
            MapElementsContainer data = JsonUtility.FromJson<MapElementsContainer>(jsonData);

            // Załaduj wczytane dane do istniejącego obiektu MapEditor
            MapEditor.Instance.LoadMapData(data);
        }
        else
        {
            Debug.LogError("Pliku nie znaleziono.");
        }
    }
    #endregion

    #region Managing saves dropdown
    public void LoadSavesDropdown()
    {
        CustomDropdown dropdown = _savesScrollViewContent.GetComponent<CustomDropdown>();

        // Wczytanie wszystkich zapisanych folderów w Application.persistentDataPath
        string[] saveFolders = Directory.GetDirectories(Application.persistentDataPath);

        // Pobranie wszystkich istniejących buttonów na wyświetlanej liście zapisanych plików
        List<UnityEngine.UI.Button> existingButtons = dropdown.Buttons;

        // Przygotowanie listy do przechowywania istniejących nazw zapisów
        List<string> existingButtonNames = existingButtons.Select(button => button.GetComponentInChildren<TextMeshProUGUI>().text).ToList();

        foreach (var folderPath in saveFolders)
        {
            // Uzyskanie nazwy folderu do wyświetlenia
            string folderName = new DirectoryInfo(folderPath).Name;

            // Sprawdź, czy przycisk z tą nazwą zapisu już istnieje
            if (existingButtonNames.Contains(folderName)) continue;

            //Dodaje nazwę pliku do ScrollViewContent w postaci buttona
            GameObject buttonObj = Instantiate(_buttonPrefab, _savesScrollViewContent);
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            //Ustala text buttona
            buttonText.text = folderName;

            UnityEngine.UI.Button button = buttonObj.GetComponent<UnityEngine.UI.Button>();
            
            //Dodaje opcję do CustomDropdowna ze wszystkimi zapisami
            dropdown.Buttons.Add(button);

            int currentIndex = dropdown.Buttons.Count; // Pobiera indeks nowego przycisku

            // Zdarzenie po kliknięciu na konkretny zapis z listy
            button.onClick.AddListener(() =>
            {
                dropdown.SetSelectedIndex(currentIndex); // Wybiera element i aktualizuje jego wygląd
            });
        }
    }

    public void RemoveSaveFile()
    {
        CustomDropdown dropdown = _savesScrollViewContent.GetComponent<CustomDropdown>();
        if(dropdown == null || dropdown.SelectedButton == null)
        {
            Debug.Log($"<color=red>Aby usunąć zapis musisz wybrać plik z listy.</color>");
            return;
        }

        string saveName = dropdown.SelectedButton.GetComponentInChildren<TextMeshProUGUI>().text;

        string saveFolderPath = Path.Combine(Application.persistentDataPath, saveName);

        // Usunięcie folderu zapisu
        if (Directory.Exists(saveFolderPath))
        {
            Directory.Delete(saveFolderPath, true); // Drugi argument 'true' pozwala na usunięcie niepustych folderów
            Debug.Log($"Plik '{saveName}' został usunięty.");
        }
        else
        {
            Debug.LogWarning($"Plik '{saveName}' nie istnieje.");
            return;
        }

        // Usunięcie przycisku z UI
        int indexToRemove = dropdown.Buttons.IndexOf(dropdown.SelectedButton);

        Destroy(dropdown.Buttons[indexToRemove].gameObject);
        dropdown.Buttons.RemoveAt(indexToRemove);
        
        // Aktualizuje SelectedIndex i zaznaczenie
        dropdown.SelectedIndex = 0;
        dropdown.SelectedButton = null;
        dropdown.InitializeButtons();
    }
    #endregion
}

