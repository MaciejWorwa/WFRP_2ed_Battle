using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;
using System.Linq;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Misc;


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
    [SerializeField] private TMP_Dropdown _weaponQualityDropdown; // Lista jakości broni
    [SerializeField] private Transform _unitScrollViewContent; // Lista wszystkich dostępnych ras (jednostek)

    public List<string> TokensPaths = new List<string>();

    #region Loading units stats
    public void LoadAndUpdateStats(GameObject unitObject = null)
    {
        // Ładowanie danych JSON
        TextAsset jsonFile = Resources.Load<TextAsset>("units");
        if (jsonFile == null)
        {
            Debug.LogError("Nie znaleziono pliku JSON.");
            return;
        }

        // Deserializacja danych JSON
        StatsData[] statsArray = JsonHelper.FromJson<StatsData>(jsonFile.text);
        if (statsArray == null)
        {
            Debug.LogError("Deserializacja JSON nie powiodła się. Sprawdź strukturę JSON.");
            return;
        }

        //Jeśli w argumencie została przekazana jakaś jednostka to pobieramy jej statystyki, które później będziemy aktualizować
        Unit unit = null;
        Stats statsToUpdate = null;
        if (unitObject != null)
        {
            unit = unitObject.GetComponent<Unit>();
            //Odniesienie do statystyk postaci
            statsToUpdate = unitObject.GetComponent<Stats>();
        }

        foreach (var stats in statsArray)
        {
            //Aktualizuje statystyki jednostki, o ile jakaś jednostka jest wybrana
            if (statsToUpdate != null)
            {
                if (stats.Id == statsToUpdate.Id)
                {
                    // Używanie refleksji do aktualizacji wartości wszystkich pól w klasie Stats
                    FieldInfo[] fields = typeof(StatsData).GetFields(BindingFlags.Instance | BindingFlags.Public);
                    foreach (var field in fields)
                    {
                        var targetField = typeof(Stats).GetField(field.Name, BindingFlags.Instance | BindingFlags.Public);
                        if (targetField != null)
                        {
                            targetField.SetValue(statsToUpdate, field.GetValue(stats));
                        }
                    }

                    // Aktualizuje wyświetlaną nazwę postaci i jej punkty żywotności, jeśli ta postać jest aktualizowana, a nie tworzona po raz pierwszy
                    if (unit.Stats != null)
                    {
                        unit.Stats.TempHealth = unit.Stats.MaxHealth;
                        unit.Stats.TempSz = unit.Stats.Sz;
                        unit.DisplayUnitName();
                        unit.DisplayUnitHealthPoints();
                    }
                }
            }

            bool buttonExists = _unitScrollViewContent.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == stats.Race);

            if (buttonExists == false)
            {
                //Dodaje jednostkę do ScrollViewContent w postaci buttona
                GameObject buttonObj = Instantiate(_buttonPrefab, _unitScrollViewContent);
                TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                //Ustala text buttona
                buttonText.text = stats.Race;

                UnityEngine.UI.Button button = buttonObj.GetComponent<UnityEngine.UI.Button>();

                //Dodaje opcję do CustomDropdowna ze wszystkimi jednostkami
                _unitScrollViewContent.GetComponent<CustomDropdown>().Buttons.Add(button);

                int currentIndex = _unitScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count; // Pobiera indeks nowego przycisku

                // Zdarzenie po kliknięciu na konkretny item z listy
                button.onClick.AddListener(() =>
                {
                    _unitScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(currentIndex); // Wybiera element i aktualizuje jego wygląd
                });
            }
        }
    }
    #endregion

    #region Loading weapons stats
    public void LoadAndUpdateWeapons()
    {
        // Ładowanie danych JSON
        TextAsset jsonFile = Resources.Load<TextAsset>("weapons");
        if (jsonFile == null)
        {
            Debug.LogError("Nie znaleziono pliku JSON.");
            return;
        }

        // Deserializacja danych JSON
        WeaponData[] weaponsArray = JsonHelper.FromJson<WeaponData>(jsonFile.text);
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
            //Ustala jakość wykonania broni
            weapon.Quality = _weaponQualityDropdown.options[_weaponQualityDropdown.value].text;

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
    public string Tag;
    public string TokenFilePath;
    public float[] position;

    public bool IsSelected;
    public bool IsRunning; // Biegnie
    public bool IsCharging; // Szarżuje
    public bool IsHelpless; // Jest bezbronny
    public bool IsStunned; // Jest ogłuszony
    public bool IsTrapped; // Jest unieruchomiony
    public int AimingBonus;
    public int DefensiveBonus;
    public int GuardedAttackBonus; //Modyfikator do uników i parowania za ostrożny atak
    public bool CanAttack = true;
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
    public string Name;
    public string Race;
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
    public bool Frightening; // Straszny
    public bool LightningParry; // Błyskawiczny blok
    public bool MasterGunner; // Artylerzysta
    public bool MightyShot; // Strzał precyzyjny
    public bool PowerfulBlow; // Potężny cios (parowanie -30)
    public bool RapidReload; // Błyskawiczne przeładowanie
    public bool StreetFighting; // Bijatyka
    public bool StrikeMightyBlow; // Silny cios
    public bool SureShot; // Strzał mierzony
    public bool Terryfying; // Przerażający
    public bool QuickDraw; // Szybkie wyciągnięcie
    public int Dodge;

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
    public float AttackRange;
    public int S;
    public int ReloadTime;
    public int ReloadLeft;
    public bool Defensive; // parujący
    public bool Fast; // szybki
    public bool Impact; // druzgoczący
    public bool Slow; // powolny
    public bool Tiring; // ciężki
    public bool ArmourPiercing; // przebijający zbroje

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
public class InventoryData
{
    public List<int> AllWeaponsId = new List<int>(); // Lista identyfikatorów wszystkich posiadanych broni
    public int[] EquippedWeaponsId = new int[2]; // Tablica identyfikatorów broni trzymanych w rękach

    public InventoryData(Inventory inventory)
    {
        // Dodaj identyfikatory wszystkich broni do listy AllWeaponIds
        foreach (var weapon in inventory.AllWeapons)
        {
            AllWeaponsId.Add(weapon.Id);
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
#endregion

