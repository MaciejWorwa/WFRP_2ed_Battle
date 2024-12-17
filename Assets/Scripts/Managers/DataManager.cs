using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;
using System.Linq;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using Unity.VisualScripting;
using System;
using System.IO;

public class DataManager : MonoBehaviour
{ 
    // Prywatne statyczne pole przechowujące instancję
    private static DataManager instance;

    // Publiczny dostęp do instancji
    public static DataManager Instance
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

    [SerializeField] private GameObject _buttonPrefab; // Przycisk odpowiadający każdej z broni
    [SerializeField] private Transform _weaponScrollViewContent; // Lista wszystkich dostępnych broni
    [SerializeField] private Transform _spellbookScrollViewContent; // Lista wszystkich dostępnych zaklęć
    [SerializeField] private TMP_Dropdown _spellLoresDropdown; // Lista tradycji magii potrzebna do sortowania listy zaklęć
    [SerializeField] private TMP_Dropdown _weaponQualityDropdown; // Lista jakości broni
    [SerializeField] private Transform _unitScrollViewContent; // Lista wszystkich dostępnych ras (jednostek)

    public List<string> TokensPaths = new List<string>();

    #region Loading units stats
    public void ChangeUnitListByToggle()
    {
        foreach (Transform child in _unitScrollViewContent)
        {
            child.gameObject.SetActive(false);
        }

        _unitScrollViewContent.GetComponent<CustomDropdown>().ClearButtons();

        LoadAndUpdateStats();
    }
    public void LoadAndUpdateStats(GameObject unitObject = null)
    {
        Stats statsToUpdate = null;
        Unit unit = null;

        if (unitObject != null)
        {
            statsToUpdate = unitObject.GetComponent<Stats>();
            unit = unitObject.GetComponent<Unit>();
        }

        if (UnitsManager.Instance.IsSavedUnitsManaging) //Obsługa listy zapisanych jednostek
        {
            string savedUnitsFolder = Path.Combine(Application.persistentDataPath, "savedUnitsList");

            if (!Directory.Exists(savedUnitsFolder))
            {
                return;
            }

            string[] statsFiles = Directory.GetFiles(savedUnitsFolder, "*_stats.json");
            foreach (string statsFile in statsFiles)
            {
                string jsonContent = File.ReadAllText(statsFile);
                StatsData statsData = JsonUtility.FromJson<StatsData>(jsonContent);

                if (statsData == null)
                {
                    Debug.LogError($"Nie udało się załadować statystyk z pliku {statsFile}.");
                    continue;
                }

                UpdateUnitStatsAndButtons(statsData, statsToUpdate, unit);
            }
        }
        else // Obsługa standardowej listy jednostek
        {
            TextAsset jsonFile = Resources.Load<TextAsset>("units");

            if (jsonFile == null)
            {
                Debug.LogError("Nie znaleziono pliku JSON units.");
                return;
            }

            StatsData[] statsArray = JsonHelper.FromJson<StatsData>(jsonFile.text);
            if (statsArray == null || statsArray.Length == 0)
            {
                Debug.LogError("Deserializacja JSON nie powiodła się lub lista jest pusta.");
                return;
            }

            foreach (var stats in statsArray)
            {
                UpdateUnitStatsAndButtons(stats, statsToUpdate, unit);
            }
        }
    }

    private void UpdateUnitStatsAndButtons(StatsData statsData, Stats statsToUpdate, Unit unit)
    {
        if (statsToUpdate != null && ((statsData.Id == statsToUpdate.Id && UnitsManager.Instance.IsSavedUnitsManaging == false) || (UnitsManager.Instance.IsSavedUnitsManaging == true && statsData.Name == statsToUpdate.Name)))
        {
            FieldInfo[] fields = typeof(StatsData).GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                if(field.Name == "Name") continue; //NIE WIEM, CZY TO MA SENS
                var targetField = typeof(Stats).GetField(field.Name, BindingFlags.Instance | BindingFlags.Public);
                targetField?.SetValue(statsToUpdate, field.GetValue(statsData));
            }
        }

