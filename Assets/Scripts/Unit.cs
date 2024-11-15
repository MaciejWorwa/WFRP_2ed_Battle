using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using System.Reflection;
using System.IO;

public class Unit : MonoBehaviour
{
    public int UnitId; // Unikalny Id jednostki

    public static GameObject SelectedUnit;
    public static GameObject LastSelectedUnit;
    public string TokenFilePath;
    public Color DefaultColor;
    public Color HighlightColor;
    public bool IsSelected = false;
    public bool IsRunning; // Biegnie
    public bool IsCharging; // Szarżuje
    public bool IsRetreating; // Wycofuje się
    public int HelplessDuration; // Czas stanu bezbronności (podany w rundach). Wartość 0 oznacza, że postać nie jest bezbronna
    public bool IsScared; // Jest przestraszony
    public bool IsFearTestPassed; // Zdał test strachu
    public int SpellDuration; // Czas trwania zaklęcia mającego wpływ na tą jednostkę
    public int StunDuration; // Czas ogłuszenia (podany w rundach). Wartość 0 oznacza, że postać nie jest ogłuszona
    public bool Trapped; // Unieruchomiony
    public int TrappedUnitId; // Cel unieruchomienia
    //public int TrappedDuration; // Czas unieruchomienia (podany w rundach). Wartość 0 oznacza, że postać nie jest unieruchomiona
    public int AimingBonus;
    public int CastingNumberBonus;
    public int DefensiveBonus;
    public int GuardedAttackBonus; //Modyfikator do uników i parowania za ostrożny atak
    public bool CanAttack = true;
    public bool CanCastSpell = false;
    public bool Feinted = false; // Określa, czy postać wykonała w poprzedniej akcji udaną fintę
    public bool CanParry = true;
    public bool CanDodge = false;
    public Stats Stats;

    public TMP_Text NameDisplay;
    public TMP_Text HealthDisplay;

    void Start()
    {
        Stats = gameObject.GetComponent<Stats>();

        DisplayUnitName();

        if(Stats.Dodge > 0) CanDodge = true;
        Stats.TempSz = Stats.Sz;
        Stats.TempHealth = Stats.MaxHealth;

        CalculateStrengthAndToughness(); // Liczy siłę i wytrzymałość

        DisplayUnitHealthPoints();

        //Aktualizuje kolejkę inicjatywy
        InitiativeQueueManager.Instance.UpdateInitiativeQueue();
    }
    private void OnMouseUp()
    { 
        if(GameManager.Instance.IsPointerOverPanel()) return;

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
        if(Input.GetMouseButtonDown(1) && SelectedUnit != null && SelectedUnit != this.gameObject && !MagicManager.IsTargetSelecting)
        {
            //Sprawdza, czy atakowanym jest nasz sojusznik i czy tryb Friendly Fire jest aktywny
            if(GameManager.IsFriendlyFire == false && this.gameObject.CompareTag(SelectedUnit.tag))
            {
                Debug.Log("Nie możesz atakować swoich sojuszników. Jest to możliwe tylko w trybie Friendly Fire.");
                return;
            }

            CombatManager.Instance.Attack(SelectedUnit.GetComponent<Unit>(), this, false);
        }
        else if (Input.GetMouseButtonDown(1) && SelectedUnit != null && MagicManager.IsTargetSelecting)
        {
            MagicManager.Instance.CastSpell(this.gameObject);
        }   
    }

    public void SelectUnit()
    {
        if (SelectedUnit == null)
        {
            SelectedUnit = this.gameObject;

            //Odświeża listę ekwipunku
            InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedIndex = 0;
            InventoryManager.Instance.UpdateInventoryDropdown(SelectedUnit.GetComponent<Inventory>().AllWeapons, true);
        }
        else if (SelectedUnit == this.gameObject)
        {
            CombatManager.Instance.ChangeAttackType(); // Resetuje wybrany typ ataku
            MovementManager.Instance.UpdateMovementRange(1); //Resetuje szarżę lub bieg, jeśli były aktywne
            MovementManager.Instance.Retreat(false); //Resetuje bezpieczny odwrót

            //Resetuje przycisk celowania i pozycji obronne jeśli były aktywne
            AimingBonus = 0;
            CombatManager.Instance.UpdateAimButtonColor(); 
            DefensiveBonus = 0;
            CombatManager.Instance.UpdateDefensiveStanceButtonColor(); 

            //Zamyka aktywne panele
            GameManager.Instance.HideActivePanels(); 

            //Wyłącza panel edycji jednostki, jeśli był włączony
            UnitsManager.Instance.EditUnitModeOff();

            LastSelectedUnit = SelectedUnit;
            SelectedUnit = null;
        }
        else
        {
            CombatManager.Instance.ChangeAttackType(); // Resetuje wybrany typ ataku
            MovementManager.Instance.UpdateMovementRange(1); //Resetuje szarżę lub bieg, jeśli były aktywne   
            MovementManager.Instance.Retreat(false); //Resetuje bezpieczny odwrót    
            SelectedUnit.GetComponent<Unit>().IsSelected = false;

            ChangeUnitColor(SelectedUnit);
            LastSelectedUnit = SelectedUnit;
            SelectedUnit = this.gameObject;

            CombatManager.Instance.UpdateAimButtonColor(); //Resetuje przycisk celowania jeśli był aktywny
            CombatManager.Instance.UpdateDefensiveStanceButtonColor(); //Resetuje przycisk pozycji obronnej jeśli był aktywny

            //Odświeża listę ekwipunku
            InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedIndex = 0;
            InventoryManager.Instance.UpdateInventoryDropdown(SelectedUnit.GetComponent<Inventory>().AllWeapons, true);
        }
        IsSelected = !IsSelected;
        ChangeUnitColor(this.gameObject);
        GridManager.Instance.HighlightTilesInMovementRange(Stats);

        //Aktualizuje panel ze statystykami postaci na górze ekranu
        UnitsManager.Instance.UpdateUnitPanel(SelectedUnit);

        //Zaznacza lub odznacza jednostkę na kolejce inicjatywy
        InitiativeQueueManager.Instance.UpdateInitiativeQueue();

        //Włącza lub wyłącza podgląd dostępnych akcji dla jednostki (w zależności, czy ją zaznaczamy, czy odznaczamy)
        RoundsManager.Instance.DisplayActionsLeft(this);

        //Zresetowanie rzucania zaklęć
        MagicManager.Instance.ResetSpellCasting();
    }

    public void ChangeUnitColor(GameObject unit)
    {
        Renderer renderer = unit.GetComponent<Renderer>();

        //Ustawia wartość HighlightColor na jaśniejszą wersję DefaultColor. Trzeci parametr określa ilość koloru białego w całości.
        HighlightColor = Color.Lerp(DefaultColor, Color.yellow, 0.3f);

        renderer.material.color = IsSelected ? unit.GetComponent<Unit>().HighlightColor : unit.GetComponent<Unit>().DefaultColor;

        //Aktualizuje kolor tokena, jeśli nie jest wgrany żaden obraz
        if (unit.GetComponent<Unit>().TokenFilePath.Length < 1)
        {
            unit.transform.Find("Token").GetComponent<SpriteRenderer>().material.color = IsSelected ? unit.GetComponent<Unit>().HighlightColor : unit.GetComponent<Unit>().DefaultColor;
        }
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

    public void CalculateStrengthAndToughness()
    {
        Stats.S = Mathf.RoundToInt(Stats.K / 10);
        Stats.Wt = Mathf.RoundToInt(Stats.Odp / 10);
    }
}
