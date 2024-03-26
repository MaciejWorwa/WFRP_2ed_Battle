using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;
using System.Linq;

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
    [SerializeField] private TMP_Dropdown _unitsDropdown;
    [SerializeField] private GameObject _weaponButtonPrefab; // Przycisk odpowiadający każdej z broni
    [SerializeField] private Transform _weaponScrollViewContent; // Lista wszystkich dostępnych broni


    #region Loading units stats
    public void LoadAndUpdateStats(GameObject unit)
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

        //Odniesienie do statystyk postaci
        Stats statsToUpdate = unit.GetComponent<Stats>();

        if (statsToUpdate == null)
        {
            Debug.LogError("Aby wczytać statystyki musisz wybrać jednostkę.");
            return;
        }

        //Czyści listę dostępnych do wyboru jednostek
        _unitsDropdown.options.Clear();

        foreach (var stats in statsArray)
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
                if(unit.GetComponent<Unit>().Stats != null)
                {
                    unit.GetComponent<Unit>().DisplayUnitName();
                    unit.GetComponent<Unit>().DisplayUnitHealthPoints();
                }
            }

            //Dodaje jednostkę do dropdowna
            _unitsDropdown.options.Add(new TMP_Dropdown.OptionData(stats.Race));
        }

        //Odświeża wyświetlaną wartość dropdowna
        _unitsDropdown.RefreshShownValue();
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
                GameObject buttonObj = Instantiate(_weaponButtonPrefab, _weaponScrollViewContent);
                TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                //Ustala text buttona
                buttonText.text = weapon.Name;

                Button button = buttonObj.GetComponent<Button>();
                
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
}
#endregion

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

#region Odzwierciedlenia klas Stats i Weapon
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
    public int PP;
    public int PS;
    public int Armor_head;
    public int Armor_arms;
    public int Armor_torso;
    public int Armor_legs;
    public int Initiative;
    public bool LightningParry; // Błyskawiczny blok
    public bool MasterGunner; // Artylerzysta
    public bool MightyShot; // Strzał precyzyjny
    public bool RapidReload; // Błyskawiczne przeładowanie
    public bool StreetFighting; // Bijatyka
    public bool StrikeMightyBlow; // Silny cios
    public bool SureShot; // Strzał mierzony
    public bool QuickDraw; // Szybkie wyciągnięcie
}

[System.Serializable]
public class WeaponData
{
    public int Id;
    public string Name;
    public string[] Type;
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
}
#endregion