        bool buttonExists;

        if(UnitsManager.Instance.IsSavedUnitsManaging)
        {
            buttonExists = _unitScrollViewContent.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == statsData.Name);
        }
        else
        {
            buttonExists = _unitScrollViewContent.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == statsData.Race);
        }
    
        if (!buttonExists)
        {
            GameObject buttonObj = Instantiate(_buttonPrefab, _unitScrollViewContent);
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

            if(UnitsManager.Instance.IsSavedUnitsManaging)
            {
                buttonText.text = statsData.Name;
            }
            else
            {
                buttonText.text = statsData.Race;
            }

            buttonObj.AddComponent<CreateUnitButton>();

            UnityEngine.UI.Button button = buttonObj.GetComponent<UnityEngine.UI.Button>();
            _unitScrollViewContent.GetComponent<CustomDropdown>().Buttons.Add(button);

            int currentIndex = _unitScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count;
            button.onClick.AddListener(() =>
            {
                _unitScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(currentIndex);
            });
        }
    }

    public void DeleteSavedUnit()
    {
        // Pobierz wybrany indeks z CustomDropdown
        int selectedIndex = _unitScrollViewContent.GetComponent<CustomDropdown>().GetSelectedIndex();

        // Sprawdzenie, czy indeks jest prawidłowy
        if (selectedIndex < 1 || selectedIndex > _unitScrollViewContent.childCount)
        {
            Debug.LogError("Nieprawidłowy indeks przycisku.");
            return;
        }

        // Korekta indeksu, ponieważ w CustomDropdown indeksy zaczynają się od 1
        int adjustedIndex = selectedIndex - 1;

        // Pobierz Transform i Button dla wybranego przycisku
        Transform buttonTransform = _unitScrollViewContent.GetChild(adjustedIndex);
        UnityEngine.UI.Button buttonToDelete = buttonTransform.GetComponent<UnityEngine.UI.Button>();
        if (buttonToDelete == null)
        {
            Debug.LogError("Nie znaleziono przycisku w podanym indeksie.");
            return;
        }

        // Pobierz nazwę przycisku (tekst) do zidentyfikowania plików w Resources
        string buttonName = buttonToDelete.GetComponentInChildren<TextMeshProUGUI>().text;
        if (string.IsNullOrEmpty(buttonName))
        {
            Debug.LogError("Nie można znaleźć nazwy przycisku.");
            return;
        }

        // Ścieżka do katalogu Resources/savedUnits
        string savedUnitsFolder = Path.Combine(Application.persistentDataPath, "savedUnitsList");

        // Usuwanie plików powiązanych z przyciskiem
        string[] relatedFiles = {
            Path.Combine(savedUnitsFolder, buttonName + "_unit.json"),
            Path.Combine(savedUnitsFolder, buttonName + "_stats.json"),
            Path.Combine(savedUnitsFolder, buttonName + "_weapon.json"),
            Path.Combine(savedUnitsFolder, buttonName + "_inventory.json"),
            Path.Combine(savedUnitsFolder, buttonName + "_token.json")
        };

        foreach (string filePath in relatedFiles)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        // Usuwa przycisk z listy CustomDropdown i widoku
        CustomDropdown dropdown = _unitScrollViewContent.GetComponent<CustomDropdown>();

        // Usunięcie przycisku z UI
        int indexToRemove = dropdown.Buttons.IndexOf(dropdown.SelectedButton);

        Destroy(dropdown.Buttons[indexToRemove].gameObject);
        dropdown.Buttons.RemoveAt(indexToRemove);
        
        // Aktualizuje SelectedIndex i zaznaczenie
        dropdown.SelectedIndex = 0;
        dropdown.SelectedButton = null;
        dropdown.InitializeButtons();

        UnitsManager.IsTileSelecting = false;

        Debug.Log($"Jednostka '{buttonName}' została usunięta z listy zapisanych jednostek.");
    }
    #endregion

    #region Loading weapons stats
    public void LoadAndUpdateWeapons(WeaponData weaponData = null)
    {
        // Ładowanie danych JSON
        TextAsset jsonFile = Resources.Load<TextAsset>("weapons");
        if (jsonFile == null)
        {
            Debug.LogError("Nie znaleziono pliku JSON.");
            return;
        }

        // Deserializacja danych JSON
        WeaponData[] weaponsArray = null;
        if(weaponData == null)
        {
            weaponsArray = JsonHelper.FromJson<WeaponData>(jsonFile.text);
        }
        else
        {
            weaponsArray = new WeaponData[] { weaponData };
        }

        if (weaponsArray == null)
        {
            Debug.LogError("Deserializacja JSON nie powiodła się. Sprawdź strukturę JSON.");
            return;
        }

        //Odniesienie do broni postaci
        Weapon weaponToUpdate = null;
        if(Unit.SelectedUnit != null)
        {
            weaponToUpdate = Unit.SelectedUnit.GetComponent<Weapon>();
        }

        foreach (var weapon in weaponsArray)
        {
            if(_weaponQualityDropdown.transform.parent.gameObject.activeSelf) //Sprawdza, czy jest otwarte okno wyboru broni. W innym wypadku oznacza to, że bronie są wczytywane z pliku i nie chcemy zmieniać ich jakości
            {
                //Ustala jakość wykonania broni
                weapon.Quality = _weaponQualityDropdown.options[_weaponQualityDropdown.value].text;
            }

            if (weaponToUpdate != null && weapon.Id == weaponToUpdate.Id)
            {
                // Używanie refleksji do aktualizacji wartości wszystkich pól w klasie Weapon
                FieldInfo[] fields = typeof(WeaponData).GetFields(BindingFlags.Instance | BindingFlags.Public);
                foreach (var field in fields)
                {
                    var targetField = typeof(Weapon).GetField(field.Name, BindingFlags.Instance | BindingFlags.Public);
                    if (targetField != null)
                    {
                        //Zapobiega zresetowaniu się czasu przeładowania przy zmianach broni. Gdy postać wcześniej posiadała/używała daną broń to jej ReloadLeft zostaje zapamiętany
                        if(weaponToUpdate.WeaponsWithReloadLeft.ContainsKey(weapon.Id) && field.Name == "ReloadLeft")
                        {
                            weaponToUpdate.ReloadLeft = weaponToUpdate.WeaponsWithReloadLeft[weapon.Id];
                            continue;
                        }

                        targetField.SetValue(weaponToUpdate, field.GetValue(weapon));             
                    }
                }

                //Dodaje przedmiot do ekwipunku postaci
                InventoryManager.Instance.AddWeaponToInventory(weapon, Unit.SelectedUnit);

                //Dodaje Id broni do słownika ekwipunku postaci
                if(weaponToUpdate.WeaponsWithReloadLeft.ContainsKey(weapon.Id) == false)
                {
                    weaponToUpdate.WeaponsWithReloadLeft.Add(weapon.Id, 0);
                }
            }

            bool buttonExists = _weaponScrollViewContent.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == weapon.Name);

            if(buttonExists == false)
            {
                //Dodaje broń do ScrollViewContent w postaci buttona
                GameObject buttonObj = Instantiate(_buttonPrefab, _weaponScrollViewContent);
                TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                //Ustala text buttona
                buttonText.text = weapon.Name;

                UnityEngine.UI.Button button = buttonObj.GetComponent<UnityEngine.UI.Button>();
                
                //Dodaje opcję do CustomDropdowna ze wszystkimi brońmi
                _weaponScrollViewContent.GetComponent<CustomDropdown>().Buttons.Add(button);

                int currentIndex = _weaponScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count; // Pobiera indeks nowego przycisku

                // Zdarzenie po kliknięciu na konkretny item z listy
                button.onClick.AddListener(() =>
                {
                    _weaponScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(currentIndex); // Wybiera element i aktualizuje jego wygląd
                });
            }
        }
    }
    #endregion

    #region Loading spells
    public void LoadAndUpdateSpells(string spellName = null)
    {
        // Ładowanie danych JSON
        TextAsset jsonFile = Resources.Load<TextAsset>("spells");
        if (jsonFile == null)
        {
            Debug.LogError("Nie znaleziono pliku JSON.");
            return;
        }

        // Deserializacja danych JSON
        List <SpellData> spellsList = null;
        spellsList = JsonHelper.FromJson<SpellData>(jsonFile.text).ToList();

        if (spellsList == null)
        {
            Debug.LogError("Deserializacja JSON nie powiodła się. Sprawdź strukturę JSON.");
            return;
        }

        //Odniesienie do klasy spell postaci
        Spell spellToUpdate = null;
        if(Unit.SelectedUnit != null && Unit.SelectedUnit.GetComponent<Spell>() != null && !string.IsNullOrEmpty(spellName))
        {
            spellToUpdate = Unit.SelectedUnit.GetComponent<Spell>();
        }

        //Filtrowanie listy zaklęć wg wybranej tradycji
        string selectedLore = _spellLoresDropdown.options[_spellLoresDropdown.value].text;

        // Czyści obecną listę
        _spellbookScrollViewContent.GetComponent<CustomDropdown>().ClearButtons();

        foreach (var spell in spellsList)
        {
            //Filtrowanie listy zaklęć wg wybranej tradycji
            if (spell.Lore != selectedLore && selectedLore != "Wszystkie zaklęcia") continue;

            //Dodaje zaklęcie do ScrollViewContent w postaci buttona
            GameObject buttonObj = Instantiate(_buttonPrefab, _spellbookScrollViewContent);
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            //Ustala text buttona
            buttonText.text = spell.Name;

            UnityEngine.UI.Button button = buttonObj.GetComponent<UnityEngine.UI.Button>();

            CustomDropdown spellbookDropdown = _spellbookScrollViewContent.GetComponent<CustomDropdown>();

            //Dodaje opcję do CustomDropdowna ze wszystkimi zaklęciami
            spellbookDropdown.Buttons.Add(button);

            // Wyświetla przy zaklęciu wymagany poziom mocy
            DisplayCastingNumberInfo(button, spell.CastingNumber);

            int currentIndex = _spellbookScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count; // Pobiera indeks nowego przycisku

            // Zdarzenie po kliknięciu na konkretny item z listy
            button.onClick.AddListener(() =>
            {
                _spellbookScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(currentIndex); // Wybiera element i aktualizuje jego wygląd
            });

            if (spellToUpdate != null && spell.Name == spellName && spellToUpdate.Name != spell.Name)
            {
                // Używanie refleksji do aktualizacji wartości wszystkich pól w klasie Spell
                FieldInfo[] fields = typeof(SpellData).GetFields(BindingFlags.Instance | BindingFlags.Public);
                foreach (var field in fields)
                {
                    var targetField = typeof(Spell).GetField(field.Name, BindingFlags.Instance | BindingFlags.Public);
                    if (targetField != null)
                    {
                        targetField.SetValue(spellToUpdate, field.GetValue(spell));
                    }
                }

                spellToUpdate.CastingTimeLeft = spell.CastingTime;
            }
        }
    }

    public void DisplayCastingNumberInfo(UnityEngine.UI.Button button, int castingNumber)
    {
        button.transform.Find("castingNumber_text").gameObject.SetActive(true);

        string castingNumberText = castingNumber.ToString();

        button.transform.Find("castingNumber_text").GetComponent<TMP_Text>().text = castingNumberText;
    }
    #endregion
}

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);

        // Sprawdzenie, które dane zostały wczytane i zwrócenie odpowiedniej tablicy
        if (wrapper.Units != null)
        {
            return wrapper.Units;
        }
        else if (wrapper.Weapons != null)
        {
            return wrapper.Weapons;
        }
        else if (wrapper.Spells != null)
        {
            return wrapper.Spells;
        }
        else
        {
            Debug.LogError("Deserializacja JSON nie powiodła się. Sprawdź składnię i strukturę JSON.");
            return null;
        }    
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] Units;
        public T[] Weapons;
        public T[] Spells;
    }
}

