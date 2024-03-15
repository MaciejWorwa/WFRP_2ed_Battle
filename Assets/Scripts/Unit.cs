using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public class Unit : MonoBehaviour
{
    public static GameObject SelectedUnit;
    public bool IsSelected { get; private set; } = false;
    public bool IsCharging;
    public bool IsRunning;
    public Stats Stats;

    private TMP_Text _health_display;

    private GridManager _gridManager;
    private AttackManager _attackManager;

    void Start()
    {
        Stats = gameObject.AddComponent<Stats>();
        _gridManager = GridManager.Instance;
        _attackManager = AttackManager.Instance;

        //Wyświetlenie punktów żywotności
        _health_display = transform.Find("Canvas/health_text").GetComponent<TMP_Text>();
        _health_display.text = Stats.TempHealth + "/" + Stats.Health;
    }

    private void OnMouseUp()
    {
        SelectUnit();
    }

    private void OnMouseOver()
    {
        if(Input.GetMouseButtonDown(1) && SelectedUnit != null && SelectedUnit != this.gameObject)
        {
            _attackManager.Attack(SelectedUnit.GetComponent<Unit>(), this);
        }
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
        _gridManager.HighlightTilesInMovementRange(Stats);
    }

    private void ChangeUnitColor(GameObject unit)
    {
        Renderer renderer = unit.GetComponent<Renderer>();
        renderer.material.color = IsSelected ? Color.green : Color.white;
    }
}
