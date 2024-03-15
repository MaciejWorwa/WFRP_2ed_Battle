using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public static GameObject SelectedUnit;
    public bool IsSelected { get; private set; } = false;
    public bool IsCharging;
    public bool IsRunning;
    public Stats Stats;

    public TMP_Text NameDisplay;
    public TMP_Text HealthDisplay;

    void Start()
    {
        Stats = gameObject.GetComponent<Stats>();

        DisplayUnitName();

        Stats.TempHealth = Stats.MaxHealth;
        DisplayUnitHealthPoints();
    }
    private void OnMouseUp()
    {
        SelectUnit();
    }

    private void OnMouseOver()
    {
        if(Input.GetMouseButtonDown(1) && SelectedUnit != null && SelectedUnit != this.gameObject)
        {
            AttackManager.Instance.Attack(SelectedUnit.GetComponent<Unit>(), this);
        }
    }

    private void SelectUnit()
    {
        if (SelectedUnit == null)
        {
            SelectedUnit = this.gameObject;
        }
        else if (SelectedUnit == this.gameObject)
        {
            MovementManager.Instance.UpdateMovementRange(1); //Resetuje szarżę lub bieg, jeśli były aktywne
            SelectedUnit = null;
        }
        else
        {
            MovementManager.Instance.UpdateMovementRange(1); //Resetuje szarżę lub bieg, jeśli były aktywne
            SelectedUnit.GetComponent<Unit>().IsSelected = false;
            ChangeUnitColor(SelectedUnit);
            SelectedUnit = this.gameObject;
        }
        IsSelected = !IsSelected;
        ChangeUnitColor(this.gameObject);
        GridManager.Instance.HighlightTilesInMovementRange(Stats);
    }

    private void ChangeUnitColor(GameObject unit)
    {
        Renderer renderer = unit.GetComponent<Renderer>();
        renderer.material.color = IsSelected ? Color.green : Color.white;
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
        }
    }

    public void DisplayUnitHealthPoints()
    {
        if (HealthDisplay == null) return;

        HealthDisplay.text = Stats.TempHealth + "/" + Stats.MaxHealth;
    }
}
