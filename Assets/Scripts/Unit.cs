using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public static GameObject SelectedUnit;
    public Color DefaultColor;
    public Color HighlightColor;
    public bool IsSelected { get; private set; } = false;
    public bool IsCharging; // Szarżuje
    public bool IsRunning; // Biegnie
    public bool IsHelpless; // Jest bezbronny
    public bool IsStunned; // Jest ogłuszony
    public bool IsTrapped; // Jest unieruchomiony
    public int AimingBonus;
    public int DefensiveBonus;
    public bool CanParry = true;
    public bool CanDodge = false;
    public Stats Stats;

    public TMP_Text NameDisplay;
    public TMP_Text HealthDisplay;

    void Start()
    {
        Stats = gameObject.GetComponent<Stats>();

        DisplayUnitName();

        //Ustawia wartość HighlightColor na jaśniejszą wersję DefaultColor. Trzeci parametr określa ilość koloru białego w całości.
        HighlightColor = Color.Lerp(DefaultColor, Color.yellow, 0.3f);

        if(Stats.Dodge > 0) CanDodge = true;
        Stats.TempSz = Stats.Sz;
        Stats.TempHealth = Stats.MaxHealth;
        DisplayUnitHealthPoints();

        //Aktualizuje kolejkę inicjatywy
        RoundsManager.Instance.UpdateInitiativeQueue();
    }
    private void OnMouseUp()
    {
        if(!UnitsManager.IsUnitRemoving)
        {
            SelectUnit();
        }
        else
        {
            UnitsManager.Instance.DestroyUnit(this.gameObject);
        }
    }

    private void OnMouseOver()
    {
        if(Input.GetMouseButtonDown(1) && SelectedUnit != null && SelectedUnit != this.gameObject)
        {
            //Sprawdza, czy atakowanym jest nasz sojusznik i czy tryb Friendly Fire jest aktywny
            if(GameManager.Instance.IsFriendlyFire == false && this.gameObject.CompareTag(SelectedUnit.tag))
            {
                Debug.Log("Nie możesz atakować swoich sojuszników. Jest to możliwe tylko w trybie Friendly Fire.");
                return;
            }

            CombatManager.Instance.Attack(SelectedUnit.GetComponent<Unit>(), this, false);
        }
    }

    public void SelectUnit()
    {
        if (SelectedUnit == null)
        {
            SelectedUnit = this.gameObject;

            //Odświeża listę ekwipunku
            InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedIndex = 0;
            InventoryManager.Instance.UpdateInventoryDropdown(SelectedUnit.GetComponent<Inventory>().AllWeapons);
        }
        else if (SelectedUnit == this.gameObject)
        {
            MovementManager.Instance.UpdateMovementRange(1); //Resetuje szarżę lub bieg, jeśli były aktywne

            //Resetuje przycisk celowania i pozycji obronne jeśli były aktywne
            AimingBonus = 0;
            CombatManager.Instance.UpdateAimButtonColor(); 
            DefensiveBonus = 0;
            CombatManager.Instance.UpdateDefensivePositionButtonColor(); 

            //Resetuje listę ekwipunku
            InventoryManager.Instance.HideInventory();

            SelectedUnit = null;
        }
        else
        {
            MovementManager.Instance.UpdateMovementRange(1); //Resetuje szarżę lub bieg, jeśli były aktywne       
            SelectedUnit.GetComponent<Unit>().IsSelected = false;

            ChangeUnitColor(SelectedUnit);
            SelectedUnit = this.gameObject;

            CombatManager.Instance.UpdateAimButtonColor(); //Resetuje przycisk celowania jeśli był aktywny
            CombatManager.Instance.UpdateDefensivePositionButtonColor(); //Resetuje przycisk pozycji obronnej jeśli był aktywny

            //Odświeża listę ekwipunku
            InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedIndex = 0;
            InventoryManager.Instance.UpdateInventoryDropdown(SelectedUnit.GetComponent<Inventory>().AllWeapons);
        }
        IsSelected = !IsSelected;
        ChangeUnitColor(this.gameObject);
        GridManager.Instance.HighlightTilesInMovementRange(Stats);

        //Aktualizuje panel ze statystykami postaci na górze ekranu
        UnitsManager.Instance.UpdateUnitPanel(SelectedUnit);
    }

    public void ChangeUnitColor(GameObject unit)
    {
        Renderer renderer = unit.GetComponent<Renderer>();

        renderer.material.color = IsSelected ? unit.GetComponent<Unit>().HighlightColor : unit.GetComponent<Unit>().DefaultColor;
    }

    public void DisplayUnitName()
    {
        if (NameDisplay == null) return;

        if (Stats.Name != null && Stats.Name.Length > 1)
        {
            NameDisplay.text = Stats.Name;
        }
        else
        {
            NameDisplay.text = this.gameObject.name;
            Stats.Name = this.gameObject.name;
        }
    }

    public void DisplayUnitHealthPoints()
    {
        if (HealthDisplay == null) return;

        HealthDisplay.text = Stats.TempHealth + "/" + Stats.MaxHealth;
    }
}
