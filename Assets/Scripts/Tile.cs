using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    [SerializeField] private Color _baseColor, _firstColor, _secondColor, _highlightColor, _rangeColor, _highlightRangeColor;
    private Renderer _renderer;
    public bool IsOccupied;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    public void Init(bool isOffset)
    {
        _renderer.material.color = isOffset ? _secondColor : _firstColor;
        _baseColor = _renderer.material.color;
        _rangeColor = _baseColor * 0.9f;
        _highlightRangeColor = Color.Lerp(_baseColor, Color.white, 0.3f);
    }

    private void OnMouseEnter()
    {
        HighlightTile();

        if(Unit.SelectedUnit != null)
        {
            MovementManager.Instance.HighlightPath(Unit.SelectedUnit, this.gameObject);
        }      
    }

    private void OnMouseExit()
    {
        ResetTileHighlight();
    }

    private void OnMouseUp()
    {
        if(UnitsManager.IsTileSelecting == true)
        {
            //Tworzy jednostkę na klikniętym polu 
            UnitsManager.Instance.CreateUnitOnSelectedTile(this.gameObject.transform.position);
            return;
        }
        if(Unit.SelectedUnit != null)
        {
            Unit unit = Unit.SelectedUnit.GetComponent<Unit>();

            if(unit.IsCharging)
            {
                Debug.Log("Wybierz przeciwnika, na którego chcesz zaszarżować.");
                return;
            }     

            //Wykonuje ruch na kliknięte pole
            MovementManager.Instance.MoveSelectedUnit(this.gameObject, Unit.SelectedUnit);
        }
    }

    public void HighlightTile()
    {
        if(_renderer.material.color == _baseColor)
            _renderer.material.color = _highlightColor;
        else if (_renderer.material.color == _rangeColor)
            _renderer.material.color = _highlightRangeColor;
    }
    private void ResetTileHighlight()
    {
        if(_renderer.material.color == _highlightColor)
            _renderer.material.color = _baseColor;
        // else if (_renderer.material.color == _highlightRangeColor)
        //     _renderer.material.color = _rangeColor;

        //Resetuje podświetlenie teoretycznej ścieżki postaci. NIE WIEM, CZY TEN SPOSÓB JEST NAJBARDZIEJ OPTYMALNY
        foreach (var tile in GridManager.Instance.Tiles)
        {
            if (tile._renderer.material.color == tile._highlightRangeColor)
                tile._renderer.material.color = tile._rangeColor;
        }
    }

    public void SetRangeColor()
    {
        ResetRangeColor();

        if (Unit.SelectedUnit != null)
            _renderer.material.color = _rangeColor;
    }

    public void ResetRangeColor()
    {      
        _renderer.material.color = _baseColor;
    }
}
