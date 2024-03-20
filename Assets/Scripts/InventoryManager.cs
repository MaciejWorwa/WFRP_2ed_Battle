using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;
using System.Reflection;

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

    [SerializeField] private TMP_Dropdown _inventoryDropdown;

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
        unit.GetComponent<Inventory>().allWeapons.Add(newWeapon);

        UpdateInventoryDropdown(unit.GetComponent<Inventory>().allWeapons);

        Debug.Log($"Przedmiot {newWeapon.Name} został dodany do ekwipunku {unit.GetComponent<Stats>().Name}.");
    }

    public void RemoveWeaponFromInventory()
    {
        if(Unit.SelectedUnit == null || _inventoryDropdown.options.Count == 0) return;

        GameObject unit = Unit.SelectedUnit;
        int selectedIndex = _inventoryDropdown.value;

        // Sprawdź, czy wybrany indeks mieści się w zakresie opcji
        if(selectedIndex >= 0 && selectedIndex < _inventoryDropdown.options.Count)
        {
            Weapon weaponToRemove = unit.GetComponent<Inventory>().allWeapons[selectedIndex];

            // Usuń przedmiot tylko jeśli znajduje się w ekwipunku
            if(unit.GetComponent<Inventory>().allWeapons.Contains(weaponToRemove))
            {
                // Usuń przedmiot z ekwipunku
                unit.GetComponent<Inventory>().allWeapons.Remove(weaponToRemove);

                // Zwróć broń do puli
                WeaponsPool.Instance.ReturnWeaponToPool(weaponToRemove.gameObject);

                // Ustaw poprzedni przedmiot jako wybrany (jeśli usunięto ostatni, to wartość indeksu może być za duża)
                int newIndex = Mathf.Clamp(selectedIndex - 1, 0, _inventoryDropdown.options.Count - 1);

                _inventoryDropdown.value = newIndex;

                UpdateInventoryDropdown(unit.GetComponent<Inventory>().allWeapons);

                Debug.Log($"Przedmiot {weaponToRemove.Name} został usunięty z ekwipunku {unit.GetComponent<Stats>().Name} i zwrócony do puli.");
            }
        }
    }

    public void GrabWeapon()
    {
        if(Unit.SelectedUnit == null || _inventoryDropdown.options.Count == 0) return;

        GameObject unit = Unit.SelectedUnit;
        int selectedIndex = _inventoryDropdown.value;

        //Wybiera broń z ekwipunku na podstawie wartości dropdowna
        Weapon selectedWeapon = unit.GetComponent<Inventory>().allWeapons[_inventoryDropdown.value];

        //Ustala rękę, do której zostanie wzięta broń (0 oznacza rękę dominującą, 1 rękę niedominującą)
        if(selectedWeapon.Type.Contains("melee") && !selectedWeapon.Type.Contains("shield")) unit.GetComponent<Inventory>().heldWeapons[0] = selectedWeapon;
        if(selectedWeapon.Type.Contains("ranged")) unit.GetComponent<Inventory>().heldWeapons[0] = selectedWeapon;
        if(selectedWeapon.Type.Contains("shield")) unit.GetComponent<Inventory>().heldWeapons[1] = selectedWeapon;

        //Jeśli broń jest dwuręczna to postać bierze ją także do drugiej ręki
        if(selectedWeapon.TwoHanded == true) unit.GetComponent<Inventory>().heldWeapons[1] = selectedWeapon;
     
        //Ustala, czy postać może parować tą bronią
        unit.GetComponent<Unit>().CanParry = selectedWeapon.Type.Contains("melee") ? true : false;

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

        Debug.Log($"{unit.GetComponent<Stats>().Name} dobył {selectedWeapon.Name}.");
    }

    public void UpdateInventoryDropdown(List<Weapon> weapons)
    {
        _inventoryDropdown.options.Clear();
        foreach (var weapon in weapons)
        {
            _inventoryDropdown.options.Add(new TMP_Dropdown.OptionData(weapon.Name));
        }
        _inventoryDropdown.RefreshShownValue();

        // Ustawienie nowo dodanej opcji jako wybranej
        _inventoryDropdown.value = _inventoryDropdown.options.Count - 1;
    }

    public void ClearInventoryDropdown()
    {
        _inventoryDropdown.options.Clear();
    }

    public Weapon ChooseWeaponToAttack(GameObject unit)
    {
        Inventory inventory = unit.GetComponent<Inventory>();

        //Gdy postać trzyma broń w ręce dominującej to atakuje za jej pomocą, w przeciwnym razie używa drugiej ręki
        Weapon weapon = inventory.heldWeapons[0] != null ? inventory.heldWeapons[0] : inventory.heldWeapons[1];

        return weapon;
    }
}
