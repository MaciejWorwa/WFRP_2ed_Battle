using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public static GameObject SelectedUnit;
    public bool IsSelected { get; private set; } = false;
    public Stats Stats;

    private GridManager _gridManager;

    void Start()
    {
        Stats = gameObject.AddComponent<Stats>();
        _gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
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
        _gridManager.HighlightTilesInMovementRange(Stats);
    }

    private void ChangeUnitColor(GameObject unit)
    {
        Renderer renderer = unit.GetComponent<Renderer>();
        renderer.material.color = IsSelected ? Color.green : Color.white;
    }
}