#region Data classes
[System.Serializable]
public class TokenData
{
    public string filePath;
}
[System.Serializable]
public class UnitData
{
    public int UnitId; // Unikalny Id jednostki

    public string Tag;
    public string TokenFilePath;
    public float[] position;

    public bool IsSelected;
    public bool IsTurnFinished; 
    public bool IsRunning; // Biegnie
    public bool IsCharging; // Szarżuje
    public int HelplessDuration; // Czas stanu bezbronności (podany w rundach). Wartość 0 oznacza, że postać nie jest bezbronna
    public bool IsScared; // Jest przestraszony
    public bool IsFearTestPassed; // Zdał test strachu
    public int SpellDuration; // Czas trwania zaklęcia mającego wpływ na tą jednostkę
    public int StunDuration; // Czas ogłuszenia (podany w rundach). Wartość 0 oznacza, że postać nie jest ogłuszona
    public bool Trapped; // Unieruchomiony
    public int TrappedUnitId; // Cel unieruchomienia
    public int AimingBonus;
    public int CastingNumberBonus;
    public int DefensiveBonus;
    public int GuardedAttackBonus; //Modyfikator do uników i parowania za ostrożny atak
    public bool CanAttack = true;
    public bool CanCastSpell = false;
    public bool Feinted = false; // Określa, czy postać wykonała w poprzedniej akcji udaną fintę
    public bool CanParry = true;
    public bool CanDodge = false;

