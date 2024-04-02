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
using UnityEngine.UIElements;
using static UnityEngine.UI.CanvasScaler;

public class SaveAndLoadManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowuj¹ce instancjê
    private static SaveAndLoadManager instance;

    // Publiczny dostêp do instancji
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
            // Jeœli instancja ju¿ istnieje, a próbujemy utworzyæ kolejn¹, niszczymy nadmiarow¹
            Destroy(gameObject);
        }
    }

    [SerializeField] private TMP_InputField _saveNameInput;
    [SerializeField] private CustomDropdown _savesDropdown;

    public bool IsLoading;

    public void SaveAllUnits()
    {

        if (_saveNameInput.text.Length < 1)
        {
            Debug.Log($"<color=red>Zapis nieudany. Niepoprawna nazwa pliku.</color>");
            return;
        }

        List<Unit> allUnits = UnitsManager.Instance.AllUnits;

        if (allUnits.Count < 1)
        {
            Debug.Log($"<color=red>Zapis nieudany. Aby zapisaæ grê, musisz stworzyæ chocia¿ jedn¹ postaæ.</color>");
            return;
        }

        SaveUnits(allUnits);

        Debug.Log($"<color=green>Zapisano stan gry.</color>");
    }

    private void SaveUnits(List<Unit> allUnits)
    {
        //BinaryFormatter formatter = new BinaryFormatter(); // ZAKOMENTOWANY KOD TO SPOSÓB SZYFROWANIA DANYCH. W PRZYPADKU KORZYSTANIA Z NIEGO ZMIENIC FORMAT PLIKÓW Z .JSON NA .FUN

        // Utworzenie listy nazw wszystkich jednostek
        List<string> unitNames = new List<string>();
        foreach (var unit in allUnits)
        {
            unitNames.Add(unit.GetComponent<Stats>().Name);
        }

        string savesFolderName;

        // Stworzenie folderu dla zapisów
        savesFolderName = _saveNameInput.text;

        Directory.CreateDirectory(Application.persistentDataPath + "/" + savesFolderName);

        // Pobranie listy zapisanych plików
        string[] files = Directory.GetFiles(Application.persistentDataPath + "/" + savesFolderName, "*.json");

        // Sprawdzenie, czy w liœcie znajduj¹ siê pliki, których nazwa nie pasuje do nazw postaci w 'unitNames' i usuniêcie ich
        foreach (string file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);

            if (!unitNames.Contains(fileName))
            {
                File.Delete(file);
            }
        }

        //Zapis statystyk wszystkich postaci
        foreach (var unit in allUnits)
        {
            string unitName = unit.GetComponent<Stats>().Name;

            string unitPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_unit.json");
            string statsPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_stats.json");
            string weaponPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_weapon.json");
            string inventoryPath = Path.Combine(Application.persistentDataPath, savesFolderName, unitName + "_inventory.json");

            //FileStream stream = new FileStream(path, FileMode.Create);

            UnitData unitData = new UnitData(unit);
            StatsData statsData = new StatsData(unit.GetComponent<Stats>());
            WeaponData weaponData = new WeaponData(unit.GetComponent<Weapon>());
            InventoryData inventoryData = new InventoryData(unit.GetComponent<Inventory>());

            // Serializacja danych do JSON
            string unitJsonData = JsonUtility.ToJson(unitData, true);
            string statsJsonData = JsonUtility.ToJson(statsData, true);
            string weaponJsonData = JsonUtility.ToJson(weaponData, true);
            string inventoryJsonData = JsonUtility.ToJson(inventoryData, true);

            // Zapisanie danych do pliku
            File.WriteAllText(unitPath, unitJsonData);
            File.WriteAllText(statsPath, statsJsonData);
            File.WriteAllText(weaponPath, weaponJsonData);
            File.WriteAllText(inventoryPath, inventoryJsonData);

            //// Serializacja weaponData
            //formatter.Serialize(stream, weaponData);

            //stream.Close();
        }
    }

    public void LoadAllUnits()
    {
        if (_saveNameInput.text.Length < 1) return;

        IsLoading = true;

        //Odznaczenie zaznaczonej postaci
        if (Unit.SelectedUnit != null)
        {
            Unit.SelectedUnit.GetComponent<Unit>().SelectUnit();
        }

        // Kopiuje listê jednostek do nowej listy, aby móc bezpiecznie modyfikowaæ oryginaln¹ listê
        List<Unit> unitsToRemove = new List<Unit>(UnitsManager.Instance.AllUnits);

        // Usuwa wszystkie obecne na polu bitwy jednostki
        foreach (var unit in unitsToRemove)
        {
            if(unit != null)
            {
                UnitsManager.Instance.DestroyUnit(unit.gameObject);
            }
        }

        StartCoroutine(LoadAllUnitsWithDelay(_saveNameInput.text));
    }

    private IEnumerator LoadAllUnitsWithDelay(string saveName)
    {
        string saveFolder = Path.Combine(Application.persistentDataPath, saveName);
        var statsFiles = Directory.GetFiles(saveFolder, "*_stats.json");
        foreach (string statsFile in statsFiles)
        {
            string baseFileName = Path.GetFileNameWithoutExtension(statsFile).Replace("_stats", "");

            // Wczytanie i deserializacja StatsData
            StatsData statsData = JsonUtility.FromJson<StatsData>(File.ReadAllText(statsFile));

            // Wczytanie i deserializacja UnitData
            string unitFilePath = Path.Combine(saveFolder, baseFileName + "_unit.json");
            UnitData unitData = JsonUtility.FromJson<UnitData>(File.ReadAllText(unitFilePath));

            //Ustalenie pozycji jednostki
            Vector3 position = new Vector3(unitData.position[0], unitData.position[1], unitData.position[2]);

            GameObject unitGameObject = UnitsManager.Instance.CreateUnit(statsData.Id, baseFileName, position);

            yield return new WaitForSeconds(0.1f); // Oczekiwanie na zainicjowanie komponentów

            // Kontynuacja wczytywania i aktualizacji pozosta³ych danych jednostki
            LoadComponentDataWithReflection<StatsData, Stats>(unitGameObject, Path.Combine(saveFolder, baseFileName + "_stats.json"));
            LoadComponentDataWithReflection<UnitData, Unit>(unitGameObject, Path.Combine(saveFolder, baseFileName + "_unit.json"));
            LoadComponentDataWithReflection<WeaponData, Weapon>(unitGameObject, Path.Combine(saveFolder, baseFileName + "_weapon.json"));
            //LoadComponentDataWithReflection<InventoryData, Inventory>(unitGameObject, Path.Combine(saveFolder, baseFileName + "_inventory.json"));
        }

        IsLoading = false;
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

        // Uzyskanie dostêpu do pól w komponencie i aktualizacja ich wartoœci
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
    }


}

