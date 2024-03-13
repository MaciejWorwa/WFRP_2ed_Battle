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
        Stats = gameObject.AddComponent<Stats>();

        DisplayUnitName();

        Stats.TempHealth = Stats.MaxHealth;
        DisplayUnitHealthPoints();
    }
    private void OnMouseUp()
    {
        SelectUnit();
    }

    private void SelectUnit()
    {
        if (SelectedUnit == null)
            SelectedUnit = this.gameObject;
        else if (SelectedUnit == this.gameObject)
            SelectedUnit = null;
        else
        {
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
        if (Stats.Name == null || NameDisplay == null) return;

        NameDisplay.text = Stats.Name;
    }

    public void DisplayUnitHealthPoints()
    {
        if (Stats.Name == null || HealthDisplay == null) return;

        HealthDisplay.text = Stats.TempHealth + "/" + Stats.MaxHealth;
    }
}