    public UnitData(Unit unit)
    {
        // Pobiera wszystkie pola (zmienne) z klasy Stats
        var fields = unit.GetType().GetFields();
        var thisFields = this.GetType().GetFields();

        // Dla każdego pola z klasy stats odnajduje pole w klasie this (czyli UnitData) i ustawia mu wartość jego odpowiednika z klasy Unit
        foreach (var thisField in thisFields)
        {
            var field = fields.FirstOrDefault(f => f.Name == thisField.Name); // Znajduje pierwsze pole o tej samej nazwie wśród pol z klasy Unit

            if (field != null && field.GetValue(unit) != null)
            {
                thisField.SetValue(this, field.GetValue(unit));
            }
        }

        Tag = unit.gameObject.tag;

        position = new float[3];
        position[0] = unit.gameObject.transform.position.x;
        position[1] = unit.gameObject.transform.position.y;
        position[2] = unit.gameObject.transform.position.z;
    }
}

[System.Serializable]
public class StatsData
{
    public int Id;
    public bool IsBig;
    public string Name;
    public string Race;
    public int PrimaryWeaponId;
    public int WW;
    public int US;
    public int K;
    public int Odp;
    public int Zr;
    public int Int;
    public int SW;
    public int Ogd;
    public int A;
    public int S;
    public int Wt;
    public int Sz;
    public int TempSz;
    public int Mag;
    public int MaxHealth;
    public int TempHealth;
    public int PO;
    public int PP;
    public int PS;
    public int Armor_head;
    public int Armor_arms;
    public int Armor_torso;
    public int Armor_legs;
    public int Initiative;
    public bool Ambidextrous; // Oburęczność
    public bool Disarm; // Rozbrojenie
    public bool Ethereal; // Eteryczny
    public bool FastHands; //Dotyk mocy
    public bool Fearless; // Nieustraszony
    public bool Frightening; // Straszny (test Fear)
    public bool LightningParry; // Błyskawiczny blok
    public bool MagicSense; //Zmysł magii
    public bool MasterGunner; // Artylerzysta
    public bool MightyShot; // Strzał precyzyjny
    public bool MightyMissile; // Morderczy pocisk
    public bool PowerfulBlow; // Potężny cios (parowanie -30)
    public bool RapidReload; // Błyskawiczne przeładowanie
    public bool Sharpshooter; // Strzał przebijający
    public bool StoutHearted; // Odwaga
    public bool StreetFighting; // Bijatyka
    public bool StrikeMightyBlow; // Silny cios
    public bool StrikeToStun; // Ogłuszanie
    public bool Sturdy; // Krzepki
    public bool SureShot; // Strzał przebijający
    public bool Terryfying; // Przerażający (test Terror)
    public bool QuickDraw; // Szybkie wyciągnięcie
    public int Channeling; // Splatanie magii
    public int Dodge; // Unik

