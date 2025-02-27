using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using TMPro;
using System.Reflection;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.Windows;
using UnityEngine.TextCore.Text;

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
    
    // Lista wszystkich dostępnych broni
    public List<WeaponData> AllWeaponData = new List<WeaponData>();
    [SerializeField] private GameObject _buttonPrefab; // Przycisk odpowiadający każdej z broni
    public Transform InventoryScrollViewContent; // Lista ekwipunku postaci
    public CustomDropdown WeaponsDropdown;
    [SerializeField] private GameObject _inventoryPanel;
    public int SelectedHand;
    [SerializeField] private UnityEngine.UI.Button _leftHandButtonInventory;
    [SerializeField] private UnityEngine.UI.Button _leftHandButtonLowerBar;
    [SerializeField] private UnityEngine.UI.Button _rightHandButtonInventory;
    [SerializeField] private UnityEngine.UI.Button _rightHandButtonLowerBar;
    [SerializeField] private TMP_Text _equippedWeaponsDisplay; // Wyświetlenie nazw dobytych broni w panelu jednostki
    [SerializeField] private UnityEngine.UI.Slider _reloadBar; // Pasek pokazujący stan naładowania broni dystansowej
    [SerializeField] private TMP_InputField _copperCoinsInput;
    [SerializeField] private TMP_InputField _silverCoinsInput;
    [SerializeField] private TMP_InputField _goldCoinsInput;

    void Start()
    {
        //Wczytuje listę wszystkich broni
        DataManager.Instance.LoadAndUpdateWeapons(); 

        //Ustawia domyślną rękę na prawą
        SelectHand(true);    
    }

    #region Inventory panel managing
    private void Update()
    {
        if (UnityEngine.Input.GetKeyDown(KeyCode.I) 
                && Unit.SelectedUnit != null 
                && !GameManager.Instance.IsAnyInputFieldFocused() 
                && !UnityEngine.Input.GetKey(KeyCode.LeftControl) 
                && !UnityEngine.Input.GetKey(KeyCode.RightControl) 
                && !UnityEngine.Input.GetKey(KeyCode.LeftCommand) 
                && !UnityEngine.Input.GetKey(KeyCode.RightCommand))
        {
            GameManager.Instance.HideActivePanels();
            GameManager.Instance.ShowPanel(_inventoryPanel);
        }
    }

    public void HideInventory()
    {
        _inventoryPanel.SetActive(false);
    }
    #endregion

    #region Add weapon from list to inventory
    public void LoadWeapons(bool grabAfterLoad)
    {
        if (Unit.SelectedUnit != null)
        {
            // Ustalenie Id broni na podstawie wyboru z dropdowna
            Unit.SelectedUnit.GetComponent<Weapon>().Id = WeaponsDropdown.GetSelectedIndex();
        }
        else
        {
            Debug.Log("Aby dodać przedmiot do ekwipunku, musisz najpierw wybrać postać.");
        }

        //Wczytanie statystyk broni
        DataManager.Instance.LoadAndUpdateWeapons();

        //Jeśli wybieramy opcję "Dodaj i wyposaż" to od razu wyposażamy jednostkę w wybraną broń
        if(grabAfterLoad == true && Unit.SelectedUnit != null)
        {
            //Znajdujemy index z listy ekwipunku dla wybranej broni
            int weaponIndex = Unit.SelectedUnit.GetComponent<Inventory>().AllWeapons.FindIndex(b => b.Id == Unit.SelectedUnit.GetComponent<Weapon>().Id);

            //Dobywamy wybraną broń
            GrabWeapon(weaponIndex + 1);
        }
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

        // Sprawdzenie, czy przedmiot o takiej samej nazwie już istnieje w ekwipunku
        if (unit.GetComponent<Inventory>().AllWeapons.Any(w => w.Name == newWeapon.Name) && !SaveAndLoadManager.Instance.IsLoading)
        {
            Debug.Log($"Przedmiot {newWeapon.Name} już znajduje się w ekwipunku {unit.GetComponent<Stats>().Name}.");
            return;
        }

        //Dodaje przedmiot do ekwipunku
        unit.GetComponent<Inventory>().AllWeapons.Add(newWeapon);

        if(!SaveAndLoadManager.Instance.IsLoading) //Zapobiega wypisywaniu wszystkich broni podczas wczytywania stanu gry
        {
            //Sortuje listę alfabetycznie
            unit.GetComponent<Inventory>().AllWeapons.Sort((x, y) => x.Name.CompareTo(y.Name));

            Debug.Log($"Przedmiot {newWeapon.Name} został dodany do ekwipunku {unit.GetComponent<Stats>().Name}.");
        }

        UpdateInventoryDropdown(unit.GetComponent<Inventory>().AllWeapons, true);
    }
    #endregion

    #region Removing weapon from inventory
    public void RemoveWeaponFromInventory()
    {
        if(Unit.SelectedUnit == null || InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count == 0) return;

        GameObject unit = Unit.SelectedUnit;

        int selectedIndex = InventoryScrollViewContent.GetComponent<CustomDropdown>().GetSelectedIndex();

        if (selectedIndex > unit.GetComponent<Inventory>().AllWeapons.Count || selectedIndex == 0) return;
        if (InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedButton == null) return;

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
            if (equippedWeapons[i] != null && equippedWeapons[i].Id == selectedWeapon.Id)
            {
                equippedWeapons[i] = null;
            }
        }

        UpdateInventoryDropdown(unit.GetComponent<Inventory>().AllWeapons, true);

        Debug.Log($"Przedmiot {selectedWeapon.Name} został usunięty z ekwipunku {unit.GetComponent<Stats>().Name}.");
    }

    public void RemoveAllWeaponsFromInventory()
    {
        if (Unit.SelectedUnit == null || InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count == 0) return;

        GameObject unit = Unit.SelectedUnit;

        // Pobiera ekwipunek postaci
        List<Weapon> allWeapons = unit.GetComponent<Inventory>().AllWeapons;

        // Iteruje przez wszystkie bronie w ekwipunku
        foreach (Weapon weapon in new List<Weapon>(allWeapons)) // Tworzymy kopię listy, aby uniknąć modyfikacji podczas iteracji
        {
            // Zwraca broń do puli
            WeaponsPool.Instance.ReturnWeaponToPool(weapon.gameObject);

            // Usuwa broń ze słownika broni z zapisanym czasem przeładowania
            Unit.SelectedUnit.GetComponent<Weapon>().WeaponsWithReloadLeft.Remove(weapon.Id);

            // Jeżeli usuwamy broń, która była aktualnym komponentem Weapon danej jednostki, ustawiamy ten komponent na Pięści
            if (weapon.Id == Unit.SelectedUnit.GetComponent<Weapon>().Id)
            {
                Unit.SelectedUnit.GetComponent<Weapon>().ResetWeapon();
            }
        }

        // Opróżnia tablicę dobytych broni
        Weapon[] equippedWeapons = unit.GetComponent<Inventory>().EquippedWeapons;
        for (int i = 0; i < equippedWeapons.Length; i++)
        {
            equippedWeapons[i] = null;
        }

        // Opróżnia ekwipunek
        allWeapons.Clear();

        UpdateInventoryDropdown(allWeapons, true);
    }
    #endregion

    #region Grabing weapons
    public void GrabWeapon(int selectedIndex = 0)
    {
        if(Unit.SelectedUnit == null || InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count == 0) return;
        if (InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedButton == null && selectedIndex == 0) return;

        GameObject unit = Unit.SelectedUnit;
        if(selectedIndex == 0)
        {
            selectedIndex = InventoryScrollViewContent.GetComponent<CustomDropdown>().GetSelectedIndex();
        }

        if (selectedIndex == 0)
        {
            Debug.Log("Musisz wybrać broń, w którą chcesz się wyposażyć.");
            return;
        }

        //Wybiera broń z ekwipunku na podstawie wartości dropdowna
        Weapon selectedWeapon = unit.GetComponent<Inventory>().AllWeapons[selectedIndex - 1];

        //Odniesienie do trzymanych przez postać broni
        Weapon[] equippedWeapons = unit.GetComponent<Inventory>().EquippedWeapons;

        //Wykonuje akcję, jeżeli obecnie wybrana broń jest inna niż ta trzymana w rękach
        bool containsSelectedWeapon = equippedWeapons.Contains(selectedWeapon);
        bool selectedWeaponIsNotInSelectedHand = !containsSelectedWeapon || (SelectedHand != Array.IndexOf(equippedWeapons, selectedWeapon));
        if (selectedWeaponIsNotInSelectedHand)
        {
            //Uwzględnia szybkie wyciągnięcie. Nie dotyczy tryby automatycznego (akcja jest zużywana bezpośrednio w AutoCombatManager, bo jednostka automatycznie wielokrotnie zmienia bronie, dopóki nie trafi na odpowiednią)
            if(!unit.GetComponent<Stats>().QuickDraw && !GameManager.IsAutoCombatMode && !SaveAndLoadManager.Instance.IsLoading)
            {
                bool canDoAction = true;
                canDoAction = RoundsManager.Instance.DoHalfAction(unit.GetComponent<Unit>());
                if (!canDoAction) return;
            }

            //W przypadku, gdy dana broń jest już trzymana, ale chcemy jedynie zmienić rękę to usuwa tą broń z poprzedniej ręki
            if(containsSelectedWeapon)
            {
                equippedWeapons[Array.IndexOf(equippedWeapons, selectedWeapon)] = null;
            }
        }
        else
        {
            // Jeżeli broń jest już trzymana, odkładamy ją
            int weaponHand = Array.IndexOf(equippedWeapons, selectedWeapon);

            // Jeśli broń jest dwuręczna, usuń ją z obu rąk
            if (selectedWeapon.TwoHanded)
            {
                equippedWeapons[0] = null;
                equippedWeapons[1] = null;
            }
            else
            {
                equippedWeapons[weaponHand] = null;
            }

            Debug.Log($"{unit.GetComponent<Stats>().Name} odłożył {selectedWeapon.Name}.");
            CheckForEquippedWeapons();
            return;
        }

        //Jeżeli postać trzymała wcześniej broń dwuręczną to "zdejmujemy" ją również z drugiej ręki
        if(equippedWeapons[0] != null && equippedWeapons[0].TwoHanded == true)
        {
            int otherHand = SelectedHand == 0 ? 1 : 0;
            equippedWeapons[otherHand] = null;
        }

        //Ustala rękę, do której zostanie wzięta broń (0 oznacza rękę dominującą, 1 rękę niedominującą)
        equippedWeapons[SelectedHand] = selectedWeapon;

        //Jeśli broń jest dwuręczna to postać bierze ją także do drugiej ręki
        if(selectedWeapon.TwoHanded == true)
        {
            equippedWeapons[0] = selectedWeapon;
            equippedWeapons[1] = selectedWeapon;
        }

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

        if(!SaveAndLoadManager.Instance.IsLoading)
        {
            Debug.Log($"{unit.GetComponent<Stats>().Name} dobył {selectedWeapon.Name}.");

            //Aktualizuje pasek przewagi w bitwie
            Unit.SelectedUnit.GetComponent<Stats>().Overall = Unit.SelectedUnit.GetComponent<Stats>().CalculateOverall();
            InitiativeQueueManager.Instance.CalculateAdvantage();
        }
    }

    public void GrabPrimaryWeapons()
    {
        Stats unitStats = Unit.SelectedUnit.GetComponent<Stats>();
        if (unitStats.PrimaryWeaponIds == null || unitStats.PrimaryWeaponIds.Count == 0) return;

        // Kopia listy Id broni
        List<int> weaponIds = new List<int>(unitStats.PrimaryWeaponIds);

        // Losowo wybieramy jedną broń
        int randomIndex = UnityEngine.Random.Range(0, weaponIds.Count);
        int selectedWeaponId = weaponIds[randomIndex];

        // Ustawiamy wybraną broń w dropdownie i dobywamy ją
        EquipSelectedPrimaryWeapon(unitStats, selectedWeaponId);

        // Sprawdź, czy wybrana broń jest dwuręczna lub tarczą
        bool isTwoHanded = IsTwoHandedWeapon(selectedWeaponId);
        bool isShield = IsShield(selectedWeaponId);

        if (!isTwoHanded && !isShield)
        {
            // Automatycznie wyposażamy tarczę, jeśli istnieje
            foreach (int weaponId in weaponIds)
            {
                if (IsShield(weaponId))
                {
                    SelectHand(false);
                    EquipSelectedPrimaryWeapon(unitStats, weaponId);
                    SelectHand(true);
                    break;
                }
            }
        }

        // Jeśli wybrana broń to tarcza, wybieramy dodatkową broń, upewniając się, że nie jest dwuręczna
        if (isShield)
        {
            SelectHand(false);
            InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(1);
            GrabWeapon();
            SelectHand(true);

            // Usuwamy tarczę z listy broni
            weaponIds.RemoveAt(randomIndex);

            // Tworzymy listę broni wykluczającą broń dwuręczną
            List<int> possibleWeapons = weaponIds.Where(id => !IsTwoHandedWeapon(id)).ToList();

            if (possibleWeapons.Count > 0)
            {
                int newRandomIndex = UnityEngine.Random.Range(0, possibleWeapons.Count);
                int newSelectedWeaponId = possibleWeapons[newRandomIndex];
                // Ustawiamy wybraną broń w dropdownie i dobywamy ją
                EquipSelectedPrimaryWeapon(unitStats, newSelectedWeaponId);
            }
        }

        SaveAndLoadManager.Instance.IsLoading = false;
        Unit.SelectedUnit = Unit.LastSelectedUnit != null ? Unit.LastSelectedUnit : null;
    }

    private void EquipSelectedPrimaryWeapon(Stats unitStats, int weaponId)
    {
        WeaponsDropdown.SetSelectedIndex(weaponId);
        LoadWeapons(false);
        int weaponsCount = unitStats.GetComponent<Inventory>().AllWeapons.Count;
        //Wybieramy ostatnią broń na liście ekwipunku (musiałem wyłączyć alfabetyczne sortowanie)
        InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(weaponsCount);
        GrabWeapon();
    }

    private bool IsTwoHandedWeapon(int weaponId)
    {
        WeaponData weaponData = AllWeaponData.FirstOrDefault(w => w.Id == weaponId);
        if (weaponData == null) return false;

        return weaponData.TwoHanded;
    }

    private bool IsShield(int weaponId)
    {
        WeaponData weaponData = AllWeaponData.FirstOrDefault(w => w.Id == weaponId);
        if (weaponData == null) return false;

        bool isShield = weaponData.Type.Contains("shield");
        return isShield;
    }

    public void SelectHand(bool rightHand)
    {
        SelectedHand = rightHand ? 0 : 1;

        // Deklaracja tablic przycisków
        UnityEngine.UI.Button[] activeButtons;
        UnityEngine.UI.Button[] inactiveButtons;

        // Sprawdzenie wartości zmiennej rightHand
        if (rightHand)
        {
            activeButtons = new UnityEngine.UI.Button[] { _rightHandButtonInventory, _rightHandButtonLowerBar };
            inactiveButtons = new UnityEngine.UI.Button[] { _leftHandButtonInventory, _leftHandButtonLowerBar };
        }
        else
        {
            activeButtons = new UnityEngine.UI.Button[] { _leftHandButtonInventory, _leftHandButtonLowerBar };
            inactiveButtons = new UnityEngine.UI.Button[] { _rightHandButtonInventory, _rightHandButtonLowerBar };
        }
        
        // Ustawia kolor aktywnych przycisków na zielony, a nieaktywnych na domyślny
        foreach(var activeButton in activeButtons)
        {
            Color activeColor = new Color(0.3f, 0.65f, 0.125f);
            activeButton.GetComponent<UnityEngine.UI.Image>().color = activeColor;
        }
        foreach(var inactiveButton in inactiveButtons)
        {
            Color inactiveColor = Color.white;
            inactiveButton.GetComponent<UnityEngine.UI.Image>().color = inactiveColor;
        }
    }

    public void DisplayHandInfo(UnityEngine.UI.Button button)
    {
        button.transform.Find("hand_text").gameObject.SetActive(true);

        string buttonText = button.transform.Find("Text (TMP)").GetComponent<TMP_Text>().text;
        string rightHandWeapon = Unit.SelectedUnit.GetComponent<Inventory>().EquippedWeapons[0]?.Name;
        string leftHandWeapon = Unit.SelectedUnit.GetComponent<Inventory>().EquippedWeapons[1]?.Name;
        string handInfoText = null;

        if (rightHandWeapon == buttonText) handInfoText = "P";     
        if (leftHandWeapon == buttonText) handInfoText = "L";
        if (rightHandWeapon == buttonText && leftHandWeapon == buttonText) handInfoText = "P + L";
            
        button.transform.Find("hand_text").GetComponent<TMP_Text>().text = handInfoText;
        if(handInfoText == null)
        {
            button.transform.Find("hand_text").gameObject.SetActive(false);
        }
    }
    #endregion

    #region Edit weapon stats
    public void EditWeaponAttribute(GameObject textInput)
    {
        if (Unit.SelectedUnit == null || Unit.SelectedUnit.GetComponent<Inventory>().AllWeapons.Count == 0) return;

        GameObject unit = Unit.SelectedUnit;

        // Pobiera pole ze statystyk postaci o nazwie takiej samej jak nazwa textInputa (z wyłączeniem słowa "input")
        string attributeName = textInput.name.Replace("_input", "");

        int selectedIndex = InventoryScrollViewContent.GetComponent<CustomDropdown>().GetSelectedIndex();
        if (selectedIndex == 0)
        {
            Debug.Log("Musisz wybrać broń, którą chcesz zmodyfikować.");
            return;
        }

        //Wybiera broń z ekwipunku na podstawie wartości dropdowna
        Weapon selectedWeapon = unit.GetComponent<Inventory>().AllWeapons[selectedIndex - 1];

        FieldInfo field = selectedWeapon.GetType().GetField(attributeName);

        if (field == null) return;

        // Zmienia wartść cechy
        if (field.FieldType == typeof(int))
        {
            // Pobiera wartość inputa, starając się przekonwertować ją na int
            int value = int.TryParse(textInput.GetComponent<TMP_InputField>().text, out int inputValue) ? inputValue : 0;

            field.SetValue(selectedWeapon, value);
        }
        else if (field.FieldType == typeof(float))
        {
            // Pobiera wartość inputa, starając się przekonwertować ją na float
            float value = float.TryParse(textInput.GetComponent<TMP_InputField>().text, out float inputValue) ? inputValue : 0;

            if (value > 3)
            {
                field.SetValue(selectedWeapon, value / 2); // dzieli wartosc na 2, zeby ustawic zasieg w polach a nie metrach
                selectedWeapon.Type[0] = "ranged"; // Zmiana typu broni na dystansowy
            }
            else
            {
                field.SetValue(selectedWeapon, 1.5f); // gdy ktos poda zasieg mniejszy niz 3 metry to ustawia domyslna wartosc zasiegu do walki wrecz
                selectedWeapon.Type[0] = "melee"; // Zmiana typu broni na broń do walki w zwarciu
            }

        }
        else if (field.FieldType == typeof(bool))
        {
            bool boolValue = textInput.GetComponent<UnityEngine.UI.Toggle>().isOn;
            field.SetValue(selectedWeapon, boolValue);
        }
        else if (field.FieldType == typeof(string) && textInput.GetComponent<TMP_Dropdown>() != null)
        {
            string value = textInput.GetComponent<TMP_Dropdown>().options[textInput.GetComponent<TMP_Dropdown>().value].text;
            field.SetValue(selectedWeapon, value);
        }
        else if (field.FieldType == typeof(string))
        {
            string value = textInput.GetComponent<TMP_InputField>().text;
            field.SetValue(selectedWeapon, value);
        }
        else
        {
            Debug.Log($"Nie udało się zmienić wartości cechy.");
        }

        //Odświeża listę ekwipunku
        UpdateInventoryDropdown(Unit.SelectedUnit.GetComponent<Inventory>().AllWeapons, false);

        DisplayEquippedWeaponsName();
    }

    public void LoadWeaponAttributes()
    {
        if (Unit.SelectedUnit == null || Unit.SelectedUnit.GetComponent<Inventory>().AllWeapons.Count == 0) return;

        GameObject unit = Unit.SelectedUnit;

        // Wyszukuje wszystkie pola tekstowe i przyciski do ustalania statystyk broni wewnatrz gry
        GameObject[] attributeInputFields = GameObject.FindGameObjectsWithTag("WeaponAttribute");

        int selectedIndex = InventoryScrollViewContent.GetComponent<CustomDropdown>().GetSelectedIndex();
        if (selectedIndex == 0)
        {
            Debug.Log("Musisz wybrać broń, którą chcesz zmodyfikować.");
            return;
        }

        //Wybiera broń z ekwipunku na podstawie wartości dropdowna
        Weapon selectedWeapon = unit.GetComponent<Inventory>().AllWeapons[selectedIndex - 1];

        foreach (var inputField in attributeInputFields)
        {
            // Pobiera pole ze statystyk postaci o nazwie takiej samej jak nazwa textInputa (z wyłączeniem słowa "input")
            string attributeName = inputField.name.Replace("_input", "");
            FieldInfo field = selectedWeapon.GetType().GetField(attributeName);

            if (field == null) continue;

            // Jeśli znajdzie takie pole, to zmienia wartość wyświetlanego tekstu na wartość tej cechy
            if (field.FieldType == typeof(int))
            {
                int value = (int)field.GetValue(selectedWeapon);

                if (inputField.GetComponent<TMPro.TMP_InputField>() != null)
                {
                    inputField.GetComponent<TMPro.TMP_InputField>().text = value.ToString();
                }
            }
            else if (field.FieldType == typeof(float))
            {
                float value = (float)field.GetValue(selectedWeapon);

                if (inputField.GetComponent<TMPro.TMP_InputField>() != null)
                {
                    if (value > 1.5f)
                    {
                        inputField.GetComponent<TMPro.TMP_InputField>().text = (value * 2).ToString(); // mnoży x2 żeby podać zasięg w metrach a nie polach
                    }
                    else
                    {
                        inputField.GetComponent<TMPro.TMP_InputField>().text = "1"; // w przypadku broni do walki w zwarciu wyświetla wartość "1"
                    }
                }
            }
            else if (field.FieldType == typeof(bool)) // to działa dla cech opisywanych wartościami bool
            {
                bool value = (bool)field.GetValue(selectedWeapon);
                inputField.GetComponent<UnityEngine.UI.Toggle>().isOn = value;
            }
            else if (field.FieldType == typeof(string) && inputField.GetComponent<TMP_Dropdown>() != null) // to działa dla cech opisywanych dropdownem
            {
                string value = (string)field.GetValue(selectedWeapon);
                TMP_Dropdown dropdown = inputField.GetComponent<TMP_Dropdown>();

                if (string.IsNullOrEmpty(value)) // Sprawdza, czy wartość jest pusta
                {
                    dropdown.value = 1; // Ustawia na zwykłą jakość (index 1)
                }
                else
                {
                    int index = dropdown.options.FindIndex(option => option.text == value);
                    if (index >= 0)
                    {
                        dropdown.value = index;
                    }
                }
            }
            else if (field.FieldType == typeof(string)) // to działa dla cech opisywanych wartościami string
            {
                string value = (string)field.GetValue(selectedWeapon);

                if (inputField.GetComponent<TMPro.TMP_InputField>() != null)
                {
                    inputField.GetComponent<TMPro.TMP_InputField>().text = value;
                }
            }
        }
    }
    #endregion

    #region Inventory dropdown list managing
    public void UpdateInventoryDropdown(List<Weapon> weapons, bool reloadEditWeaponPanel)
    {
        //Ustala wyświetlaną nazwę właściciela ekwipunku
        if(Unit.SelectedUnit != null)
        {
            _inventoryPanel.transform.Find("inventory_name").GetComponent<TMP_Text>().text = "Ekwipunek " + Unit.SelectedUnit.GetComponent<Stats>().Name;

            _copperCoinsInput.text = Unit.SelectedUnit.GetComponent<Inventory>().CopperCoins.ToString();
            _silverCoinsInput.text = (Unit.SelectedUnit.GetComponent<Inventory>().SilverCoins).ToString();
            _goldCoinsInput.text = (Unit.SelectedUnit.GetComponent<Inventory>().GoldCoins).ToString();
        }

        ResetInventoryDropdown();

        // Ustala wyświetlany ekwipunek postaci
        foreach (var weapon in weapons)
        {
            // Dodaje broń do ScrollViewContent w postaci buttona
            GameObject buttonObj = Instantiate(_buttonPrefab, InventoryScrollViewContent);
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            // Ustala text buttona
            buttonText.text = weapon.Name;

            UnityEngine.UI.Button button = buttonObj.GetComponent<UnityEngine.UI.Button>();

            // Dodaje opcję do CustomDropdowna ze wszystkimi brońmi
            InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Add(button);
            //Sortuje listę alfabetycznie
            InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Sort((x, y) => x.GetComponentInChildren<TextMeshProUGUI>().text.CompareTo(y.GetComponentInChildren<TextMeshProUGUI>().text));


            // Zdarzenie po kliknięciu na konkretny item z listy
            button.onClick.AddListener(() =>
            {
                int buttonIndex = InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.IndexOf(button);

                InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(buttonIndex + 1); // Wybiera element i aktualizuje jego wygląd
            });
        }

        //Aktualizuje panel edycji broni, w przypadku gdyby był otwarty
        if(reloadEditWeaponPanel == true)
        {
            //Domyślnie zaznacza pierwszą pozycję na liście
            if(weapons.Count > 0)
            {
                InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(1);
            }
            LoadWeaponAttributes();
        }

        //Ponowna inicjalizacja przycisków po dodaniu/usunięciu przycisków z listy Buttons
        InventoryScrollViewContent.GetComponent<CustomDropdown>().InitializeButtons();

        CheckForEquippedWeapons();
    }

    private void ResetInventoryDropdown()
    {
        // Resetowanie wyświetlanego ekwipunku poprzez usunięcie wszystkich przycisków
        var buttons = InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons;
        for (int i = buttons.Count - 1; i >= 0; i--)
        {
            UnityEngine.UI.Button button = buttons[i];
            buttons.Remove(button);
            Destroy(button.gameObject);
        }
    }

    public void CheckForEquippedWeapons()
    {
        if(Unit.SelectedUnit == null) return;

        List<UnityEngine.UI.Button> allWeaponButtons = InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons;
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

        //Gdy postać trzyma broń w ręce, która jest oznaczona jako aktywna to atakuje za jej pomocą, w przeciwnym razie używa drugiej ręki
        int otherHand = SelectedHand == 0 ? 1 : 0;
        Weapon weapon = inventory.EquippedWeapons[SelectedHand] != null ? inventory.EquippedWeapons[SelectedHand] : inventory.EquippedWeapons[otherHand];

        if(weapon == null)
        {
            unit.GetComponent<Weapon>().ResetWeapon();
            weapon = unit.GetComponent<Weapon>();
        }

        return weapon;
    }

    public void DisplayEquippedWeaponsName()
    {
        if(Unit.SelectedUnit == null) return;

        Weapon[] equippedWeapons = Unit.SelectedUnit.GetComponent<Inventory>().EquippedWeapons;

        //Wyświetla informacje o dobytej broni
        if (equippedWeapons[0] != null && equippedWeapons[1] != null)
        {
            if (equippedWeapons[0].Name == equippedWeapons[1].Name)
            {
                _equippedWeaponsDisplay.text = $"Broń: {equippedWeapons[0].Name}";
            }
            else
            {
                _equippedWeaponsDisplay.text = $"Broń: {equippedWeapons[0].Name}, {equippedWeapons[1].Name}";
            }
        }
        else if (equippedWeapons[0] != null)
        {
            _equippedWeaponsDisplay.text = $"Broń: {equippedWeapons[0].Name}";
        }
        else if (equippedWeapons[1] != null)
        {
            _equippedWeaponsDisplay.text = $"Broń: {equippedWeapons[1].Name}";
        }
        else
        {
            _equippedWeaponsDisplay.text = "Broń: brak";
        }

        DisplayReloadTime();
    }

    public void DisplayReloadTime()
    {
        if(Unit.SelectedUnit == null) return;

        Weapon[] equippedWeapons = Unit.SelectedUnit.GetComponent<Inventory>().EquippedWeapons;

        bool reloadBarActive = false;

        foreach (Weapon weapon in equippedWeapons)
        {
            if (weapon != null && weapon.ReloadTime > 0)
            {
                // Ustawia slider jako aktywny
                _reloadBar.gameObject.SetActive(true);

                // Ustawia wartości slidera
                _reloadBar.maxValue = weapon.ReloadTime;
                _reloadBar.value = weapon.ReloadTime - weapon.ReloadLeft;

                // Znajduje komponent TextMeshProUGUI w obiekcie ReloadBar i ustawia tekst
                TextMeshProUGUI reloadTimeText = _reloadBar.GetComponentInChildren<TextMeshProUGUI>();
                if (reloadTimeText != null)
                {
                    reloadTimeText.text = $"{weapon.ReloadTime - weapon.ReloadLeft}/{weapon.ReloadTime}";
                }

                reloadBarActive = true;
                break; // Zatrzymuje pętlę, ponieważ znaleźliśmy broń wymagającą przeładowania
            }
        }

        // Jeśli żadna broń nie wymaga przeładowania, ukrywa slider
        if (!reloadBarActive)
        {
            _reloadBar.gameObject.SetActive(false);
        }
    }

    #region Money managing
    public void UpdateMoneyAmount(TMP_InputField inputField)
    {
        if (Unit.SelectedUnit == null) return;

        Inventory inventory = Unit.SelectedUnit.GetComponent<Inventory>();

        string rawText = inputField.text.Trim();
        bool isAddition = false;

        // Sprawdzamy czy jest znak '+' na początku.
        if (rawText.StartsWith("+"))
        {
            isAddition = true;
            rawText = rawText.Substring(1).Trim();
        }

        // Próba parsowania wpisanej wartości
        int inputValue;
        if (!int.TryParse(rawText, out inputValue))
        {
            Debug.LogError("Wprowadzono nieprawidłową wartość!");
            return;
        }

        // Rozróżniamy przypadki: ustawianie wartości, dodawanie, odejmowanie
        if (isAddition)
        {
            // Dodajemy do aktualnej wartości
            AddCoins(inventory, inputField, inputValue);
        }
        else if (inputValue < 0)
        {
            // Odejmowanie (inputValue jest ujemne)
            SubtractCoins(inventory, inputField, -inputValue); 
        }
        else
        {
            // Ustawianie wartości na sztywno (wartość >= 0, brak plusa)
            SetCoins(inventory, inputField, inputValue);
        }

        // Normalizujemy walutę – konwertujemy nadmiary/deficyty
        NormalizeCoins(inventory);

        // Uaktualniamy pola tekstowe w UI
        UpdateMoneyInputFields(inventory);
    }

    // Dodaje określoną liczbę monet do właściwego typu (Gold, Silver lub Copper).
    private void AddCoins(Inventory inventory, TMP_InputField inputField, int amount)
    {
        if (inputField == _goldCoinsInput)
        {
            inventory.GoldCoins += amount;
        }
        else if (inputField == _silverCoinsInput)
        {
            inventory.SilverCoins += amount;
        }
        else if (inputField == _copperCoinsInput)
        {
            inventory.CopperCoins += amount;
        }
    }

    // Ustawia (nadpisuje) wartość w danym polu (Gold, Silver lub Copper).
    private void SetCoins(Inventory inventory, TMP_InputField inputField, int newValue)
    {
        if (inputField == _goldCoinsInput)
        {
            inventory.GoldCoins = newValue;
        }
        else if (inputField == _silverCoinsInput)
        {
            inventory.SilverCoins = newValue;
        }
        else if (inputField == _copperCoinsInput)
        {
            inventory.CopperCoins = newValue;
        }
    }

    // Odejmuje z określonego typu monety.  
    // Jeśli środków nie wystarczy, próbuje pobrać różnicę z innych monet.  
    // Jeśli nadal nie wystarczy, zeruje wszystko.
    private void SubtractCoins(Inventory inventory, TMP_InputField inputField, int amountToSubtract)
    {
        // Wskazujemy, na którym typie monety wykonujemy odejmowanie.
        if (inputField == _goldCoinsInput)
        {
            SubtractFromGold(inventory, amountToSubtract);
        }
        else if (inputField == _silverCoinsInput)
        {
            SubtractFromSilver(inventory, amountToSubtract);
        }
        else if (inputField == _copperCoinsInput)
        {
            SubtractFromCopper(inventory, amountToSubtract);
        }
    }

    // Odejmuje najpierw z CopperCoins, w razie deficytu próbuje użyć SilverCoins,
    // a jeśli to nie wystarczy – GoldCoins (wszystko zgodnie z przelicznikiem).
    private void SubtractFromCopper(Inventory inv, int amount)
    {
        if (amount <= inv.CopperCoins)
        {
            // Mamy wystarczająco w copper
            inv.CopperCoins -= amount;
        }
        else
        {
            // Brakuje w copper
            int deficit = amount - inv.CopperCoins;
            inv.CopperCoins = 0;

            // Próbujemy pobrać z SilverCoins
            int silverNeeded = Mathf.CeilToInt(deficit / 12f); // 1 Silver = 12 Copper
            if (silverNeeded <= inv.SilverCoins)
            {
                inv.SilverCoins -= silverNeeded;
                // Po “przelaniu” Silver → Copper, musimy jeszcze odjąć pozostały deficyt z Copper
                inv.CopperCoins = silverNeeded * 12 - deficit; 
            }
            else
            {
                // Za mało Silver, więc zgarniamy wszystko z Silver i idziemy dalej
                deficit -= inv.SilverCoins * 12;
                inv.SilverCoins = 0;

                // Próbujemy pobrać z GoldCoins
                int goldNeeded = Mathf.CeilToInt(deficit / 240f); // 1 Gold = 240 Copper
                if (goldNeeded <= inv.GoldCoins)
                {
                    inv.GoldCoins -= goldNeeded;
                    // Po “przelaniu” Gold → Copper, odejmujemy pozostały deficyt z Copper
                    inv.CopperCoins = goldNeeded * 240 - deficit;
                }
                else
                {
                    // Jeśli nawet Gold nie wystarczy, zerujemy wszystko
                    inv.GoldCoins = 0;
                    inv.SilverCoins = 0;
                    inv.CopperCoins = 0;
                }
            }
        }
    }

    // Odejmuje najpierw z SilverCoins, w razie deficytu próbuje użyć GoldCoins.
    // Jeśli dalej brakuje, zero dla wszystkich.
    private void SubtractFromSilver(Inventory inv, int amount)
    {
        if (amount <= inv.SilverCoins)
        {
            inv.SilverCoins -= amount;
        }
        else
        {
            // Brakuje w silver
            int deficit = amount - inv.SilverCoins;
            inv.SilverCoins = 0;

            // Próbujemy pobrać z GoldCoins
            if (deficit <= inv.GoldCoins * 20) // 1 Gold = 20 Silver
            {
                int goldNeeded = Mathf.CeilToInt(deficit / 20f);
                inv.GoldCoins -= goldNeeded;
                // “Przelewamy” z Gold do Silver i odejmujemy pozostały deficyt
                inv.SilverCoins = goldNeeded * 20 - deficit;
            }
            else
            {
                inv.GoldCoins = 0;
                inv.SilverCoins = 0;
                inv.CopperCoins = 0;
            }
        }
    }

    // Odejmuje bezpośrednio z GoldCoins. Jeśli zabraknie, zeruje Gold, a resztę zostawia.
    private void SubtractFromGold(Inventory inv, int amount)
    {
        if (amount <= inv.GoldCoins)
        {
            inv.GoldCoins -= amount;
        }
        else
        {
            // Za mało Gold – zerujemy Gold i nic więcej nie jesteśmy w stanie zrobić
            inv.GoldCoins = 0;
            inv.SilverCoins = 0;
            inv.CopperCoins = 0;
        }
    }

    // Normalizuje ilość monet, konwertując ewentualne nadmiary z Copper → Silver i Silver → Gold (lub braki Copper ← Silver i Silver ← Gold).
    private void NormalizeCoins(Inventory inv)
    {
        // 1) Nadmiar Copper → Silver
        if (inv.CopperCoins >= 12)
        {
            int silverGained = inv.CopperCoins / 12;  
            inv.SilverCoins += silverGained;
            inv.CopperCoins %= 12;
        }
        else if (inv.CopperCoins < 0)
        {
            // Deficyt w Copper – próbujemy “pożyczyć” z Silver
            int silverNeeded = Mathf.CeilToInt(Mathf.Abs(inv.CopperCoins) / 12f);
            if (silverNeeded <= inv.SilverCoins)
            {
                inv.SilverCoins -= silverNeeded;
                inv.CopperCoins += silverNeeded * 12;
            }
            else
            {
                // Jak brakuje Silver, to finalnie może wyzerować wszystko – 
                // ale zależy od Twojego założenia. Poniżej domyślnie zeruję.
                inv.GoldCoins = 0;
                inv.SilverCoins = 0;
                inv.CopperCoins = 0;
            }
        }

        // 2) Nadmiar Silver → Gold
        if (inv.SilverCoins >= 20)
        {
            int goldGained = inv.SilverCoins / 20;
            inv.GoldCoins += goldGained;
            inv.SilverCoins %= 20;
        }
        else if (inv.SilverCoins < 0)
        {
            // Deficyt w Silver – próbujemy “pożyczyć” z Gold
            int goldNeeded = Mathf.CeilToInt(Mathf.Abs(inv.SilverCoins) / 20f);
            if (goldNeeded <= inv.GoldCoins)
            {
                inv.GoldCoins -= goldNeeded;
                inv.SilverCoins += goldNeeded * 20;
            }
            else
            {
                // Jak brakuje Gold – analogicznie wyzerowanie:
                inv.GoldCoins = 0;
                inv.SilverCoins = 0;
                inv.CopperCoins = 0;
            }
        }
    }

    // Aktualizuje pola inputów na podstawie bieżącej ilości monet w inventory.
    private void UpdateMoneyInputFields(Inventory inventory)
    {
        _goldCoinsInput.text = inventory.GoldCoins.ToString();
        _silverCoinsInput.text = inventory.SilverCoins.ToString();
        _copperCoinsInput.text = inventory.CopperCoins.ToString();
    }
    #endregion
}
