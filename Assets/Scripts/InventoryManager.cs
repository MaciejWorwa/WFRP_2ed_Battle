using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;
using System.Reflection;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static InventoryManager instance;

    // Publiczny dostęp do instancji
    public static InventoryManager Instance
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

    [SerializeField] private GameObject _weaponButtonPrefab; // Przycisk odpowiadający każdej z broni
    public Transform InventoryScrollViewContent; // Lista ekwipunku postaci
    [SerializeField] private CustomDropdown _weaponsDropdown;
    [SerializeField] private GameObject _inventoryPanel;

    void Start()
    {
        //Wczytuje listę wszystkich broni
        DataManager.Instance.LoadAndUpdateWeapons();
        ShowOrHidePanel(GameObject.Find("AddWeapons_Panel")); //TO JEST TYMCZASOWO. MUSZE ROZKMINIĆ TO LEPIEJ. PO PROSTU JAK TEN PANEL NA START JEST NIEAKTYWNY TO SIE ŹLE WCZYTUJĄ INDEKSY BRONI, BO NIE URUCHAMIA SIĘ FUNKCJA AWAKE W KOMPONENCIE CUSTOMDROPDOWN (JEST ON PODPIETY DO TEGO PANELU)
    }

    #region Inventory panel managing
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.I) && Unit.SelectedUnit != null)
        {
            ShowOrHidePanel(_inventoryPanel);
        }
    }

    public void ShowOrHidePanel(GameObject panel)
    {
        //Gdy panel jest zamknięty to go otwiera, a gdy otwarty to go zamyka
        panel.SetActive(!panel.activeSelf);
    }

    public void HideInventory()
    {
        _inventoryPanel.SetActive(false);
    }
    #endregion

    #region Add weapon from list to inventory
    public void LoadWeapons()
    {
        if (Unit.SelectedUnit != null)
        {
            // Ustalenie Id broni na podstawie wyboru z dropdowna
            Unit.SelectedUnit.GetComponent<Weapon>().Id = _weaponsDropdown.GetSelectedIndex();
        }
        else
        {
            Debug.Log("Aby dodać przedmiot do ekwipunku, musisz najpierw wybrać postać.");
        }

        //Wczytanie statystyk broni
        DataManager.Instance.LoadAndUpdateWeapons();
    }

    public void AddWeaponToInventory(WeaponData weaponData, GameObject unit)
    {
        //Pobiera komponent weapon z puli
        GameObject weaponObj = WeaponsPool.Instance.GetWeapon();
        Weapon newWeapon = weaponObj.GetComponent<Weapon>();

        // Używanie refleksji do aktualizacji wartości wszystkich pól utworzonej broni
        FieldInfo[] fields = typeof(WeaponData).GetFields(BindingFlags.Instance | BindingFlags.Public);
        foreach (var field in fields)
        {
            var targetField = typeof(Weapon).GetField(field.Name, BindingFlags.Instance | BindingFlags.Public);
            if (targetField != null)
            {
                targetField.SetValue(newWeapon, field.GetValue(weaponData));
            }
        }

        //Dodaje przedmiot do ekwipunku
        unit.GetComponent<Inventory>().AllWeapons.Add(newWeapon);

        UpdateInventoryDropdown(unit.GetComponent<Inventory>().AllWeapons);

        Debug.Log($"Przedmiot {newWeapon.Name} został dodany do ekwipunku {unit.GetComponent<Stats>().Name}.");
    }
    #endregion

    #region Removing weapon from inventory
    public void RemoveWeaponFromInventory()
    {
        if(Unit.SelectedUnit == null || InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count == 0) return;

        GameObject unit = Unit.SelectedUnit;
        int selectedIndex = InventoryScrollViewContent.GetComponent<CustomDropdown>().GetSelectedIndex();

        //Wybiera broń z ekwipunku na podstawie wartości dropdowna
        Weapon selectedWeapon = unit.GetComponent<Inventory>().AllWeapons[selectedIndex - 1];

        // Usuwa przedmiot z ekwipunku
        unit.GetComponent<Inventory>().AllWeapons.Remove(selectedWeapon);

        // Zwraca broń do puli
        WeaponsPool.Instance.ReturnWeaponToPool(selectedWeapon.gameObject);

        //Usuwa broń ze słownika broni z zapisanym czasem przeładowania
        Unit.SelectedUnit.GetComponent<Weapon>().WeaponsWithReloadLeft.Remove(selectedWeapon.Id);

        //Jeżeli usuwamy broń, która była aktualnym komponentem Weapon danej jednostki to ustawiamy ten komponent na Pięści, aby zapobiec używaniu statystyk usuniętej broni podczas ataków
        if(selectedWeapon.Id == Unit.SelectedUnit.GetComponent<Weapon>().Id)
        {
            Unit.SelectedUnit.GetComponent<Weapon>().ResetWeapon();
        }

        //Jeżeli usuwamy broń, która była w rękach, aktualizujemy tablicę dobytych broni
        Weapon[] equippedWeapons = unit.GetComponent<Inventory>().EquippedWeapons;
        for (int i = 0; i < equippedWeapons.Length; i++)
        {
            if (equippedWeapons[i].Id == selectedWeapon.Id)
            {
                equippedWeapons[i] = null;
            }
        }

        UpdateInventoryDropdown(unit.GetComponent<Inventory>().AllWeapons);

        Debug.Log($"Przedmiot {selectedWeapon.Name} został usunięty z ekwipunku {unit.GetComponent<Stats>().Name}.");
    }
    #endregion

    #region Grabing weapons
    public void GrabWeapon()
    {
        if(Unit.SelectedUnit == null || InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count == 0) return;

        GameObject unit = Unit.SelectedUnit;
        int selectedIndex = InventoryScrollViewContent.GetComponent<CustomDropdown>().GetSelectedIndex();

        //Wybiera broń z ekwipunku na podstawie wartości dropdowna
        Weapon selectedWeapon = unit.GetComponent<Inventory>().AllWeapons[selectedIndex - 1];

        //Jeżeli postać trzymała wcześniej broń dwuręczną to odkłada ją z powrotem do ekwipunku
        if(unit.GetComponent<Inventory>().EquippedWeapons[0] != null && unit.GetComponent<Inventory>().EquippedWeapons[0].TwoHanded == true)
        {
            unit.GetComponent<Inventory>().EquippedWeapons[0] = null;
            unit.GetComponent<Inventory>().EquippedWeapons[1] = null;
        }

        //Ustala rękę, do której zostanie wzięta broń (0 oznacza rękę dominującą, 1 rękę niedominującą)
        if(selectedWeapon.Type.Contains("melee") && !selectedWeapon.Type.Contains("shield")) unit.GetComponent<Inventory>().EquippedWeapons[0] = selectedWeapon;
        if(selectedWeapon.Type.Contains("ranged")) unit.GetComponent<Inventory>().EquippedWeapons[0] = selectedWeapon;
        if(selectedWeapon.Type.Contains("shield")) unit.GetComponent<Inventory>().EquippedWeapons[1] = selectedWeapon;

        //Jeśli broń jest dwuręczna to postać bierze ją także do drugiej ręki
        if(selectedWeapon.TwoHanded == true) unit.GetComponent<Inventory>().EquippedWeapons[1] = selectedWeapon;
     
        //Ustala, czy postać może parować tą bronią
        unit.GetComponent<Unit>().CanParry = selectedWeapon.Type.Contains("melee") && selectedWeapon.Id != 0 ? true : false;

        //Odwołanie do komponentu Weapon wybranej postaci
        Weapon unitWeapon = unit.GetComponent<Weapon>();

        // Używanie refleksji do aktualizacji wartości wszystkich pól kompenentu Weapon wybranej postaci
        FieldInfo[] fields = typeof(Weapon).GetFields(BindingFlags.Instance | BindingFlags.Public);
        foreach (var field in fields)
        {
            var targetField = typeof(Weapon).GetField(field.Name, BindingFlags.Instance | BindingFlags.Public);
            if (targetField != null)
            {
                targetField.SetValue(unitWeapon, field.GetValue(selectedWeapon));
            }
        }

        // Ponowna inicjalizacja przycisków po dodaniu/usunięciu przycisków z listy Buttons
        InventoryScrollViewContent.GetComponent<CustomDropdown>().InitializeButtons();

        //Aktualizuje kolor broni w ekwipunku na aktywny
        CheckForEquippedWeapons();

        Debug.Log($"{unit.GetComponent<Stats>().Name} dobył {selectedWeapon.Name}.");
    }
    #endregion

    #region Inventory dropdown list managing
    public void UpdateInventoryDropdown(List<Weapon> weapons)
    {
        // Resetuje wyświetlany ekwipunek
        var buttons = InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons;
        for (int i = buttons.Count - 1; i >= 0; i--)
        {
            Button button = buttons[i];
            string buttonText = button.GetComponentInChildren<TextMeshProUGUI>().text;

            Weapon weapon = weapons.Find(obj => obj.Name == buttonText);

            if (weapon == null)
            {
                buttons.Remove(button);
                Destroy(button.gameObject);
            }
        }
        // Ustala wyświetlany ekwipunek postaci
        foreach (var weapon in weapons)
        {
            bool buttonExists = InventoryScrollViewContent.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == weapon.Name);

            if (!buttonExists)
            {
                // Dodaje broń do ScrollViewContent w postaci buttona
                GameObject buttonObj = Instantiate(_weaponButtonPrefab, InventoryScrollViewContent);
                TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                // Ustala text buttona
                buttonText.text = weapon.Name;

                Button button = buttonObj.GetComponent<Button>();

                // Dodaje opcję do CustomDropdowna ze wszystkimi brońmi
                InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Add(button);

                // Zdarzenie po kliknięciu na konkretny item z listy
                button.onClick.AddListener(() =>
                {
                    int buttonIndex = InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.IndexOf(button);

                    InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(buttonIndex + 1); // Wybiera element i aktualizuje jego wygląd
                });
            }
        }

        //Ponowna inicjalizacja przycisków po dodaniu/usunięciu przycisków z listy Buttons
        InventoryScrollViewContent.GetComponent<CustomDropdown>().InitializeButtons();

        CheckForEquippedWeapons();
    }

    private void CheckForEquippedWeapons()
    {
        if(Unit.SelectedUnit == null) return;

        //List<Weapon> allWeapons = Unit.SelectedUnit.GetComponent<Inventory>().AllWeapons;
        List<Button> allWeaponButtons = InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons;
        Weapon[] equippedWeapons = Unit.SelectedUnit.GetComponent<Inventory>().EquippedWeapons;

        for (int i = 0; i < allWeaponButtons.Count; i++)
        {
            //Tekst przycisku
            string buttonText = allWeaponButtons[i].GetComponentInChildren<TextMeshProUGUI>().text;
            
            //Jeśli broń jest w rękach to ustawia ja jako aktywną
            if(equippedWeapons[0] != null && equippedWeapons[0].Name == buttonText || equippedWeapons[1] != null &&  equippedWeapons[1].Name == buttonText)
            {
                //Ustawia kolor przycisku na aktywny
                InventoryScrollViewContent.GetComponent<CustomDropdown>().MakeOptionActive(i + 1);
            }
            else
            {
                //Resetuje kolor przycisku
                InventoryScrollViewContent.GetComponent<CustomDropdown>().ResetColor(i + 1);
            }
        }
    }
    #endregion

    public Weapon ChooseWeaponToAttack(GameObject unit)
    {
        Inventory inventory = unit.GetComponent<Inventory>();

        //Gdy postać trzyma broń w ręce dominującej to atakuje za jej pomocą, w przeciwnym razie używa drugiej ręki
        Weapon weapon = inventory.EquippedWeapons[0] != null ? inventory.EquippedWeapons[0] : inventory.EquippedWeapons[1];

        return weapon;
    }
}