    public StatsData(Stats stats)
    {
        // Pobiera wszystkie pola (zmienne) z klasy Stats
        var fields = stats.GetType().GetFields();
        var thisFields = this.GetType().GetFields();

        // Dla każdego pola z klasy stats odnajduje pole w klasie this (czyli StatsData) i ustawia mu wartość jego odpowiednika z klasy stats
        foreach (var thisField in thisFields)
        {
            var field = fields.FirstOrDefault(f => f.Name == thisField.Name); // Znajduje pierwsze pole o tej samej nazwie wśród pol z klasy Stats

            if (field != null && field.GetValue(stats) != null)
            {
                thisField.SetValue(this, field.GetValue(stats));
            }
        }
    }
}

[System.Serializable]
public class WeaponData
{
    public int Id;
    public string Name;
    public string[] Type;
    public string Quality;
    public bool TwoHanded;
    public bool NaturalWeapon;
    public float AttackRange;
    public int S;
    public int ReloadTime;
    public int ReloadLeft;
    public bool ArmourIgnoring; // ignorujący zbroje
    public bool ArmourPiercing; // przebijający zbroje
    public bool Balanced; // wyważony
    public bool Defensive; // parujący
    public bool Fast; // szybki
    public bool Impact; // druzgoczący
    public bool Pummelling; // ogłuszający
    public bool Slow; // powolny
    public bool Snare; // unieruchamiający
    public bool Tiring; // ciężki

    public WeaponData(Weapon weapons)
    {
        // Pobiera wszystkie pola (zmienne) z klasy Stats
        var fields = weapons.GetType().GetFields();
        var thisFields = this.GetType().GetFields();

        // Dla każdego pola z klasy stats odnajduje pole w klasie this (czyli WeaponData) i ustawia mu wartość jego odpowiednika z klasy Weapon
        foreach (var thisField in thisFields)
        {
            var field = fields.FirstOrDefault(f => f.Name == thisField.Name); // Znajduje pierwsze pole o tej samej nazwie wśród pol z klasy Weapon

            if (field != null && field.GetValue(weapons) != null)
            {
                thisField.SetValue(this, field.GetValue(weapons));
            }
        }
    }
}
[System.Serializable]
public class SpellData
{
    public int Id;
    public string Name;
    public string Lore;
    public string[] Type;
    public int CastingNumber; //poziom mocy
    public float Range;
    public int Strength;
    public int AreaSize;
    public int CastingTime;
    public int CastingTimeLeft;
    public int Duration;

    public bool SaveTestRequiring; // określa, czy zaklęcie powoduje konieczność wykonania testu obronnego
    public string[] Attribute; // określa cechę, jaka jest testowana podczas próby oparcia się zaklęciu lub cechę na którą wpływa zaklęcie (np. podnosi ją lub obniża). Czasami jest to więcej cech, np. Pancerz Etery wpływa na każdą z lokalizacji

    public bool ArmourIgnoring; // ignorujący zbroję
    public bool WtIgnoring; // ignorujący wytrzymałość
    public bool Stunning;  // ogłuszający
    public bool Paralyzing; // wprowadzający w stan bezbronności

    public SpellData(Spell spell)
    {
        // Pobiera wszystkie pola (zmienne) z klasy spell
        var fields = spell.GetType().GetFields();
        var thisFields = this.GetType().GetFields();

        // Dla każdego pola z klasy spell odnajduje pole w klasie this (czyli SpellData) i ustawia mu wartość jego odpowiednika z klasy spell
        foreach (var thisField in thisFields)
        {
            var field = fields.FirstOrDefault(f => f.Name == thisField.Name); // Znajduje pierwsze pole o tej samej nazwie wśród pól z klasy spell

            if (field != null && field.GetValue(spell) != null)
            {
                thisField.SetValue(this, field.GetValue(spell));
            }
        }
    }
}

[System.Serializable]
public class InventoryData
{
    public List<WeaponData> AllWeapons = new List<WeaponData>(); //Wszystkie posiadane przez postać przedmioty
    public int[] EquippedWeaponsId = new int[2]; // Tablica identyfikatorów broni trzymanych w rękach

    public InventoryData(Inventory inventory)
    {
        foreach (var weapon in inventory.AllWeapons)
        {
            WeaponData weaponData = new WeaponData(weapon);
            AllWeapons.Add(weaponData);
        }

        // Dodaj identyfikatory broni trzymanych w rękach do tablicy EquippedWeaponIds
        for (int i = 0; i < inventory.EquippedWeapons.Length; i++)
        {
            if (inventory.EquippedWeapons[i] != null)
            {
                EquippedWeaponsId[i] = inventory.EquippedWeapons[i].Id;
            }
        }
    }
}

[System.Serializable]
public class GridManagerData
{
    public int Width;
    public int Height;
    public string GridColor;

    public GridManagerData()
    {
        Width = GridManager.Width;
        Height = GridManager.Height;
        GridColor = GridManager.GridColor;
    }
}

[System.Serializable]
public class RoundsManagerData
{
    public int RoundNumber;
    public List<UnitNameAndActionsLeft> Entries = new List<UnitNameAndActionsLeft>();

    public RoundsManagerData(List<Unit> units)
    {
        RoundNumber = RoundsManager.RoundNumber;
    }
}

[System.Serializable]
public class UnitNameAndActionsLeft
{
    public string UnitName;
    public int ActionsLeft;
}

[System.Serializable]
public class MapElementsData
{
    public string Name;
    public string Tag;
    public bool IsHighObstacle;
    public bool IsLowObstacle;
    public bool IsCollider;
    public float[] position;
    public int rotationZ;

    public MapElementsData(MapElement mapElement)
    {
        Name = mapElement.gameObject.name.Replace("(Clone)", "");
        Tag = mapElement.gameObject.tag;
        IsHighObstacle= mapElement.IsHighObstacle;
        IsLowObstacle= mapElement.IsLowObstacle;
        IsCollider= mapElement.IsCollider;

        position = new float[3];
        position[0] = mapElement.gameObject.transform.position.x;
        position[1] = mapElement.gameObject.transform.position.y;
        position[2] = mapElement.gameObject.transform.position.z;
        rotationZ = (int)mapElement.gameObject.transform.eulerAngles.z;
    }
}

[System.Serializable]
public class TileCoverData
{
    public float[] Position; // Pozycja tile cover
    public int Number;

    public TileCoverData(Vector3 position, int number)
    {
        Position = new float[3];
        Position[0] = position.x;
        Position[1] = position.y;
        Position[2] = position.z;
        Number = number;
    }
}

[System.Serializable]
public class MapElementsContainer
{
    public List<MapElementsData> Elements = new List<MapElementsData>();
    public List<TileCoverData> TileCovers = new List<TileCoverData>();

    public string BackgroundImagePath;
    public float BackgroundPositionX;
    public float BackgroundPositionY;
    public float BackgroundScale;
    public float BackgroundColorR;
    public float BackgroundColorG;
    public float BackgroundColorB;
}

[System.Serializable]
public class GameSettings
{
    public bool IsAutoDiceRollingMode;
    public bool IsAutoDefenseMode;
    public bool IsAutoKillMode;
    public bool IsAutoSelectUnitMode;
    public bool IsFriendlyFire;
    public bool IsFearIncluded;
    public bool IsAutoCombatMode;
    public bool IsStatsHidingMode;
    public bool IsNamesHidingMode;
    public bool IsHealthPointsHidingMode;
    public bool IsAutosaveMode;
    public bool IsShowAnimationsMode;
    public float BackgroundColorR;
    public float BackgroundColorG;
    public float BackgroundColorB;
}

#endregion

